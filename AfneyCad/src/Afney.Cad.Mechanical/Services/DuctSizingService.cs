using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Havalandırma Kanal Boyutlandırma Servisi (DuctSizingService)
   NEDEN: TS EN 13779 / TS EN 12237 kapsamında mekanik havalandırma sistemlerinin
          kanal boyutlarını, hava debilerini ve fan seçimini yapmak için.

   HESAP YÖNTEMLERİ:
   - Eşit Sürtünme Yöntemi: Tüm hatlarda aynı Pa/m sürtünme basıncı
   - Hız Sınırı: Ana kanal ≤ 6 m/s, branş ≤ 4 m/s (TS EN 13779 §8.3)
   - Dikdörtgen veya dairesel kanal
*/
public class DuctSizingService
{
    // ── Kanal/Zon Tanımı ─────────────────────────────────────────────────────────

    public class Zone
    {
        public string  Name            { get; set; } = "";
        public string  ZoneType        { get; set; } = "Ofis";  // Ofis, Toplantı, WC, Koridor...
        public double  FloorAreaM2     { get; set; }
        public double  HeightM         { get; set; } = 3.0;
        public double  AirChanges      { get; set; }            // n/h (TS EN 13779 Tablo B.1)
        public bool    IsExhaust       { get; set; } = false;   // Egzoz mu, taze hava mı?
        public double  VolumeM3        => FloorAreaM2 * HeightM;
        public double  AirFlowM3h      => VolumeM3 * AirChanges;
        public double  AirFlowM3s      => AirFlowM3h / 3600.0;
    }

    // ── Kanal Segment Sonucu ──────────────────────────────────────────────────────

    public class DuctSegment
    {
        public string  ZoneName        { get; set; } = "";
        public double  AirFlowM3h      { get; set; }
        public double  AirFlowM3s      { get; set; }
        public double  VelocityMs      { get; set; }
        public double  DiameterMm      { get; set; }   // Dairesel kanal
        public double  WidthMm         { get; set; }   // Dikdörtgen — genişlik
        public double  HeightMm        { get; set; }   // Dikdörtgen — yükseklik
        public double  FrictionPaPer1m { get; set; }   // Pa/m sürtünme
        public string  Note            { get; set; } = "";
        public bool    HasWarning      { get; set; }
    }

    // ── Fan / Sistem Sonucu ───────────────────────────────────────────────────────

    public class HvacResult
    {
        public List<DuctSegment> Segments   { get; set; } = [];
        public double TotalFlowM3h          { get; set; }
        public double TotalPressurePa       { get; set; }   // Sistem toplam basınç kaybı
        public string RecommendedFan        { get; set; } = "";
        public double FanPowerW             { get; set; }
        public int    CategoryLevel         { get; set; }   // TS EN 13779: IDA 1-4
        public string AirQualityClass       { get; set; } = "";
        public List<string> Warnings        { get; set; } = [];
        public string Summary               { get; set; } = "";
    }

    // ── Parametreler ──────────────────────────────────────────────────────────────

    public double MaxVelocityMainMs   { get; set; } = 6.0;   // Ana kanal maks. hız
    public double MaxVelocityBranchMs { get; set; } = 4.0;   // Branş kanal maks. hız
    public double TargetFrictionPaPer1m { get; set; } = 1.0; // Hedef sürtünme

    // ── Hava Değişim Sayıları (TS EN 13779 Tablo B.1) ─────────────────────────────

    public static readonly Dictionary<string, double> DefaultAirChanges = new()
    {
        ["Ofis"]          = 4,   ["Açık Ofis"]     = 5,  ["Toplantı"]    = 8,
        ["Yemekhane"]     = 8,   ["Mutfak (end.)"] = 20, ["WC"]          = 10,
        ["Duş"]           = 15,  ["Koridor"]        = 2,  ["Depo"]        = 1,
        ["Derslik"]       = 6,   ["Hastane Oda"]    = 6,  ["Ameliyat"]    = 20,
        ["Laboratuvar"]   = 8,   ["Otopark"]        = 6,  ["Otel Oda"]    = 4,
        ["Alışveriş"]     = 6,   ["Sinema"]         = 8,  ["Spor Salonu"] = 6,
    };

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public HvacResult Calculate(List<Zone> zones, bool rectangularDuct = false, double lengthEstimateM = 20.0)
    {
        var result = new HvacResult();

        foreach (var zone in zones)
        {
            double qM3s = zone.AirFlowM3s;
            bool isMain  = zone == zones[0];
            double maxV  = isMain ? MaxVelocityMainMs : MaxVelocityBranchMs;

            // Kanal boyutu
            double d, w, h, v, pa;
            if (rectangularDuct)
            {
                (d, w, h, v, pa) = SizeRectangular(qM3s, maxV);
            }
            else
            {
                (d, v, pa) = SizeCircular(qM3s, maxV);
                w = d; h = d;
            }

            string note = "";
            bool warn = false;
            if (v > maxV) { note += $"⚠ Hız {v:F1} m/s > {maxV} m/s. "; warn = true; }
            if (pa > 2.0) { note += $"⚠ Sürtünme {pa:F2} Pa/m yüksek. "; warn = true; }

            result.Segments.Add(new DuctSegment
            {
                ZoneName         = zone.Name,
                AirFlowM3h       = Math.Round(zone.AirFlowM3h, 1),
                AirFlowM3s       = Math.Round(qM3s, 4),
                VelocityMs       = Math.Round(v, 2),
                DiameterMm       = Math.Round(d, 0),
                WidthMm          = Math.Round(w, 0),
                HeightMm         = Math.Round(h, 0),
                FrictionPaPer1m  = Math.Round(pa, 3),
                Note             = note.Trim(),
                HasWarning       = warn
            });
        }

        double totalQ = zones.Sum(z => z.AirFlowM3h);
        result.TotalFlowM3h    = Math.Round(totalQ, 1);
        result.TotalPressurePa = Math.Round(TargetFrictionPaPer1m * lengthEstimateM + 50, 0); // +50 Pa fitting
        result.RecommendedFan  = SelectFan(totalQ, result.TotalPressurePa);
        result.FanPowerW       = Math.Round((totalQ / 3600) * result.TotalPressurePa / 0.65, 1); // η=0.65
        result.CategoryLevel   = DetermineCategory(zones);
        result.AirQualityClass = result.CategoryLevel switch { 1 => "IDA 1 (Çok İyi)", 2 => "IDA 2 (İyi)", 3 => "IDA 3 (Orta)", _ => "IDA 4 (Düşük)" };
        result.Warnings.AddRange(result.Segments.Where(s => s.HasWarning).Select(s => $"{s.ZoneName}: {s.Note}"));
        result.Summary = $"Toplam debi: {totalQ:F1} m³/h | Sistem ΔP: {result.TotalPressurePa:F0} Pa | Fan: {result.FanPowerW:F0} W | {result.AirQualityClass}";

        return result;
    }

    // ── Kanal Hesap Yardımcıları ──────────────────────────────────────────────────

    private static (double d, double v, double pa) SizeCircular(double qM3s, double maxV)
    {
        // Q = v × A → d = √(4Q/πv)
        double d = Math.Sqrt(4 * qM3s / (Math.PI * maxV)) * 1000; // mm
        d = RoundUp(d, 25); // 25mm aralıkla standart çap
        double area = Math.PI * (d / 1000) * (d / 1000) / 4;
        double v = qM3s / area;
        double pa = 0.025 * Math.Pow(v, 1.9) / (d / 1000); // Darcy-Weisbach basitleştirilmiş
        return (d, v, pa);
    }

    private static (double d, double w, double h, double v, double pa) SizeRectangular(double qM3s, double maxV)
    {
        double a = qM3s / maxV; // m²
        double w = Math.Sqrt(a * 2.0) * 1000; // 2:1 oranı — genişlik
        double h = a / (w / 1000) * 1000;      // yükseklik
        w = RoundUp(w, 50); h = RoundUp(h, 50);
        double area = (w / 1000) * (h / 1000);
        double v = qM3s / area;
        double dEq = 1.3 * Math.Pow(w * h, 0.625) / Math.Pow(w + h, 0.25) / 1000 * 1000;
        double pa = 0.025 * Math.Pow(v, 1.9) / (dEq / 1000);
        return (dEq, w, h, v, pa);
    }

    private static double RoundUp(double value, double step) => Math.Ceiling(value / step) * step;

    private static string SelectFan(double m3h, double pa)
    {
        if (m3h < 500  && pa < 200) return "Çatı fanı veya kanal tipi aksiyal fan 500 m³/h";
        if (m3h < 2000 && pa < 300) return "Radyal kanal fanı 2000 m³/h";
        if (m3h < 5000 && pa < 500) return "Çift emişli radyal fan 5000 m³/h";
        if (m3h < 15000) return "Santrifüj fan grubu — Fan seçim yazılımı önerilir";
        return $"Özel fan seçimi: Q={m3h:F0} m³/h, P={pa:F0} Pa";
    }

    private static int DetermineCategory(List<Zone> zones)
    {
        // TS EN 13779: düşük hava değişimli zonlar → düşük kategori
        double avg = zones.Count > 0 ? zones.Average(z => z.AirChanges) : 0;
        if (avg >= 8) return 1;
        if (avg >= 5) return 2;
        if (avg >= 3) return 3;
        return 4;
    }
}
