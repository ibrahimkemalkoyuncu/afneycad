using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

// EN 308 / ASHRAE 84 — Isı Geri Kazanım Ünitesi (ERV/HRV) Hesabı
public class EnergyRecoveryService
{
    // ERV tipleri ve tipik verim aralıkları
    public static readonly Dictionary<ErvType, ErvSpec> Catalog = new()
    {
        [ErvType.PlateHeatExchanger] = new("Plakalı Eşanjör", 0.55, 0.75, 50, 300, false),
        [ErvType.RotaryWheel] = new("Döner Tamburlu (Entalpik)", 0.70, 0.85, 100, 1500, true),
        [ErvType.HeatPipe] = new("Isı Borusu", 0.45, 0.65, 80, 500, false),
        [ErvType.RunAroundCoil] = new("Dolaşımlı Serpantin", 0.40, 0.60, 50, 800, false),
        [ErvType.MembranePlate] = new("Membranlı Plaka (Entalpik)", 0.60, 0.80, 50, 400, true),
    };

    public ErvResult Calculate(ErvInput input)
    {
        var result = new ErvResult();

        var spec = Catalog.GetValueOrDefault(input.ErvType, Catalog[ErvType.PlateHeatExchanger]);
        double eta = input.CustomEfficiency > 0 ? input.CustomEfficiency : (spec.MinEfficiency + spec.MaxEfficiency) / 2.0;

        var supplyIn = PsychrometricService.CalculateState(input.OutdoorTempC, input.OutdoorRH);
        var exhaustIn = PsychrometricService.CalculateState(input.IndoorTempC, input.IndoorRH);

        // Sensible ısı geri kazanımı
        double tSupplyOut = input.OutdoorTempC + eta * (input.IndoorTempC - input.OutdoorTempC);
        result.SupplyOutletTempC = tSupplyOut;

        // Latent geri kazanım (entalpik tip ise)
        if (spec.HasLatentRecovery)
        {
            double wOut = supplyIn.HumidityRatio + eta * 0.7 * (exhaustIn.HumidityRatio - supplyIn.HumidityRatio);
            result.SupplyOutletHumidityRatio = wOut;
        }
        else
        {
            result.SupplyOutletHumidityRatio = supplyIn.HumidityRatio;
        }

        // Enerji tasarrufu (kW)
        double massFlow = input.AirFlowM3h * PsychrometricService.AirDensity(input.OutdoorTempC, input.OutdoorRH) / 3600.0;
        double cp = 1005.0; // J/(kg·K)

        result.SensibleRecoveryKW = massFlow * cp * Math.Abs(tSupplyOut - input.OutdoorTempC) / 1000.0;
        result.AnnualSavingsKWh = result.SensibleRecoveryKW * input.OperatingHoursPerYear * 0.6; // %60 ortalama yük
        result.Efficiency = eta;
        result.ErvTypeName = spec.Name;
        result.PressureDropPa = (spec.MinFlowM3h + spec.MaxFlowM3h) / 2.0 * 0.3; // yaklaşık

        // CO2 tasarrufu (0.5 kg CO2 / kWh elektrik, COP=3 için)
        result.AnnualCO2SavingsKg = result.AnnualSavingsKWh / 3.0 * 0.5;

        return result;
    }
}

public enum ErvType { PlateHeatExchanger, RotaryWheel, HeatPipe, RunAroundCoil, MembranePlate }

public record ErvSpec(string Name, double MinEfficiency, double MaxEfficiency, double MinFlowM3h, double MaxFlowM3h, bool HasLatentRecovery);

public class ErvInput
{
    public ErvType ErvType { get; set; } = ErvType.PlateHeatExchanger;
    public double OutdoorTempC { get; set; } = -12;
    public double OutdoorRH { get; set; } = 0.8;
    public double IndoorTempC { get; set; } = 22;
    public double IndoorRH { get; set; } = 0.5;
    public double AirFlowM3h { get; set; } = 500;
    public double CustomEfficiency { get; set; }
    public double OperatingHoursPerYear { get; set; } = 4000;
}

public class ErvResult
{
    public double SupplyOutletTempC { get; set; }
    public double SupplyOutletHumidityRatio { get; set; }
    public double SensibleRecoveryKW { get; set; }
    public double AnnualSavingsKWh { get; set; }
    public double AnnualCO2SavingsKg { get; set; }
    public double Efficiency { get; set; }
    public string ErvTypeName { get; set; } = "";
    public double PressureDropPa { get; set; }
}
