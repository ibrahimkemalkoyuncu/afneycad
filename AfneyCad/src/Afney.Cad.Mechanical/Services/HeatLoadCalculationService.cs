using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// EN 12831-1:2017 — Bina Isıtma Yük Hesabı
// Referans: TS EN 12831, VDI 2078, ASHRAE Handbook Fundamentals
public class HeatLoadCalculationService
{
    // Dış tasarım sıcaklıkları (°C) — Türkiye illeri (TS 825 / MGM verileri)
    private static readonly Dictionary<string, double> _outdoorDesignTemps = new()
    {
        ["İstanbul"] = -3, ["Ankara"] = -12, ["İzmir"] = -1, ["Bursa"] = -5,
        ["Antalya"] = 3, ["Erzurum"] = -21, ["Kars"] = -24, ["Trabzon"] = -2,
        ["Sivas"] = -16, ["Van"] = -17, ["Konya"] = -12, ["Diyarbakır"] = -7,
        ["Gaziantep"] = -5, ["Eskişehir"] = -11, ["Samsun"] = -3, ["Adana"] = 1,
        ["Kayseri"] = -14, ["Malatya"] = -12, ["Elazığ"] = -12, ["Ağrı"] = -22,
    };

    // Varsayılan U-değerleri (W/m²K) — TS 825:2024 minimum performans
    public static readonly Dictionary<string, double> DefaultUValues = new()
    {
        ["Dış Duvar (yalıtımlı)"] = 0.40,
        ["Dış Duvar (yalıtımsız)"] = 1.80,
        ["Çatı (yalıtımlı)"] = 0.25,
        ["Çatı (yalıtımsız)"] = 2.50,
        ["Taban (toprak üzeri)"] = 0.50,
        ["Taban (toprak altı)"] = 0.80,
        ["Pencere (çift cam)"] = 2.80,
        ["Pencere (Low-E)"] = 1.60,
        ["Pencere (üçlü cam)"] = 1.10,
        ["Dış Kapı"] = 3.50,
        ["İç Duvar (ısıtılmayan komşu)"] = 0.60,
    };

    public HeatLoadResult Calculate(HeatLoadInput input)
    {
        var result = new HeatLoadResult();

        double tOut = GetOutdoorDesignTemp(input.City);
        double tIn = input.IndoorDesignTemp;
        double deltaT = tIn - tOut;

        // 1. İletim (Transmisyon) kayıpları — EN 12831-1 Madde 7
        foreach (var surface in input.Surfaces)
        {
            double area = surface.Area;
            double u = surface.UValue;
            double psi = surface.LinearThermalBridge; // Isı köprüsü ek payı (W/mK)
            double bridgeLength = surface.BridgeLength;

            double qTrans = u * area * deltaT;
            double qBridge = psi * bridgeLength * deltaT;

            result.TransmissionLossW += qTrans + qBridge;

            result.SurfaceDetails.Add(new SurfaceHeatLoss
            {
                Name = surface.Name,
                Area = area,
                UValue = u,
                DeltaT = deltaT,
                LossW = qTrans + qBridge
            });
        }

        // 2. Havalandırma (Ventilasyon) kayıpları — EN 12831-1 Madde 8
        // Q_v = V_dot * rho * cp * (t_in - t_out)
        double airDensity = 1.2; // kg/m³
        double airCp = 1005.0; // J/(kg·K)
        double airChangeRate = input.AirChangesPerHour > 0 ? input.AirChangesPerHour : GetMinAirChangeRate(input.RoomType);
        double volumeFlowM3s = input.RoomVolume * airChangeRate / 3600.0;

        result.VentilationLossW = volumeFlowM3s * airDensity * airCp * deltaT;
        result.AirChangeRate = airChangeRate;

        // 3. Isıtma Ek Payı (Intermittent heating reheat factor) — EN 12831-1 Madde 9
        // f_RH = (yeniden ısıtma süresi ve bina ısıl kütlesine bağlı)
        double fRH = input.ReheatFactor > 0 ? input.ReheatFactor : GetReheatFactor(input.BuildingMass, input.ReheatTimeHours);
        result.ReheatAllowanceW = (result.TransmissionLossW + result.VentilationLossW) * fRH;

        // 4. Toplam
        result.TotalHeatLoadW = result.TransmissionLossW + result.VentilationLossW + result.ReheatAllowanceW;
        result.TotalHeatLoadKW = result.TotalHeatLoadW / 1000.0;

        // 5. Spesifik ısı yükü (W/m²)
        if (input.FloorArea > 0)
            result.SpecificHeatLoad = result.TotalHeatLoadW / input.FloorArea;

        result.OutdoorDesignTemp = tOut;
        result.IndoorDesignTemp = tIn;
        result.City = input.City;
        result.ReheatFactor = fRH;

        return result;
    }

    public double GetOutdoorDesignTemp(string city)
    {
        if (string.IsNullOrEmpty(city)) return -12;
        foreach (var kvp in _outdoorDesignTemps)
        {
            if (city.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return -12; // Ankara varsayılan
    }

    private double GetMinAirChangeRate(string roomType)
    {
        return roomType?.ToLowerInvariant() switch
        {
            "banyo" or "wc" or "tuvalet" => 5.0,
            "mutfak" => 4.0,
            "yatak odası" or "yatak" => 0.5,
            "oturma odası" or "salon" => 0.7,
            "ofis" => 1.5,
            "toplantı" or "konferans" => 4.0,
            "merdiven" or "koridor" => 0.5,
            "hastane" or "ameliyat" => 8.0,
            "laboratuvar" => 6.0,
            "restoran" => 3.0,
            _ => 1.0
        };
    }

    // EN 12831-1 Tablo NA.1 — yeniden ısıtma faktörü
    private double GetReheatFactor(BuildingMassType mass, double reheatHours)
    {
        if (reheatHours <= 0) reheatHours = 2.0;
        return mass switch
        {
            BuildingMassType.Light => 0.04 * reheatHours,
            BuildingMassType.Medium => 0.03 * reheatHours,
            BuildingMassType.Heavy => 0.02 * reheatHours,
            _ => 0.03 * reheatHours
        };
    }

    public List<HeatLoadResult> CalculateBuilding(List<HeatLoadInput> rooms)
    {
        return rooms.Select(r => Calculate(r)).ToList();
    }

    public double GetBuildingTotalKW(List<HeatLoadResult> results)
    {
        return results.Sum(r => r.TotalHeatLoadKW);
    }
}

public class HeatLoadInput
{
    public string City { get; set; } = "İstanbul";
    public double IndoorDesignTemp { get; set; } = 22.0;
    public string RoomType { get; set; } = "oturma odası";
    public double RoomVolume { get; set; }
    public double FloorArea { get; set; }
    public double AirChangesPerHour { get; set; }
    public double ReheatFactor { get; set; }
    public double ReheatTimeHours { get; set; } = 2.0;
    public BuildingMassType BuildingMass { get; set; } = BuildingMassType.Medium;
    public List<BuildingSurface> Surfaces { get; set; } = new();
}

public class BuildingSurface
{
    public string Name { get; set; } = "";
    public double Area { get; set; }
    public double UValue { get; set; }
    public double LinearThermalBridge { get; set; } = 0.05; // W/mK varsayılan
    public double BridgeLength { get; set; }
}

public enum BuildingMassType { Light, Medium, Heavy }

public class HeatLoadResult
{
    public string City { get; set; } = "";
    public double OutdoorDesignTemp { get; set; }
    public double IndoorDesignTemp { get; set; }
    public double TransmissionLossW { get; set; }
    public double VentilationLossW { get; set; }
    public double ReheatAllowanceW { get; set; }
    public double TotalHeatLoadW { get; set; }
    public double TotalHeatLoadKW { get; set; }
    public double SpecificHeatLoad { get; set; }
    public double AirChangeRate { get; set; }
    public double ReheatFactor { get; set; }
    public List<SurfaceHeatLoss> SurfaceDetails { get; set; } = new();
}

public class SurfaceHeatLoss
{
    public string Name { get; set; } = "";
    public double Area { get; set; }
    public double UValue { get; set; }
    public double DeltaT { get; set; }
    public double LossW { get; set; }
}
