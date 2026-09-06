using System;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: NFPA13SprinklerService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu): Bu servis, yangın
          söndürme sistemi tasarımı (sprinkler debisi/basınç/boru çapı) yapan, hatalı
          çıktısının doğrudan yetersiz yangın koruması (can/mal güvenliği riski) anlamına
          geldiği bir servistir — ama kod tabanında HİÇ testi yoktu (FireFightingService,
          farklı bir standardı — EN 12845 — uygulayan KARDEŞ servis, zaten test ediliyordu,
          bu servis atlanmıştı). Bu testler NFPA 13'ün elle hesaplanmış temel formüllerini
          (q = K×√P, toplam debi = tek sprinkler debisi × aktif sayı) ve uyumluluk/uyarı
          mantığını kilitler.
*/
public class NFPA13SprinklerServiceTests
{
    [Fact]
    public void Calculate_LightHazard_MatchesHandComputedDesignParamsAndFlows()
    {
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard,
            AreaM2 = 300
        };

        var result = NFPA13SprinklerService.Calculate(input);

        // NFPA 13 Light Hazard: yoğunluk 4.1 L/(dak·m²), tasarım alanı 139 m².
        Assert.Equal(4.1, result.DesignDensityLpmdpm2, precision: 6);
        Assert.Equal(139, result.DesignAreaM2, precision: 6);

        // ActiveSprinklerCount = ceil(139 / 12) = 12 (MaxCoverageM2 varsayılan 12).
        Assert.Equal(12, result.ActiveSprinklerCount);
        // TotalSprinklerCount = ceil(300 / 12) = 25.
        Assert.Equal(25, result.TotalSprinklerCount);

        // Tek sprinkler debisi: q = yoğunluk × kapsama = 4.1 × 12 = 49.2 L/dak.
        Assert.Equal(49.2, result.SprinklerFlowLpd, precision: 6);
        // Toplam tasarım debisi = 49.2 × 12 = 590.4 L/dak = 35.424 m³/sa.
        Assert.Equal(590.4, result.TotalDesignFlowLpd, precision: 6);
        Assert.Equal(35.424, result.TotalDesignFlowM3h, precision: 6);
    }

    [Fact]
    public void Calculate_MinPressureRequired_UsesFormulaWhenItExceedsInputMinimum()
    {
        // P = (q/K)² formülü, girilen MinPressureBar'dan (varsayılan 0.70) BÜYÜKSE onu
        // ezip gerçek hesaplanan değeri kullanmalı — sadece sabit 0.70 döndürmemeli.
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard1,
            AreaM2 = 300
        };

        var result = NFPA13SprinklerService.Calculate(input);

        // q = 6.1 × 12 = 73.2 L/dak. P = (73.2/80)² = 0.837225 bar > 0.70 varsayılan min.
        double expectedFlow = 6.1 * 12;
        double expectedPressure = Math.Pow(expectedFlow / input.KFactor, 2);

        Assert.Equal(expectedFlow, result.SprinklerFlowLpd, precision: 6);
        Assert.Equal(expectedPressure, result.MinPressureBarRequired, precision: 6);
        Assert.True(result.MinPressureBarRequired > input.MinPressureBar);
    }

    [Fact]
    public void Calculate_MinPressureRequired_FallsBackToInputMinimum_WhenFormulaGivesLessThanIt()
    {
        // LightHazard'ın düşük yoğunluğunda hesaplanan P, varsayılan 0.70 bar'ın altında
        // kalır — bu durumda MinPressureBarRequired girilen MinPressureBar'a (taban) eşit
        // olmalı, hesaplanan (daha düşük) değere DEĞİL — güvenlik tarafı hep daha büyük olan
        // kazanmalı.
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard,
            AreaM2 = 300
        };

        var result = NFPA13SprinklerService.Calculate(input);

        double expectedFlow = 4.1 * 12;
        double formulaPressure = Math.Pow(expectedFlow / input.KFactor, 2);
        Assert.True(formulaPressure < input.MinPressureBar, "Test kurgusu bozuk: formül değeri tabanı geçmemeli.");

        Assert.Equal(input.MinPressureBar, result.MinPressureBarRequired, precision: 6);
    }

    [Fact]
    public void Calculate_HigherHazardClass_ProducesHigherDesignFlowAndPressure()
    {
        var light = NFPA13SprinklerService.Calculate(new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard, AreaM2 = 300
        });
        var extra2 = NFPA13SprinklerService.Calculate(new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard2, AreaM2 = 300
        });

        Assert.True(extra2.TotalDesignFlowLpd > light.TotalDesignFlowLpd,
            $"Extra Hazard 2 toplam debisi ({extra2.TotalDesignFlowLpd}) Light Hazard'dan ({light.TotalDesignFlowLpd}) büyük olmalı.");
        Assert.True(extra2.MinPressureBarRequired > light.MinPressureBarRequired);
    }

    [Fact]
    public void Calculate_CoverageWithinLimit_AddsComplianceMessage_NotWarning()
    {
        // Varsayılan MaxCoverageM2=12, OrdinaryHazard1 limiti 12.1 m² — 12 ≤ 12.1, uygun.
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard1,
            AreaM2 = 300
        };

        var result = NFPA13SprinklerService.Calculate(input);

        Assert.Contains(result.Compliance, m => m.Contains("Sprinkler başına kapsama"));
        Assert.DoesNotContain(result.Warnings, m => m.Contains("NFPA 13 limiti"));
    }

    [Fact]
    public void Calculate_CoverageExceedsLimit_AddsWarningMessage()
    {
        // OrdinaryHazard1 limiti 12.1 m² — 15 m² kapsama bu limiti aşar.
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard1,
            AreaM2 = 300,
            MaxCoverageM2 = 15
        };

        var result = NFPA13SprinklerService.Calculate(input);

        Assert.Contains(result.Warnings, m => m.Contains("NFPA 13 limiti"));
    }

    [Fact]
    public void Calculate_DryPipeTrue_AddsDryPipeFillTimeWarning()
    {
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard,
            AreaM2 = 300,
            DryPipe = true
        };

        var result = NFPA13SprinklerService.Calculate(input);

        Assert.Contains(result.Warnings, m => m.Contains("Kuru sistem"));
    }

    [Fact]
    public void Calculate_DryPipeFalse_DoesNotAddDryPipeWarning()
    {
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard,
            AreaM2 = 300,
            DryPipe = false
        };

        var result = NFPA13SprinklerService.Calculate(input);

        Assert.DoesNotContain(result.Warnings, m => m.Contains("Kuru sistem"));
    }

    [Fact]
    public void Calculate_LowResidualPressure_AddsPumpRequiredWarning()
    {
        // ExtraHazard2'nin yüksek gerekli basıncı (~5.98 bar), varsayılan 7.0 bar statik
        // basınçtan 0.5 bar kayıp düşülünce artık basıncı 0.70 bar eşiğinin altına düşürür
        // -> pompa gerekli uyarısı beklenir.
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard2,
            AreaM2 = 300
        };

        var result = NFPA13SprinklerService.Calculate(input);

        Assert.True(result.ResidualPressureBar < 0.7,
            $"Test kurgusu bozuk: artık basınç ({result.ResidualPressureBar}) 0.70 altında olmalıydı.");
        Assert.Contains(result.Warnings, m => m.Contains("pompa gerekli"));
    }

    [Fact]
    public void Calculate_AdequateResidualPressure_AddsComplianceMessage()
    {
        var input = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard,
            AreaM2 = 300
        };

        var result = NFPA13SprinklerService.Calculate(input);

        Assert.True(result.ResidualPressureBar >= 0.7);
        Assert.Contains(result.Compliance, m => m.Contains("Artık basınç"));
    }

    [Fact]
    public void Calculate_HigherFlow_ProducesEqualOrLargerSupplyPipeSize()
    {
        // Boru çapı, debiyle birlikte monoton artmalı (küçülmemeli) — Hazen-Williams
        // ters hesabının yönü doğru mu diye kaba bir sağlama.
        var light = NFPA13SprinklerService.Calculate(new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.LightHazard, AreaM2 = 300
        });
        var extra2 = NFPA13SprinklerService.Calculate(new NFPA13SprinklerService.SprinklerInput
        {
            Hazard = NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard2, AreaM2 = 300
        });

        static int DnRank(string dn) => dn switch
        {
            "DN25" => 25, "DN32" => 32, "DN40" => 40, "DN50" => 50,
            "DN65" => 65, "DN80" => 80, "DN100" => 100, "DN125+" => 125,
            _ => throw new Exception($"Beklenmeyen DN: {dn}")
        };

        Assert.True(DnRank(extra2.SupplyPipeSize) >= DnRank(light.SupplyPipeSize),
            $"Extra Hazard 2 besleme borusu ({extra2.SupplyPipeSize}) Light Hazard'dan ({light.SupplyPipeSize}) küçük olamaz.");
    }

    [Theory]
    [InlineData(NFPA13SprinklerService.NFPA13HazardClass.LightHazard)]
    [InlineData(NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard1)]
    [InlineData(NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard2)]
    [InlineData(NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard1)]
    [InlineData(NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard2)]
    [InlineData(NFPA13SprinklerService.NFPA13HazardClass.EarlySuppressionFastResponse)]
    public void HazardDescription_AllClasses_ReturnsNonEmptyDescription(NFPA13SprinklerService.NFPA13HazardClass hazard)
    {
        string description = NFPA13SprinklerService.HazardDescription(hazard);
        Assert.False(string.IsNullOrWhiteSpace(description));
    }
}
