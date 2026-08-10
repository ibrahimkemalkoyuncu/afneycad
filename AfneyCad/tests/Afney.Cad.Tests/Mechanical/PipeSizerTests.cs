using System;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: PipeSizer Formül Doğrulama Testleri (PipeSizerTests)
   NEDEN: PipeSizer, projedeki tüm boru çaplandırma komutlarının (AutoSizing, hesap tablosu,
          basınç kaybı raporu) temelinde yer alan saf matematik katmanıdır — hiç testi yoktu.
          Bu testler; TS 1258/DIN 1988 K-katsayı tablosunun formüle doğru uygulandığını,
          ASPE Hunter Curve tablosunun (Uniform Plumbing Code Table A-2) doğru enterpole
          edildiğini ve süreklilik denklemiyle (Q=A·V) çap hesabının matematiksel olarak
          doğru olduğunu doğruluyor.

   KAPSAM:
   1. CalculateDesignFlow — Q = k·√ΣLU (varsayılan k=0.25)
   2. CalculateDesignFlowByStandard — Standart/Bina tipine göre K katsayı seçimi
   3. HunterCurveLookup — ASPE WSFU→l/s tablosu (bilinen noktalar + ara değer interpolasyonu)
   4. CalculateRequiredInnerDiameter — d = √(4Q/(π·v)) süreklilik denklemi
   5. GetStandardSize — Katalogdan DN yuvarlama (PPRC_PN20 iç çap tablosuna göre)
   6. GetMaxVelocity / GetStandardDescription — Yardımcı metodlar
*/
public class PipeSizerTests
{
    // ─────────────────────────────────────────────────────────────────
    // 1. CalculateDesignFlow — Q = k * sqrt(sumLU)
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16.0, 0.25, 1.0)]   // 0.25 * sqrt(16) = 0.25*4 = 1.0
    [InlineData(4.0, 0.25, 0.5)]    // 0.25 * sqrt(4)  = 0.25*2 = 0.5
    [InlineData(100.0, 0.5, 5.0)]   // 0.5  * sqrt(100)= 0.5*10 = 5.0
    public void CalculateDesignFlow_KnownInputs_MatchFormula(double sumLU, double k, double expected)
    {
        double result = PipeSizer.CalculateDesignFlow(sumLU, k);
        Assert.Equal(expected, result, precision: 9);
    }

    [Fact]
    public void CalculateDesignFlow_DefaultK_Is025()
    {
        // Varsayılan parametre k=0.25 — TS 1258/DIN 1988 konut katsayısı
        double result = PipeSizer.CalculateDesignFlow(9.0);
        Assert.Equal(0.25 * Math.Sqrt(9.0), result, precision: 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void CalculateDesignFlow_ZeroOrNegativeLU_ReturnsZero(double sumLU)
    {
        Assert.Equal(0, PipeSizer.CalculateDesignFlow(sumLU));
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. CalculateDesignFlowByStandard — K katsayı tablosu doğru uygulanıyor mu
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PipeSizer.PlumbingStandard.TS1258_DIN1988, PipeSizer.BuildingCategory.Residential, 0.25)]
    [InlineData(PipeSizer.PlumbingStandard.TS1258_DIN1988, PipeSizer.BuildingCategory.Commercial, 0.50)]
    [InlineData(PipeSizer.PlumbingStandard.TS1258_DIN1988, PipeSizer.BuildingCategory.Hotel, 0.70)]
    [InlineData(PipeSizer.PlumbingStandard.TS1258_DIN1988, PipeSizer.BuildingCategory.Industrial, 1.00)]
    [InlineData(PipeSizer.PlumbingStandard.TSEN806_3, PipeSizer.BuildingCategory.Residential, 0.50)]
    [InlineData(PipeSizer.PlumbingStandard.BS6700, PipeSizer.BuildingCategory.Residential, 0.20)]
    [InlineData(PipeSizer.PlumbingStandard.ASHRAE_90_1, PipeSizer.BuildingCategory.Commercial, 0.65)]
    public void CalculateDesignFlowByStandard_UsesCorrectKFactor(
        PipeSizer.PlumbingStandard standard, PipeSizer.BuildingCategory category, double expectedK)
    {
        double sumLU = 25.0; // sqrt = 5 -> kolay doğrulama
        double expected = expectedK * Math.Sqrt(sumLU);

        double result = PipeSizer.CalculateDesignFlowByStandard(sumLU, standard, category);

        Assert.Equal(expected, result, precision: 9);
    }

    [Fact]
    public void CalculateDesignFlowByStandard_ZeroOrNegativeLU_ReturnsZero()
    {
        Assert.Equal(0, PipeSizer.CalculateDesignFlowByStandard(0));
        Assert.Equal(0, PipeSizer.CalculateDesignFlowByStandard(-5));
    }

    [Fact]
    public void CalculateDesignFlowByStandard_AspeHunter_DelegatesToHunterCurveLookup()
    {
        // ASPE_Hunter standardı seçildiğinde K katsayı formülü değil, Hunter eğrisi kullanılmalı.
        double wsfu = 20;
        double expected = PipeSizer.HunterCurveLookup(wsfu);

        double result = PipeSizer.CalculateDesignFlowByStandard(wsfu, PipeSizer.PlumbingStandard.ASPE_Hunter);

        Assert.Equal(expected, result, precision: 9);
        Assert.Equal(0.600, result, precision: 9); // Tablodaki tam nokta: WSFU=20 -> 0.600 l/s
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. HunterCurveLookup — Uniform Plumbing Code Table A-2 tabanlı eğri
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0.063)]
    [InlineData(10, 0.379)]
    [InlineData(100, 1.380)]
    [InlineData(1000, 6.310)]
    public void HunterCurveLookup_ExactTablePoints_ReturnsTableValue(double wsfu, double expected)
    {
        double result = PipeSizer.HunterCurveLookup(wsfu);
        Assert.Equal(expected, result, precision: 9);
    }

    [Fact]
    public void HunterCurveLookup_BelowMinimum_ClampsToFirstEntry()
    {
        Assert.Equal(0.063, PipeSizer.HunterCurveLookup(0.5), precision: 9);
    }

    [Fact]
    public void HunterCurveLookup_AboveMaximum_ClampsToLastEntry()
    {
        Assert.Equal(6.310, PipeSizer.HunterCurveLookup(5000), precision: 9);
    }

    [Fact]
    public void HunterCurveLookup_MidpointBetweenKnownEntries_LinearlyInterpolates()
    {
        // WSFU=5 tabloda yok; 4(0.220) ile 6(0.284) arasında, tam ortada (t=0.5).
        // Beklenen: 0.220 + 0.5*(0.284-0.220) = 0.252
        double expected = 0.220 + 0.5 * (0.284 - 0.220);
        double result = PipeSizer.HunterCurveLookup(5);
        Assert.Equal(expected, result, precision: 9);
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. CalculateRequiredInnerDiameter — d = sqrt(4Q / (pi*v))  [Süreklilik denklemi]
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateRequiredInnerDiameter_KnownFlowAndVelocity_MatchesContinuityEquation()
    {
        double flowLs = 2.0;
        double targetVelocity = 2.0;

        double qM3s = flowLs / 1000.0;
        double expectedMeters = Math.Sqrt((4.0 * qM3s) / (Math.PI * targetVelocity));
        double expectedMm = expectedMeters * 1000.0;

        double result = PipeSizer.CalculateRequiredInnerDiameter(flowLs, targetVelocity);

        Assert.Equal(expectedMm, result, precision: 9);
    }

    [Fact]
    public void CalculateRequiredInnerDiameter_DefaultTargetVelocity_Is1_5()
    {
        double flowLs = 1.0;
        double expected = PipeSizer.CalculateRequiredInnerDiameter(flowLs, 1.5);

        double result = PipeSizer.CalculateRequiredInnerDiameter(flowLs);

        Assert.Equal(expected, result, precision: 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void CalculateRequiredInnerDiameter_ZeroOrNegativeFlow_ReturnsZero(double flowLs)
    {
        Assert.Equal(0, PipeSizer.CalculateRequiredInnerDiameter(flowLs));
    }

    [Fact]
    public void CalculateRequiredInnerDiameter_HigherVelocity_ProducesSmallerDiameter()
    {
        // Aynı debi için hız arttıkça gereken çap küçülmeli (A=Q/V, d~1/sqrt(V)).
        double flowLs = 3.0;
        double dLowV = PipeSizer.CalculateRequiredInnerDiameter(flowLs, 1.0);
        double dHighV = PipeSizer.CalculateRequiredInnerDiameter(flowLs, 3.0);

        Assert.True(dHighV < dLowV);
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. GetStandardSize — Katalogdan (PPRC_PN20) uygun DN'e yukarı yuvarlama
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GetStandardSize_RequiredDiameterMatchesExactCatalogEntry_ReturnsThatDN()
    {
        // PPRC_PN20 kataloğunda DN25 -> iç çap 20.4mm (bkz PipeCatalog.InitializePPRC_PN20)
        double result = PipeSizer.GetStandardSize(20.4, PipeMaterial.PPRC_PN20);
        Assert.Equal(25, result);
    }

    [Fact]
    public void GetStandardSize_RequiredDiameterBetweenCatalogEntries_RoundsUpToNextDN()
    {
        // Gereken iç çap 17mm -> DN20 (ID=16.2) yetmez, DN25 (ID=20.4) yeterli -> DN25 seçilmeli
        double result = PipeSizer.GetStandardSize(17.0, PipeMaterial.PPRC_PN20);
        Assert.Equal(25, result);
    }

    [Fact]
    public void GetStandardSize_RequiredDiameterAboveAllCatalogEntries_ReturnsLargestDN()
    {
        // PPRC_PN20 kataloğundaki en büyük DN 110 (ID=90.0)
        double result = PipeSizer.GetStandardSize(500.0, PipeMaterial.PPRC_PN20);
        Assert.Equal(110, result);
    }

    [Fact]
    public void GetStandardSize_VerySmallRequirement_ReturnsSmallestSufficientDN()
    {
        // PPRC_PN20 kataloğundaki en küçük DN 20 (ID=16.2)
        double result = PipeSizer.GetStandardSize(1.0, PipeMaterial.PPRC_PN20);
        Assert.Equal(20, result);
    }

    // ─────────────────────────────────────────────────────────────────
    // 6. Yardımcı Metodlar
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMaxVelocity_TS1258_Supply_Is2_0()
    {
        Assert.Equal(2.0, PipeSizer.GetMaxVelocity(PipeSizer.PlumbingStandard.TS1258_DIN1988, isSupply: true));
    }

    [Fact]
    public void GetMaxVelocity_TS1258_Return_Is1_5()
    {
        Assert.Equal(1.5, PipeSizer.GetMaxVelocity(PipeSizer.PlumbingStandard.TS1258_DIN1988, isSupply: false));
    }

    [Fact]
    public void GetMaxVelocity_BS6700_IsStricterThanTS1258()
    {
        // BS 6700 (İngiltere), TS1258/DIN1988'e göre daha düşük gürültü limiti uygular.
        double bs = PipeSizer.GetMaxVelocity(PipeSizer.PlumbingStandard.BS6700, true);
        double ts = PipeSizer.GetMaxVelocity(PipeSizer.PlumbingStandard.TS1258_DIN1988, true);

        Assert.True(bs < ts);
    }

    [Fact]
    public void GetStandardDescription_ReturnsNonEmptyDescriptionForEachStandard()
    {
        foreach (PipeSizer.PlumbingStandard std in Enum.GetValues(typeof(PipeSizer.PlumbingStandard)))
        {
            string desc = PipeSizer.GetStandardDescription(std);
            Assert.False(string.IsNullOrWhiteSpace(desc));
            Assert.NotEqual("Bilinmeyen standart", desc);
        }
    }
}
