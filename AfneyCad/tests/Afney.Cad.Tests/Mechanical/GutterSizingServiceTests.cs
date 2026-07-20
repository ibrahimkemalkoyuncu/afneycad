using System;
using System.Collections.Generic;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: GutterSizingService (TS EN 12056-3 Yağmur Oluğu/Deresi) Testleri
   NEDEN: Servis hiç test edilmemişti. Bu testler; efektif alan (A×C) ve tasarım debisi
          (Q=r×A_eff) hesabını, Manning formülüyle oluk çapı seçimini (%50 doluluk sınırı)
          ve dere borusu katalog seçimini elle hesaplanmış değerlerle doğruluyor.
*/
public class GutterSizingServiceTests
{
    [Fact]
    public void Calculate_TotalFlow_MatchesRainfallTimesEffectiveArea()
    {
        var svc = new GutterSizingService();
        var sections = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Çatı A", AreaM2 = 100, SurfaceType = "Çatı (kiremit/metal)" }, // C=1.0
            new() { Name = "Çatı B", AreaM2 = 50,  SurfaceType = "Çakıllı Çatı" },          // C=0.7
        };

        var result = svc.Calculate("İstanbul", sections);

        // r(İstanbul) = 0.028 l/s·m2
        // A_eff = 100*1.0 + 50*0.7 = 135
        // Q = 0.028 * 135 = 3.78 l/s
        Assert.Equal(0.028, result.RainfallLsM2, precision: 4);
        Assert.Equal(150, result.TotalAreaM2, precision: 2);
        Assert.Equal(135, result.TotalEffAreaM2, precision: 2);
        Assert.Equal(3.78, result.TotalFlowLs, precision: 3);
    }

    [Fact]
    public void Calculate_UnknownCity_FallsBackToGenelRainfallIntensity()
    {
        var svc = new GutterSizingService();
        var sections = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Çatı", AreaM2 = 10, SurfaceType = "Çatı (kiremit/metal)" },
        };

        var result = svc.Calculate("Bilinmeyen Şehir", sections);

        Assert.Equal(GutterSizingService.RainfallIntensity["Genel"], result.RainfallLsM2, precision: 6);
    }

    [Fact]
    public void Calculate_RainfallOverride_TakesPrecedenceOverCityLookup()
    {
        var svc = new GutterSizingService { RainfallOverride = 0.05 };
        var sections = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Çatı", AreaM2 = 20, SurfaceType = "Çatı (kiremit/metal)" },
        };

        var result = svc.Calculate("Ankara", sections); // Ankara r=0.020 normalde

        Assert.Equal(0.05, result.RainfallLsM2, precision: 6);
        Assert.Equal(0.05 * 20, result.TotalFlowLs, precision: 3);
    }

    [Fact]
    public void Calculate_GutterDiameter_SatisfiesManningHalfFullCapacityConstraint()
    {
        var svc = new GutterSizingService(); // slope=0.005, n=0.013
        var sections = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Çatı", AreaM2 = 80, SurfaceType = "Çatı (kiremit/metal)" },
        };

        var result = svc.Calculate("Ankara", sections);
        double qLs = result.TotalFlowLs;

        // Seçilen çapta: 0.5 * Q_full >= Q_tasarım (Manning formülüyle elle hesaplanmış)
        double dm = result.GutterDiameterMm / 1000.0;
        double radius = dm / 2.0;
        double area = Math.PI * radius * radius / 2.0;
        double rHyd = radius / 2.0;
        double v = (1.0 / svc.ManningN) * Math.Pow(rHyd, 2.0 / 3.0) * Math.Pow(svc.GutterSlope, 0.5);
        double qFullLs = v * area * 1000;

        Assert.True(qFullLs * 0.5 >= qLs - 1e-9);

        // Bir alt standart çap (varsa) bu koşulu SAĞLAMAMALI (minimum yeterli çap seçildiğini kanıtlar).
        int[] sizes = [75, 100, 125, 150, 200, 250, 300];
        int idx = Array.IndexOf(sizes, (int)result.GutterDiameterMm);
        if (idx > 0)
        {
            double dmPrev = sizes[idx - 1] / 1000.0;
            double radiusPrev = dmPrev / 2.0;
            double areaPrev = Math.PI * radiusPrev * radiusPrev / 2.0;
            double rPrev = radiusPrev / 2.0;
            double vPrev = (1.0 / svc.ManningN) * Math.Pow(rPrev, 2.0 / 3.0) * Math.Pow(svc.GutterSlope, 0.5);
            double qFullPrevLs = vPrev * areaPrev * 1000;

            Assert.True(qFullPrevLs * 0.5 < qLs);
        }
    }

    [Fact]
    public void Calculate_LargerRoofArea_RequiresLargerOrEqualGutterDiameter()
    {
        var svc = new GutterSizingService();
        var smallRoof = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Küçük", AreaM2 = 20, SurfaceType = "Çatı (kiremit/metal)" },
        };
        var largeRoof = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Büyük", AreaM2 = 500, SurfaceType = "Çatı (kiremit/metal)" },
        };

        var small = svc.Calculate("İstanbul", smallRoof);
        var large = svc.Calculate("İstanbul", largeRoof);

        Assert.True(large.GutterDiameterMm >= small.GutterDiameterMm);
        Assert.True(large.DownpipeCount >= small.DownpipeCount);
    }

    [Fact]
    public void Calculate_DownpipeSelection_FirstCatalogEntryCoveringDemandPerPipe()
    {
        var svc = new GutterSizingService();
        var sections = new List<GutterSizingService.RoofSection>
        {
            new() { Name = "Çatı", AreaM2 = 40, SurfaceType = "Çatı (kiremit/metal)" },
        };

        var result = svc.Calculate("Antalya", sections); // r=0.032

        double qLs = result.TotalFlowLs;
        // Katalog: (50,0.8) ilk uygun DN. count = ceil(Q/qMax).
        (int DN, double QMaxLs)[] catalog =
        [
            (50, 0.8), (63, 1.3), (75, 2.2), (90, 3.8), (100, 5.5), (110, 7.0), (125, 10.0), (160, 18.0)
        ];
        var (expectedDn, expectedQMax) = catalog[0];
        int expectedCount = (int)Math.Ceiling(qLs / expectedQMax);

        Assert.Equal(expectedDn, result.DownpipeDiameterMm);
        Assert.Equal(expectedCount, result.DownpipeCount);
    }

    [Fact]
    public void RoofSection_EffectiveArea_AppliesRunoffCoefficientCorrectly()
    {
        var section = new GutterSizingService.RoofSection { AreaM2 = 200, SurfaceType = "Yeşil Çatı" }; // C=0.5

        Assert.Equal(0.5, section.RunoffCoeff, precision: 6);
        Assert.Equal(100, section.EffectiveAreaM2, precision: 6);
    }

    [Fact]
    public void RoofSection_UnknownSurfaceType_DefaultsToRunoffCoefficientOfOne()
    {
        var section = new GutterSizingService.RoofSection { AreaM2 = 10, SurfaceType = "Bilinmeyen Yüzey" };

        Assert.Equal(1.0, section.RunoffCoeff, precision: 6);
        Assert.Equal(10, section.EffectiveAreaM2, precision: 6);
    }
}
