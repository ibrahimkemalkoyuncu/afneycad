using System;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Hidrolik Hesap Formül Doğrulama Testleri (HydraulicCalculationTests)
   NEDEN: TS 1258 ve DIN 1988 standartlarına dayanan debi, çap ve hız hesaplarının
          matematiksel doğruluğundan emin olmak için. Herhangi bir refactoring
          bu testleri kırarsa mühendislik hatası olarak anında fark edilir.

   KAPSAM (Faz 28 — QA Mühendisi İyileştirmeleri):
   1. CalculateDesignFlow  — TS1258 Konut formülü (Q = 0.25·√LU · Diversity)
   2. DeterminePipeDiameter — Max-hız kısıtlı standart çap seçimi
   3. CalculateCirculationPumpFlow — Sirkülasyon pompası debi formülü
   4. BuildingType parametresi — Hastane vs Konut farkı
   5. Sınır Değerleri — Sıfır/negatif giriş koruması
*/
public class HydraulicCalculationTests
{
    private readonly HydraulicCalculationService _svc = new();

    // ─────────────────────────────────────────────────────────────────
    // 1. CalculateDesignFlow — Konut (TS 1258)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateDesignFlow_Residential_4LU_ShouldMatchFormula()
    {
        // Q = 0.25 * sqrt(4) * 0.70 = 0.25 * 2 * 0.70 = 0.35 l/s → × 3.6 = 1.26 m³/h
        double expected = 0.25 * Math.Pow(4, 0.5) * 0.70 * 3.6;

        double result = _svc.CalculateDesignFlow(4.0, HydraulicCalculationService.BuildingType.Residential);

        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void CalculateDesignFlow_ZeroLU_ReturnsZero()
    {
        double result = _svc.CalculateDesignFlow(0);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateDesignFlow_NegativeLU_ReturnsZero()
    {
        double result = _svc.CalculateDesignFlow(-5);
        Assert.Equal(0, result);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. BuildingType Farkı — Hastane vs Konut için aynı LU'da
    //    Hastane daha yüksek çarpan (a=0.35, diversity=0.90) → daha büyük debi
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateDesignFlow_Hospital_HigherThanResidential_ForSameLU()
    {
        double residential = _svc.CalculateDesignFlow(10, HydraulicCalculationService.BuildingType.Residential);
        double hospital    = _svc.CalculateDesignFlow(10, HydraulicCalculationService.BuildingType.Hospital);

        Assert.True(hospital > residential,
            $"Hastane debisi ({hospital:F4}) Konut debisinden ({residential:F4}) büyük olmalıdır.");
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. DeterminePipeDiameter — Minimum standart çap seçimi
    //    Sıfır debi → en küçük standart çap (15 mm)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DeterminePipeDiameter_ZeroFlow_ReturnsMinimumStandardDiameter()
    {
        double result = _svc.DeterminePipeDiameter(0);
        Assert.Equal(15.0, result);
    }

    [Fact]
    public void DeterminePipeDiameter_LargeFlow_ReturnsStandardDiameter()
    {
        // Çok yüksek debi → büyük çap, mutlaka standart tablodaki bir değer olmalı
        double[] standardDiameters = { 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150 };
        double result = _svc.DeterminePipeDiameter(500.0, HydraulicCalculationService.BuildingType.Residential);

        Assert.Contains(result, standardDiameters);
    }

    [Fact]
    public void DeterminePipeDiameter_Hospital_LargerOrEqualToResidential_ForSameFlow()
    {
        // Hastane max hız (1.2 m/s) < Konut max hız (1.5 m/s) → aynı debi için hastane daha geniş çap gerektirir
        double flowM3h = 3.0;
        double residential = _svc.DeterminePipeDiameter(flowM3h, HydraulicCalculationService.BuildingType.Residential);
        double hospital    = _svc.DeterminePipeDiameter(flowM3h, HydraulicCalculationService.BuildingType.Hospital);

        Assert.True(hospital >= residential,
            $"Hastane çapı ({hospital} mm) ≥ Konut çapı ({residential} mm) olmalıdır (düşük hız sınırı nedeniyle).");
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. CalculateCirculationPumpFlow — Sirkülasyon pompası formülü
    //    P = m·c·ΔT  →  m = P/c/ΔT  →  Q = m·3.6
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateCirculationPumpFlow_KnownInput_ShouldMatchFormula()
    {
        // P = 5000 W, ΔT = 5 K
        // m = 5000 / (4180 * 5) = 0.23923... kg/s
        // Q = 0.23923... * 3.6 = 0.8612... m³/h
        double expected = (5000.0 / (4180.0 * 5.0)) * 3.6;
        double result = _svc.CalculateCirculationPumpFlow(5000, deltaT: 5);

        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void CalculateCirculationPumpFlow_ZeroHeat_ReturnsZero()
    {
        double result = _svc.CalculateCirculationPumpFlow(0, 5);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateCirculationPumpFlow_ZeroDeltaT_ReturnsZero()
    {
        double result = _svc.CalculateCirculationPumpFlow(5000, 0);
        Assert.Equal(0, result);
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. GetActiveParams — Varsayılan BuildingType Konut olmalı
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultBuildingType_IsResidential()
    {
        Assert.Equal(HydraulicCalculationService.BuildingType.Residential, _svc.GetBuildingType());
    }

    [Fact]
    public void SetBuildingType_ChangesActiveParams()
    {
        _svc.SetBuildingType(HydraulicCalculationService.BuildingType.Hotel);
        Assert.Equal(HydraulicCalculationService.BuildingType.Hotel, _svc.GetBuildingType());

        // Temizle
        _svc.SetBuildingType(HydraulicCalculationService.BuildingType.Residential);
    }
}
