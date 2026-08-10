using System;
using System.Collections.Generic;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: DIN1988300Service Testleri (DIN1988300ServiceTests)
   NEDEN: Almanya/AB projelerinde zorunlu olan DIN 1988-300 debi/çap hesabı motoru hiç test
          edilmemişti. Bu testler; LU (Armatür Birimi) toplama mantığının, LU->Qd tablosunun
          (DIN 1988-300 Tablo 3) doğru enterpole edildiğinin ve Qd->DN dönüşümünün (süreklilik
          denklemi, v<=2.5 m/s soğuk / v<=2.0 m/s sıcak) doğru çalıştığını doğruluyor.

   KAPSAM:
   1. GetQdFromLU — Tablo sınır/ara değerleri
   2. SelectPipeDN — Soğuk/sıcak su hız limitine göre DN seçimi
   3. Calculate — Uçtan uca: LU toplama, Qd hesabı, DN seçimi, eşzamanlılık katsayısı
*/
public class DIN1988300ServiceTests
{
    // ─────────────────────────────────────────────────────────────────
    // 1. GetQdFromLU — DIN 1988-300 Tablo 3
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0.07)]
    [InlineData(10, 0.25)]
    [InlineData(100, 0.85)]
    [InlineData(5000, 5.52)]
    public void GetQdFromLU_ExactTablePoints_ReturnsTableValue(int lu, double expected)
    {
        Assert.Equal(expected, DIN1988300Service.GetQdFromLU(lu), precision: 9);
    }

    [Fact]
    public void GetQdFromLU_ZeroOrNegative_ReturnsZero()
    {
        Assert.Equal(0, DIN1988300Service.GetQdFromLU(0));
        Assert.Equal(0, DIN1988300Service.GetQdFromLU(-5));
    }

    [Fact]
    public void GetQdFromLU_BelowFirstTableEntry_ClampsToFirstValue()
    {
        // LU=1 tablonun ilk noktası; LU aralığı 1'den başlıyor (0 ve altı ayrı ele alınıyor),
        // bu yüzden en küçük pozitif LU (1) test edilir.
        Assert.Equal(0.07, DIN1988300Service.GetQdFromLU(1), precision: 9);
    }

    [Fact]
    public void GetQdFromLU_MidpointBetweenKnownEntries_LinearlyInterpolates()
    {
        // LU=7: tabloda 6(0.19) ile 8(0.22) arasında, t=(7-6)/(8-6)=0.5
        // Beklenen: 0.19 + 0.5*(0.22-0.19) = 0.205
        double expected = 0.19 + 0.5 * (0.22 - 0.19);
        double result = DIN1988300Service.GetQdFromLU(7);
        Assert.Equal(expected, result, precision: 9);
    }

    [Fact]
    public void GetQdFromLU_AboveLastTableEntry_UsesLinearExtrapolation()
    {
        // Tablo son noktası: (5000, 5.52). Üstünde: 5.52 + (LU-5000)*0.0005
        int lu = 6000;
        double expected = 5.52 + (lu - 5000) * 0.0005;
        double result = DIN1988300Service.GetQdFromLU(lu);
        Assert.Equal(expected, result, precision: 9);
    }

    [Fact]
    public void GetQdFromLU_MonotonicallyIncreasing()
    {
        double prev = 0;
        foreach (int lu in new[] { 1, 5, 10, 50, 100, 500, 1000, 3000, 5000, 8000 })
        {
            double qd = DIN1988300Service.GetQdFromLU(lu);
            Assert.True(qd >= prev, $"LU={lu} için Qd ({qd}) önceki değerden ({prev}) küçük olamaz.");
            prev = qd;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. SelectPipeDN — Süreklilik denklemi + standart DN yuvarlama
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SelectPipeDN_ColdWater_UsesMaxVelocity2_5()
    {
        // Qd=0.4 l/s, v=2.5 m/s -> A=Q/v -> d=sqrt(4A/pi)
        double qd = 0.4;
        double aM2 = (qd / 1000.0) / 2.5;
        double dMm = Math.Sqrt(4.0 * aM2 / Math.PI) * 1000.0;

        string result = DIN1988300Service.SelectPipeDN(qd, hotWater: false);

        Assert.Equal(ExpectedDNBucket(dMm), result);
    }

    [Fact]
    public void SelectPipeDN_HotWater_UsesMaxVelocity2_0_RequiresLargerDNThanCold()
    {
        // Aynı debi için sıcak su (v<=2.0) daha düşük hız limiti nedeniyle
        // soğuk suya (v<=2.5) göre eşit veya daha büyük DN gerektirmelidir.
        double qd = 0.5;
        string cold = DIN1988300Service.SelectPipeDN(qd, hotWater: false);
        string hot = DIN1988300Service.SelectPipeDN(qd, hotWater: true);

        int coldRank = DNRank(cold);
        int hotRank = DNRank(hot);

        Assert.True(hotRank >= coldRank,
            $"Sıcak su DN'i ({hot}) soğuk su DN'inden ({cold}) küçük olamaz.");
    }

    [Fact]
    public void SelectPipeDN_VerySmallFlow_ReturnsSmallestDN()
    {
        Assert.Equal("DN10", DIN1988300Service.SelectPipeDN(0.0001, hotWater: false));
    }

    [Fact]
    public void SelectPipeDN_VeryLargeFlow_ReturnsDN125Plus()
    {
        Assert.Equal("DN125+", DIN1988300Service.SelectPipeDN(50.0, hotWater: false));
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. Calculate — Uçtan uca hesap
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_SingleWashbasin_SumsLuCorrectlyAndSelectsDN()
    {
        var washbasin = DIN1988300Service.FixtureTable.Find(f => f.Name == "Lavabo (DN15)")!;
        var input = new DIN1988300Service.DIN1988Input
        {
            Fixtures = new List<(DIN1988300Service.FixtureUnit, int)> { (washbasin, 3) }
        };

        var result = DIN1988300Service.Calculate(input);

        // 3 lavabo * (1 soğuk, 1 sıcak) LU
        Assert.Equal(3, result.TotalColdLU);
        Assert.Equal(3, result.TotalHotLU);
        Assert.Equal(DIN1988300Service.GetQdFromLU(3), result.QdColdLps, precision: 9);
        Assert.Equal(DIN1988300Service.GetQdFromLU(3), result.QdHotLps, precision: 9);
        Assert.Equal(DIN1988300Service.SelectPipeDN(result.QdColdLps, false), result.ColdPipeDN);
        Assert.Equal(DIN1988300Service.SelectPipeDN(result.QdHotLps, true), result.HotPipeDN);
    }

    [Fact]
    public void Calculate_MixedFixtures_TotalMaxFlowIsSumOfNominalFlows()
    {
        var washbasin = DIN1988300Service.FixtureTable.Find(f => f.Name == "Lavabo (DN15)")!;
        var wc = DIN1988300Service.FixtureTable.Find(f => f.Name == "Klozet sifon (DN15)")!;

        var input = new DIN1988300Service.DIN1988Input
        {
            Fixtures = new List<(DIN1988300Service.FixtureUnit, int)>
            {
                (washbasin, 2), (wc, 1)
            }
        };

        var result = DIN1988300Service.Calculate(input);

        double expectedMaxFlow = washbasin.QnLps * 2 + wc.QnLps * 1;
        Assert.Equal(expectedMaxFlow, result.TotalMaxFlow, precision: 9);

        // Eşzamanlılık katsayısı: Qd(soğuk) / TeorikMaksimum, her zaman <= 1 olmalı (çeşitlilik etkisi).
        Assert.True(result.SimFactor <= 1.0);
        Assert.Equal(result.QdColdLps / result.TotalMaxFlow, result.SimFactor, precision: 9);
    }

    [Fact]
    public void Calculate_NoFixtures_ReturnsZeroAndSimFactorOne()
    {
        var input = new DIN1988300Service.DIN1988Input
        {
            Fixtures = new List<(DIN1988300Service.FixtureUnit, int)>()
        };

        var result = DIN1988300Service.Calculate(input);

        Assert.Equal(0, result.TotalColdLU);
        Assert.Equal(0.0, result.TotalMaxFlow);
        Assert.Equal(1.0, result.SimFactor); // maxFlow=0 -> güvenli varsayılan 1.0
    }

    [Fact]
    public void Calculate_LargeBuilding_AddsHighLoadWarningNote()
    {
        // 1000 LU üzeri bina için düşey besleme borusunun kat gruplarına bölünmesi uyarısı eklenmeli.
        var wc = DIN1988300Service.FixtureTable.Find(f => f.Name == "Klozet basınç deposu")!; // ColdLU=5
        var input = new DIN1988300Service.DIN1988Input
        {
            Fixtures = new List<(DIN1988300Service.FixtureUnit, int)> { (wc, 300) } // 1500 LU
        };

        var result = DIN1988300Service.Calculate(input);

        Assert.True(result.TotalColdLU > 1000);
        Assert.Contains(result.Notes, n => n.Contains("Büyük bina"));
    }

    // ── Yardımcılar ──────────────────────────────────────────────────

    private static string ExpectedDNBucket(double dMm)
    {
        if (dMm <= 10) return "DN10";
        if (dMm <= 12) return "DN12";
        if (dMm <= 15) return "DN15";
        if (dMm <= 20) return "DN20";
        if (dMm <= 25) return "DN25";
        if (dMm <= 32) return "DN32";
        if (dMm <= 40) return "DN40";
        if (dMm <= 50) return "DN50";
        if (dMm <= 65) return "DN65";
        if (dMm <= 80) return "DN80";
        if (dMm <= 100) return "DN100";
        return "DN125+";
    }

    private static int DNRank(string dn) => dn switch
    {
        "DN10" => 0,
        "DN12" => 1,
        "DN15" => 2,
        "DN20" => 3,
        "DN25" => 4,
        "DN32" => 5,
        "DN40" => 6,
        "DN50" => 7,
        "DN65" => 8,
        "DN80" => 9,
        "DN100" => 10,
        _ => 11 // DN125+
    };
}
