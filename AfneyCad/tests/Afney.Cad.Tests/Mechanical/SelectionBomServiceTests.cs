using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: SelectionBomService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu, BomService'in kardeşi):
          Bu servis kullanıcının o an seçtiği nesnelerden anlık bir metraj/maliyet özeti
          üretir — BomService gibi hiç testi yoktu. Bu testler tip bazlı sayım/uzunluk
          toplamasını ve maliyet formüllerini (boru: RealTimeCostService ile birebir,
          kanal: sabit 95 TRY/m, cihaz: sabit 850 TRY) kilitler.
*/
public class SelectionBomServiceTests
{
    [Fact]
    public void Calculate_MixedEntities_CountsAndLengthsAggregateByType()
    {
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 25);
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0), 200);
        var fixture = new SanitaryFixtureEntity(new Vector3D(0, 0, 0), "WC", 1.0);
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0));

        var svc = new SelectionBomService();
        var result = svc.Calculate(new CadEntity[] { pipe, duct, fixture, line });

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(1, result.PipeCount);
        Assert.Equal(5.0, result.PipeLengthM, precision: 3);
        Assert.Equal(1, result.DuctCount);
        Assert.Equal(3.0, result.DuctLengthM, precision: 3);
        Assert.Equal(1, result.FixtureCount);
        Assert.Equal(1, result.LineCount);
        Assert.Equal(2.0, result.LineLengthM, precision: 3);
    }

    [Fact]
    public void Calculate_PipeOnly_CostMatchesIndependentRealTimeCostServiceCall()
    {
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 25);

        var svc = new SelectionBomService();
        var result = svc.Calculate(new CadEntity[] { pipe });

        var costSvc = new RealTimeCostService();
        double expectedCost = costSvc.CalculateSinglePipeCost(pipe.GetLength(), PipeMaterial.PPRC_PN20, pipe.InnerDiameter);

        Assert.Equal(expectedCost, result.EstimatedCost, precision: 6);
    }

    [Fact]
    public void Calculate_DuctOnly_CostMatchesFixedPerMeterRate()
    {
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0), 250);

        var svc = new SelectionBomService();
        var result = svc.Calculate(new CadEntity[] { duct });

        // Sabit oran: 95 TRY/m (bkz. SelectionBomService.Calculate)
        Assert.Equal(4.0 * 95.0, result.EstimatedCost, precision: 6);
    }

    [Fact]
    public void Calculate_MultipleFixtures_CostAddsFixedRatePerFixture()
    {
        var f1 = new SanitaryFixtureEntity(new Vector3D(0, 0, 0), "WC", 1.0);
        var f2 = new SanitaryFixtureEntity(new Vector3D(1, 0, 0), "Washbasin", 0.5);

        var svc = new SelectionBomService();
        var result = svc.Calculate(new CadEntity[] { f1, f2 });

        Assert.Equal(2, result.FixtureCount);
        Assert.Equal(850.0 * 2, result.EstimatedCost, precision: 6);
    }

    [Fact]
    public void Calculate_EmptySelection_ReturnsZeroedResult()
    {
        var svc = new SelectionBomService();
        var result = svc.Calculate(Enumerable.Empty<CadEntity>());

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.EstimatedCost);
    }

    [Fact]
    public void Summary_OnlyIncludesSectionsForPresentEntityTypes()
    {
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25);
        var svc = new SelectionBomService();
        var result = svc.Calculate(new CadEntity[] { pipe });

        Assert.Contains("Boru:", result.Summary);
        Assert.DoesNotContain("Kanal:", result.Summary);
        Assert.DoesNotContain("Cihaz:", result.Summary);
        Assert.DoesNotContain("Cizgi:", result.Summary);
    }

    [Fact]
    public void ExportToHtml_ContainsCoreSummaryFigures()
    {
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 25);
        var svc = new SelectionBomService();
        var result = svc.Calculate(new CadEntity[] { pipe });

        string html = svc.ExportToHtml(result);

        Assert.Contains("Secim Bazli Metraj", html);
        Assert.Contains(result.PipeCount.ToString(), html);
        Assert.Contains("TOPLAM", html);
    }
}
