using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Serilog;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Sıhhi Tesisat Mühendislik Orkestratörü (PlumbingEngineeringService)
   NEDEN: Mimari mahal verilerini tesisat ağ foksiyonlarıyla birleştirerek "Akıllı Hesaplama" ve "Otomatik Çaplandırma" süreçlerini yönetmek için.

   GÖREVLERİ (Mete & Kemal):
   1. MAHAL ANALİZİ: Mahal içindeki vitrifiye yüklerini toplar.
   2. TOPOLOJİ DOĞRULAMA: Vitrifiyelerin boru hattına bağlı olup olmadığını kontrol eder.
   3. HİDROLİK HESAP: TS 1258 / DIN 1988'e göre pik debileri ve çapları hesaplar.
   4. KOLON ŞEMASI VERİ SETİ: Şema için gerekli mühendislik verilerini hazır hale getirir.
*/
public class PlumbingEngineeringService
{
    private readonly CadDatabase _database;
    private readonly MechanicalTopologyGraph _topology;
    private readonly FlowCalculationService _flowService;

    public PlumbingEngineeringService(CadDatabase database, MechanicalTopologyGraph topology)
    {
        _database = database;
        _topology = topology;
        _flowService = new FlowCalculationService(topology);
    }

    /*
       NE: Mahale Göre Hesapla (CalculateByMahal)
       NEDEN: Seçilen bir odanın içindeki tüm cihazların yüklerini boru hattına yansıtmak ve çapları güncellemek için.
    */
    public EngineeringResult CalculateByMahal(Guid mahalId)
    {
        var result = new EngineeringResult();
        var mahal = _database.GetEntity(mahalId) as MahalEntity;
        if (mahal == null) return result;

        Log.Information(">>> {MahalName} için mühendislik analizi başlatıldı.", mahal.Name);

        // 1. Mahalin içindeki vitrifiyeleri topla
        var fixtures = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .Where(f => mahal.FixtureIds.Contains(f.Id))
            .ToList();

        result.FixtureCount = fixtures.Count;
        result.TotalLoadUnits = fixtures.Sum(f => f.LoadUnits);

        // 2. Hidrolik Hesaplama Motorunu Çalıştır
        // Tüm sistemi tarayıp yükleri akış yönünde (Sink'e doğru) dağıtır.
        var allMechanicalEntries = _database.GetAllEntities().OfType<MechanicalEntity>();
        _flowService.CalculateSystemFlow(allMechanicalEntries);

        // 3. Otomatik Çaplandırma
        _flowService.AutoSizePipes(allMechanicalEntries);

        Log.Information("Mühendislik hesabı tamamlandı. Toplam Yük: {LU} LU", result.TotalLoadUnits);
        
        return result;
    }

    /*
       NE: Tüm Sistemi Çaplandır
       NEDEN: Tüm katlardaki tüm mahaller hazırken projenin genel boru çaplarını optimize etmek için.
    */
    public void AutoSizeAllSystem()
    {
        var allMechanical = _database.GetAllEntities().OfType<MechanicalEntity>();
        _flowService.CalculateSystemFlow(allMechanical);
        _flowService.AutoSizePipes(allMechanical);
    }
}

public class EngineeringResult
{
    public int FixtureCount { get; set; }
    public double TotalLoadUnits { get; set; }
    public List<string> Warnings { get; } = new();
}
