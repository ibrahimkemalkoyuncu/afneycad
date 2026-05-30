using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Su Deposu ve Hidrofor Hesap Servisi (WaterTankService)
   NEDEN: TS 1258 / TS EN 806-3 kapsamında günlük su ihtiyacı, depo hacmi ve hidrofor seçimini yapmak için.

   HESAP ZİNCİRİ (TS 1258 Bölüm 6):
   1. Kişi başı günlük su ihtiyacı × kişi sayısı → Qgünlük (L/gün)
   2. Depo hacmi = Qgünlük × depolama günü katsayısı (TS: min 1 gün, çoğu proje 1.5–2 gün)
   3. Günlük pik debi (Qpik) = toplam LU → Walther formülü → l/s
   4. Hidrofor kapasitesi = Qpik × 3600 s/h (pompa saatte çalışırsa)
   5. Basınç yüksekliği = statik yük (bina yüksekliği) + sürtünme kaybı (% 20 katsayı)
*/
public class WaterTankService
{
    private readonly CadDatabase _database;

    // TS 1258 kişi başı günlük su ihtiyacı (L/kişi/gün)
    public double LitersPerPersonPerDay { get; set; } = 150.0;
    // Depolama günü katsayısı
    public double StorageDays           { get; set; } = 1.5;
    // Sistemin statik basınç yüksekliği (m)
    public double StaticHeadM           { get; set; } = 20.0;
    // Pompa güvenlik katsayısı
    public double PumpSafetyFactor      { get; set; } = 1.2;

    public WaterTankService(CadDatabase database) { _database = database; }

    // ── Sonuç Sınıfı ─────────────────────────────────────────────────────────────

    public class TankResult
    {
        public double DailyDemandL     { get; set; }   // Toplam günlük ihtiyaç (L/gün)
        public double TankVolumeL      { get; set; }   // Hesaplanan depo hacmi (L)
        public double TankVolumeM3     { get; set; }   // m³ olarak
        public string RecommendedTank  { get; set; } = "";  // Standart depo önerisi
        public double PeakFlowLs       { get; set; }   // Pik debi (l/s)
        public double PumpFlowM3h      { get; set; }   // Pompa debisi (m³/h)
        public double PumpHeadM        { get; set; }   // Pompa manometrik yükseklik (m)
        public double PumpPowerKw      { get; set; }   // Tahmini pompa gücü (kW)
        public string RecommendedPump  { get; set; } = "";  // Pompa önerisi
        public int    PersonCount      { get; set; }
        public double TotalLoadUnits   { get; set; }
        public List<string> Warnings   { get; set; } = [];
    }

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public TankResult Calculate(int personCount)
    {
        var result = new TankResult { PersonCount = personCount };

        // 1. Günlük ihtiyaç
        double dailyL = personCount * LitersPerPersonPerDay;
        result.DailyDemandL = dailyL;

        // 2. Depo hacmi
        double tankL  = dailyL * StorageDays;
        result.TankVolumeL  = tankL;
        result.TankVolumeM3 = tankL / 1000.0;
        result.RecommendedTank = SelectStandardTank(tankL);

        // 3. Pik debi — DB'deki LU toplamı üzerinden Walther formülü
        double totalLU = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .Sum(f => f.LoadUnits);
        result.TotalLoadUnits = totalLU;

        double peakLs = totalLU > 0 ? WaltherFlow(totalLU) : dailyL / (10 * 3600.0);
        result.PeakFlowLs = Math.Round(peakLs, 3);

        // 4. Hidrofor / pompa
        double pumpM3h = peakLs * 3.6;
        double headM   = (StaticHeadM + StaticHeadM * 0.20) * PumpSafetyFactor; // +%20 sürtünme
        double powerKw = (pumpM3h * headM * 1000 * 9.81) / (3_600_000.0 * 0.70); // η=0.70

        result.PumpFlowM3h    = Math.Round(pumpM3h, 2);
        result.PumpHeadM      = Math.Round(headM,    1);
        result.PumpPowerKw    = Math.Round(powerKw,  2);
        result.RecommendedPump = SelectPump(pumpM3h, headM);

        // 5. Uyarılar
        if (personCount < 5)
            result.Warnings.Add("Çok az kişi — depo yerine bağlantı suyu yeterli olabilir.");
        if (tankL > 50_000)
            result.Warnings.Add("Depo > 50 m³ — yangın depo ihtiyacı ayrıca kontrol edilmeli.");
        if (totalLU == 0)
            result.Warnings.Add("DB'de vitrifiye bulunamadı — LU bazlı pik debi kestirme ile hesaplandı.");

        return result;
    }

    // ── Hesap Yardımcıları ────────────────────────────────────────────────────────

    // Walther formülü (TS EN 806-3): q = 0.682 × (Σk)^0.45 − 0.14   [l/s]
    private static double WaltherFlow(double sumLU)
    {
        double q = 0.682 * Math.Pow(sumLU, 0.45) - 0.14;
        return Math.Max(q, 0.1);
    }

    private static string SelectStandardTank(double liters)
    {
        int[] standards = [500, 1000, 2000, 3000, 5000, 10000, 20000, 30000, 50000];
        int vol = (int)Math.Ceiling(liters / 100.0) * 100;
        foreach (int s in standards)
            if (s >= vol) return $"{s} L polietilen/GRP depo";
        return $"{(int)Math.Ceiling(liters / 1000.0)} m³ özel yapım beton/GRP depo";
    }

    private static string SelectPump(double m3h, double headM)
    {
        if (m3h < 2  && headM < 30) return "Grundfos CM3 veya eşdeğeri (küçük hidrofor)";
        if (m3h < 6  && headM < 40) return "Grundfos CM5 / Wilo PB-401EA veya eşdeğeri";
        if (m3h < 12 && headM < 50) return "Grundfos CM10 / Wilo MHI veya eşdeğeri";
        if (m3h < 25 && headM < 60) return "Grundfos CM15 / Wilo MVIS veya eşdeğeri";
        return $"Özel seçim gerekli (Q={m3h:F1} m³/h · H={headM:F0} m)";
    }
}
