using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Yerden Isıtma Hesap Servisi (FloorHeatingService)
   NEDEN: TS EN 1264 — yerden ısıtma boru aralığı, devre uzunluğu ve basınç kaybı.
          FINE MEP'te yoktur. Konut projelerinin >%60'ında kullanılıyor.

   YÖNTEMİ:
   - q = α × (T_supply + T_return)/2 - T_room) [W/m²] — basitleştirilmiş EN 1264-2
   - Boru aralığı: s = q_design / q → optimize et
   - Devre: zon alanı / s → boru uzunluğu; Lcirc = A / s
   - Basınç kaybı: Hagen-Poiseuille yaklaşımı (16×L×v²×ρ / (2×d) × f faktörü)
*/
public class FloorHeatingService
{
    // ── Giriş ────────────────────────────────────────────────────────────────────

    public class FloorHeatingZone
    {
        public string Name           { get; set; } = "";
        public double AreaM2         { get; set; }    // Alan (m²)
        public double HeatingLoadW   { get; set; }    // Isıtma yükü (W)
        public double MaxSpacingMm   { get; set; } = 200;  // Maks boru aralığı (mm)
        public string FloorCovering  { get; set; } = "Seramik";
    }

    public class FloorHeatingInput
    {
        public List<FloorHeatingZone> Zones    { get; set; } = [];
        public double SupplyTempC              { get; set; } = 35;   // Besleme sıcaklığı (°C)
        public double ReturnTempC              { get; set; } = 30;   // Dönüş sıcaklığı (°C)
        public double RoomTempC                { get; set; } = 20;   // Oda sıcaklığı (°C)
        public double PipeDiameterMm           { get; set; } = 16;   // Boru dış çapı (mm), tipik 16/2
        public double PipeWallMm               { get; set; } = 2.0;  // Duvar kalınlığı
        public string PipeMaterial             { get; set; } = "PEXa";
        public double MaxCircuitLengthM        { get; set; } = 100;  // Maks devre uzunluğu (m)
        public double FloorResistanceM2K_W     { get; set; } = 0.10; // Zemin termal direnci (m²K/W)
    }

    // ── Çıktı ─────────────────────────────────────────────────────────────────────

    public class FloorHeatingZoneResult
    {
        public FloorHeatingZone Zone         { get; set; } = null!;
        public double           HeatFluxWpm2 { get; set; }   // Isı akısı (W/m²)
        public double           SpacingMm    { get; set; }   // Boru aralığı (mm)
        public int              CircuitCount { get; set; }   // Devre sayısı
        public double           CircuitLenM  { get; set; }   // Tek devre uzunluğu (m)
        public double           TotalPipeLenM{ get; set; }   // Toplam boru (m)
        public double           FlowLph      { get; set; }   // Debi (L/sa)
        public double           PressureDropKpa { get; set; }// Basınç kaybı (kPa)
        public string           Status       { get; set; } = "";
    }

    public class FloorHeatingResult
    {
        public List<FloorHeatingZoneResult> Zones        { get; set; } = [];
        public double                       TotalPipeLenM { get; set; }
        public double                       TotalFlowLph  { get; set; }
        public double                       MaxCircuitDP  { get; set; }  // Pompa seçim basıncı
        public string                       ManifoldSize  { get; set; } = "";
        public List<string>                 Warnings      { get; set; } = [];
    }

    // ── Hesap ─────────────────────────────────────────────────────────────────────

    public static FloorHeatingResult Calculate(FloorHeatingInput inp)
    {
        var result  = new FloorHeatingResult();
        double tmean = (inp.SupplyTempC + inp.ReturnTempC) / 2.0;
        double dt    = tmean - inp.RoomTempC;
        double innerD_m = (inp.PipeDiameterMm - 2 * inp.PipeWallMm) / 1000.0;

        // EN 1264-2: kısaltılmış — ısı geçiş katsayısı α (zemine bağlı)
        double alpha = FloorCoveringAlpha(inp.FloorResistanceM2K_W);

        foreach (var zone in inp.Zones)
        {
            double qDesign = zone.HeatingLoadW / zone.AreaM2;  // W/m²

            // Isı akısı q = alpha × dt — boru aralığı: s = α×dt / qDesign × 100 (cm→mm)
            double spacing = alpha * dt / qDesign * 1000.0;   // mm
            spacing = Math.Max(50, Math.Min(spacing, zone.MaxSpacingMm));
            spacing = RoundSpacing(spacing);   // 50, 75, 100, 150, 200 mm

            double actualQ = alpha * dt * (1000.0 / spacing / 1000.0);  // W/m²

            // Devre uzunluğu
            double circLen = zone.AreaM2 / (spacing / 1000.0);  // toplam boru m²→m
            int circuits   = (int)Math.Ceiling(circLen / inp.MaxCircuitLengthM);
            circuits = Math.Max(circuits, 1);
            double singleLen = circLen / circuits;

            // Debi: Q = P / (ρ×cp×ΔT), ρ=1000, cp=4186
            double flowM3h  = zone.HeatingLoadW / (1000.0 * 4186 * (inp.SupplyTempC - inp.ReturnTempC)) * 3600.0;
            double flowLph  = flowM3h * 1000.0;

            // Basınç kaybı — darcy-weisbach basit: ΔP = f × L/d × ρv²/2
            double velocity = flowM3h / circuits / (Math.PI * innerD_m * innerD_m / 4.0) / 3600.0;
            double re       = velocity * innerD_m / 0.553e-6;   // kinematik vizkozite 45°C
            double fDarcy   = re < 2300 ? 64.0 / re : 0.316 / Math.Pow(re, 0.25);
            double dp_Pa    = fDarcy * (singleLen / innerD_m) * 1000.0 * velocity * velocity / 2.0;
            double dp_kPa   = dp_Pa / 1000.0;

            string status = qDesign > actualQ * 1.15 ? "⚠ Yetersiz — aralık daraltın" :
                            qDesign < actualQ * 0.70 ? "ℹ Aşırı kapasite" : "✓ Uygun";

            result.Zones.Add(new FloorHeatingZoneResult
            {
                Zone           = zone,
                HeatFluxWpm2   = actualQ,
                SpacingMm      = spacing,
                CircuitCount   = circuits,
                CircuitLenM    = singleLen,
                TotalPipeLenM  = circLen,
                FlowLph        = flowLph,
                PressureDropKpa = dp_kPa,
                Status         = status
            });

            result.TotalPipeLenM += circLen;
            result.TotalFlowLph  += flowLph;
            if (dp_kPa > result.MaxCircuitDP) result.MaxCircuitDP = dp_kPa;
        }

        int totalCircuits = 0;
        foreach (var z in result.Zones) totalCircuits += z.CircuitCount;
        result.ManifoldSize = $"{totalCircuits} çıkışlı kolektör ({totalCircuits} devre)";

        if (result.MaxCircuitDP > 30)
            result.Warnings.Add($"Maks. devre basıncı {result.MaxCircuitDP:F1} kPa > 30 kPa — pompa seçiminde dikkate alın.");
        if (inp.SupplyTempC > 45)
            result.Warnings.Add("Besleme sıcaklığı >45°C — yerden ısıtma için 35-45°C önerilir.");

        return result;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────────

    private static double FloorCoveringAlpha(double r_m2kw) =>
        // α_eq = 1 / (0.093 + Rf) — standart EN 1264 yüzey direnci 0.093 m²K/W
        1.0 / (0.093 + r_m2kw);

    private static double RoundSpacing(double s)
    {
        if (s <= 75)  return 75;
        if (s <= 100) return 100;
        if (s <= 150) return 150;
        return 200;
    }

    // ── Zemin Kaplama Referans ────────────────────────────────────────────────────

    public static readonly Dictionary<string, double> FloorCoveringResistance = new()
    {
        ["Seramik/Porselen"]    = 0.00,
        ["Mermer/Taş"]          = 0.01,
        ["Parke (İnce)"]        = 0.10,
        ["Laminat"]             = 0.10,
        ["Parke (Kalın)"]       = 0.15,
        ["Halı (İnce)"]         = 0.10,
        ["Halı (Kalın)"]        = 0.15,
        ["Epoksi/Beton"]        = 0.02,
    };
}
