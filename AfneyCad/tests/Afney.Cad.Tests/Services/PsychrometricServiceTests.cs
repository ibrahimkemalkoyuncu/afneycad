using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

/*
   NE: PsychrometricService — Bağımsız Referans (Buhar Tablosu) Doğrulama Testleri
   NEDEN: Denetim raporu bulgusu: HVAC tarafında (P/hidrolik kategorisinin aksine)
          Colebrook-White/Swamee-Jain tarzı bağımsız bir çapraz doğrulama YOKTU —
          `PsychrometricService`'in hiç testi de yoktu. Bu servis ASHRAE Hyland-Wexler
          tabanlı bir doyma buhar basıncı formülü kullanıyor; bu formülün doğruluğu,
          KODDAN BAĞIMSIZ, evrensel olarak bilinen buhar tablosu (steam table) referans
          noktalarıyla (her termodinamik ders kitabında/Cengel Tablo A-4'te bulunan
          değerler) karşılaştırılarak doğrulanabilir:
            - 0°C  → Psat ≈ 611 Pa   (suyun üçlü nokta civarı — evrensel sabit)
            - 20°C → Psat ≈ 2339 Pa  (psikrometrik diyagramların klasik referans noktası)
            - 100°C → Psat = 101325 Pa (TANIM GEREĞİ: su 1 atm'de 100°C'de kaynar)
          Bu üç nokta koddan TAMAMEN bağımsız, harici olarak bilinen değerlerdir.
*/
public class PsychrometricServiceTests
{
    [Theory]
    [InlineData(-20.0, 103.3, 0.02)]    // Buz üzeri — WMO/Magnus referansı
    [InlineData(-10.0, 259.9, 0.02)]    // Buz üzeri — WMO/Magnus referansı
    [InlineData(0.0, 611.2, 0.03)]      // Buz noktası — steam table referansı
    [InlineData(20.0, 2338.8, 0.02)]    // Klasik psikrometrik diyagram referans noktası
    [InlineData(100.0, 101325.0, 0.02)] // TANIM: 1 atm'de kaynama noktası
    public void SaturationPressure_MatchesSteamTableReference(double tempC, double expectedPa, double tolerance)
    {
        double actual = PsychrometricService.SaturationPressure(tempC);
        double percentDiff = System.Math.Abs(actual - expectedPa) / expectedPa;

        Assert.True(percentDiff < tolerance,
            $"{tempC}°C için beklenen ~{expectedPa}Pa (buhar tablosu), hesaplanan: {actual:F1}Pa — fark %{percentDiff * 100:F2}.");
    }

    /*
       NE: Klasik ASHRAE Psikrometrik Diyagram Referans Noktası (20°C, %50 RH)
       NEDEN: Bu, ASHRAE psikrometrik diyagramlarında ve ders kitaplarında EN SIK
              kullanılan örnek durum noktasıdır — deniz seviyesinde nem oranı ≈ 0.0073
              kg su/kg kuru hava olarak evrensel şekilde bilinir/tablolanır.
    */
    [Fact]
    public void HumidityRatio_ClassicReferencePoint_20C_50RH_MatchesKnownValue()
    {
        double w = PsychrometricService.HumidityRatio(20.0, 0.5);

        Assert.True(System.Math.Abs(w - 0.00727) < 0.0003,
            $"20°C/%50 RH için beklenen ~0.00727 kg/kg, hesaplanan: {w:F5}.");
    }

    [Fact]
    public void SaturationPressure_IsMonotonicallyIncreasingWithTemperature()
    {
        // Fiziksel olarak zorunlu: sıcaklık arttıkça doyma buhar basıncı hep artmalı.
        double[] temps = { -10, 0, 10, 20, 30, 40, 60, 80, 100 };
        double prev = 0;
        foreach (var t in temps)
        {
            double p = PsychrometricService.SaturationPressure(t);
            Assert.True(p > prev, $"{t}°C'de Psat ({p:F1}Pa) bir önceki sıcaklıktan ({prev:F1}Pa) küçük olamaz.");
            prev = p;
        }
    }
}
