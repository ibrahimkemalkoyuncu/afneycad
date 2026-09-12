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
