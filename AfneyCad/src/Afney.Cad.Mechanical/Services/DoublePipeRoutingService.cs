using System;
using System.Collections.Generic;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Çift Boru Otomatik Rotalama Servisi (DoublePipeRoutingService)
   NEDEN: FineSANI'de "Çift Hat Çizimi" olarak bilinen özellik: Sıcak su (Kırmızı) ve
          Soğuk su (Mavi) hatlarını birbirine paralel, ayarlanabilir eksenel mesafeyle
          tek komutla otomatik oluşturmak için.

   MÜHENDİSLİK DETAYI (TS 1258 / DIN 1988):
   - Sıcak ve soğuk su hatları minimum 100mm ayrı yerleştirilir (ısı yalıtımı).
   - Sıcak hat üstte, soğuk hat altta ya da solda/sağda konumlandırılır.
   - AutoBranch: Her armatür (Lavabo, WC) için iki hattan da otomatik branşman üretir.
*/
public class DoublePipeRoutingService
{
    private readonly CadDatabase _database;
    
    // TS 1258 Minimum Ayrım Mesafesi (mm) - Sıcak/Soğuk hat arası
    public double SeparationDistance { get; set; } = 150.0;

    public DoublePipeRoutingService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Çift Hat Çizim Sonucu
       NEDEN: Sıcak ve soğuk hat boru listelerini birlikte döndürmek için.
    */
    public class DoublePipeResult
    {
        public List<PipeEntity> HotPipes { get; set; } = new();
        public List<PipeEntity> ColdPipes { get; set; } = new();
        public double TotalLength => HotPipes.Sum(p => p.GetLength()) + ColdPipes.Sum(p => p.GetLength());
    }

    /*
       NE: İki Nokta Arası Çift Hat Oluştur (RouteDoublePipe)
       NEDEN: Kullanıcı 2 nokta verdiğinde sıcak+soğuk boruyu paralel çizer.

       NASIL:
       1. P1-P2 yön vektörü hesaplanır.
       2. Bu vektöre dik (normal) yönde SeparationDistance kadar offset uygulanır.
       3. Sıcak hat orijinal eksende, soğuk hat offset eksende çizilir.
       
       PARAMETRE orientation:
       - "Horizontal": Yanyana (Yatay mesafe - tipik duvar içi şafta uygun)
       - "Vertical": Üstüste (Dikey mesafe - tavan içi rota için)
    */
    public DoublePipeResult RouteDoublePipe(
        Vector3D start, 
        Vector3D end,
        double hotDiameter = 20.0,
        double coldDiameter = 20.0,
        string orientation = "Horizontal")
    {
        var result = new DoublePipeResult();
        
        // Hat yönü
        var dir = (end - start).Normalize();
        
        // Dik ofset vektörü (boru eksenine 90°)
        Vector3D offsetDir;
        if (orientation == "Vertical")
        {
            // Dikey ayrım: Z ekseninde
            offsetDir = new Vector3D(0, 0, 1);
        }
        else
        {
            // Yatay ayrım: Hat yönüne dik, Z düzleminde
            offsetDir = new Vector3D(-dir.Y, dir.X, 0).Normalize();
        }

        double halfSep = SeparationDistance / 2.0;

        // Sıcak Hat: Ofset pozitif tarafta
        var hotStart = start + offsetDir * halfSep;
        var hotEnd   = end   + offsetDir * halfSep;
        var hotPipe  = new PipeEntity(hotStart, hotEnd, hotDiameter)
        {
            SystemType        = MechanicalSystemType.DomesticHotWater,
            PipeMaterialType  = PipeMaterial.PPRC_PN20,
            Color             = 0xFFDD2222, // Kırmızı
            Layer             = "SIHHI-SICAK"
        };
        result.HotPipes.Add(hotPipe);

        // Soğuk Hat: Ofset negatif tarafta
        var coldStart = start - offsetDir * halfSep;
        var coldEnd   = end   - offsetDir * halfSep;
        var coldPipe  = new PipeEntity(coldStart, coldEnd, coldDiameter)
        {
            SystemType        = MechanicalSystemType.DomesticColdWater,
            PipeMaterialType  = PipeMaterial.PPRC_PN20,
            Color             = 0xFF2266DD, // Mavi
            Layer             = "SIHHI-SOGUK"
        };
        result.ColdPipes.Add(coldPipe);

        Serilog.Log.Information(
            ">>> ÇIFT HAT: S({HotLen:F0}mm) + S({ColdLen:F0}mm) — Orientation={Ori}",
            hotPipe.GetLength(), coldPipe.GetLength(), orientation);

        return result;
    }

    /*
       NE: Poliçizgi Boyunca Çift Hat Oluştur (RouteDoublePipeAlongPath)
       NEDEN: Kullanıcı bir rota verdiğinde (örn. A*'dan gelen nokta listesi) tüm
              segmentlerin her ikisi için boru oluşturur.
    */
    public DoublePipeResult RouteDoublePipeAlongPath(
        List<Vector3D> pathPoints,
        double hotDiameter  = 20.0,
        double coldDiameter = 20.0,
        string orientation  = "Horizontal")
    {
        var result = new DoublePipeResult();
        if (pathPoints == null || pathPoints.Count < 2) return result;

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            var segResult = RouteDoublePipe(
                pathPoints[i], pathPoints[i + 1],
                hotDiameter, coldDiameter, orientation);

            result.HotPipes.AddRange(segResult.HotPipes);
            result.ColdPipes.AddRange(segResult.ColdPipes);
        }

        return result;
    }

    /*
       NE: Armatürden Ana Hatta Çift Branşman Ekle (ConnectFixtureToDoubleLine)
       NEDEN: Lavabo, WC gibi her armatür hem sıcak hem soğuk hattan
              branşman almak zorundadır.
       
       NEDENİ (TS 1258 §6.3): Her armatüre ayrım vanalı çift branşman şarttır.
    */
    public DoublePipeResult ConnectFixtureToDoubleLine(
        SanitaryFixtureEntity fixture,
        Vector3D hotMainLinePoint,
        Vector3D coldMainLinePoint,
        double branchDiameter = 15.0)
    {
        var result = new DoublePipeResult();
        
        // Cihaz portlarını al
        var ports = fixture.GetPorts();
        var hotPort  = ports.FirstOrDefault(p => p.Name == "HotWater");
        var coldPort = ports.FirstOrDefault(p => p.Name == "ColdWater");

        // Sıcak branşman
        if (hotPort != null)
        {
            var hotBranch = new PipeEntity(hotMainLinePoint, hotPort.Position, branchDiameter)
            {
                SystemType       = MechanicalSystemType.DomesticHotWater,
                PipeMaterialType = PipeMaterial.PPRC_PN20,
                Color            = 0xFFDD2222,
                Layer            = "SIHHI-SICAK-BRANSMAN"
            };
            result.HotPipes.Add(hotBranch);
        }

        // Soğuk branşman
        if (coldPort != null)
        {
            var coldBranch = new PipeEntity(coldMainLinePoint, coldPort.Position, branchDiameter)
            {
                SystemType       = MechanicalSystemType.DomesticColdWater,
                PipeMaterialType = PipeMaterial.PPRC_PN20,
                Color            = 0xFF2266DD,
                Layer            = "SIHHI-SOGUK-BRANSMAN"
            };
            result.ColdPipes.Add(coldBranch);
        }

        if (hotPort == null && coldPort == null)
        {
            Serilog.Log.Warning(">>> ÇIFT HAT: Armatür '{Type}' üzerinde S/S portu bulunamadı.",
                fixture.FixtureType);
        }

        return result;
    }
}
