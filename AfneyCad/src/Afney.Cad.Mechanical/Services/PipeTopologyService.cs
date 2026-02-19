using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Boru Tesisat Topolojisi ve Hesap Servisi (PipeTopologyService)
    NEDEN: Sistemdeki boruların birbirine bağını analiz etmek ve kümülatif yükleri (Flow) hesaplamak için.
    
    NASIL (Mühendislik Modu):
    1. Projedeki tüm armatürleri (SanitaryFixtureEntity) ve boruları (PipeEntity) bulur.
    2. Her bir armatürden gelen yükü (Fixture Unit), topolojik olarak bağlı olduğu boru hattı boyunca 'ilerletir' (Propagate).
    3. Her boru üzerindeki toplam yükü hesaplar ve buna göre debi (Q) değerini bulur.
    4. PipeSizer kullanarak boru çaplarını otomatik olarak revize eder.
*/
public class PipeTopologyService
{
    private readonly CadDatabase _database;

    public PipeTopologyService(CadDatabase database)
    {
        _database = database;
    }

    /*
        NE: Sistemi Analiz Et ve Boyutlandır (AnalyzeAndSizeSystem)
        NEDEN: Tasarımcının tek tıkla tüm tesisatın mühendislik verilerini güncellemesini sağlamak için.
    */
    public void AnalyzeAndSizeSystem()
    {
        var entities = _database.GetAllEntities().ToList();
        var mechEntities = entities.OfType<MechanicalEntity>().ToList();

        // 1. Yeni Çekirdeği Kullan (FlowCalculationService)
        // Not: MechanicalKernel'deki graph'ı kullanmak daha doğru olur ama burada basitçe yeniden kuruyoruz.
        var graph = new MechanicalTopologyGraph();
        foreach (var entity in mechEntities) graph.AddEntity(entity);
        
        // Topolojik bağları kur (Otomatik bağlantı tespiti gerebilir veya mevcut portlar üzerinden)
        // Şimdilik MechanicalKernel'in güncel tuttuğu graph'ı pass etmek en iyisi.

        var flowService = new FlowCalculationService(graph);
        flowService.CalculateSystemFlow(mechEntities);
        flowService.AutoSizePipes(mechEntities);
    }
}
