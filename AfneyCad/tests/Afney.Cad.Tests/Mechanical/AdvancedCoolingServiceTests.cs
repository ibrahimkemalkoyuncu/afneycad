using System;
using System.Collections.Generic;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: AdvancedCoolingService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu): Bu servis (ASHRAE CLTD
          saatlik tablo, infiltrasyon, ekipman iç kazanç, gölgeleme faktörü, kanal kayıp,
          fan sistem eğrisi/çalışma noktası) HVAC arayüzüne bağlandığı halde (AdvancedCoolingDialog)
          hiç testi yoktu. Bu testler tablo bakışlarını, elle hesaplanmış infiltrasyon/kanal-kayıp
          formüllerini ve fan çalışma noktası kesişim algoritmasını kilitler.
*/
public class AdvancedCoolingServiceTests
{
    [Fact]
    public void GetPeakCLTD_HourWithinRange_ReturnsExactTableEntry()
    {
        // Guney dizisi: idx 0 -> saat 8, idx 6 -> saat 14. Guney[6] = 15.0 (tablodan).
        double result = AdvancedCoolingService.GetPeakCLTD("Guney", 14);
        Assert.Equal(15.0, result, precision: 6);
    }

    [Fact]
    public void GetPeakCLTD_HourBeforeRange_ClampsToFirstEntry()
    {
        double atStart = AdvancedCoolingService.GetPeakCLTD("Guney", 8);
        double beforeStart = AdvancedCoolingService.GetPeakCLTD("Guney", 2); // idx = -6 -> clamp 0
        Assert.Equal(atStart, beforeStart, precision: 6);
    }

    [Fact]
    public void GetPeakCLTD_HourAfterRange_ClampsToLastEntry()
    {
        double atEnd = AdvancedCoolingService.GetPeakCLTD("Guney", 20); // idx 12, son eleman
        double afterEnd = AdvancedCoolingService.GetPeakCLTD("Guney", 30); // idx clamp -> son eleman
        Assert.Equal(atEnd, afterEnd, precision: 6);
    }

    [Theory]
    [InlineData("KuzeyDogu")]
    [InlineData("KuzeyBati")]
    public void GetPeakCLTD_CompositeNorthOrientations_MapToNorthTable(string orientation)
    {
        double composite = AdvancedCoolingService.GetPeakCLTD(orientation, 8);
        double pureNorth = AdvancedCoolingService.GetPeakCLTD("Kuzey", 8);
        Assert.Equal(pureNorth, composite, precision: 6);
    }

    [Fact]
    public void GetPeakCLTD_UnknownOrientation_FallsBackToSouthTable()
    {
        double unknown = AdvancedCoolingService.GetPeakCLTD("Gecersiz-Yon", 8);
        double south = AdvancedCoolingService.GetPeakCLTD("Guney", 8);
        Assert.Equal(south, unknown, precision: 6);
    }

    [Fact]
    public void CalculateInfiltration_MatchesHandComputedSensibleAndLatentFormulas()
    {
        double roomVolume = 100; // m3
        double ach = 1.0; // hava değişim/saat
        double outdoorTemp = -5, indoorTemp = 22, outdoorRh = 0.8, indoorRh = 0.4;

        var result = AdvancedCoolingService.CalculateInfiltration(roomVolume, ach, outdoorTemp, indoorTemp, outdoorRh, indoorRh);

        double rho = 1.2, cp = 1005.0, hfg = 2454000.0;
        double vInfM3s = roomVolume * ach / 3600.0;
        double mDot = vInfM3s * rho;
        double expectedSensible = mDot * cp * Math.Abs(outdoorTemp - indoorTemp);
        double wOut = PsychrometricService.HumidityRatio(outdoorTemp, outdoorRh);
        double wIn = PsychrometricService.HumidityRatio(indoorTemp, indoorRh);
        double expectedLatent = mDot * hfg * Math.Abs(wOut - wIn);

        Assert.Equal(expectedSensible, result.SensibleW, precision: 3);
        Assert.Equal(expectedLatent, result.LatentW, precision: 3);
        Assert.Equal(expectedSensible + expectedLatent, result.TotalW, precision: 3);
        Assert.Equal(vInfM3s * 3600, result.AirFlowM3h, precision: 6);
    }

    [Fact]
    public void CalculateInfiltration_HigherAirChangesPerHour_ProducesHigherLoad()
    {
        var low = AdvancedCoolingService.CalculateInfiltration(100, 0.5, -5, 22, 0.8, 0.4);
        var high = AdvancedCoolingService.CalculateInfiltration(100, 3.0, -5, 22, 0.8, 0.4);

        Assert.True(high.TotalW > low.TotalW);
    }

    [Theory]
    [InlineData("bilgisayar", 150)]
    [InlineData("pc", 150)]
    [InlineData("monitor", 80)]
    [InlineData("yazıcı", 100)]
    [InlineData("fotokopi", 400)]
    [InlineData("server", 500)]
    [InlineData("buzdolabı", 200)]
    [InlineData("fırın", 2000)]
    [InlineData("ocak", 3000)]
    [InlineData("coffee", 150)]
    [InlineData("projeksiyon", 300)]
    [InlineData("bilinmeyen-cihaz", 100)] // varsayılan
    public void EquipmentHeatGain_KnownAndUnknownTypes_ReturnsExpectedPerUnitValue(string type, double expectedPerUnit)
    {
        double result = AdvancedCoolingService.EquipmentHeatGain(type, count: 1);
        Assert.Equal(expectedPerUnit, result, precision: 6);
    }

    [Fact]
    public void EquipmentHeatGain_MultipleCount_ScalesLinearly()
    {
        double single = AdvancedCoolingService.EquipmentHeatGain("server", 1);
        double five = AdvancedCoolingService.EquipmentHeatGain("server", 5);
        Assert.Equal(single * 5, five, precision: 6);
    }

    [Theory]
    [InlineData("iç perde", 0.55)]
    [InlineData("dış perde", 0.15)]
    [InlineData("dış jaluzi", 0.20)]
    [InlineData("iç stor", 0.45)]
    [InlineData("markiz", 0.30)]
    [InlineData("film", 0.40)]
    [InlineData("yok", 1.0)]
    [InlineData("bilinmeyen", 0.55)] // varsayılan
    public void ShadingCorrectionFactor_KnownAndUnknownTypes_ReturnsExpectedValue(string type, double expected)
    {
        double result = AdvancedCoolingService.ShadingCorrectionFactor(type);
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void CalculateDuctFittingLoss_MatchesHandComputedKSumFormula()
    {
        var fittings = new DuctFittingList
        {
            Elbow90Count = 2, Elbow45Count = 1, TeeCount = 1,
            DamperCount = 1, DiffuserCount = 1, FilterCount = 1, SilencerCount = 1
        };
        double velocity = 5.0;

        double result = AdvancedCoolingService.CalculateDuctFittingLoss(fittings, velocity);

        double totalK = 2 * 1.2 + 1 * 0.5 + 1 * 1.8 + 1 * 0.5 + 1 * 2.5 + 1 * 3.0 + 1 * 1.5;
        double expected = totalK * 1.2 * velocity * velocity / 2.0;

        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void CalculateDuctFittingLoss_NoFittings_ReturnsZero()
    {
        double result = AdvancedCoolingService.CalculateDuctFittingLoss(new DuctFittingList(), 5.0);
        Assert.Equal(0, result);
    }

    [Fact]
    public void GenerateSystemCurve_FollowsQuadraticLaw_DesignPointMatchesInput()
    {
        double designFlow = 1000, designPressure = 200;
        var curve = AdvancedCoolingService.GenerateSystemCurve(designFlow, designPressure, points: 10);

        Assert.Equal(11, curve.Count); // points+1
        Assert.Equal(0, curve[0].FlowM3h, precision: 6);
        Assert.Equal(0, curve[0].PressurePa, precision: 6);

        // Tasarım noktası (son eleman) tam girilen değerlere eşit olmalı.
        Assert.Equal(designFlow, curve[^1].FlowM3h, precision: 6);
        Assert.Equal(designPressure, curve[^1].PressurePa, precision: 6);

        // Kuadratik yasa: ΔP = C×Q², yarı debide basınç 1/4 olmalı.
        var half = curve[5]; // points=10 -> index 5 = %50 debi
        Assert.Equal(designFlow / 2.0, half.FlowM3h, precision: 6);
        Assert.Equal(designPressure / 4.0, half.PressurePa, precision: 1);
    }

    [Fact]
    public void FindOperatingPoint_IntersectingCurves_ReturnsInterpolatedCrossing()
    {
        // Sistem eğrisi artan (kuadratik), fan eğrisi azalan (lineer) — ortada kesişmeli.
        var systemCurve = AdvancedCoolingService.GenerateSystemCurve(designFlowM3h: 1000, designPressurePa: 500, points: 10);
        var fanCurve = new List<(double FlowM3h, double PressurePa)>();
        for (int i = 0; i <= 10; i++)
        {
            double q = 1000.0 * i / 10;
            fanCurve.Add((q, 600 - 0.4 * q)); // lineer azalan fan eğrisi
        }

        var (flow, pressure) = AdvancedCoolingService.FindOperatingPoint(systemCurve, fanCurve);

        Assert.True(flow > 0 && flow < 1000);
        Assert.True(pressure > 0);

        // Metod, iki ayrık eğri (10 segmentli, kaba çözünürlük) arasında noktasal lineer
        // enterpolasyon yapıyor — sürekli fonksiyonların cebirsel kesişimiyle birebir aynı
        // değeri ÜRETMEZ (kasıtlı bir yaklaşıklık). Bu yüzden tam eşitlik yerine, bulunan
        // noktanın gerçek kesişime makul bir toleransla (±30 birim) yakın olduğu doğrulanıyor.
        double C = 500.0 / (1000.0 * 1000.0);
        double systemPressureAtFlow = C * flow * flow;
        double fanPressureAtFlow = 600 - 0.4 * flow;
        Assert.True(Math.Abs(systemPressureAtFlow - fanPressureAtFlow) < 30,
            $"Beklenenden uzak kesişim: sistem={systemPressureAtFlow}, fan={fanPressureAtFlow}, flow={flow}");
    }

    [Fact]
    public void FindOperatingPoint_FanAlwaysAboveSystemCurve_ReturnsLastSystemCurvePoint()
    {
        // Fan eğrisi TÜM aralıkta sistem eğrisinin üzerinde kalırsa (hiç kesişme yok — sistem
        // eğrisi (0,0)'dan başlayıp kuadratik arttığı için fan eğrisinin sistemin maksimumundan
        // da yüksek sabit bir değerde olması gerekir), metodun sonsuz döngüye girmeden son
        // sistem noktasını döndürmesi beklenir.
        var systemCurve = AdvancedCoolingService.GenerateSystemCurve(designFlowM3h: 1000, designPressurePa: 500, points: 10);
        var fanCurve = new List<(double FlowM3h, double PressurePa)>();
        for (int i = 0; i <= 10; i++)
            fanCurve.Add((1000.0 * i / 10, 10000.0)); // sistem eğrisinin tepe değerinin (500) çok üzerinde, sabit

        var (flow, pressure) = AdvancedCoolingService.FindOperatingPoint(systemCurve, fanCurve);

        Assert.Equal(systemCurve[^1].FlowM3h, flow, precision: 6);
        Assert.Equal(systemCurve[^1].PressurePa, pressure, precision: 6);
    }
}
