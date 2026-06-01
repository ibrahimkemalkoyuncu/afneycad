using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Isıl Genleşme Kompansatör Servisi (ExpansionLoopService)
   NEDEN: Isıtma ve sıcak su borularındaki ısıl genleşmeyi absorbe etmek için
          gerekli U/Z/L dirsek boyutlarını hesaplamak için.
          TS EN 13480 / ASME B31.1 referanslı.

   FİZİK:
   ΔL = α × L × ΔT
   α (çelik) = 12×10⁻⁶ m/m·K
   α (bakır) = 17×10⁻⁶ m/m·K
   α (PVC/PP) = 70×10⁻⁶ m/m·K

   U-DİRSEK BOYU:
   L_u = C × √(D × ΔL)     [m]
   C = 0.7 (çelik/bakır), 1.0 (plastik)
*/
public class ExpansionLoopService
{
    // ── Malzeme Genleşme Katsayıları ─────────────────────────────────────────────
    public static readonly Dictionary<string, double> AlphaPerK = new()
    {
        ["Çelik (St/galvaniz)"] = 12e-6,
        ["Bakır"]               = 17e-6,
        ["Paslanmaz Çelik"]     = 16e-6,
        ["PVC (sert)"]          = 70e-6,
        ["PP-R (polipropilen)"] = 150e-6,
        ["PE (polietilen)"]     = 200e-6,
        ["PE-X"]                = 140e-6,
        ["Dökme Demir"]         = 10e-6,
    };

    // ── Sonuç Sınıfı ─────────────────────────────────────────────────────────────

    public class LoopResult
    {
        public double DeltaLMm          { get; set; }   // Toplam genleşme (mm)
        public double ULoopLengthM      { get; set; }   // U-dirsek kol boyu (m)
        public double ZLoopOffsetM      { get; set; }   // Z-offset kol boyu (m)
        public double LLoopOffsetM      { get; set; }   // L-dirsek kol boyu (m)
        public string ULoopLabel        { get; set; } = "";
        public string ZLoopLabel        { get; set; } = "";
        public string LLoopLabel        { get; set; } = "";
        public string Recommendation    { get; set; } = "";
        public List<string> Notes       { get; set; } = [];
    }

    // ── Parametreler ──────────────────────────────────────────────────────────────

    public double PipeLengthM        { get; set; } = 10.0;
    public double DiameterMm         { get; set; } = 50.0;
    public double TempInstallC       { get; set; } = 20.0;
    public double TempOperatingC     { get; set; } = 80.0;
    public string Material           { get; set; } = "Çelik (St/galvaniz)";

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public LoopResult Calculate()
    {
        double alpha = AlphaPerK.GetValueOrDefault(Material, 12e-6);
        double dT    = TempOperatingC - TempInstallC;
        double dL    = alpha * PipeLengthM * dT;            // m
        double dLmm  = dL * 1000;                           // mm

        double D     = DiameterMm / 1000.0;                 // m
        bool   isPlastic = Material.StartsWith("PVC") || Material.StartsWith("PP") || Material.StartsWith("PE");
        double C     = isPlastic ? 1.0 : 0.7;

        // U-dirsek kol boyu: L_u = C × √(D × ΔL)
        double lu = C * Math.Sqrt(D * dL);
        // Z-dirsek: ~%30 daha kısa
        double lz = lu * 0.70;
        // L-dirsek: Köşe dirsek → kol = ΔL / (0.02 × D)  kaba kural
        double ll = dL / (0.02 * D);

        var result = new LoopResult
        {
            DeltaLMm     = Math.Round(dLmm, 2),
            ULoopLengthM = Math.Round(lu, 3),
            ZLoopOffsetM = Math.Round(lz, 3),
            LLoopOffsetM = Math.Round(ll, 3),
            ULoopLabel   = $"U-Dirsek kol boyu: {lu * 1000:F0} mm × 2 (toplam {lu * 2 * 1000:F0} mm)",
            ZLoopLabel   = $"Z-Dirsek offset: {lz * 1000:F0} mm",
            LLoopLabel   = $"L-Dirsek kol: {ll * 1000:F0} mm",
        };

        // Öneri
        if (dLmm < 10)
            result.Recommendation = "Genleşme düşük — sabit mesnet yeterli, kompansatör gerekmeyebilir.";
        else if (dLmm < 50)
            result.Recommendation = $"L veya Z-dirsek önerilir. Kol boyu ≥ {lz * 1000:F0} mm.";
        else if (dLmm < 150)
            result.Recommendation = $"U-dirsek gerekli. Her {Math.Round(PipeLengthM, 0)} m'de bir U-dirsek. Kol: {lu * 1000:F0} mm.";
        else
            result.Recommendation = $"⚠ Büyük genleşme ({dLmm:F0} mm) — lyre/dalga kompansatör veya esnek boru eklemi gerekli.";

        result.Notes.Add($"α ({Material}) = {alpha * 1e6:F1} × 10⁻⁶ m/m·K");
        result.Notes.Add($"ΔT = {dT:F0}°C ({TempInstallC:F0}°C → {TempOperatingC:F0}°C)");
        result.Notes.Add($"ΔL = α × L × ΔT = {dLmm:F2} mm");
        result.Notes.Add($"Boru çapı: DN {DiameterMm:F0}mm, Uzunluk: {PipeLengthM:F1} m");
        result.Notes.Add("Referans: TS EN 13480 / ASME B31.1");

        return result;
    }
}
