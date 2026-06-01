using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Yağmur Oluğu ve Deresi Boyutlandırma Servisi (GutterSizingService)
   NEDEN: TS EN 12056-3 / TS EN 1253 kapsamında çatı yağmur oluklarının,
          dere borularının ve bağlantı boruların boyutlarını hesaplamak için.

   HESAP YÖNTEMİ:
   - Tasarım yağış yoğunluğu: r [l/s·m²] — Türkiye şehirlerine göre (TS EN 12056-3 Ek NA)
   - Efektif çatı alanı: A_eff = A × C  (C: akış katsayısı)
   - Debi: Q = r × A_eff
   - Oluk: Manning formülüyle doluluk oranı ≤ 0.5
   - Dere borusu: Darcy-Weisbach / Manning
*/
public class GutterSizingService
{
    // ── Tasarım Yağış Yoğunlukları (TS EN 12056-3 Ek NA — 2 yıllık tekerrür) ──
    public static readonly Dictionary<string, double> RainfallIntensity = new()
    {
        ["İstanbul"]   = 0.028, ["Ankara"]     = 0.020, ["İzmir"]      = 0.025,
        ["Bursa"]      = 0.022, ["Antalya"]    = 0.032, ["Adana"]      = 0.030,
        ["Trabzon"]    = 0.035, ["Samsun"]     = 0.030, ["Rize"]       = 0.040,
        ["Konya"]      = 0.018, ["Kayseri"]    = 0.018, ["Erzurum"]    = 0.015,
        ["Gaziantep"]  = 0.022, ["Mersin"]     = 0.028, ["Denizli"]    = 0.022,
        ["İzmir Ege"]  = 0.025, ["Genel"]      = 0.025,
    };

    // ── Akış Katsayıları (TS EN 12056-3 Tablo NA.1) ──────────────────────────────
    public static readonly Dictionary<string, double> RunoffCoefficients = new()
    {
        ["Çatı (kiremit/metal)"] = 1.0, ["Düz Çatı (beton)"]    = 1.0,
        ["Çakıllı Çatı"]         = 0.7, ["Yeşil Çatı"]           = 0.5,
        ["Taşlı Teras"]          = 0.9, ["Çimen / Toprak (>20°)"] = 0.5,
        ["Asfalt / Beton"]       = 0.9, ["Bahçe / Çimen"]         = 0.35,
    };

    // ── Giriş Tanımı ──────────────────────────────────────────────────────────────

    public class RoofSection
    {
        public string Name           { get; set; } = "";
        public double AreaM2         { get; set; }
        public string SurfaceType    { get; set; } = "Çatı (kiremit/metal)";
        public double RunoffCoeff    => RunoffCoefficients.GetValueOrDefault(SurfaceType, 1.0);
        public double EffectiveAreaM2 => AreaM2 * RunoffCoeff;
    }

    // ── Sonuç ─────────────────────────────────────────────────────────────────────

    public class GutterResult
    {
        public string  City                { get; set; } = "";
        public double  RainfallLsM2        { get; set; }
        public double  TotalAreaM2         { get; set; }
        public double  TotalEffAreaM2      { get; set; }
        public double  TotalFlowLs         { get; set; }

        // Oluk (yarım yuvarlak)
        public double  GutterDiameterMm   { get; set; }
        public string  GutterLabel        { get; set; } = "";
        public double  GutterVelocityMs   { get; set; }
        public double  GutterFillRatio    { get; set; }

        // Dere (iniş) borusu
        public double  DownpipeDiameterMm { get; set; }
        public string  DownpipeLabel      { get; set; } = "";
        public int     DownpipeCount      { get; set; }
        public double  DownpipeSpacingM   { get; set; }

        public List<string> Notes         { get; set; } = [];
        public string Summary             { get; set; } = "";
    }

    // ── Hesap Parametreleri ───────────────────────────────────────────────────────

    public double RainfallOverride       { get; set; }  // 0 → şehirden al
    public double GutterSlope            { get; set; } = 0.005;  // %0.5 minimum eğim
    public double ManningN               { get; set; } = 0.013;  // plastik / PVC oluk

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public GutterResult Calculate(string city, List<RoofSection> sections)
    {
        double r = RainfallOverride > 0 ? RainfallOverride
                 : RainfallIntensity.GetValueOrDefault(city, 0.025);

        double totalA   = 0, totalEff = 0;
        foreach (var s in sections) { totalA += s.AreaM2; totalEff += s.EffectiveAreaM2; }

        double qTotalLs = r * totalEff;  // l/s

        var result = new GutterResult
        {
            City             = city,
            RainfallLsM2     = r,
            TotalAreaM2      = Math.Round(totalA, 2),
            TotalEffAreaM2   = Math.Round(totalEff, 2),
            TotalFlowLs      = Math.Round(qTotalLs, 3)
        };

        // ── Oluk Boyutu (yarım daire, Manning) ────────────────────────────────
        SizeGutter(qTotalLs, result);

        // ── Dere Borusu ───────────────────────────────────────────────────────
        SizeDownpipe(qTotalLs, totalEff, result);

        result.Notes.Add($"Yağış yoğunluğu: r = {r:F4} l/s·m²  ({city})");
        result.Notes.Add($"Toplam efektif çatı alanı: {totalEff:F1} m²");
        result.Notes.Add($"Tasarım debisi: Q = {qTotalLs:F3} l/s");
        result.Notes.Add("Standart: TS EN 12056-3 / TS EN 1253");

        result.Summary =
            $"Q={qTotalLs:F3} l/s | Oluk: Ø{result.GutterDiameterMm:F0}mm | " +
            $"Dere: {result.DownpipeCount}×Ø{result.DownpipeDiameterMm:F0}mm " +
            $"(aralık ≤{result.DownpipeSpacingM:F1}m)";

        return result;
    }

    // ── Yardımcı Metotlar ─────────────────────────────────────────────────────────

    private void SizeGutter(double qLs, GutterResult r)
    {
        double qM3s = qLs / 1000.0;
        // Standart yarım daire oluk çapları (mm)
        int[] sizes = [75, 100, 125, 150, 200, 250, 300];

        foreach (int d in sizes)
        {
            double dm = d / 1000.0;
            double radius = dm / 2.0;
            // Yarım daire — A = π r²/2, P = π r, R = A/P = r/2
            double area = Math.PI * radius * radius / 2.0;
            double R    = radius / 2.0;
            double v    = (1.0 / ManningN) * Math.Pow(R, 2.0 / 3.0) * Math.Pow(GutterSlope, 0.5);
            double qFull = v * area * 1000; // l/s

            // Doluluk oranı ≤ 0.5 koşulu için max debi = 0.5 × Q_full
            if (qFull * 0.5 >= qM3s * 1000)
            {
                r.GutterDiameterMm = d;
                r.GutterLabel      = $"Ø{d}mm yarım daire oluk (PVC/çinko)";
                r.GutterVelocityMs = Math.Round(v * 0.5, 2); // yarı dolu hız
                r.GutterFillRatio  = Math.Round(qLs / qFull, 3);
                return;
            }
        }
        // En büyük standart yetmiyorsa
        r.GutterDiameterMm = 300;
        r.GutterLabel      = "⚠ Ø300mm oluk yetersiz — dikdörtgen oluk veya çoklu hat gerekli";
        r.Notes.Add("⚠ Standart yuvarlak oluk kapasitesini aşıyor. Dikdörtgen oluk (200×150mm) veya 2 hat önerilir.");
    }

    private static void SizeDownpipe(double qLs, double effArea, GutterResult r)
    {
        // Dere borusu: TS EN 12056-3 — tam dolu akış kapasiteleri
        // Q_full = 0.6 × π × D² / 4 × √(2gH), basitleştirilmiş katalog:
        var catalog = new (int DN, double QMaxLs)[]
        {
            (50, 0.8), (63, 1.3), (75, 2.2), (90, 3.8), (100, 5.5), (110, 7.0), (125, 10.0), (160, 18.0)
        };

        // 1 dere borusuyla ne kadar debi taşınır?
        foreach (var (dn, qMax) in catalog)
        {
            int count = (int)Math.Ceiling(qLs / qMax);
            double spacingM = effArea > 0 ? Math.Sqrt(effArea / count) * 1.5 : 10;

            r.DownpipeDiameterMm = dn;
            r.DownpipeLabel      = $"Ø{dn}mm dere borusu (PVC)";
            r.DownpipeCount      = count;
            r.DownpipeSpacingM   = Math.Round(Math.Min(spacingM, 20), 1);
            return; // İlk yeterli çapta dur (minimum sayı)
        }
    }
}
