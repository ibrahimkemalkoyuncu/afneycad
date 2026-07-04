using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Pis Su Hesap Föyü Servisi (WasteWaterCalcSheetService)
   NEDEN: FINE SANI / OTONET'teki "Hesap Föyü" işlevinin AfneyCad karşılığı.
          Manning formülü ile segment bazlı pis su hesabı yapar.

   HESAP YÖNTEMLERİ:
     - Sarfiyat (DU) Yöntemi : TS EN 12056-2 — Q = K·√(ΣDU)
     - DIN Normu             : DIN 1986/1988  — Q = f(DU, bina tipi)

   MANNING FORMÜLİ (Kısmi Dolu Dairesel Boru):
     Q_dolu = (1/n) · (π·D²/4) · (D/4)^(2/3) · S^(1/2)
     Kısmi dolu için tablolu oran: q/Q_dolu = f(h/D)
*/
public class WasteWaterCalcSheetService
{
    // ── Devre Seçenekleri ─────────────────────────────────────────────────────
    public class CircuitOptions
    {
        public double WaterTempC         { get; set; } = 20.0;   // °C
        public string BuildingType       { get; set; } = "Konut"; // Konut/Ofis/Otel/Hastane
        public double FrequencyFactor    { get; set; } = 0.5;    // K (Konut=0.5)
        public string PipeMaterial       { get; set; } = "PVC";
        public double RoughnessN         { get; set; } = 0.011;  // Manning n
        public double DefaultSlopePct    { get; set; } = 2.0;    // %
        public CalcMethod Method         { get; set; } = CalcMethod.DU_Sarfiyat;
        public double MaxFillRatioBranch { get; set; } = 0.50;   // Branşman max doluluk
        public double MaxFillRatioMain   { get; set; } = 0.70;   // Kollektör max doluluk
    }

    public enum CalcMethod { DU_Sarfiyat, DIN_Norm }

    // ── Hesap Föyü Satırı ─────────────────────────────────────────────────────
    public class CalcRow
    {
        public int    SegmentNo     { get; set; }
        public string SegmentId     { get; set; } = "";
        public string PipeType      { get; set; } = ""; // "Yatay" / "Kolon"
        public double LengthM       { get; set; }
        public double LoadUnits     { get; set; }       // DU (Drenaj Birimi)
        public double DesignFlowLs  { get; set; }       // Q (l/s)
        public double DiameterMm    { get; set; }       // Seçilen DN (mm)
        public double SlopePct      { get; set; }       // Eğim (%)
        public double VelocityMs    { get; set; }       // Hız (m/s)
        public double FillRatio     { get; set; }       // Doluluk (0-1)
        public double CapacityLs    { get; set; }       // Dolu boru kapasitesi (l/s)
        public bool   IsOk          { get; set; }
        public string Warnings      { get; set; } = "";
    }

    // ── Hesap Föyü Sonucu ─────────────────────────────────────────────────────
    public class CalcSheetResult
    {
        public List<CalcRow>  Rows        { get; set; } = [];
        public CircuitOptions Options     { get; set; } = new();
        public int  TotalSegments         { get; set; }
        public int  WarningCount          { get; set; }
        public double TotalLengthM        { get; set; }
        public string Summary             { get; set; } = "";
        public List<string> Notes         { get; set; } = [];
    }

    // ── Foseptik / Kapalı Çukur Hesabı ───────────────────────────────────────
    public class SepticTankInput
    {
        public int    PersonCount        { get; set; } = 10;
        public double DailyWaterLiters   { get; set; } = 150;  // L/kişi/gün
        public double RetentionDays      { get; set; } = 3;    // gün (konut=3)
        public double SludgeFactor       { get; set; } = 1.5;  // çamur faktörü
        public string TankType           { get; set; } = "Foseptik"; // Foseptik / Kapalı Çukur / Emdirme
    }

    public class SepticTankResult
    {
        public double DailyFlowM3        { get; set; }
        public double RetentionVolumeM3  { get; set; }
        public double SludgeVolumeM3     { get; set; }
        public double TotalVolumeM3      { get; set; }
        public double RecommendedDepthM  { get; set; } = 2.0;
        public double RecommendedAreaM2  { get; set; }
        public double RecommendedWidthM  { get; set; }
        public double RecommendedLengthM { get; set; }
        public string Standard           { get; set; } = "TS 8358";
        public List<string> Notes        { get; set; } = [];
    }

    // ── Emdirme Çukuru Hesabı (Perkolasyon) ──────────────────────────────────
    public class SoakPitInput
    {
        public int    PersonCount       { get; set; } = 10;
        public double DailyWaterLiters  { get; set; } = 150;   // L/kişi/gün
        public double PercolationRate   { get; set; } = 50;    // L/m²/gün (saha testi)
        public double SafetyFactor      { get; set; } = 2.0;   // emniyet faktörü
        public double PitDepthM         { get; set; } = 2.0;   // çukur derinliği (m)
        public double PitDiameterM      { get; set; } = 1.5;   // çukur çapı (m)
    }

    public class SoakPitResult
    {
        public double DailyFlowM3       { get; set; }
        public double DesignFlowM3      { get; set; }   // güvenlik faktörlü
        public double RequiredAreaM2    { get; set; }   // m² (temas yüzeyi)
        public double PitLateralAreaM2  { get; set; }   // yan yüzey = π × D × H
        public double PitCount          { get; set; }   // gerekli çukur adedi
        public double RecommendedDepthM { get; set; }
        public double RecommendedDiamM  { get; set; }
        public bool   IsFeasible        { get; set; }
        public string Standard          { get; set; } = "TS 7880 / BS 6297";
        public List<string> Notes       { get; set; } = [];
    }

    // ── Pis Su Pompası Hesabı ─────────────────────────────────────────────────
    public class SewagePumpResult
    {
        public double PitVolumeM3        { get; set; }
        public double InflowLs           { get; set; }
        public double RequiredFlowM3h    { get; set; }
        public double RequiredHeadM      { get; set; }
        public int    CyclesPerHour      { get; set; } = 6; // Max start sayısı
        public string Recommendation     { get; set; } = "";
    }

    // ── Manning n değerleri ───────────────────────────────────────────────────
    private static readonly Dictionary<string, double> _manningN = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PVC"]       = 0.011,
        ["HDPE"]      = 0.011,
        ["PP"]        = 0.012,
        ["Beton"]     = 0.013,
        ["Beton/Sıvalı"] = 0.014,
        ["DökmeDemir"]= 0.013,
        ["Seramik"]   = 0.011,
        ["Vitrify"]   = 0.011,
    };

    // ── TS EN 12056-2 Boru Tablosu (DN → {MinSlope%, MaxQBranch l/s, MaxQMain l/s}) ──
    private static readonly (double DN, double MinSlopePct, double MaxQBranch, double MaxQMain)[] _pipeTable =
    [
        (50,  2.50, 0.80,  0.80),
        (75,  1.50, 2.00,  2.00),
        (100, 1.00, 5.20,  5.20),
        (125, 0.80, 8.40,  8.40),
        (150, 0.70, 12.80, 16.00),
        (200, 0.50, 25.00, 32.00),
        (250, 0.40, 42.00, 55.00),
        (300, 0.30, 65.00, 85.00),
    ];

    // ── Ana API ──────────────────────────────────────────────────────────────

    public CalcSheetResult CalculateFromDatabase(CadDatabase database, CircuitOptions options)
    {
        var pipes = database.GetAllEntities()
            .OfType<PipeEntity>()
            .Where(p => p.SystemType == MechanicalSystemType.WasteWater ||
                        p.SystemType == MechanicalSystemType.RainWater)
            .OrderBy(p => p.LoadUnits)
            .ToList();

        var result = new CalcSheetResult { Options = options };
        int segNo = 1;

        foreach (var pipe in pipes)
        {
            var row = CalculateSegment(pipe, options, segNo++);
            result.Rows.Add(row);

            if (!row.IsOk) result.WarningCount++;

            // Write back to pipe entity
            pipe.FlowRate = row.DesignFlowLs * 3.6; // l/s → m³/h
            pipe.Velocity = row.VelocityMs;
        }

        result.TotalSegments = result.Rows.Count;
        result.TotalLengthM  = result.Rows.Sum(r => r.LengthM);
        result.Summary = $"{result.TotalSegments} devre parçası hesaplandı. " +
                         $"Toplam boru uzunluğu: {result.TotalLengthM:F1} m. " +
                         $"{result.WarningCount} uyarı.";

        result.Notes.Add("Hesaplamalar TS EN 12056-2 ve Manning formülü esas alınarak yapılmıştır.");
        if (options.Method == CalcMethod.DIN_Norm)
            result.Notes.Add("Seçilen yöntem: DIN 1986 Normu (K = 1.0).");

        return result;
    }

    private CalcRow CalculateSegment(PipeEntity pipe, CircuitOptions options, int segNo)
    {
        bool isVertical = IsVerticalPipe(pipe);
        double dia = pipe.InnerDiameter; // mm
        double slope = pipe.Slope > 0 ? pipe.Slope : options.DefaultSlopePct / 100.0;
        double n = _manningN.TryGetValue(options.PipeMaterial, out double nv) ? nv : options.RoughnessN;

        double du = pipe.LoadUnits;
        double k = options.Method == CalcMethod.DIN_Norm ? 1.0 : options.FrequencyFactor;
        double flowLs = du < 5 ? 0.15 + 0.10 * du : k * Math.Sqrt(du);

        // Manning: Q_dolu = (1/n) * A * R^(2/3) * S^(1/2)
        double r = dia / 1000.0; // radius [m]
        double D = dia / 1000.0;
        double area = Math.PI * D * D / 4.0;
        double hydraulicR = D / 4.0;
        double slopeFrac = slope;
        double qFull = (1.0 / n) * area * Math.Pow(hydraulicR, 2.0 / 3.0) * Math.Sqrt(slopeFrac);
        double qFullLs = qFull * 1000.0;

        // Doluluk oranı: q/Q_dolu
        double fillRatio = qFullLs > 0 ? flowLs / qFullLs : 1.0;
        fillRatio = Math.Min(fillRatio, 1.0);

        // Hız (kısmi dolu için Manning kullanılır, basit yaklaşım: tam dolu hız * düzeltme)
        double vFull = qFull / area; // m/s
        double velocity = vFull * Math.Pow(fillRatio, 1.0 / 6.0); // yaklaşık

        double maxFill = isVertical ? 1.0 : options.MaxFillRatioBranch;
        bool isOk = fillRatio <= maxFill && velocity >= 0.6;
        string warnings = "";
        if (fillRatio > maxFill) warnings += $"Doluluk %{fillRatio * 100:F0} > max %{maxFill * 100:F0}. ";
        if (velocity < 0.6 && flowLs > 0) warnings += "Hız < 0.6 m/s (çökelme riski). ";
        if (velocity > 3.0) warnings += "Hız > 3.0 m/s (aşınma riski). ";

        return new CalcRow
        {
            SegmentNo    = segNo,
            SegmentId    = pipe.Id.ToString()[..8],
            PipeType     = isVertical ? "Kolon" : "Yatay",
            LengthM      = pipe.Length,
            LoadUnits    = du,
            DesignFlowLs = Math.Round(flowLs, 3),
            DiameterMm   = dia,
            SlopePct     = slope * 100,
            VelocityMs   = Math.Round(velocity, 2),
            FillRatio    = Math.Round(fillRatio * 100, 1),
            CapacityLs   = Math.Round(qFullLs, 2),
            IsOk         = isOk,
            Warnings     = warnings.Trim(),
        };
    }

    // ── Foseptik / Kapalı Çukur ───────────────────────────────────────────────

    public SepticTankResult CalculateSepticTank(SepticTankInput input)
    {
        double dailyFlowM3 = input.PersonCount * input.DailyWaterLiters / 1000.0;

        // TS 8358: V_bek = Q_günlük × T_bekleme × çamur faktörü
        double retentionVol = dailyFlowM3 * input.RetentionDays;
        double sludgeVol    = retentionVol * (input.SludgeFactor - 1.0);
        double totalVol     = retentionVol * input.SludgeFactor;

        // Boyutlandırma: Derinlik 2 m (standart), genişlik : uzunluk = 1:2
        double depth  = 2.0;
        double area   = totalVol / depth;
        double width  = Math.Sqrt(area / 2.0);
        double length = 2.0 * width;

        // Standart modül: 1.5 m genişlik, 3.0 m uzunluk
        width  = Math.Ceiling(width  / 0.5) * 0.5;
        length = Math.Ceiling(length / 0.5) * 0.5;

        var result = new SepticTankResult
        {
            DailyFlowM3       = Math.Round(dailyFlowM3, 2),
            RetentionVolumeM3 = Math.Round(retentionVol, 2),
            SludgeVolumeM3    = Math.Round(sludgeVol, 2),
            TotalVolumeM3     = Math.Round(totalVol, 2),
            RecommendedDepthM = depth,
            RecommendedAreaM2 = Math.Round(width * length, 1),
            RecommendedWidthM = width,
            RecommendedLengthM= length,
            Standard          = input.TankType == "Foseptik" ? "TS 8358" : "TS EN 12566-1",
        };

        result.Notes.Add($"Günlük debi: {result.DailyFlowM3:F2} m³/gün ({input.PersonCount} kişi × {input.DailyWaterLiters} lt/kişi)");
        result.Notes.Add($"Bekleme süresi: {input.RetentionDays} gün | Çamur faktörü: {input.SludgeFactor}");
        result.Notes.Add($"Önerilen boyut: {result.RecommendedWidthM:F1} m × {result.RecommendedLengthM:F1} m × {result.RecommendedDepthM:F1} m = {result.TotalVolumeM3:F1} m³");

        return result;
    }

    // ── Emdirme Çukuru (Perkolasyon) ──────────────────────────────────────────

    /*
       FORMÜL: BS EN 12566-2 / TS 7880
       Gerekli temas alanı: A = Q_tasarım / f_perc
         Q_tasarım   = kişi × günlük debi × güvenlik faktörü (m³/gün)
         f_perc      = perkolasyon hızı (L/m²/gün) — saha testi (VTP veya TS 7880 Ek A)
       Çukur kapasitesi: A_çukur = π × D × H (silindir yan yüzey)
       Gerekli çukur sayısı: n = ⌈A / A_çukur⌉
    */
    public SoakPitResult CalculateSoakPit(SoakPitInput input)
    {
        // Emniyet faktörü en az 1.0 olmalıdır; 0 veya negatif girilirse tasarım debisi
        // sıfırlanır ve gerekli alan/çukur adedi yanıltıcı biçimde 0 çıkar. (TS 7880 min f=1.5–2.0)
        double safetyFactor = input.SafetyFactor >= 1.0 ? input.SafetyFactor : 1.0;

        double dailyM3     = input.PersonCount * input.DailyWaterLiters / 1000.0;
        double designM3    = dailyM3 * safetyFactor;
        double designL     = designM3 * 1000.0;

        // Gerekli temas alanı (m²)
        double requiredAreaM2 = input.PercolationRate > 0
            ? designL / input.PercolationRate
            : 0;

        // Tek çukurun yan yüzey alanı (m²) — taban hariç (drenaj standart yaklaşım)
        double pitLateralM2 = Math.PI * input.PitDiameterM * input.PitDepthM;

        int pitCount = pitLateralM2 > 0
            ? (int)Math.Ceiling(requiredAreaM2 / pitLateralM2)
            : 1;

        bool feasible = input.PercolationRate >= 10; // < 10 L/m²/gün → zemin geçirimsiz

        var result = new SoakPitResult
        {
            DailyFlowM3       = Math.Round(dailyM3, 3),
            DesignFlowM3      = Math.Round(designM3, 3),
            RequiredAreaM2    = Math.Round(requiredAreaM2, 1),
            PitLateralAreaM2  = Math.Round(pitLateralM2, 2),
            PitCount          = pitCount,
            RecommendedDepthM = input.PitDepthM,
            RecommendedDiamM  = input.PitDiameterM,
            IsFeasible        = feasible,
            Standard          = "TS 7880 / BS EN 12566-2",
        };

        result.Notes.Add($"Kişi sayısı: {input.PersonCount} kişi × {input.DailyWaterLiters} L/kişi/gün = {dailyM3 * 1000:F0} L/gün");
        if (input.SafetyFactor < 1.0)
            result.Notes.Add($"⚠ Girilen emniyet faktörü ({input.SafetyFactor}) < 1.0 — hesapta 1.0 kullanıldı (TS 7880 önerisi 1.5–2.0).");
        result.Notes.Add($"Tasarım debisi (güvenlik f={safetyFactor}): {designL:F0} L/gün");
        result.Notes.Add($"Perkolasyon hızı: {input.PercolationRate} L/m²/gün  →  Gerekli alan: {requiredAreaM2:F1} m²");
        result.Notes.Add($"Tek çukur yan yüzeyi (Ø{input.PitDiameterM:F1} m × {input.PitDepthM:F1} m): {pitLateralM2:F2} m²");
        result.Notes.Add($"Gerekli çukur adedi: {pitCount} adet");

        if (!feasible)
            result.Notes.Add("⚠ Perkolasyon hızı < 10 L/m²/gün — zemin emdirme için yeterince geçirgen değil. Alternatif (fosseptik + arıtma) değerlendirin.");
        else
            result.Notes.Add($"✓ Emdirme çukuru uygulanabilir (TS 7880 min 10 L/m²/gün şartı karşılandı).");

        return result;
    }

    // ── Pis Su Pompası ────────────────────────────────────────────────────────

    public SewagePumpResult CalculateSewagePump(double inflowLs, double staticHeadM, double sumpVolumeM3 = 0)
    {
        // Pompa debisi: min 2× giriş debisi (NFPA/TS 9811 öneri)
        double pumpFlowM3h = inflowLs * 2 * 3.6;

        // Çalışma basıncı: statik yük + boruda sürtünme kaybı (%20 ekstra)
        double totalHead = staticHeadM * 1.20;

        // Sump hacmi: 6 start/saat kuralı → V = Q_pump / (4 * N_max)
        double minVol = pumpFlowM3h / 3600.0 / 4.0 / 6.0 * 1000; // lt
        minVol = Math.Max(minVol, 200); // min 200 lt

        return new SewagePumpResult
        {
            PitVolumeM3      = sumpVolumeM3 > 0 ? sumpVolumeM3 : minVol / 1000.0,
            InflowLs         = inflowLs,
            RequiredFlowM3h  = Math.Round(pumpFlowM3h, 1),
            RequiredHeadM    = Math.Round(totalHead, 1),
            CyclesPerHour    = 6,
            Recommendation   = $"Pompa: Q ≥ {pumpFlowM3h:F1} m³/h, H ≥ {totalHead:F1} mSS. Min sump hacmi: {minVol:F0} lt.",
        };
    }

    // ── HTML Rapor ────────────────────────────────────────────────────────────

    public string ExportToHtml(CalcSheetResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(
            "<!DOCTYPE html><html><head><meta charset='utf-8'/>" +
            "<title>Pis Su Hesap Föyü — AfneyCAD</title>" +
            "<style>body{font-family:Segoe UI,Arial;background:#1a1a2e;color:#eee;padding:20px}" +
            "h2{color:#90CAF9}h3{color:#A5D6A7}" +
            "table{border-collapse:collapse;width:100%}" +
            "th{background:#0D47A1;color:#fff;padding:6px 8px;font-size:11px;text-align:left}" +
            "td{padding:5px 8px;border-bottom:1px solid #333;font-size:11px}" +
            "tr:nth-child(even){background:#252540}" +
            ".warn{color:#FFD54F}.err{color:#EF5350}.ok{color:#69F0AE}" +
            "</style></head><body>");

        sb.AppendLine($"<h2>PİS SU HESAP FÖYÜ</h2>");
        sb.AppendLine($"<p style='color:#888'>AfneyCAD — {DateTime.Now:dd.MM.yyyy HH:mm} | {result.Options.Method} Yöntemi | Malzeme: {result.Options.PipeMaterial} | n={result.Options.RoughnessN}</p>");

        sb.AppendLine("<table><tr><th>#</th><th>Segment</th><th>Tür</th><th>Boy (m)</th><th>DU</th><th>Q (l/s)</th><th>DN (mm)</th><th>Eğim %</th><th>Hız (m/s)</th><th>Doluluk %</th><th>Kapasite (l/s)</th><th>Durum</th></tr>");
        foreach (var r in result.Rows)
        {
            string cls = r.IsOk ? "ok" : "err";
            string status = r.IsOk ? "✓" : $"⚠ {r.Warnings}";
            sb.AppendLine($"<tr class='{cls}'><td>{r.SegmentNo}</td><td>{r.SegmentId}</td><td>{r.PipeType}</td><td>{r.LengthM:F1}</td><td>{r.LoadUnits:F1}</td><td>{r.DesignFlowLs:F3}</td><td>{r.DiameterMm:F0}</td><td>{r.SlopePct:F2}</td><td>{r.VelocityMs:F2}</td><td>{r.FillRatio:F1}</td><td>{r.CapacityLs:F2}</td><td>{status}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine($"<p><b>Özet:</b> {result.Summary}</p>");
        foreach (var n in result.Notes)
            sb.AppendLine($"<p style='color:#888'>{n}</p>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────────

    private static bool IsVerticalPipe(PipeEntity pipe)
    {
        var delta = pipe.EndPoint - pipe.StartPoint;
        double len = delta.Length();
        if (len < 0.001) return false;
        return Math.Abs(delta.Z) / len > 0.8;
    }

    public static double[] GetStandardDiameters() => [50, 75, 100, 125, 150, 200, 250, 300];

    public static (double MinSlopePct, double MaxCapacityLs) GetPipeTableEntry(double dn)
    {
        foreach (var (DN, slope, maxBranch, _) in _pipeTable)
            if (DN >= dn) return (slope, maxBranch);
        return (0.3, 85);
    }
}
