using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: UnifiedBomService (Genel Keşif) Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 iş akışı denetiminde bulundu, madde 04): 4 ayrı BOM
          servisi (BomService/HvacBomService/SelectionBomService/ArchitecturalBomService)
          hiçbir zaman tek bir raporda birleşmiyordu. Bu testler UnifiedBomService'in
          BomService (tesisat/HVAC) ve ArchitecturalBomService (mimari) çıktısını gerçekten
          birleştirdiğini ve maliyet toplamının yalnızca doğrulanmış kaynaklardan
          (PipeCostService + HvacBomService'in kanal fiyat formülü) geldiğini kilitler.
*/
public class UnifiedBomServiceTests
{
    [Fact]
    public void Generate_CombinesMechanicalAndArchitecturalItems()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 });
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5, 0, 0), 200));

        var result = new UnifiedBomService(db).Generate();

        Assert.Contains(result.MechanicalItems, i => i.Category == "Boru");
        Assert.Contains(result.ArchitecturalItems, i => i.Category == "Duvar");
    }

    [Fact]
    public void Generate_PipeCost_MatchesPipeCostService()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 });

        var expected = new PipeCostService().CalculateFromDatabase(db).TotalCostTl;
        var result = new UnifiedBomService(db).Generate();

        Assert.Equal(expected, result.PipeCostTl, precision: 2);
    }

    [Fact]
    public void Generate_DuctCost_ScalesWithRealLength()
    {
        var dbShort = new CadDatabase();
        dbShort.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 400, 300));
        var shortCost = new UnifiedBomService(dbShort).Generate().DuctCostTl;

        var dbLong = new CadDatabase();
        dbLong.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(20000, 0, 0), 400, 300));
        var longCost = new UnifiedBomService(dbLong).Generate().DuctCostTl;

        Assert.True(shortCost > 0);
        Assert.Equal(longCost, shortCost * 2, precision: 2);
    }

    [Fact]
    public void Generate_NoDucts_DuctCostIsZero()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 });

        var result = new UnifiedBomService(db).Generate();

        Assert.Equal(0, result.DuctCostTl);
    }

    /*
       NE/NEDEN — Session #75 iş akışı denetiminde bulunan boşluk (madde 04/68): poz kataloğu
       HVAC'ı hiç kapsamıyordu, Genel Keşif'te kanal/terminal/damper fiyatsızdı. PozKatalogService'e
       eklenen GRUP 30 (Havalandırma) kalemleri artık gerçek sac yüzey alanı (perimeter × uzunluk)
       ve adet üzerinden fiyatlandırıyor. Bu testler poz kataloğuyla birebir eşleştiğini kilitler.
    */
    [Fact]
    public void Generate_DuctCost_MatchesPozKatalogUnitPriceTimesSurfaceArea()
    {
        var db = new CadDatabase();
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 400, 300);
        db.AddEntity(duct);

        var poz = new PozKatalogService().FindForDuct(DuctShape.Rectangular)!;
        double expected = duct.GetInsulationArea() * (double)poz.BirimFiyat;

        var result = new UnifiedBomService(db).Generate();

        Assert.Equal(expected, result.DuctCostTl, precision: 2);
    }

    [Fact]
    public void Generate_AirTerminalAndDamperCost_MatchesPozKatalogUnitPriceTimesCount()
    {
        var db = new CadDatabase();
        db.AddEntity(new AirTerminalEntity(new Vector3D(0, 0, 0), AirTerminalType.SupplyDiffuser, 500));
        db.AddEntity(new AirTerminalEntity(new Vector3D(1000, 0, 0), AirTerminalType.ReturnGrille, 500));
        db.AddEntity(new DamperEntity(new Vector3D(0, 0, 0), DamperType.Volume, 300));

        var terminalPoz = new PozKatalogService().FindForAirTerminal()!;
        var damperPoz = new PozKatalogService().FindForDamper()!;

        var result = new UnifiedBomService(db).Generate();

        Assert.Equal(2 * (double)terminalPoz.BirimFiyat, result.AirTerminalCostTl, precision: 2);
        Assert.Equal(1 * (double)damperPoz.BirimFiyat, result.DamperCostTl, precision: 2);
    }

    [Fact]
    public void ExportToHtml_ContainsBothSections()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 100) { PipeMaterialType = PipeMaterial.PPRC_PN20 });
        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5, 0, 0), 200));

        var svc = new UnifiedBomService(db);
        var html = svc.ExportToHtml(svc.Generate(), "Test Projesi");

        Assert.Contains("GENEL KEŞİF", html);
        Assert.Contains("Tesisat / HVAC", html);
        Assert.Contains("Mimari", html);
    }
}
