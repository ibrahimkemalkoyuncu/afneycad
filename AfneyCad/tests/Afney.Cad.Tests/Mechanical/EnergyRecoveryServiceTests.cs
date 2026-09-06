using System;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: EnergyRecoveryService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu): Bu servis (ERV/HRV ısı geri
          kazanım hesabı, EN 308/ASHRAE 84) HVAC arayüzüne bağlandığı halde (EnergyRecoveryDialog)
          hiç testi yoktu. Bu testler sensible/latent geri kazanım formüllerini ve tip kataloğuna
          bağlı davranışı (entalpik tipte nem geri kazanımı, özel verim override'ı) kilitler.
*/
public class EnergyRecoveryServiceTests
{
    [Fact]
    public void Calculate_PlateHeatExchangerDefaultEfficiency_UsesAverageOfCatalogRange()
    {
        var svc = new EnergyRecoveryService();
        var input = new ErvInput { ErvType = ErvType.PlateHeatExchanger, CustomEfficiency = 0 };

        var result = svc.Calculate(input);

        // Katalog: MinEfficiency=0.55, MaxEfficiency=0.75 -> ortalama 0.65.
        Assert.Equal(0.65, result.Efficiency, precision: 6);
        Assert.Equal("Plakalı Eşanjör", result.ErvTypeName);
    }

    [Fact]
    public void Calculate_CustomEfficiency_OverridesCatalogAverage()
    {
        var svc = new EnergyRecoveryService();
        var input = new ErvInput { ErvType = ErvType.PlateHeatExchanger, CustomEfficiency = 0.9 };

        var result = svc.Calculate(input);

        Assert.Equal(0.9, result.Efficiency, precision: 6);
    }

    [Fact]
    public void Calculate_SupplyOutletTemp_MatchesHandComputedSensibleFormula()
    {
        var svc = new EnergyRecoveryService();
        var input = new ErvInput
        {
            ErvType = ErvType.PlateHeatExchanger,
            OutdoorTempC = -12,
            IndoorTempC = 22,
            CustomEfficiency = 0.65
        };

        var result = svc.Calculate(input);

        // T_out = T_dış + eta * (T_iç - T_dış) = -12 + 0.65*(22-(-12)) = 10.1
        Assert.Equal(-12 + 0.65 * (22 - (-12)), result.SupplyOutletTempC, precision: 6);
    }

    [Fact]
    public void Calculate_NonLatentType_SupplyOutletHumidityRatioEqualsOutdoorInletRatio()
    {
        // PlateHeatExchanger HasLatentRecovery=false -> nem geri kazanımı yok, çıkış nem oranı
        // doğrudan dış hava giriş nem oranına eşit olmalı (Latent karışım formülü hiç uygulanmamalı).
        var svc = new EnergyRecoveryService();
        var input = new ErvInput { ErvType = ErvType.PlateHeatExchanger, OutdoorTempC = -12, OutdoorRH = 0.8 };

        var result = svc.Calculate(input);
        double expectedInletRatio = PsychrometricService.HumidityRatio(input.OutdoorTempC, input.OutdoorRH);

        Assert.Equal(expectedInletRatio, result.SupplyOutletHumidityRatio, precision: 6);
    }

    [Fact]
    public void Calculate_LatentType_SupplyOutletHumidityRatioMatchesHandComputedFormula()
    {
        // RotaryWheel (Döner Tamburlu) HasLatentRecovery=true -> w_out = w_supply + eta*0.7*(w_exhaust - w_supply).
        var svc = new EnergyRecoveryService();
        var input = new ErvInput
        {
            ErvType = ErvType.RotaryWheel,
            OutdoorTempC = -12,
            OutdoorRH = 0.8,
            IndoorTempC = 22,
            IndoorRH = 0.5,
            CustomEfficiency = 0.775 // (0.70+0.85)/2 katalog ortalaması
        };

        var result = svc.Calculate(input);

        double wSupply = PsychrometricService.HumidityRatio(input.OutdoorTempC, input.OutdoorRH);
        double wExhaust = PsychrometricService.HumidityRatio(input.IndoorTempC, input.IndoorRH);
        double expected = wSupply + input.CustomEfficiency * 0.7 * (wExhaust - wSupply);

        Assert.Equal(expected, result.SupplyOutletHumidityRatio, precision: 6);
        Assert.NotEqual(wSupply, result.SupplyOutletHumidityRatio);
    }

    [Fact]
    public void Calculate_AnnualSavingsAndCO2_MatchHandComputedDerivedFormulas()
    {
        var svc = new EnergyRecoveryService();
        var input = new ErvInput
        {
            ErvType = ErvType.PlateHeatExchanger,
            OutdoorTempC = -12,
            IndoorTempC = 22,
            OperatingHoursPerYear = 4000,
            CustomEfficiency = 0.65
        };

        var result = svc.Calculate(input);

        // AnnualSavingsKWh = SensibleRecoveryKW * saat * 0.6 ; CO2 = kWh/3 * 0.5 (COP=3, 0.5 kg CO2/kWh)
        Assert.Equal(result.SensibleRecoveryKW * 4000 * 0.6, result.AnnualSavingsKWh, precision: 6);
        Assert.Equal(result.AnnualSavingsKWh / 3.0 * 0.5, result.AnnualCO2SavingsKg, precision: 6);
        Assert.True(result.SensibleRecoveryKW > 0);
    }

    [Fact]
    public void Calculate_HigherAirFlow_ProducesHigherSensibleRecovery()
    {
        var svc = new EnergyRecoveryService();
        var low = svc.Calculate(new ErvInput { AirFlowM3h = 200, CustomEfficiency = 0.65 });
        var high = svc.Calculate(new ErvInput { AirFlowM3h = 1000, CustomEfficiency = 0.65 });

        Assert.True(high.SensibleRecoveryKW > low.SensibleRecoveryKW);
    }

    [Fact]
    public void Calculate_PressureDrop_MatchesHandComputedCatalogAverageFormula()
    {
        var svc = new EnergyRecoveryService();
        var result = svc.Calculate(new ErvInput { ErvType = ErvType.RunAroundCoil });

        // Katalog: MinFlow=50, MaxFlow=800 -> (50+800)/2*0.3 = 127.5
        Assert.Equal((50.0 + 800.0) / 2.0 * 0.3, result.PressureDropPa, precision: 6);
    }
}
