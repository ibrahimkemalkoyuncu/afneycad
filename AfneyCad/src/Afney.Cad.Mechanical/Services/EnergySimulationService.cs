using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// EN 15603 / TS 825 — Yıllık Enerji Simülasyonu (Bin Method)
public class EnergySimulationService
{
    // Türkiye iklim bölgeleri — aylık ortalama sıcaklıklar (°C)
    private static readonly Dictionary<string, double[]> MonthlyTemps = new()
    {
        ["İstanbul"] = new[] { 6.0, 6.5, 8.5, 13.0, 17.5, 22.0, 25.0, 25.0, 21.0, 16.5, 12.0, 8.0 },
        ["Ankara"]   = new[] { 0.5, 2.0, 6.0, 11.5, 16.0, 20.0, 23.5, 23.5, 19.0, 13.0, 7.0, 2.5 },
        ["İzmir"]    = new[] { 9.0, 9.5, 12.0, 16.0, 20.5, 25.5, 28.0, 28.0, 24.0, 19.0, 14.0, 10.5 },
        ["Antalya"]  = new[] { 10.5, 11.0, 13.0, 17.0, 21.0, 25.5, 28.5, 28.5, 25.0, 20.5, 15.5, 12.0 },
        ["Erzurum"]  = new[] { -9.0, -7.5, -2.0, 5.5, 10.5, 15.0, 19.5, 20.0, 15.0, 8.5, 1.5, -5.5 },
        ["Trabzon"]  = new[] { 7.5, 7.5, 9.0, 12.0, 16.0, 20.0, 23.0, 23.5, 20.0, 16.0, 12.0, 9.0 },
        ["Diyarbakır"] = new[] { 2.0, 3.5, 8.5, 14.0, 19.5, 26.0, 31.0, 30.5, 25.0, 17.5, 10.0, 4.0 },
    };

    // Güneş radyasyonu (W/m²) — aylık ortalama yatay yüzey (Türkiye ortalaması)
    private static readonly double[] SolarRadiation = { 80, 120, 180, 240, 290, 320, 330, 300, 240, 160, 100, 70 };

    public EnergySimulationResult Simulate(EnergySimulationInput input)
    {
        var result = new EnergySimulationResult { City = input.City };
        var monthlyTemps = MonthlyTemps.GetValueOrDefault(input.City, MonthlyTemps["İstanbul"]);

        double totalHeating = 0, totalCooling = 0, totalDHW = 0, totalLighting = 0, totalFan = 0;

        for (int month = 0; month < 12; month++)
        {
            double tOut = monthlyTemps[month];
            int daysInMonth = DateTime.DaysInMonth(2025, month + 1);
            double hoursInMonth = daysInMonth * input.OccupiedHoursPerDay;

            // Isıtma yükü (kWh)
            double heatingLoad = 0;
            if (tOut < input.HeatingSetpointC)
            {
                double deltaT = input.HeatingSetpointC - tOut;
                double qHeat = input.BuildingUAValueWK * deltaT / 1000.0; // kW
                heatingLoad = qHeat * hoursInMonth;
            }

            // Soğutma yükü (kWh) — iç kazançlar + güneş dahil
            double coolingLoad = 0;
            if (tOut > input.CoolingSetpointC)
            {
                double deltaT = tOut - input.CoolingSetpointC;
                double qCool = input.BuildingUAValueWK * deltaT / 1000.0;
                double solarGain = SolarRadiation[month] * input.TotalWindowAreaM2 * input.SHGC / 1000.0;
                double internalGain = input.InternalGainWm2 * input.FloorAreaM2 / 1000.0;
                coolingLoad = (qCool + solarGain + internalGain) * hoursInMonth;
            }

            // Sıcak su (kWh)
            double dhwLoad = input.DHWDemandLitersPerDay * daysInMonth * 4.186 * (input.DHWTempC - tOut) / 3600.0;

            // Aydınlatma (kWh)
            double lightingLoad = input.LightingWm2 * input.FloorAreaM2 * hoursInMonth / 1000.0;

            // Fan/pompa (kWh)
            double fanLoad = input.FanPowerKW * hoursInMonth;

            var monthData = new MonthlyEnergyData
            {
                Month = month + 1,
                MonthName = new DateTime(2025, month + 1, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
                OutdoorTempC = tOut,
                HeatingKWh = heatingLoad / Math.Max(input.HeatingCOP, 1),
                CoolingKWh = coolingLoad / Math.Max(input.CoolingCOP, 1),
                DHWKWh = dhwLoad / Math.Max(input.DHW_COP, 1),
                LightingKWh = lightingLoad,
                FanPumpKWh = fanLoad
            };
            monthData.TotalKWh = monthData.HeatingKWh + monthData.CoolingKWh + monthData.DHWKWh + monthData.LightingKWh + monthData.FanPumpKWh;

            result.MonthlyData.Add(monthData);

            totalHeating += monthData.HeatingKWh;
            totalCooling += monthData.CoolingKWh;
            totalDHW += monthData.DHWKWh;
            totalLighting += monthData.LightingKWh;
            totalFan += monthData.FanPumpKWh;
        }

        result.AnnualHeatingKWh = totalHeating;
        result.AnnualCoolingKWh = totalCooling;
        result.AnnualDHWKWh = totalDHW;
        result.AnnualLightingKWh = totalLighting;
        result.AnnualFanPumpKWh = totalFan;
        result.AnnualTotalKWh = totalHeating + totalCooling + totalDHW + totalLighting + totalFan;
        result.AnnualPrimaryEnergyKWh = result.AnnualTotalKWh * input.PrimaryEnergyFactor;
        result.SpecificEnergyKWhM2 = input.FloorAreaM2 > 0 ? result.AnnualTotalKWh / input.FloorAreaM2 : 0;
        result.AnnualCO2Tons = result.AnnualTotalKWh * input.CO2FactorKgPerKWh / 1000.0;
        result.AnnualCostTRY = result.AnnualTotalKWh * input.ElectricityPriceTRYPerKWh;

        // TS 825 enerji sınıfı
        result.EnergyClass = result.SpecificEnergyKWhM2 switch
        {
            <= 50 => "A",
            <= 75 => "B",
            <= 100 => "C",
            <= 150 => "D",
            <= 200 => "E",
            <= 250 => "F",
            _ => "G"
        };

        return result;
    }
}

public class EnergySimulationInput
{
    public string City { get; set; } = "İstanbul";
    public double FloorAreaM2 { get; set; } = 200;
    public double BuildingUAValueWK { get; set; } = 400; // W/K
    public double HeatingSetpointC { get; set; } = 20;
    public double CoolingSetpointC { get; set; } = 26;
    public double HeatingCOP { get; set; } = 3.5; // Isı pompası
    public double CoolingCOP { get; set; } = 3.0;
    public double DHW_COP { get; set; } = 2.5;
    public double DHWDemandLitersPerDay { get; set; } = 200;
    public double DHWTempC { get; set; } = 60;
    public double TotalWindowAreaM2 { get; set; } = 30;
    public double SHGC { get; set; } = 0.4;
    public double InternalGainWm2 { get; set; } = 20;
    public double LightingWm2 { get; set; } = 10;
    public double FanPowerKW { get; set; } = 1.5;
    public double OccupiedHoursPerDay { get; set; } = 12;
    public double PrimaryEnergyFactor { get; set; } = 2.36; // Elektrik (Türkiye)
    public double CO2FactorKgPerKWh { get; set; } = 0.47;
    public double ElectricityPriceTRYPerKWh { get; set; } = 4.5;
}

public class EnergySimulationResult
{
    public string City { get; set; } = "";
    public double AnnualHeatingKWh { get; set; }
    public double AnnualCoolingKWh { get; set; }
    public double AnnualDHWKWh { get; set; }
    public double AnnualLightingKWh { get; set; }
    public double AnnualFanPumpKWh { get; set; }
    public double AnnualTotalKWh { get; set; }
    public double AnnualPrimaryEnergyKWh { get; set; }
    public double SpecificEnergyKWhM2 { get; set; }
    public double AnnualCO2Tons { get; set; }
    public double AnnualCostTRY { get; set; }
    public string EnergyClass { get; set; } = "";
    public List<MonthlyEnergyData> MonthlyData { get; set; } = new();
}

public class MonthlyEnergyData
{
    public int Month { get; set; }
    public string MonthName { get; set; } = "";
    public double OutdoorTempC { get; set; }
    public double HeatingKWh { get; set; }
    public double CoolingKWh { get; set; }
    public double DHWKWh { get; set; }
    public double LightingKWh { get; set; }
    public double FanPumpKWh { get; set; }
    public double TotalKWh { get; set; }
}
