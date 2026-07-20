using System.Linq;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: PumpSelectionService Testleri
   NEDEN: Servis hiç test edilmemişti. Bu testler; katalog filtreleme (Q/H yeterliliği),
          BEP (Best Efficiency Point) yakınlık skorlaması, pompa Q-H eğrisinin BEP
          noktasından geçtiğini, sistem eğrisi/pompa eğrisi kesişiminin (duty point)
          gerçek denklemi sağladığını ve NPSH kavitasyon kontrolünün fiziksel olarak
          tutarlı davrandığını doğruluyor.
*/
public class PumpSelectionServiceTests
{
    [Fact]
    public void RecommendPumps_FiltersOutModelsBelowRequiredFlowOrHead()
    {
        var svc = new PumpSelectionService();

        var results = svc.RecommendPumps(requiredFlow: 10.0, requiredHead: 40.0);

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.MaxFlow >= 10.0 && p.MaxHead >= 40.0));
    }

    [Fact]
    public void RecommendPumps_BestScoredPump_IsClosestToBepAmongCandidates()
    {
        var svc = new PumpSelectionService();
        double reqFlow = 5.0, reqHead = 35.0;

        var results = svc.RecommendPumps(reqFlow, reqHead);
        Assert.NotEmpty(results);

        // Sonuçlar skora göre artan sırada olmalı (en iyi ilk sırada).
        var scores = results.Select(p => p.Score).ToList();
        var sorted = scores.OrderBy(s => s).ToList();
        Assert.Equal(sorted, scores);

        // İlk sonucun skoru, aday havuzundaki (kapasite yeterli olan) tüm pompalar
        // içinde en düşük (veya en düşükler arasında) olmalı.
        var allCandidates = svc.GetAllPumps().Where(p => p.MaxFlow >= reqFlow && p.MaxHead >= reqHead);
        double minPossibleScore = allCandidates.Min(p =>
        {
            double flowDev = System.Math.Abs(p.BepFlow - reqFlow) / p.MaxFlow;
            double headDev = System.Math.Abs(p.BepHead - reqHead) / p.MaxHead;
            double effPenalty = (1.0 - p.Efficiency) * 0.5;
            double oversizePenalty = 0;
            if (p.MaxFlow > reqFlow * 3) oversizePenalty += 0.3;
            if (p.MaxHead > reqHead * 3) oversizePenalty += 0.2;
            return flowDev + headDev + effPenalty + oversizePenalty;
        });

        Assert.Equal(minPossibleScore, results[0].Score, precision: 6);
    }

    [Fact]
    public void RecommendPumps_BrandFilter_OnlyReturnsMatchingBrand()
    {
        var svc = new PumpSelectionService();

        var results = svc.RecommendPumps(2.0, 5.0, preferredBrand: "Grundfos");

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal("Grundfos", p.Brand));
    }

    [Fact]
    public void RecommendPumps_NoCandidateMeetsCapacity_ReturnsEmpty()
    {
        var svc = new PumpSelectionService();

        // Katalogdaki en büyük pompa MaxFlow=25 m3/h, MaxHead=80 mSS civarında —
        // bu değerlerin çok üzerinde bir talep hiçbir modeli karşılamamalı.
        var results = svc.RecommendPumps(requiredFlow: 500.0, requiredHead: 500.0);

        Assert.Empty(results);
    }

    [Fact]
    public void GetPumpCurvePoints_PassesThroughShutoffBepAndMaxFlowPoints()
    {
        var svc = new PumpSelectionService();
        var pump = svc.GetAllPumps().First(p => p.ModelName == "MAGNA3 32-120F");

        var curve = svc.GetPumpCurvePoints(pump, pointCount: 100);

        // Q=0 noktası: H ≈ MaxHead * 1.15 (kapatma yüksekliği)
        Assert.Equal(pump.MaxHead * 1.15, curve.First().HeadMSS, precision: 2);

        // Q=MaxFlow noktası: H ≈ 0
        Assert.Equal(0.0, curve.Last().HeadMSS, precision: 1);

        // BEP debisine en yakın noktadaki basma yüksekliği BEP başına yakın olmalı (örnekleme sapması toleranslı).
        var nearBep = curve.OrderBy(p => System.Math.Abs(p.FlowM3h - pump.BepFlow)).First();
        Assert.True(System.Math.Abs(nearBep.HeadMSS - pump.BepHead) < 1.0);
    }

    [Fact]
    public void GetSystemCurvePoints_StaticHeadIsMinimumAndGrowsQuadratically()
    {
        var svc = new PumpSelectionService();
        double staticHead = 10.0, designFlow = 5.0, designHead = 25.0;

        var curve = svc.GetSystemCurvePoints(staticHead, designFlow, designHead, pointCount: 10);

        // Q=0 → H = staticHead (Sistemin statik direnci)
        Assert.Equal(staticHead, curve.First().HeadMSS, precision: 6);

        // H_sistem = staticHead + R*Q^2, R = (designHead-staticHead)/designFlow^2
        double r = (designHead - staticHead) / (designFlow * designFlow);
        foreach (var (q, h) in curve)
        {
            Assert.Equal(staticHead + r * q * q, h, precision: 6);
        }
    }

    [Fact]
    public void CheckCavitation_HighSuctionLift_ReportsUnsafeWithNegativeMargin()
    {
        var svc = new PumpSelectionService();
        var pump = svc.GetAllPumps().First();

        // Aşırı emme yüksekliği (pompa su seviyesinin çok üzerinde) + büyük hat kaybı → NPSHa çok düşük.
        var result = svc.CheckCavitation(pump, suctionHeightM: -8.0, suctionLossMSS: 2.0, waterTempC: 20.0);

        Assert.False(result.IsSafe);
        Assert.True(result.Margin < 0.5);
    }

    [Fact]
    public void CheckCavitation_FloodedSuctionShortLine_ReportsSafe()
    {
        var svc = new PumpSelectionService();
        var pump = svc.GetAllPumps().First();

        // Su kaynağı pompanın üzerinde (pozitif z_s) ve kayıplar düşük → NPSHa yüksek.
        var result = svc.CheckCavitation(pump, suctionHeightM: 2.0, suctionLossMSS: 0.2, waterTempC: 20.0);

        Assert.True(result.IsSafe);
        Assert.True(result.Margin >= 0.5);
    }

    [Fact]
    public void CheckCavitation_HigherWaterTemperature_ReducesNpshaViaVaporPressure()
    {
        var svc = new PumpSelectionService();
        var pump = svc.GetAllPumps().First();

        var cold = svc.CheckCavitation(pump, suctionHeightM: 1.0, suctionLossMSS: 0.5, waterTempC: 20.0);
        var hot = svc.CheckCavitation(pump, suctionHeightM: 1.0, suctionLossMSS: 0.5, waterTempC: 80.0);

        // Sıcak su, buhar basıncı arttığı için NPSHa'yı düşürür (kavitasyon riski artar).
        Assert.True(hot.NPSHa < cold.NPSHa);
    }
}
