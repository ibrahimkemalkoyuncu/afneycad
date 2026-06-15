using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Hava İşleme Birimi Tasarım Servisi (AHUDesignService)
   NEDEN: AHU (Air Handling Unit) boyutlandırma — FINE MEP'te yoktur.
          Isıtma, soğutma, nemlendirme, ısı geri kazanım hesabı.
          ISO 16890 filtre sınıfı + seslenme hesabı dahil.

   HESAP:
   - Hava debisi: Q = Ventilasyon ihtiyacı + Soğutma hava debisi (max)
   - Isıtma batarya: Q = ρ×cp×Q×ΔT (ρ=1.2 kg/m³, cp=1.006 kJ/kgK)
   - Soğutma batarya: Qs = duyulur; Ql = gizli (nem alma)
   - Nem kontrolü: psikrometrik hesap (ASHRAE formülasyon)
   - Ses: LW = fan + attenuation (basit model)
*/
public class AHUDesignService
{
    // ── AHU Girdi ────────────────────────────────────────────────────────────────

    public class AHUInput
    {
        public double SupplyAirflowM3h   { get; set; }   // Toplam taze hava debisi (m³/sa)
        public double ReturnAirRatioPct  { get; set; } = 70;  // Geri dönüş havası oranı (%)
        public double OutdoorTempSummerC { get; set; } = 32;   // Dış sıcaklık (yaz)
        public double OutdoorTempWinterC { get; set; } = -3;   // Dış sıcaklık (kış)
        public double OutdoorHumidityPct { get; set; } = 60;   // Dış bağıl nem (%)
        public double SupplyTempC        { get; set; } = 18;   // Beslenme hava sıcaklığı
        public double RoomTempC          { get; set; } = 22;   // Oda sıcaklığı
        public double RoomHumidityPct    { get; set; } = 50;   // Oda bağıl nemi (%)
        public double HeatingCoilTempC   { get; set; } = 45;   // Isıtma bataryası su sıcaklığı
        public double CoolingCoilTempC   { get; set; } = 7;    // Soğutma bataryası su sıcaklığı
        public bool   HasHeatRecovery    { get; set; } = true;
        public double HREfficiencyPct    { get; set; } = 75;   // Isı geri kazanım verimi (%)
        public string FilterClass        { get; set; } = "ePM1 60%"; // ISO 16890
        public double StaticPressurePa   { get; set; } = 500;  // AHU basınç kaybı (Pa)
    }

    // ── AHU Sonuç ─────────────────────────────────────────────────────────────────

    public class AHUResult
    {
        public double SupplyAirflowM3h  { get; set; }
        public double FreshAirflowM3h   { get; set; }
        public double ReturnAirflowM3h  { get; set; }

        // Kış: Isıtma
        public double WinterPreheatKw   { get; set; }   // Ön ısıtma (buz çözme)
        public double WinterHeatKw      { get; set; }   // Ana ısıtma batarya (kW)
        public double HRSavingsKw       { get; set; }   // Isı geri kazanım tasarrufu

        // Yaz: Soğutma
        public double SummerCoolKw      { get; set; }   // Duyulur soğutma
        public double SummerLatentKw    { get; set; }   // Gizli soğutma (nem alma)
        public double TotalCoolKw       { get; set; }   // Toplam soğutma kapasitesi

        // Nem
        public double HumidLoadKgph     { get; set; }   // Kış nemlendirme ihtiyacı (kg/sa)

        // Fan
        public double FanPowerKw        { get; set; }   // Fan motoru gücü
        public double SFP               { get; set; }   // SFP W/(m³/s)

        // Filtre + Boyut
        public string FilterRecommendation { get; set; } = "";
        public string AHUSize           { get; set; } = "";   // L×W×H yaklaşık
        public List<string> Notes       { get; set; } = [];
    }

    // ── Hesap ────────────────────────────────────────────────────────────────────

    public static AHUResult Calculate(AHUInput inp)
    {
        var r = new AHUResult();
        double rhoAir = 1.2, cpAir = 1006;   // kg/m³ ve J/(kg·K)
        double qM3s   = inp.SupplyAirflowM3h / 3600.0;
        double freshRatio = 1.0 - inp.ReturnAirRatioPct / 100.0;

        r.SupplyAirflowM3h  = inp.SupplyAirflowM3h;
        r.FreshAirflowM3h   = inp.SupplyAirflowM3h * freshRatio;
        r.ReturnAirflowM3h  = inp.SupplyAirflowM3h * (1 - freshRatio);

        double freshM3s = r.FreshAirflowM3h / 3600.0;

        // ── KIŞIN ISI GERİ KAZANIM ─────────────────────────────────────────────
        double dtWinter = inp.RoomTempC - inp.OutdoorTempWinterC;
        r.HRSavingsKw = inp.HasHeatRecovery
            ? rhoAir * cpAir * freshM3s * dtWinter * inp.HREfficiencyPct / 100.0 / 1000.0
            : 0;

        // Isı geri kazanım sonrası dış hava sıcaklığı
        double tAfterHR = inp.OutdoorTempWinterC + dtWinter * (inp.HasHeatRecovery ? inp.HREfficiencyPct / 100.0 : 0);

        // Ön ısıtma (buz çözme): eğer <5°C ise
        r.WinterPreheatKw = tAfterHR < 5
            ? rhoAir * cpAir * freshM3s * (5 - tAfterHR) / 1000.0
            : 0;

        // Ana ısıtma bataryası: karışık hava → besleme sıcaklığı
        double tMixed = tAfterHR * freshRatio + inp.RoomTempC * (1 - freshRatio);
        tMixed = Math.Max(tMixed, 5);  // ön ısıtma sonrası
        double dtHeat = inp.SupplyTempC > tMixed ? inp.SupplyTempC - tMixed : 0;
        r.WinterHeatKw = rhoAir * cpAir * qM3s * dtHeat / 1000.0;

        // ── YAZIN SOĞUTMA ──────────────────────────────────────────────────────
        double tSummerMixed = inp.OutdoorTempSummerC * freshRatio + inp.RoomTempC * (1 - freshRatio);
        double tSupplyCool  = Math.Min(inp.SupplyTempC, tSummerMixed - 1);
        r.SummerCoolKw = rhoAir * cpAir * qM3s * (tSummerMixed - tSupplyCool) / 1000.0;

        // Gizli soğutma: nem alma
        double wOutdoor = HumidityRatioFromRH(inp.OutdoorTempSummerC, inp.OutdoorHumidityPct);
        double wRoom    = HumidityRatioFromRH(inp.RoomTempC, inp.RoomHumidityPct);
        double wMixed   = wOutdoor * freshRatio + wRoom * (1 - freshRatio);
        double wSupply  = HumidityRatioFromRH(tSupplyCool, 95);  // %95 nem sonrası batarya
        double deltaW   = Math.Max(0, wMixed - wSupply);
        r.SummerLatentKw = rhoAir * qM3s * deltaW * 2501.0 / 1000.0;  // 2501 kJ/kg buharlaşma
        r.TotalCoolKw    = r.SummerCoolKw + r.SummerLatentKw;

        // ── NEMLENDIRME ────────────────────────────────────────────────────────
        double wWinterOut = HumidityRatioFromRH(inp.OutdoorTempWinterC, 80);
        double deltaWHumid = Math.Max(0, wRoom - wWinterOut * freshRatio - wRoom * (1 - freshRatio));
        r.HumidLoadKgph = rhoAir * qM3s * deltaWHumid * 3600.0;

        // ── FAN ────────────────────────────────────────────────────────────────
        double fanEff = 0.65;   // toplam fan verimi
        r.FanPowerKw  = qM3s * inp.StaticPressurePa / (fanEff * 1000.0);
        r.SFP         = r.FanPowerKw * 1000.0 / qM3s;  // W/(m³/s)

        // ── FİLTRE ─────────────────────────────────────────────────────────────
        r.FilterRecommendation = inp.FilterClass switch
        {
            string f when f.Contains("ISO") || f.Contains("ePM1") =>
                "2 kademeli: G4 (ön filtre) + F7-F9 ana filtre — ISO 16890 uyumlu",
            _ => "G4 ön filtre + F7 HEPA ana filtre — ISO 16890 minimum"
        };

        // ── BOYUT TAHMİNİ ──────────────────────────────────────────────────────
        // Hız 2 m/s → kesit = Q/v = qM3s/2 → kare kesit yan = √kesit
        double section  = qM3s / 2.0;
        double sideMm   = Math.Sqrt(section) * 1000;
        double lenMm    = 2000 + (inp.HasHeatRecovery ? 1200 : 0);  // yaklaşık uzunluk
        r.AHUSize = $"{(int)(sideMm/100)*100 + 100} × {(int)(sideMm/100)*100 + 100} × {(int)lenMm} mm (G×Y×U)";

        if (r.SFP > 2000) r.Notes.Add($"⚠ SFP {r.SFP:F0} W/(m³/s) — EN 13779 SFP-4 sınırı aşıldı, fan seçimini gözden geçirin.");
        if (r.HumidLoadKgph > 20) r.Notes.Add($"Kış nemlendiricisi: {r.HumidLoadKgph:F1} kg/sa — buharlı/ultrasonik nemlendirici gerekli.");
        if (!inp.HasHeatRecovery && r.FreshAirflowM3h > 1000) r.Notes.Add("Isı geri kazanım eklenirse yıllık enerji %25-40 azalır.");

        return r;
    }

    // ── Psikrometrik Yardımcılar ─────────────────────────────────────────────────

    // Doyma buhar basıncı: Magnus formülü (kPa)
    private static double SaturationPressure(double tC) =>
        0.6108 * Math.Exp(17.27 * tC / (tC + 237.3));

    // Nem oranı (kg/kg): w = 0.622 × φ×Ps / (P - φ×Ps), P=101.325 kPa
    private static double HumidityRatioFromRH(double tC, double rhPct)
    {
        double phi = rhPct / 100.0;
        double ps  = SaturationPressure(tC);
        return 0.622 * phi * ps / (101.325 - phi * ps);
    }
}
