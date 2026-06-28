using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// ASHRAE Handbook HVAC Applications Ch. 48 / VDI 2081 — Gürültü Analizi
public class AcousticAnalysisService
{
    // Oktav bant frekansları (Hz)
    private static readonly int[] OctaveBands = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };

    // NR (Noise Rating) eğrileri — ISO 1996 / BS 8233
    private static readonly Dictionary<string, int> NoiseLimitsNR = new()
    {
        ["Yatak Odası"] = 25, ["Oturma Odası"] = 30, ["Ofis (Özel)"] = 35,
        ["Ofis (Açık Plan)"] = 40, ["Toplantı Odası"] = 30, ["Restoran"] = 40,
        ["Sınıf"] = 30, ["Kütüphane"] = 30, ["Hastane (Oda)"] = 25,
        ["Ameliyathane"] = 35, ["Konser Salonu"] = 20, ["Sinema"] = 25,
        ["Spor Salonu"] = 45, ["Endüstri"] = 55,
    };

    // Fan gürültü seviyesi (dBA) — tipik katalog değerleri
    public static double EstimateFanNoise(double airFlowM3h, double totalPressurePa, double fanEfficiency)
    {
        if (airFlowM3h <= 0 || totalPressurePa <= 0) return 0;
        // ASHRAE yaklaşımı: Lw = 10 + 10·log10(Q) + 20·log10(ΔP) - 10·log10(η)
        double Lw = 10 + 10 * Math.Log10(airFlowM3h / 3600.0) + 20 * Math.Log10(totalPressurePa) - 10 * Math.Log10(Math.Max(fanEfficiency, 0.1));
        return Math.Max(Lw, 0);
    }

    // Kanal gürültü zayıflaması (dB/m)
    public static double DuctAttenuation(double ductWidthMm, double ductLengthM, bool isLined)
    {
        double w = ductWidthMm / 1000.0;
        double baseAtt = 0.3 / w; // dB/m (çıplak sac)
        if (isLined) baseAtt *= 3.0; // akustik kaplama ile 3x
        return baseAtt * ductLengthM;
    }

    // Dallanma kaybı (dB)
    public static double BranchLoss(double mainFlowM3h, double branchFlowM3h)
    {
        if (mainFlowM3h <= 0) return 0;
        double ratio = branchFlowM3h / mainFlowM3h;
        return -10 * Math.Log10(Math.Max(ratio, 0.01));
    }

    // Dirsek kaybı (dB)
    public static double ElbowLoss(bool isLined) => isLined ? 7.0 : 1.0;

    // Son eleman kaybı — menfez/anemostat (dB)
    public static double TerminalDeviceLoss(double neckVelocityMs)
    {
        // Tipik menfez: Lw = 10·log10(Q) + 30·log10(v) + 10
        return 30 * Math.Log10(Math.Max(neckVelocityMs, 0.5)) + 10;
    }

    // Oda düzeltmesi (dB) — ASHRAE
    public static double RoomCorrection(double roomVolumeM3, double absorptionCoeff = 0.15)
    {
        double surfaceArea = Math.Pow(roomVolumeM3, 2.0 / 3.0) * 6;
        double totalAbsorption = surfaceArea * absorptionCoeff;
        return 10 * Math.Log10(4.0 / totalAbsorption);
    }

    public AcousticResult AnalyzeSystem(AcousticInput input)
    {
        var result = new AcousticResult();

        // 1. Fan gürültüsü
        double fanLw = EstimateFanNoise(input.AirFlowM3h, input.FanPressurePa, input.FanEfficiency);
        result.FanSoundPowerLw = fanLw;

        // 2. Kanal zayıflaması
        double ductAtt = DuctAttenuation(input.DuctWidthMm, input.DuctLengthM, input.IsDuctLined);
        result.DuctAttenuationDb = ductAtt;

        // 3. Dallanma
        double branchAtt = input.BranchCount > 0 ? BranchLoss(input.AirFlowM3h, input.AirFlowM3h / input.BranchCount) : 0;
        result.BranchLossDb = branchAtt;

        // 4. Dirsekler
        double elbowAtt = input.ElbowCount * ElbowLoss(input.IsDuctLined);
        result.ElbowLossDb = elbowAtt;

        // 5. Susturucu (varsa)
        result.SilencerAttenuationDb = input.SilencerInsertionLossDb;

        // 6. Son eleman
        double termLw = TerminalDeviceLoss(input.TerminalVelocityMs);
        result.TerminalNoiseLw = termLw;

        // 7. Oda düzeltmesi
        double roomCorr = RoomCorrection(input.RoomVolumeM3);
        result.RoomCorrectionDb = roomCorr;

        // Toplam: Lp = Lw(fan) - kanal - dallanma - dirsek - susturucu + terminal + oda
        double totalLp = fanLw - ductAtt - branchAtt - elbowAtt - input.SilencerInsertionLossDb + termLw + roomCorr;
        result.RoomSoundPressureLp = Math.Max(totalLp, 0);

        // NR kontrolü
        int nrLimit = NoiseLimitsNR.GetValueOrDefault(input.RoomType, 35);
        result.NRLimit = nrLimit;
        result.NRCompliant = result.RoomSoundPressureLp <= nrLimit;
        result.Recommendation = result.NRCompliant
            ? $"Gürültü seviyesi {result.RoomSoundPressureLp:F0} dBA ≤ NR {nrLimit} — uygun"
            : $"Gürültü seviyesi {result.RoomSoundPressureLp:F0} dBA > NR {nrLimit} — susturucu veya kanal kaplaması gerekli (hedef azalma: {result.RoomSoundPressureLp - nrLimit:F0} dB)";

        return result;
    }

    public static IReadOnlyDictionary<string, int> GetNoiseLimits() => NoiseLimitsNR;
}

public class AcousticInput
{
    public double AirFlowM3h { get; set; } = 1000;
    public double FanPressurePa { get; set; } = 400;
    public double FanEfficiency { get; set; } = 0.7;
    public double DuctWidthMm { get; set; } = 400;
    public double DuctLengthM { get; set; } = 10;
    public bool IsDuctLined { get; set; } = false;
    public int BranchCount { get; set; } = 2;
    public int ElbowCount { get; set; } = 3;
    public double SilencerInsertionLossDb { get; set; } = 0;
    public double TerminalVelocityMs { get; set; } = 3.0;
    public double RoomVolumeM3 { get; set; } = 50;
    public string RoomType { get; set; } = "Ofis (Özel)";
}

public class AcousticResult
{
    public double FanSoundPowerLw { get; set; }
    public double DuctAttenuationDb { get; set; }
    public double BranchLossDb { get; set; }
    public double ElbowLossDb { get; set; }
    public double SilencerAttenuationDb { get; set; }
    public double TerminalNoiseLw { get; set; }
    public double RoomCorrectionDb { get; set; }
    public double RoomSoundPressureLp { get; set; }
    public int NRLimit { get; set; }
    public bool NRCompliant { get; set; }
    public string Recommendation { get; set; } = "";
}
