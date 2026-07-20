using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: MultiStoryEnhancementService Testleri
   NEDEN: `CopyFloorWithConnections` ve `AutoConnectInterFloorRisers` daha önce hiç test
          edilmiyordu. Bu oturumda ayrıca GERÇEK bir hata bulundu ve düzeltildi:
          `CopyFloorWithConnections`, doğrudan üst üste istiflenmiş katlar için (en yaygın
          senaryo — dz == kaynak kat yüksekliği) sıfır uzunluklu, dejenere bir bağlantı borusu
          üretiyordu çünkü koşul kat-arası mesafeyi (`dz`) kontrol ediyordu, bağlantı borusunun
          KENDİ uzunluğunu değil. Bu testler hem düzeltmeyi hem de asıl (boşluklu kat) senaryosunu
          doğruluyor.
*/
public class MultiStoryEnhancementServiceTests
{
    private static LevelManager MakeLevelManager(params MepLevel[] levels)
    {
        var lm = new LevelManager();
        lm.Clear(); // LevelManager 4 varsayılan kat ile geliyor (0/3000/6000/9000mm) — testler kendi katlarını tanımlar
        foreach (var l in levels) lm.AddLevel(l);
        return lm;
    }

    [Fact]
    public void CopyFloorWithConnections_DirectlyStackedFloor_DoesNotCreateDegenerateZeroLengthConnector()
    {
        // dz == source.Height (en yaygın durum: yeni kat doğrudan kaynak katın üstünde) —
        // eski hatalı kodda burada sıfır uzunluklu bir bağlantı borusu üretilirdi.
        var db = new CadDatabase();
        var source = new MepLevel("Zemin", 0, 3000);
        var target = new MepLevel("1. Kat", 3000, 3000); // dz = 3000 = source.Height

        var riser = new PipeEntity(new Vector3D(1000, 1000, 0), new Vector3D(1000, 1000, 3000), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            Layer = "MEP_TEMIZ_SU"
        };
        db.AddEntity(riser);

        var lm = MakeLevelManager(source, target);
        var service = new MultiStoryEnhancementService(db, lm);

        var result = service.CopyFloorWithConnections(source, target);

        Assert.Equal(0, result.RiserConnectionsCreated);
    }

    [Fact]
    public void CopyFloorWithConnections_FloorWithGap_CreatesConnectorSpanningTheGap()
    {
        // Kaynak ile hedef arasında gerçek bir boşluk var (dz = 3500, source.Height = 3000
        // → 500mm boşluk) — bu durumda gerçek, sıfır olmayan uzunlukta bir bağlantı borusu
        // üretilmeli.
        var db = new CadDatabase();
        var source = new MepLevel("Zemin", 0, 3000);
        var target = new MepLevel("1. Kat (boşluklu)", 3500, 3000);

        var riser = new PipeEntity(new Vector3D(1000, 1000, 0), new Vector3D(1000, 1000, 3000), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            Layer = "MEP_TEMIZ_SU"
        };
        db.AddEntity(riser);

        var lm = MakeLevelManager(source, target);
        var service = new MultiStoryEnhancementService(db, lm);

        var result = service.CopyFloorWithConnections(source, target);

        Assert.Equal(1, result.RiserConnectionsCreated);
    }

    [Fact]
    public void CopyFloorWithConnections_TwoAlreadyConnectedPipes_PreservesConnectionAfterCopy()
    {
        var db = new CadDatabase();
        var source = new MepLevel("Zemin", 0, 3000);
        var target = new MepLevel("1. Kat", 3000, 3000);

        // pipeA'nın bitişi, pipeB'nin başlangıcıyla tam örtüşüyor (bağlı).
        var pipeA = new PipeEntity(new Vector3D(0, 0, 500), new Vector3D(2000, 0, 500), 25);
        var pipeB = new PipeEntity(new Vector3D(2000, 0, 500), new Vector3D(2000, 2000, 500), 25);
        db.AddEntity(pipeA);
        db.AddEntity(pipeB);

        var lm = MakeLevelManager(source, target);
        var service = new MultiStoryEnhancementService(db, lm);

        var result = service.CopyFloorWithConnections(source, target);

        Assert.Equal(2, result.CopiedCount);
        Assert.True(result.ConnectionsPreserved >= 1, "Kopyalanan katta önceden bağlı olan borular kopya sonrası da bağlı kalmalı.");
    }

    [Fact]
    public void AutoConnectInterFloorRisers_MatchingRisersAcrossLevels_FillsGapWithConnector()
    {
        var db = new CadDatabase();
        var lower = new MepLevel("Zemin", 0, 3000);
        var upper = new MepLevel("1. Kat", 3200, 3000); // 200mm boşluk (0'dan farklı kot hizası)

        // Alt kat riser'ı: z=[0,3000], üst kat riser'ı: z=[3200,6200] — aradaki boşluk 200mm.
        var lowerRiser = new PipeEntity(new Vector3D(500, 500, 0), new Vector3D(500, 500, 3000), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        var upperRiser = new PipeEntity(new Vector3D(500, 500, 3200), new Vector3D(500, 500, 6200), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        db.AddEntity(lowerRiser);
        db.AddEntity(upperRiser);

        var lm = MakeLevelManager(lower, upper);
        var service = new MultiStoryEnhancementService(db, lm);

        int connections = service.AutoConnectInterFloorRisers(toleranceMm: 100);

        Assert.Equal(1, connections);
    }

    [Fact]
    public void AutoConnectInterFloorRisers_DifferentSystemTypes_DoesNotConnect()
    {
        // Aynı XY konumunda ama farklı sistem tipi (soğuk su vs. pis su) olan riser'lar
        // birbirine bağlanmamalı — karışık sistem bağlantısı gerçek bir mühendislik hatası olurdu.
        var db = new CadDatabase();
        var lower = new MepLevel("Zemin", 0, 3000);
        var upper = new MepLevel("1. Kat", 3200, 3000);

        var lowerRiser = new PipeEntity(new Vector3D(500, 500, 0), new Vector3D(500, 500, 3000), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        var upperRiser = new PipeEntity(new Vector3D(500, 500, 3200), new Vector3D(500, 500, 6200), 100)
        {
            SystemType = MechanicalSystemType.WasteWater
        };
        db.AddEntity(lowerRiser);
        db.AddEntity(upperRiser);

        var lm = MakeLevelManager(lower, upper);
        var service = new MultiStoryEnhancementService(db, lm);

        int connections = service.AutoConnectInterFloorRisers(toleranceMm: 100);

        Assert.Equal(0, connections);
    }
}
