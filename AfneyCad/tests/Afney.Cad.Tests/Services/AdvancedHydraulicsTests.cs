using System;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

public class AdvancedHydraulicsTests
{
    [Fact]
    public void ColebrookWhite_Laminar_Returns64OverRe()
    {
        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(1000, 0.007, 20);
        Assert.InRange(f, 0.063, 0.065); // 64/1000 = 0.064
    }

    [Fact]
    public void ColebrookWhite_Turbulent_ReturnsReasonableValue()
    {
        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(50000, 0.045, 50);
        Assert.InRange(f, 0.01, 0.05);
    }

    /*
       NE: Colebrook-White — Bağımsız Referansa Karşı Doğrulama (Swamee-Jain Çapraz Kontrolü)
       NEDEN: Denetim raporu bulgusu: yukarıdaki "ReturnsReasonableValue" testi f'nin sadece
              [0.01, 0.05] aralığında OLMASINI kontrol ediyor — bu, formülün DOĞRU çalıştığını
              değil, sadece "saçma bir değer dönmediğini" kanıtlar. Kod, Newton-Raphson'a
              başlangıç tahmini olarak Swamee-Jain kullanıyor (bkz. AdvancedHydraulicsService
              satır 22) — yani üretim kodunun KENDİ ürettiği tahminle karşılaştırmak dairesel
              olur. Bu test yerine, Swamee-Jain'i (f = 0.25 / [log10(ε/3.7D + 5.74/Re^0.9)]²)
              BAĞIMSIZ OLARAK burada yeniden hesaplıyor ve Newton-Raphson'un YAKINSADIĞI
              sonucun, literatürde bilinen ~%1-2 farkla örtüştüğünü — gerçek boru tasarımında
              kullanılan bir Reynolds/pürüzlülük matrisinde — doğruluyor. İki formül anlaşırsa,
              iterasyonun kendi kendine yanlış bir sabit noktaya "kilitlenmediğinin" kanıtıdır.
    */
    [Theory]
    [InlineData(4000, 0.0015, 15)]     // Re=4000 (geçiş bölgesi sınırı), PPRC pürüzlülüğü, DN15
    [InlineData(10000, 0.0015, 25)]    // Tipik lavabo/WC besleme hattı
    [InlineData(100000, 0.045, 50)]    // Çelik boru, orta çaplı ana hat (Moody diyagramının klasik bölgesi)
    [InlineData(1000000, 0.045, 150)]  // Büyük çaplı çelik kolon, yüksek Re
    public void ColebrookWhite_MatchesIndependentSwameeJainReference_Within2Percent(
        double reynolds, double roughnessMm, double diameterMm)
    {
        double eps = roughnessMm / 1000.0;
        double D = diameterMm / 1000.0;
        double relRoughness = eps / D;

        // Bağımsız referans: Swamee-Jain (1976) kapalı-form yaklaşımı — literatürde ±%1-2
        // hata payıyla Colebrook-White'a denk kabul edilir (Cengel & Cimbala, Fluid Mechanics).
        double swameeJain = 0.25 / Math.Pow(Math.Log10(relRoughness / 3.7 + 5.74 / Math.Pow(reynolds, 0.9)), 2);

        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(reynolds, roughnessMm, diameterMm);

        double percentDiff = Math.Abs(f - swameeJain) / swameeJain * 100.0;
        Assert.True(percentDiff < 2.0,
            $"Re={reynolds}, ε/D={relRoughness:F5}: Colebrook-White={f:F5}, Swamee-Jain={swameeJain:F5} — fark %{percentDiff:F2} (beklenen <%2).");
    }

    [Fact]
    public void PartialFlow_HalfFull_ReturnsExpectedRatio()
    {
        var result = AdvancedHydraulicsService.CalculatePartialFlow(1.0, 100, 2.0);
        Assert.True(result.FillingRatio > 0 && result.FillingRatio < 1);
        Assert.True(result.ActualVelocity > 0);
    }

    [Fact]
    public void PartialFlow_OverCapacity_FlagsViolation()
    {
        var result = AdvancedHydraulicsService.CalculatePartialFlow(100.0, 50, 1.0);
        Assert.True(result.IsOverCapacity);
    }

    [Fact]
    public void WaterHammer_ReturnsPositivePressure()
    {
        var pipe = new Afney.Cad.Mechanical.Entities.PipeEntity(
            new Afney.Cad.Geometry.Primitives.Vector3D(0, 0, 0),
            new Afney.Cad.Geometry.Primitives.Vector3D(10000, 0, 0), 50)
        {
            FlowRate = 3.6,
            PipeMaterialType = Afney.Cad.Mechanical.Enums.PipeMaterial.PPRC_PN20
        };
        pipe.Velocity = pipe.GetVelocity();

        var result = AdvancedHydraulicsService.CalculateWaterHammer(pipe);
        Assert.True(result.PressureSurgebar >= 0);
        Assert.True(result.WaveSpeedMs > 100);
    }

    [Fact]
    public void FixtureUnitTable_Lavabo_Returns05()
    {
        var entry = FixtureUnitTable.GetEntry("Lavabo");
        Assert.NotNull(entry);
        Assert.Equal(0.5, entry!.LoadUnits);
    }

    [Fact]
    public void FixtureUnitTable_WC_Returns25()
    {
        var entry = FixtureUnitTable.GetEntry("WC (Rezervuar)");
        Assert.NotNull(entry);
        Assert.Equal(2.5, entry!.LoadUnits);
    }

    [Fact]
    public void FixtureUnitTable_FuzzyMatch_Works()
    {
        var entry = FixtureUnitTable.GetEntry("Shower");
        Assert.NotNull(entry);
    }
}
