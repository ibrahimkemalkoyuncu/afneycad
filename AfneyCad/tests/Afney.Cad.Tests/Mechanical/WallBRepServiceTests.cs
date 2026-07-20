using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: WallBRepService Testleri
   NEDEN: WallEntity'ler bu oturuma kadar hiç 3D katı model üretmiyordu (sadece Draw() ile
          2D dört çizgi). Bu testler, üretilen B-Rep Solid'in hacminin WallEntity'nin KENDİ
          analitik formülüyle (GetVolumeM3, uzunluk×kalınlık×yükseklik) eşleştiğini — yani
          B-Rep kernel'inin gerçek domain verisiyle çapraz doğrulandığını kanıtlar.
*/
public class WallBRepServiceTests
{
    [Fact]
    public void GenerateWallSolid_AxisAlignedWall_VolumeMatchesAnalyticalFormula()
    {
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), thickness: 200)
        {
            HeightMm = 3000
        };
        var db = new CadDatabase();
        db.AddEntity(wall);

        var service = new WallBRepService(db);
        var solid = service.GenerateWallSolid(wall);

        Assert.NotNull(solid);
        Assert.True(solid!.IsValid());

        double expectedVolumeMm3 = wall.GetVolumeM3() * 1000.0 * 1000.0 * 1000.0;
        Assert.Equal(expectedVolumeMm3, solid.GetVolume(), precision: 0);
    }

    [Fact]
    public void GenerateWallSolid_DiagonalWall_VolumeMatchesAnalyticalFormula()
    {
        // Duvar ekseni eksene hizalı DEĞİL (45 derece) — ExtrudeBox'ın herhangi bir
        // yönelimde de doğru hacim ürettiğini kanıtlar.
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 3000, 0), thickness: 250)
        {
            HeightMm = 2800
        };
        var db = new CadDatabase();
        db.AddEntity(wall);

        var service = new WallBRepService(db);
        var solid = service.GenerateWallSolid(wall);

        Assert.NotNull(solid);
        double expectedVolumeMm3 = wall.GetVolumeM3() * 1e9;
        double relativeError = Math.Abs(solid!.GetVolume() - expectedVolumeMm3) / expectedVolumeMm3;
        Assert.True(relativeError < 1e-6, $"Relative error too high: {relativeError}");
    }

    [Fact]
    public void GenerateAllWallSolids_ReturnsOneSolidPerWallEntity()
    {
        var db = new CadDatabase();
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0)));
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(0, 4000, 0)));

        var service = new WallBRepService(db);
        var solids = service.GenerateAllWallSolids();

        Assert.Equal(2, solids.Count);
        Assert.All(solids, s => Assert.True(s.IsValid()));
    }

    /*
       NE/NEDEN: Gerçek boolean (CSG subtract) kapsam dışı bırakıldığı için WallBRepService,
       kapı/pencere boşluklarını duvarı SEGMENTLERE bölerek (boolean'sız) modelliyor. Bu test,
       segmentlerin hacimleri toplamının analitik olarak (duvar_hacmi - boşluk_hacmi) İLE TAM
       eşleştiğini kanıtlıyor — segmentasyonun geometrik olarak doğru (ne fazla ne eksik
       malzeme) olduğunun somut kanıtı.
    */
    [Fact]
    public void GenerateWallSolids_WithDoorInMiddle_SegmentVolumesEqualWallMinusOpening()
    {
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), thickness: 200) { HeightMm = 3000 };
        var door = new DoorEntity(new Vector3D(2500, 0, 0), width: 900, height: 2100);

        var service = new WallBRepService(new CadDatabase());
        var solids = service.GenerateWallSolids(wall, doors: new[] { door });

        Assert.True(solids.Count >= 3); // sol dilim + lento (sağ dilim de var) — boşluksuz değil
        Assert.All(solids, s => Assert.True(s.IsValid()));

        double wallVolume = 5000.0 * 200.0 * 3000.0;
        double doorOpeningVolume = 900.0 * 200.0 * 2100.0; // sill=0, head=door yüksekliği
        double expected = wallVolume - doorOpeningVolume;

        double actual = solids.Sum(s => s.GetVolume());
        double relativeError = Math.Abs(actual - expected) / expected;
        Assert.True(relativeError < 1e-6, $"Relative error too high: {relativeError} (expected {expected}, actual {actual})");
    }

    [Fact]
    public void GenerateWallSolids_WithWindow_ProducesSillAndLintelSegments()
    {
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0), thickness: 200) { HeightMm = 3000 };
        var window = new WindowEntity(new Vector3D(2000, 0, 0), width: 1200, height: 1500) { SillHeightMm = 900 };

        var service = new WallBRepService(new CadDatabase());
        var solids = service.GenerateWallSolids(wall, windows: new[] { window });

        // Beklenen: sol tam-yükseklik + sağ tam-yükseklik + denizlik-altı + lento = 4 parça
        Assert.Equal(4, solids.Count);
        Assert.All(solids, s => Assert.True(s.IsValid()));

        double wallVolume = 4000.0 * 200.0 * 3000.0;
        double windowOpeningVolume = 1200.0 * 200.0 * 1500.0;
        double expected = wallVolume - windowOpeningVolume;

        double actual = solids.Sum(s => s.GetVolume());
        double relativeError = Math.Abs(actual - expected) / expected;
        Assert.True(relativeError < 1e-6, $"Relative error too high: {relativeError}");
    }

    [Fact]
    public void GenerateWallSolids_DoorOffWall_IsIgnored()
    {
        // Duvar eksenine dik uzaklığı toleransın (kalınlık/2 + 50mm) çok dışında olan bir kapı
        // bu duvara ait sayılmamalı — boşluk açılmamalı, tek parça (tam) duvar dönmeli.
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0), thickness: 200) { HeightMm = 3000 };
        var farDoor = new DoorEntity(new Vector3D(2000, 5000, 0), width: 900, height: 2100);

        var service = new WallBRepService(new CadDatabase());
        var solids = service.GenerateWallSolids(wall, doors: new[] { farDoor });

        Assert.Single(solids);
        double expected = 4000.0 * 200.0 * 3000.0;
        Assert.Equal(expected, solids[0].GetVolume(), precision: 0);
    }
}
