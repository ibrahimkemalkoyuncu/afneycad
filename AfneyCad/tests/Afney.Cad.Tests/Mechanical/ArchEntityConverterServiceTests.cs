using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: ArchEntityConverterService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu): DWG layer'larından mimari
          eleman algılama hiç test edilmiyordu. Ayrıca denetim raporunun "kavisli duvar belirsiz"
          bulgusu somutlaştırıldı: `ConvertFromLayers`, duvar katmanındaki bir `ArcEntity`'yi HİÇBİR
          KOŞULLA eşleştirmiyordu (sadece `is LineEntity` kontrolü vardı) — sessizce atlanıyordu,
          ne duvar oluşuyordu ne kullanıcıya uyarı veriliyordu. Artık ArcEntity'ler düz segmentlere
          (chord) yaklaşıklanıp gerçek WallEntity'ler olarak ekleniyor. Bu testler hem eski (düz
          duvar) davranışını kilitliyor hem yeni (kavisli duvar) davranışını doğruluyor.
*/
public class ArchEntityConverterServiceTests
{
    [Fact]
    public void ConvertFromLayers_StraightLineOnWallLayer_CreatesWallEntity()
    {
        var db = new CadDatabase();
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0)) { Layer = "A-DUVAR" });

        var result = new ArchEntityConverterService(db).ConvertFromLayers();

        Assert.Equal(1, result.WallsCreated);
        Assert.Equal(0, result.CurvedWallsApproximated);
        var wall = Assert.Single(db.GetAllEntities().OfType<WallEntity>());
        Assert.Equal(new Vector3D(0, 0, 0), wall.StartPoint);
        Assert.Equal(new Vector3D(5000, 0, 0), wall.EndPoint);
    }

    [Fact]
    public void ConvertFromLayers_ArcOnWallLayer_NoLongerSilentlySkipped_ProducesWallSegments()
    {
        // NE/NEDEN: Bu, "kavisli duvar belirsiz" bulgusunun tam olarak kanıtladığı senaryo —
        // eskiden bu ArcEntity hiçbir CurvedWallsApproximated/WallsCreated artışı yapmadan
        // sessizce yok sayılırdı.
        var db = new CadDatabase();
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 3000, 0, System.Math.PI / 2) { Layer = "A-DUVAR" };
        db.AddEntity(arc);

        var result = new ArchEntityConverterService(db).ConvertFromLayers();

        Assert.True(result.WallsCreated > 0, "ArcEntity'den en az bir WallEntity üretilmeliydi.");
        Assert.Equal(1, result.CurvedWallsApproximated);

        var walls = db.GetAllEntities().OfType<WallEntity>().ToList();
        Assert.Equal(result.WallsCreated, walls.Count);
    }

    [Fact]
    public void ConvertFromLayers_ArcWallSegments_FormAContinuousChainApproximatingTheArc()
    {
        var db = new CadDatabase();
        var arc = new ArcEntity(new Vector3D(1000, 2000, 0), 1500, 0, System.Math.PI) { Layer = "DUVAR" };
        db.AddEntity(arc);

        new ArchEntityConverterService(db).ConvertFromLayers();

        var walls = db.GetAllEntities().OfType<WallEntity>()
            .OrderBy(w => System.Math.Atan2(w.StartPoint.Y - arc.Center.Y, w.StartPoint.X - arc.Center.X))
            .ToList();

        Assert.True(walls.Count >= 4); // 180° sweep -> en az 18 segment (10°/segment, min 4 clamp)

        // Zincir sürekliliği: her segmentin bitişi bir sonrakinin başlangıcına (yaklaşık) eşit olmalı.
        for (int i = 0; i < walls.Count - 1; i++)
        {
            double dist = walls[i].EndPoint.DistanceTo(walls[i + 1].StartPoint);
            Assert.True(dist < 1.0, $"Segment {i} bitişi ile {i + 1} başlangıcı arasında süreksizlik: {dist}mm");
        }

        // İlk ve son segment uçları, yayın gerçek başlangıç/bitiş noktalarına yakın olmalı.
        var arcStart = new Vector3D(arc.Center.X + arc.Radius, arc.Center.Y, arc.Center.Z);
        var arcEnd = new Vector3D(arc.Center.X - arc.Radius, arc.Center.Y, arc.Center.Z);
        Assert.True(walls[0].StartPoint.DistanceTo(arcStart) < 1.0);
        Assert.True(walls[^1].EndPoint.DistanceTo(arcEnd) < 1.0);

        // Her segment, yay yarıçapından (Öklid mesafe merkeze) makul bir sapma içinde olmalı
        // (chord her zaman yarıçaptan biraz kısadır ama merkeze uzaklığı yarıçaptan büyük olamaz).
        foreach (var w in walls)
        {
            double distToCenter = w.StartPoint.DistanceTo(arc.Center);
            Assert.True(distToCenter <= arc.Radius + 0.01);
        }
    }

    [Fact]
    public void ConvertFromLayers_NonWallLayerArc_IsIgnored()
    {
        var db = new CadDatabase();
        db.AddEntity(new ArcEntity(new Vector3D(0, 0, 0), 1000, 0, System.Math.PI / 2) { Layer = "MOBLE" });

        var result = new ArchEntityConverterService(db).ConvertFromLayers();

        Assert.Equal(0, result.WallsCreated);
        Assert.Equal(0, result.CurvedWallsApproximated);
    }

    [Fact]
    public void ConvertFromLayers_MultipleEntityTypes_CountedIndependently()
    {
        var db = new CadDatabase();
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0)) { Layer = "DUVAR" });
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0)) { Layer = "KIRIS" });

        var result = new ArchEntityConverterService(db).ConvertFromLayers();

        Assert.Equal(1, result.WallsCreated);
        Assert.Equal(1, result.BeamsCreated);
        Assert.Equal(2, result.Total);
    }
}
