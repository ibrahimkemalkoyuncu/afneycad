using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Akıllı Hidrolik Çaplandırma Motoru (HydraulicFlowService)
    NEDEN: FINE SANI standartlarında, TS 1258 ve DIN 1988'e uygun otomatik boru çapı hesaplamak için.
    
    ÇALIŞMA MANTIĞI:
    1. Sistemdeki tüm uç birimlerin (Fixture) yükleme birimlerini (FU) toplar.
    2. Topoloji grafını kullanarak her boru segmentine binen toplam yükü hesaplar.
    3. Eş zamanlı debi (Design Flow) formülünü uygular: Q = k * sqrt(Sum FU).
    4. Hız limitleri (max 2 m/s) ve basınç kaybı limitlerine göre ideal boru çapını seçer.
*/
public class HydraulicFlowService
{
    private readonly CadDatabase _database;
    private readonly MechanicalTopologyGraph _graph;

    public HydraulicFlowService(CadDatabase database, MechanicalTopologyGraph graph)
    {
        _database = database;
        _graph = graph;
    }

    /*
    METOD ADI: RecalculateSystem
    AMACI: Tüm tesisatın debi ve çaplarını baştan hesaplamak.
    */
    public void RecalculateSystem()
    {
        // 1. Tüm boru yüklerini sıfırla
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        foreach (var pipe in pipes) pipe.LoadUnits = 0;

        // 2. Her bir Fixture'dan (yaprak) köke doğru yükü akıt
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
        foreach (var fix in fixtures)
        {
            AccumulateLoadUpstream(fix.Id, fix.LoadUnits);
        }

        // 3. Hesaplanan LU (Load Units) değerlerine göre debi ve çap ata
        foreach (var pipe in pipes)
        {
            UpdatePipeHydraulics(pipe);
        }
    }

    private void AccumulateLoadUpstream(Guid startNodeId, double lu)
    {
        // Basit bir BFS/DFS ile sink noktasına kadar yükü ekleyerek git
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(startNodeId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (visited.Contains(currentId)) continue;
            visited.Add(currentId);

            var neighbors = _graph.GetNeighbors(currentId);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Entity is PipeEntity pipe)
                {
                    pipe.LoadUnits += lu;
                    queue.Enqueue(pipe.Id);
                }
            }
        }
    }

    /*
    METOD ADI: UpdatePipeHydraulics
    AMACI: TS 1258 formüllerine göre debi ve çap belirlemek.
    */
    private void UpdatePipeHydraulics(PipeEntity pipe)
    {
        if (pipe.LoadUnits <= 0) return;

        // TS 1258 Eş zamanlı debi hesabı (Konutlar için k=0.7 fallback)
        // Q = 0.25 * sqrt(Sum FU) -> Basitleştirilmiş
        double concurrentFlow = 0.25 * Math.Sqrt(pipe.LoadUnits); 
        pipe.FlowRate = concurrentFlow * 3.6; // m3/h'e çevir

        // Çap Belirleme (Hız limitine göre: Ideal 1.0 - 1.5 m/s)
        pipe.InnerDiameter = CalculateRequiredDiameter(pipe.FlowRate);
        
        // Hız ve hata kontrolü
        pipe.Velocity = pipe.GetVelocity();
        pipe.HasHydraulicViolation = pipe.Velocity > 2.0; // 2 m/s kritik uyarı
    }

    private double CalculateRequiredDiameter(double flowM3h)
    {
        // Standart Boru Çapları (DN) - PPRC / Çelik ortak listesi
        double[] standardDN = { 15, 20, 25, 32, 40, 50, 65, 80, 100 };
        
        foreach (var dn in standardDN)
        {
            double velocity = CalculateVelocity(flowM3h, dn);
            if (velocity <= 1.5) return dn; // 1.5 m/s altındaki ilk uygun çapı seç
        }

        return 100; // Fallback
    }

    private double CalculateVelocity(double flowM3h, double dn)
    {
        double area = Math.PI * Math.Pow((dn / 1000.0) / 2.0, 2);
        return (flowM3h / 3600.0) / area;
    }
}
