using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Isıtma Tesisat Hesap Servisi (HeatingSystemService)
   NEDEN: TS 825 / TS EN 12831 kapsamında bina ısı ihtiyacı, radyatör seçimi,
          kazan kapasitesi ve ısıtma devresi boru boyutlandırmasını yapmak için.

   HESAP ZİNCİRİ:
   1. Her oda için ısı kaybı = Σ(U × A × ΔT) + havalandırma kaybı
   2. Toplam ısı ihtiyacı → Kazan kapasitesi (kW)
   3. Her oda için radyatör seçimi (katalog tabanlı)
   4. Isıtma devresi boru boyutlandırma (60/40°C veya 80/60°C devre)
*/
public class HeatingSystemService
{
    // ── Oda Tanımı ────────────────────────────────────────────────────────────────

    public class Room
    {
        public string Name            { get; set; } = "";
        public string RoomType        { get; set; } = "Oturma Odası";
        public double FloorAreaM2     { get; set; }
        public double HeightM         { get; set; } = 2.8;
        public double DesignTempC     { get; set; } = 22.0;   // İç sıcaklık
        public double ExternalWallM2  { get; set; }            // Dış duvar alanı
        public double WindowM2        { get; set; }            // Pencere alanı
        public double ExternalRoofM2  { get; set; }            // Çatı/döşeme alanı
        public double PartitionWallM2 { get; set; }            // Bölme duvar alanı

        // U-değerleri (W/m²K)
        public double UWall           { get; set; } = 0.6;
        public double UWindow         { get; set; } = 2.4;
        public double URoof           { get; set; } = 0.4;
        public double UFloor          { get; set; } = 0.8;
        public double UPartition      { get; set; } = 1.5;

        public double AirChangesPerHour { get; set; } = 0.5;  // Havalandırma
    }

    // ── Oda Hesap Sonucu ─────────────────────────────────────────────────────────

    public class RoomHeatResult
    {
        public Room   Room                { get; set; } = null!;
        public double TransmissionLossW   { get; set; }   // İletim kaybı (W)
        public double VentilationLossW    { get; set; }   // Havalandırma kaybı (W)
        public double TotalHeatLossW      { get; set; }   // Toplam ısı ihtiyacı (W)
        public double TotalHeatLossKw     => TotalHeatLossW / 1000.0;
        public RadiatorOption? Radiator   { get; set; }
        public double RequiredFlowM3h     { get; set; }
        public double RecommendedDN       { get; set; }
    }

    // ── Radyatör Seçenek ─────────────────────────────────────────────────────────

    public class RadiatorOption
    {
        public string Type     { get; set; } = "";   // Panel 11, Panel 22, Petek vb.
        public int    Width    { get; set; }          // mm
        public int    Height   { get; set; }          // mm
        public double OutputW  { get; set; }          // W @ 60/40°C Delta50
        public string Model    { get; set; } = "";
    }

    // ── Sistem Sonucu ────────────────────────────────────────────────────────────

    public class HeatingResult
    {
        public List<RoomHeatResult> Rooms     { get; set; } = [];
        public double TotalHeatKw             { get; set; }
        public double BoilerCapacityKw        { get; set; }   // +%20 güvenlik
        public string RecommendedBoiler       { get; set; } = "";
        public double SystemFlowM3h           { get; set; }
        public double PumpHeadM               { get; set; }
        public string RecommendedPump         { get; set; } = "";
        public int    WarningCount            { get; set; }
        public List<string> Warnings          { get; set; } = [];
        public string Summary                 { get; set; } = "";
    }

    // ── Parametreler ──────────────────────────────────────────────────────────────

    public double OutdoorDesignTempC  { get; set; } = -12.0;  // Türkiye ortalama (TS 825)
    public double SupplyTempC         { get; set; } = 80.0;   // Kazan çıkış
    public double ReturnTempC         { get; set; } = 60.0;   // Kazan dönüş
    public double SafetyFactor        { get; set; } = 1.20;   // %20 güvenlik

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public HeatingResult Calculate(List<Room> rooms)
    {
        var result = new HeatingResult();
        double totalW = 0;

        foreach (var room in rooms)
        {
            double ΔT = room.DesignTempC - OutdoorDesignTempC;

            // İletim kaybı
            double qTrans =
                room.ExternalWallM2 * room.UWall     * ΔT +
                room.WindowM2       * room.UWindow    * ΔT +
                room.ExternalRoofM2 * room.URoof      * ΔT +
                room.FloorAreaM2    * room.UFloor     * ΔT +
                room.PartitionWallM2 * room.UPartition * Math.Max(ΔT - 5, 0); // komşu oda etkisi

            // Havalandırma kaybı: Q = 0.34 × n × V × ΔT
            double volumeM3 = room.FloorAreaM2 * room.HeightM;
            double qVent = 0.34 * room.AirChangesPerHour * volumeM3 * ΔT;

            double qTotal = (qTrans + qVent) * SafetyFactor;

            // Radyatör seçimi
            var radiator = SelectRadiator(qTotal);

            // Debi: Q[m³/h] = P[W] / (cp × ρ × ΔT[K]) × 3600
            double ΔTSystem = SupplyTempC - ReturnTempC;
            double flowM3h = ΔTSystem > 0 ? qTotal / (4186 * 998 * ΔTSystem) * 3600 : 0;
            double dn = SelectPipeDN(flowM3h);

            var roomResult = new RoomHeatResult
            {
                Room              = room,
                TransmissionLossW = Math.Round(qTrans, 1),
                VentilationLossW  = Math.Round(qVent, 1),
                TotalHeatLossW    = Math.Round(qTotal, 1),
                Radiator          = radiator,
                RequiredFlowM3h   = Math.Round(flowM3h, 4),
                RecommendedDN     = dn
            };

            if (qTotal < 200)
                result.Warnings.Add($"{room.Name}: ısı ihtiyacı çok düşük ({qTotal:F0} W) — girişleri kontrol edin.");

            result.Rooms.Add(roomResult);
            totalW += qTotal;
        }

        double totalKw = totalW / 1000.0;
        result.TotalHeatKw       = Math.Round(totalKw, 2);
        result.BoilerCapacityKw  = Math.Round(totalKw * SafetyFactor, 1);
        result.RecommendedBoiler = SelectBoiler(result.BoilerCapacityKw);

        double sysFlow = totalKw * 1000 / (4186 * 998 * (SupplyTempC - ReturnTempC)) * 3600;
        result.SystemFlowM3h  = Math.Round(sysFlow, 3);
        result.PumpHeadM      = Math.Round(sysFlow * 2.5, 1); // kaba kural: Q×2.5 mSS
        result.RecommendedPump = SelectPump(sysFlow, result.PumpHeadM);
        result.WarningCount   = result.Warnings.Count;

        result.Summary =
            $"Toplam ısı ihtiyacı: {totalKw:F2} kW | " +
            $"Kazan kapasitesi: {result.BoilerCapacityKw:F1} kW | " +
            $"Sistem debisi: {sysFlow:F3} m³/h | " +
            $"Oda sayısı: {rooms.Count}";

        return result;
    }

    // ── Radyatör Seçimi (TS EN 442 — Delta50 değerleri) ──────────────────────────

    private static readonly (int W, int H, double OutputW, string Type)[] RadiatorCatalog =
    [
        (400,  300, 155,  "Panel 11"), (600,  300, 233,  "Panel 11"),
        (800,  300, 310,  "Panel 11"), (1000, 300, 388,  "Panel 11"),
        (400,  500, 258,  "Panel 11"), (600,  500, 388,  "Panel 11"),
        (800,  500, 517,  "Panel 11"), (1000, 500, 646,  "Panel 11"),
        (400,  600, 310,  "Panel 22"), (600,  600, 466,  "Panel 22"),
        (800,  600, 621,  "Panel 22"), (1000, 600, 776,  "Panel 22"),
        (400,  900, 465,  "Panel 22"), (600,  900, 698,  "Panel 22"),
        (800,  900, 931,  "Panel 22"), (1000, 900, 1164, "Panel 22"),
        (400,  600, 420,  "Panel 33"), (600,  600, 630,  "Panel 33"),
        (800,  600, 840,  "Panel 33"), (1000, 600, 1050, "Panel 33"),
        (400,  900, 630,  "Panel 33"), (600,  900, 945,  "Panel 33"),
        (800,  900, 1260, "Panel 33"), (1000, 900, 1575, "Panel 33"),
    ];

    private static RadiatorOption SelectRadiator(double requiredW)
    {
        // 60/40°C devresinde Delta50 düzeltmesi: gerçek çıktı × 0.69
        double requiredAtDelta50 = requiredW / 0.69;
        var best = RadiatorCatalog
            .Where(r => r.OutputW >= requiredAtDelta50)
            .OrderBy(r => r.OutputW)
            .FirstOrDefault();

        if (best == default) best = RadiatorCatalog.OrderByDescending(r => r.OutputW).First();

        return new RadiatorOption
        {
            Type    = best.Type,
            Width   = best.W,
            Height  = best.H,
            OutputW = Math.Round(best.OutputW * 0.69, 0),
            Model   = $"{best.Type} {best.W}×{best.H} ({best.OutputW * 0.69:F0} W @ 60/40°C)"
        };
    }

    private static double SelectPipeDN(double flowM3h)
    {
        if (flowM3h < 0.03) return 10;
        if (flowM3h < 0.06) return 12;
        if (flowM3h < 0.12) return 15;
        if (flowM3h < 0.25) return 18;
        if (flowM3h < 0.50) return 22;
        if (flowM3h < 0.80) return 28;
        return 35;
    }

    private static string SelectBoiler(double kw)
    {
        if (kw <= 24) return "24 kW Kombine Kazan (Duvar tipi)";
        if (kw <= 32) return "32 kW Kombine Kazan (Duvar tipi)";
        if (kw <= 48) return "48 kW Yoğuşmalı Kazan";
        if (kw <= 80) return "80 kW Döküm Kazan";
        if (kw <= 120) return "120 kW Çelik Kazan";
        return $"{(int)Math.Ceiling(kw / 10) * 10} kW Endüstriyel Kazan";
    }

    private static string SelectPump(double flowM3h, double headM)
    {
        if (flowM3h < 1  && headM < 4) return "Grundfos UP 15-14 veya eşdeğeri";
        if (flowM3h < 2  && headM < 6) return "Grundfos UP 20-15 / Wilo Star-RS 25/4";
        if (flowM3h < 4  && headM < 8) return "Grundfos UPS 25-60 / Wilo Star-RS 25/6";
        if (flowM3h < 8  && headM < 10) return "Grundfos UPS 32-80 / Wilo Top-S 40/4";
        return $"Özel seçim gerekli (Q={flowM3h:F2} m³/h, H={headM:F1} m)";
    }

    // ── Şehir Dış Tasarım Sıcaklıkları (TS 825 Ek A) ───────────────────────────

    public static readonly Dictionary<string, double> CityDesignTemps = new()
    {
        ["İstanbul"]  = -3,  ["Ankara"]    = -12, ["İzmir"]    = -1,
        ["Bursa"]     = -7,  ["Antalya"]   = 0,   ["Adana"]    = 0,
        ["Konya"]     = -13, ["Kayseri"]   = -14, ["Trabzon"]  = -4,
        ["Samsun"]    = -6,  ["Erzurum"]   = -25, ["Diyarbakır"] = -10,
        ["Eskişehir"] = -14, ["Gaziantep"] = -6,  ["Mersin"]   = 1,
        ["Kocaeli"]   = -5,  ["Denizli"]   = -4,  ["Malatya"]  = -12,
    };

    // ── Oda Tipi Sıcaklıkları (TS EN 12831) ─────────────────────────────────────

    public static readonly Dictionary<string, double> RoomDesignTemps = new()
    {
        ["Oturma Odası"]   = 22, ["Yatak Odası"] = 20, ["Çocuk Odası"]  = 22,
        ["Mutfak"]         = 20, ["Banyo"]        = 24, ["WC"]           = 22,
        ["Hol / Koridor"]  = 18, ["Bodrum"]       = 12, ["Depo"]         = 10,
        ["Ofis"]           = 20, ["Toplantı"]     = 22, ["Derslik"]      = 20,
    };
}
