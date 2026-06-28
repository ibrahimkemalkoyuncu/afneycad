using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// ASHRAE Handbook Fundamentals Ch.18 — Gelişmiş Soğutma Yük Hesabı
// İnfiltrasyon, CLTD saatlik tablo, ekipman çeşitlendirme, güneş korumalı cam
public static class AdvancedCoolingService
{
    // CLTD değerleri — saatlik (saat 8-20 arası, güney cephe) (°C)
    // Kaynak: ASHRAE Handbook 2021 Ch.18 Table 1
    private static readonly Dictionary<string, double[]> CltdHourly = new()
    {
        ["Kuzey"]     = new[] { 3.0, 3.5, 4.0, 4.5, 5.0, 5.5, 6.0, 6.5, 7.0, 7.5, 7.0, 6.0, 5.0 },
        ["Dogu"]      = new[] { 5.0, 8.0, 11.0, 13.0, 14.0, 13.0, 11.0, 9.0, 7.0, 6.0, 5.0, 4.5, 4.0 },
        ["Guney"]     = new[] { 4.0, 5.0, 6.0, 8.0, 10.0, 13.0, 15.0, 16.0, 15.0, 13.0, 10.0, 7.0, 5.0 },
        ["Bati"]      = new[] { 4.0, 4.5, 5.0, 5.5, 6.0, 7.0, 9.0, 12.0, 15.0, 17.0, 16.0, 13.0, 9.0 },
        ["Cati"]      = new[] { 4.0, 6.0, 9.0, 13.0, 17.0, 21.0, 24.0, 26.0, 27.0, 25.0, 21.0, 16.0, 10.0 },
    };

    // Saatlik pik CLTD değeri al
    public static double GetPeakCLTD(string orientation, int hourOfDay)
    {
        string key = orientation switch
        {
            "Kuzey" or "KuzeyDogu" or "KuzeyBati" => "Kuzey",
            "Dogu" or "GuneyDogu" => "Dogu",
            "Guney" => "Guney",
            "Bati" or "GuneyBati" => "Bati",
            "Cati" => "Cati",
            _ => "Guney"
        };

        var hourly = CltdHourly.GetValueOrDefault(key, CltdHourly["Guney"]);
        int idx = Math.Clamp(hourOfDay - 8, 0, hourly.Length - 1);
        return hourly[idx];
    }

    // İnfiltrasyon yük hesabı — Crack method (ASHRAE)
    // Q_inf = V_inf × ρ × Cp × ΔT (sensible) + V_inf × ρ × h_fg × Δw (latent)
    public static InfiltrationResult CalculateInfiltration(
        double roomVolumeM3, double airChangesPerHour,
        double outdoorTempC, double indoorTempC,
        double outdoorRH, double indoorRH)
    {
        double rho = 1.2; // kg/m³
        double cp = 1005.0; // J/(kg·K)
        double hfg = 2454000.0; // J/kg (buharlaşma gizil ısısı)

        double vInfM3s = roomVolumeM3 * airChangesPerHour / 3600.0;
        double mDot = vInfM3s * rho;

        double sensible = mDot * cp * Math.Abs(outdoorTempC - indoorTempC); // W
        double wOut = PsychrometricService.HumidityRatio(outdoorTempC, outdoorRH);
        double wIn = PsychrometricService.HumidityRatio(indoorTempC, indoorRH);
        double latent = mDot * hfg * Math.Abs(wOut - wIn); // W

        return new InfiltrationResult
        {
            SensibleW = sensible,
            LatentW = latent,
            TotalW = sensible + latent,
            AirFlowM3h = vInfM3s * 3600
        };
    }

    // Ekipman iç ısı kazancı detaylandırma
    public static double EquipmentHeatGain(string equipmentType, int count = 1)
    {
        double perUnit = equipmentType.ToLowerInvariant() switch
        {
            "bilgisayar" or "pc" or "computer" => 150, // W
            "monitör" or "monitor" => 80,
            "yazıcı" or "printer" => 100,
            "fotokopi" or "copier" => 400,
            "sunucu" or "server" => 500,
            "buzdolabı" or "fridge" => 200,
            "fırın" or "oven" => 2000,
            "ocak" or "stove" => 3000,
            "kahve makinesi" or "coffee" => 150,
            "projeksiyon" or "projector" => 300,
            _ => 100
        };
        return perUnit * count;
    }

    // Güneş korumalı cam düzeltme faktörü
    public static double ShadingCorrectionFactor(string shadingType)
    {
        return shadingType.ToLowerInvariant() switch
        {
            "iç perde" or "internal blind" => 0.55,
            "dış perde" or "external blind" => 0.15,
            "dış jaluzi" or "external louver" => 0.20,
            "iç stor" or "internal roller" => 0.45,
            "markiz" or "awning" => 0.30,
            "film" or "reflective film" => 0.40,
            "yok" or "none" => 1.0,
            _ => 0.55
        };
    }

    // Kanal kayıp detay hesabı — fitting + damper
    public static double CalculateDuctFittingLoss(DuctFittingList fittings, double velocityMs)
    {
        double totalK = 0;
        totalK += fittings.Elbow90Count * 1.2;
        totalK += fittings.Elbow45Count * 0.5;
        totalK += fittings.TeeCount * 1.8;
        totalK += fittings.DamperCount * 0.5;
        totalK += fittings.DiffuserCount * 2.5;
        totalK += fittings.FilterCount * 3.0;
        totalK += fittings.SilencerCount * 1.5;

        // ΔP = K × ρ × v² / 2 (Pa)
        double rho = 1.2;
        return totalK * rho * velocityMs * velocityMs / 2.0;
    }

    // Fan sistem eğrisi (system curve) — ΔP = C × Q²
    public static List<(double FlowM3h, double PressurePa)> GenerateSystemCurve(
        double designFlowM3h, double designPressurePa, int points = 20)
    {
        double C = designPressurePa / (designFlowM3h * designFlowM3h);
        var curve = new List<(double, double)>();
        for (int i = 0; i <= points; i++)
        {
            double q = designFlowM3h * i / points;
            curve.Add((q, C * q * q));
        }
        return curve;
    }

    // Fan çalışma noktası tespiti (system curve × fan curve kesişimi)
    public static (double FlowM3h, double PressurePa) FindOperatingPoint(
        List<(double FlowM3h, double PressurePa)> systemCurve,
        List<(double FlowM3h, double PressurePa)> fanCurve)
    {
        for (int i = 1; i < systemCurve.Count && i < fanCurve.Count; i++)
        {
            double sysDiff = systemCurve[i].PressurePa - fanCurve[i].PressurePa;
            double prevDiff = systemCurve[i - 1].PressurePa - fanCurve[i - 1].PressurePa;

            if (sysDiff >= 0 && prevDiff < 0)
            {
                double t = Math.Abs(prevDiff) / (Math.Abs(prevDiff) + Math.Abs(sysDiff));
                double flow = systemCurve[i - 1].FlowM3h + t * (systemCurve[i].FlowM3h - systemCurve[i - 1].FlowM3h);
                double pressure = systemCurve[i - 1].PressurePa + t * (systemCurve[i].PressurePa - systemCurve[i - 1].PressurePa);
                return (flow, pressure);
            }
        }
        return (systemCurve.Last().FlowM3h, systemCurve.Last().PressurePa);
    }
}

public class InfiltrationResult
{
    public double SensibleW { get; set; }
    public double LatentW { get; set; }
    public double TotalW { get; set; }
    public double AirFlowM3h { get; set; }
}

public class DuctFittingList
{
    public int Elbow90Count { get; set; }
    public int Elbow45Count { get; set; }
    public int TeeCount { get; set; }
    public int DamperCount { get; set; }
    public int DiffuserCount { get; set; }
    public int FilterCount { get; set; }
    public int SilencerCount { get; set; }
}
