using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: BomService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu): BomService, projenin
          metraj/maliyet çıktısının temelini oluşturan gruplama-ve-toplama mantığını
          içerir — bir gruplama hatası (ör. yanlış anahtarla gruplama, uzunlukların yanlış
          toplanması) doğrudan yanlış bir maliyet teklifine yol açar (aynı sınıf hata:
          DIN1988300Service'in daha önce bulunan ~31.6x boru çapı hatası, ama burada
          "metraj" katmanında). Bu servisin hiç testi yoktu. Bu testler gruplama
          anahtarının (çap+malzeme / çap) doğru ayrıştığını ve toplamaların doğru
          olduğunu kilitler.
*/
public class BomServiceTests
{
    /*
       NE/NEDEN — GERÇEK HATA (bu turda bulundu): `BomService`, boru uzunluğunu (dünya
       koordinatlarında mm) "m" etiketiyle HİÇ dönüştürmeden gösteriyordu — aynı metottaki
       kanal (Kanal) grubu ve uygulamadaki HER DİĞER servis (PipeCostService,
       PressureDropService, HydraulicReportService, vb.) pipe.Length'i mm→m için /1000.0
       ile böler. Bu test artık doğru (mm→m dönüştürülmüş) değeri kilitliyor — eskiden
       ham mm değerini "m" olarak kabul edip metraj raporunda her boru kalemini gerçek
       uzunluğunun 1000 katı gösteriyordu.
    */
    [Fact]
    public void GenerateBom_PipesWithSameDiameterAndMaterial_AreGroupedAndLengthsSummedInMeters()
    {
        var db = new CadDatabase();
        var pipe1 = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 };
        var pipe2 = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 };
        db.AddEntity(pipe1);
        db.AddEntity(pipe2);

        var bom = new BomService(db).GenerateBom();

        var pipeItem = Assert.Single(bom, b => b.Category == "Boru");
        Assert.Equal("PPRC_PN20", pipeItem.Material);
        Assert.Equal((pipe1.Length + pipe2.Length) / 1000.0, pipeItem.Quantity, precision: 2);
        Assert.Equal(15.0, pipeItem.Quantity, precision: 2); // 10000mm + 5000mm = 15m
        Assert.Equal("m", pipeItem.Unit);
    }

    [Fact]
    public void GenerateBom_PipesWithDifferentDiameterOrMaterial_ProduceSeparateGroups()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 });
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 50) { PipeMaterialType = PipeMaterial.PPRC_PN20 });
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 100) { PipeMaterialType = PipeMaterial.PEX_b });

        var bom = new BomService(db).GenerateBom();

        var pipeItems = bom.Where(b => b.Category == "Boru").ToList();
        // Farklı çap (100 vs 50) VE farklı malzeme (PPRC_PN20 vs PEX_b) -> 3 ayrı grup.
        Assert.Equal(3, pipeItems.Count);
    }

    [Fact]
    public void GenerateBom_Elbows_AreGroupedByDiameterAndCounted()
    {
        var db = new CadDatabase();
        db.AddEntity(new ElbowEntity(new Vector3D(0, 0, 0), 100, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)));
        db.AddEntity(new ElbowEntity(new Vector3D(10, 0, 0), 100, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)));
        db.AddEntity(new ElbowEntity(new Vector3D(20, 0, 0), 50, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)));

        var bom = new BomService(db).GenerateBom();

        var elbowItems = bom.Where(b => b.Description.StartsWith("Dirsek")).ToList();
        Assert.Equal(2, elbowItems.Count); // DN100 grubu ve DN50 grubu

        var dn100 = elbowItems.Single(b => b.Description.Contains("100"));
        Assert.Equal(2, dn100.Quantity);
        Assert.Equal("Adet", dn100.Unit);

        var dn50 = elbowItems.Single(b => b.Description.Contains("50"));
        Assert.Equal(1, dn50.Quantity);
    }

    [Fact]
    public void GenerateBom_Tees_AreGroupedByDiameterAndCounted()
    {
        var db = new CadDatabase();
        db.AddEntity(new TeeEntity(new Vector3D(0, 0, 0), 100, 100, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)));
        db.AddEntity(new TeeEntity(new Vector3D(10, 0, 0), 100, 100, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)));

        var bom = new BomService(db).GenerateBom();

        var teeItem = Assert.Single(bom, b => b.Description.StartsWith("T-Parçası"));
        Assert.Equal(2, teeItem.Quantity);
    }

    [Fact]
    public void GenerateBom_Fixtures_AreGroupedByFixtureTypeAndCounted()
    {
        var db = new CadDatabase();
        db.AddEntity(new SanitaryFixtureEntity(new Vector3D(0, 0, 0), "WC", 1.0));
        db.AddEntity(new SanitaryFixtureEntity(new Vector3D(10, 0, 0), "WC", 1.0));
        db.AddEntity(new SanitaryFixtureEntity(new Vector3D(20, 0, 0), "Washbasin", 0.5));

        var bom = new BomService(db).GenerateBom();

        var wcItem = Assert.Single(bom, b => b.Description == "WC");
        Assert.Equal(2, wcItem.Quantity);
        Assert.Equal("Sağlık Gereci", wcItem.Category);

        var lavItem = Assert.Single(bom, b => b.Description == "Washbasin");
        Assert.Equal(1, lavItem.Quantity);
    }

    [Fact]
    public void GenerateBom_EmptyDatabase_ReturnsEmptyList()
    {
        var db = new CadDatabase();
        var bom = new BomService(db).GenerateBom();
        Assert.Empty(bom);
    }

    [Fact]
    public void GenerateBom_Ducts_AreGroupedByShapeTypeSizeAndLengthSummed()
    {
        // NE/NEDEN — Session #75 iş akışı denetiminde bulunan gerçek boşluk: BomService
        // önceden DuctEntity/AirTerminalEntity/DamperEntity'yi hiç saymıyordu — yerleştirilen
        // HVAC donanımı metraj raporunda görünmüyordu.
        var db = new CadDatabase();
        var d1 = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0), 250);
        var d2 = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 250);
        db.AddEntity(d1);
        db.AddEntity(d2);

        var bom = new BomService(db).GenerateBom();

        var ductItem = Assert.Single(bom, b => b.Category == "Kanal");
        Assert.Equal((d1.GetLength() + d2.GetLength()) / 1000.0, ductItem.Quantity, precision: 3);
        Assert.Equal("m", ductItem.Unit);
    }

    [Fact]
    public void GenerateBom_AirTerminals_AreGroupedByTypeAndCounted()
    {
        var db = new CadDatabase();
        db.AddEntity(new AirTerminalEntity(new Vector3D(0, 0, 0), AirTerminalType.SupplyDiffuser, 100));
        db.AddEntity(new AirTerminalEntity(new Vector3D(10, 0, 0), AirTerminalType.SupplyDiffuser, 100));
        db.AddEntity(new AirTerminalEntity(new Vector3D(20, 0, 0), AirTerminalType.ReturnGrille, 100));

        var bom = new BomService(db).GenerateBom();

        var terminalItems = bom.Where(b => b.Category == "Hava Terminali").ToList();
        Assert.Equal(2, terminalItems.Count);
        Assert.Contains(terminalItems, i => i.Description == "SupplyDiffuser" && i.Quantity == 2);
        Assert.Contains(terminalItems, i => i.Description == "ReturnGrille" && i.Quantity == 1);
    }

    [Fact]
    public void GenerateBom_Dampers_AreGroupedByTypeAndCounted()
    {
        var db = new CadDatabase();
        db.AddEntity(new DamperEntity(new Vector3D(0, 0, 0), DamperType.Volume, 250));
        db.AddEntity(new DamperEntity(new Vector3D(10, 0, 0), DamperType.Volume, 250));

        var bom = new BomService(db).GenerateBom();

        var damperItem = Assert.Single(bom, b => b.Category == "Damper");
        Assert.Equal(2, damperItem.Quantity);
    }

    [Fact]
    public void GenerateBom_MixedEntities_IgnoresUnrelatedEntityTypes()
    {
        // LineEntity gibi BOM'da hiç kapsanmayan bir tip, sonuca kirlilik/hata olarak
        // yansımamalı — sadece bilinen kategoriler (Boru/Dirsek/T-Parçası/Sağlık Gereci) çıkmalı.
        var db = new CadDatabase();
        db.AddEntity(new Afney.Cad.Domain.Entities.Basic.LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0)));
        db.AddEntity(new SanitaryFixtureEntity(new Vector3D(0, 0, 0), "WC", 1.0));

        var bom = new BomService(db).GenerateBom();

        Assert.Single(bom);
        Assert.Equal("WC", bom[0].Description);
    }
}
