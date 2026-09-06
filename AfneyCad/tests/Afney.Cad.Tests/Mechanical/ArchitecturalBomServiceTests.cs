using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: ArchitecturalBomService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu, BomService'in kardeşi):
          Mimari metraj (duvar/kolon/kiriş/kapı/pencere/mahal) hiç test edilmiyordu. Testler
          yazılırken GERÇEK bir çift-dönüşüm hatası bulundu: RoomEntity.Area zaten m² cinsinden
          hesaplanıp saklanıyordu (RoomEntity.CalculateArea, mm²→m² dönüşümü orada yapılıyor),
          ama ArchitecturalBomService.Generate() bunu AYRICA bir kez daha 1.000.000'a bölüyordu —
          mimari metraj raporundaki her RoomEntity tabanlı mahalin alanı gerçek değerinin
          ~milyonda biri kadar görünüyordu (MahalEntity etkilenmiyordu). Düzeltildi.
*/
public class ArchitecturalBomServiceTests
{
    [Fact]
    public void Generate_WallsSameMaterialAndThickness_AreGroupedAndSummed()
    {
        var db = new CadDatabase();
        var w1 = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 200);
        var w2 = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0), 200);
        db.AddEntity(w1);
        db.AddEntity(w2);

        var result = new ArchitecturalBomService(db).Generate();

        var wallItem = Assert.Single(result.Items, i => i.Category == "Duvar");
        Assert.Equal(2, wallItem.Quantity);
        Assert.Equal(w1.GetAreaM2() + w2.GetAreaM2(), wallItem.Area, precision: 3);
        Assert.Equal(w1.GetAreaM2() + w2.GetAreaM2(), result.TotalWallAreaM2, precision: 3);
        Assert.Equal(w1.GetLengthM() + w2.GetLengthM(), result.TotalWallLengthM, precision: 3);
        Assert.Equal(2, result.WallCount);
    }

    [Fact]
    public void Generate_WallsDifferentThickness_ProduceSeparateGroups()
    {
        var db = new CadDatabase();
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 100));
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 200));

        var result = new ArchitecturalBomService(db).Generate();

        Assert.Equal(2, result.Items.Count(i => i.Category == "Duvar"));
    }

    [Fact]
    public void Generate_Columns_GroupedByShapeMaterialSize()
    {
        var db = new CadDatabase();
        var c1 = new ColumnEntity(new Vector3D(0, 0, 0));
        var c2 = new ColumnEntity(new Vector3D(1000, 0, 0));
        db.AddEntity(c1);
        db.AddEntity(c2);

        var result = new ArchitecturalBomService(db).Generate();

        var colItem = Assert.Single(result.Items, i => i.Category == "Kolon");
        Assert.Equal(2, colItem.Quantity);
        Assert.Equal(c1.GetVolumeM3() + c2.GetVolumeM3(), colItem.Volume, precision: 3);
        Assert.Equal(2, result.ColumnCount);
    }

    [Fact]
    public void Generate_Beams_SumsLengthAndVolume()
    {
        var db = new CadDatabase();
        var b1 = new BeamEntity(new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0));
        db.AddEntity(b1);

        var result = new ArchitecturalBomService(db).Generate();

        var beamItem = Assert.Single(result.Items, i => i.Category == "Kiris");
        Assert.Equal(b1.GetLengthM(), beamItem.Area, precision: 3); // Area alani "uzunluk" tasimak icin kullaniliyor (mevcut tasarim)
        Assert.Equal(b1.GetVolumeM3(), beamItem.Volume, precision: 3);
        Assert.Equal(1, result.BeamCount);
    }

    [Fact]
    public void Generate_Doors_GroupedByTypeWidthHeight()
    {
        var db = new CadDatabase();
        db.AddEntity(new DoorEntity(new Vector3D(0, 0, 0), 900, 2100));
        db.AddEntity(new DoorEntity(new Vector3D(1000, 0, 0), 900, 2100));
        db.AddEntity(new DoorEntity(new Vector3D(2000, 0, 0), 800, 2100));

        var result = new ArchitecturalBomService(db).Generate();

        var doorItems = result.Items.Where(i => i.Category == "Kapi").ToList();
        Assert.Equal(2, doorItems.Count); // 900x2100 grubu (2 adet) + 800x2100 grubu (1 adet)
        Assert.Equal(3, result.DoorCount);
        Assert.Contains(doorItems, i => i.Quantity == 2 && i.Size == "900x2100");
        Assert.Contains(doorItems, i => i.Quantity == 1 && i.Size == "800x2100");
    }

    [Fact]
    public void Generate_Windows_AreaMatchesHandComputedWidthHeightFormula()
    {
        var db = new CadDatabase();
        db.AddEntity(new WindowEntity(new Vector3D(0, 0, 0), 1200, 1500));
        db.AddEntity(new WindowEntity(new Vector3D(1000, 0, 0), 1200, 1500));

        var result = new ArchitecturalBomService(db).Generate();

        var winItem = Assert.Single(result.Items, i => i.Category == "Pencere");
        double expectedArea = 2 * (1.2 * 1.5);
        Assert.Equal(expectedArea, winItem.Area, precision: 6);
        Assert.Equal(2, result.WindowCount);
    }

    [Fact]
    public void Generate_RoomEntity_AreaIsUsedDirectlyNotDoubleConverted()
    {
        // NE/NEDEN: RoomEntity.Area ZATEN m² cinsinden hesaplanip saklaniyor (bkz. dosya basi
        // yorumu) — bu test, servisin onu tekrar 1e6'ya BOLMEDIGINI dogrudan kanitlar. 1000x2000mm
        // (1m x 2m) dikdortgen bir oda -> Area = 2.0 m² olmali (2,000,000 mm² / 1e6, DEGIL
        // 2,000,000 / 1e12 gibi asiri kucuk bir deger).
        var db = new CadDatabase();
        var boundary = new System.Collections.Generic.List<Vector3D>
        {
            new(0, 0, 0), new(1000, 0, 0), new(1000, 2000, 0), new(0, 2000, 0)
        };
        var room = new RoomEntity(boundary, "Oda1");
        db.AddEntity(room);

        Assert.Equal(2.0, room.Area, precision: 3); // RoomEntity'nin kendi hesabi (referans sagliamasi)

        var result = new ArchitecturalBomService(db).Generate();

        var roomItem = Assert.Single(result.Items, i => i.Category == "Mahal");
        Assert.Equal(2.0, roomItem.Area, precision: 3);
        Assert.Equal(2.0, result.TotalRoomAreaM2, precision: 3);
        Assert.Equal(1, result.RoomCount);
    }

    [Fact]
    public void Generate_MahalEntity_AreaUsedDirectly()
    {
        var db = new CadDatabase();
        var boundary = new System.Collections.Generic.List<Vector3D>
        {
            new(0, 0, 0), new(1000, 0, 0), new(1000, 1000, 0), new(0, 1000, 0)
        };
        var mahal = new MahalEntity(boundary, "Mutfak", "Kitchen") { Area = 12.5 };
        db.AddEntity(mahal);

        var result = new ArchitecturalBomService(db).Generate();

        var mahalItem = Assert.Single(result.Items, i => i.Category == "Mahal");
        Assert.Equal(12.5, mahalItem.Area, precision: 3);
        Assert.Equal(12.5, result.TotalRoomAreaM2, precision: 3);
        Assert.Equal(1, result.RoomCount);
    }

    [Fact]
    public void Generate_EmptyDatabase_ReturnsZeroCountsAndNoItems()
    {
        var db = new CadDatabase();
        var result = new ArchitecturalBomService(db).Generate();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.WallCount);
        Assert.Equal(0, result.ColumnCount);
        Assert.Equal(0, result.BeamCount);
        Assert.Equal(0, result.DoorCount);
        Assert.Equal(0, result.WindowCount);
        Assert.Equal(0, result.RoomCount);
    }

    [Fact]
    public void ExportToHtml_ContainsCoreSummaryFigures()
    {
        var db = new CadDatabase();
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0)));

        var result = new ArchitecturalBomService(db).Generate();
        string html = new ArchitecturalBomService(db).ExportToHtml(result, "Test Projesi");

        Assert.Contains("MİMARİ METRAJ TABLOSU", html);
        Assert.Contains("Test Projesi", html);
        Assert.Contains(result.WallCount.ToString(), html);
    }
}
