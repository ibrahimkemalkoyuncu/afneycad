using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: HvacBomService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu, BomService'in kardeşi):
          HVAC kanal metrajı/maliyeti hiç test edilmiyordu. Bu testler gruplama (Shape+Type+Boyut),
          fitting tahmini (dirsek=grup sayısı-1, T-parçası=dirsek/3), izolasyon alanı ekleme
          koşulunu ve menfez/damper minimum-1 tahminini kilitler.
*/
public class HvacBomServiceTests
{
    private static DuctEntity MakeCircularDuct(double lengthMm = 5000, double diameterMm = 250)
        => new(new Vector3D(0, 0, 0), new Vector3D(lengthMm, 0, 0), diameterMm);

    [Fact]
    public void Generate_SingleDuct_ProducesDuctItemWithCorrectLength()
    {
        var db = new CadDatabase();
        db.AddEntity(MakeCircularDuct(5000, 250));

        var result = new HvacBomService(db).Generate();

        var ductItem = Assert.Single(result.Items, i => i.Category == "Kanal");
        Assert.Equal(5.0, ductItem.Quantity, precision: 2);
        Assert.Equal(1, result.DuctCount);
        Assert.Equal(5.0, result.TotalDuctLength, precision: 2);
    }

    [Fact]
    public void Generate_SingleDuct_NoFittingsProduced()
    {
        // Grup icinde 1 kanal varsa elbows = max(0, 1-1) = 0 -> hic fitting eklenmemeli.
        var db = new CadDatabase();
        db.AddEntity(MakeCircularDuct());

        var result = new HvacBomService(db).Generate();

        Assert.DoesNotContain(result.Items, i => i.Category == "Fitting");
        Assert.Equal(0, result.FittingCount);
    }

    [Fact]
    public void Generate_FourDuctsSameGroup_ProducesThreeElbowsAndOneTee()
    {
        // Ayni Shape+Type+Boyut grubunda 4 kanal -> elbows = max(0,4-1) = 3, tees = 3/3 = 1.
        var db = new CadDatabase();
        for (int i = 0; i < 4; i++)
            db.AddEntity(MakeCircularDuct(1000 + i, 250));

        var result = new HvacBomService(db).Generate();

        var elbowItem = Assert.Single(result.Items, i => i.Category == "Fitting" && i.Description.Contains("Dirsek"));
        Assert.Equal(3, elbowItem.Quantity);

        var teeItem = Assert.Single(result.Items, i => i.Category == "Fitting" && i.Description.Contains("Te"));
        Assert.Equal(1, teeItem.Quantity);

        Assert.Equal(4, result.FittingCount); // 3 dirsek + 1 T
    }

    [Fact]
    public void Generate_InsulatedDuct_ProducesInsulationLineItem()
    {
        var duct = MakeCircularDuct();
        duct.InsulationMm = 25; // varsayilan zaten >0

        var db = new CadDatabase();
        db.AddEntity(duct);

        var result = new HvacBomService(db).Generate();

        Assert.Contains(result.Items, i => i.Category == "Izolasyon");
        Assert.True(result.TotalInsulationArea > 0);
    }

    [Fact]
    public void Generate_ZeroInsulationDuct_NoInsulationLineItem()
    {
        var duct = MakeCircularDuct();
        duct.InsulationMm = 0;

        var db = new CadDatabase();
        db.AddEntity(duct);

        var result = new HvacBomService(db).Generate();

        Assert.DoesNotContain(result.Items, i => i.Category == "Izolasyon");
    }

    [Fact]
    public void Generate_DifferentShapeOrType_ProducesSeparateGroups()
    {
        var db = new CadDatabase();
        db.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 250) { Shape = DuctShape.Circular });
        db.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 300, 200) { Shape = DuctShape.Rectangular });

        var result = new HvacBomService(db).Generate();

        Assert.Equal(2, result.Items.Count(i => i.Category == "Kanal"));
    }

    [Fact]
    public void Generate_EmptyDatabase_StillAddsMinimumOneGrillAndOneDamper()
    {
        // grillCount = max(1, 0/2) = 1 ; damperCount = max(1, 0/4) = 1 -- kanal olmasa bile.
        var db = new CadDatabase();
        var result = new HvacBomService(db).Generate();

        var grill = Assert.Single(result.Items, i => i.Category == "Menfez");
        Assert.Equal(1, grill.Quantity);

        var damper = Assert.Single(result.Items, i => i.Category == "Damper");
        Assert.Equal(1, damper.Quantity);

        Assert.Equal(0, result.DuctCount);
    }

    [Fact]
    public void Generate_TotalCost_EqualsSumOfAllItemTotalPrices()
    {
        var db = new CadDatabase();
        db.AddEntity(MakeCircularDuct(5000, 250));
        db.AddEntity(MakeCircularDuct(3000, 250));

        var result = new HvacBomService(db).Generate();

        double expectedTotal = result.Items.Sum(i => i.Quantity * i.UnitPrice);
        Assert.Equal(expectedTotal, result.TotalCost, precision: 3);
        Assert.True(result.TotalCost > 0);
    }

    [Fact]
    public void ExportToHtml_ContainsCoreSummaryFigures()
    {
        var db = new CadDatabase();
        db.AddEntity(MakeCircularDuct());
        var svc = new HvacBomService(db);
        var result = svc.Generate();

        string html = svc.ExportToHtml(result, "Test Projesi");

        Assert.Contains("HVAC KANAL METRAJ TABLOSU", html);
        Assert.Contains("Test Projesi", html);
        Assert.Contains("GENEL TOPLAM", html);
    }

    [Fact]
    public void ExportToCsv_ContainsHeaderAndItemRows()
    {
        var db = new CadDatabase();
        db.AddEntity(MakeCircularDuct());
        var svc = new HvacBomService(db);
        var result = svc.Generate();

        string csv = svc.ExportToCsv(result);

        Assert.Contains("Kategori;Açıklama", csv);
        Assert.Contains("Kanal;", csv);
    }
}
