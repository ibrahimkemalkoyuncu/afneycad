using System;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Genleşme Tankı Hesap Servisi (ThermalExpansionService)
   NEDEN: TS EN 12828 / TS EN 13831 kapsamında kapalı ısıtma sistemlerindeki genleşme hacmini ve
          precharge (ön şarj) basıncını hesaplamak için.

   FORMÜLLER (TS EN 13831):
   Ve = V_sys × Δv × n
   n  = (P_max + 1) / (P_max - P_pre)
   P_pre ≥ statik yükseklik (bar) + 0.2 bar

   Δv = özgül hacim farkı (80°C ve 10°C arasındaki su için ≈ 0.0286)
*/
public class ThermalExpansionService
{
    // ── Varsayılanlar ─────────────────────────────────────────────────────────────

    public double SystemVolumeL     { get; set; } = 100.0;  // Sistem su hacmi (L)
    public double TempCold          { get; set; } = 10.0;   // Dolum sıcaklığı (°C)
    public double TempHot           { get; set; } = 80.0;   // Çalışma sıcaklığı (°C)
    public double StaticHeadM       { get; set; } = 5.0;    // Statik yükseklik (m su sütunu)
    public double MaxPressureBar    { get; set; } = 3.0;    // Emniyet valfi açılış basıncı (bar)

    // ── Sonuç ────────────────────────────────────────────────────────────────────

    public class ExpansionResult
    {
        public double DeltaV            { get; set; }   // Özgül hacim farkı (-)
        public double ExpansionVolumeL  { get; set; }   // Genleşme hacmi (L)
        public double PrechargeBar      { get; set; }   // Ön şarj basıncı (bar)
        public double TankVolumeL       { get; set; }   // Minimum tank hacmi (L)
        public string RecommendedTank   { get; set; } = "";
        public double AcceptanceFactor  { get; set; }   // n katsayısı
    }

    // ── Hesap ────────────────────────────────────────────────────────────────────

    public ExpansionResult Calculate()
    {
        double Δv     = SpecificVolumeDiff(TempCold, TempHot);
        double Vpre   = StaticHeadM / 10.0 + 0.2;                  // P_pre (bar)
        double n      = (MaxPressureBar + 1.0) / (MaxPressureBar - Vpre);
        double Ve     = SystemVolumeL * Δv;
        double Vtank  = Ve * n;

        Vtank = Math.Max(Vtank, 8.0); // minimum 8 L (piyasa standart)

        return new ExpansionResult
        {
            DeltaV           = Math.Round(Δv,     4),
            ExpansionVolumeL = Math.Round(Ve,     2),
            PrechargeBar     = Math.Round(Vpre,   2),
            AcceptanceFactor = Math.Round(n,      3),
            TankVolumeL      = Math.Round(Vtank,  1),
            RecommendedTank  = SelectTank(Vtank)
        };
    }

    // ── Hesap Yardımcıları ────────────────────────────────────────────────────────

    // Su özgül hacmi farkı (T2−T1). IAPWS-IF97 yaklaşımı.
    private static double SpecificVolumeDiff(double tCold, double tHot)
    {
        double vCold = WaterDensity(tCold);
        double vHot  = WaterDensity(tHot);
        return (1.0 / vHot - 1.0 / vCold) / (1.0 / vCold);
    }

    // Su yoğunluğu kg/m³ — polinom yaklaşımı (0–100°C)
    private static double WaterDensity(double t)
    {
        return 999.842 + 0.06986 * t - 0.003821 * t * t
                       + 4.171e-6  * t * t * t
                       - 4.01e-9   * t * t * t * t;
    }

    private static string SelectTank(double liters)
    {
        int[] standards = [8, 12, 18, 24, 35, 50, 80, 100, 150, 200];
        foreach (int s in standards)
            if (s >= liters) return $"{s} L membran genleşme tankı (TS EN 13831)";
        return $"{(int)Math.Ceiling(liters / 10.0) * 10} L özel membran tankı";
    }
}
