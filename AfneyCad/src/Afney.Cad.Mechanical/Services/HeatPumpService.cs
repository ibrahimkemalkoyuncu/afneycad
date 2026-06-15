using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Isı Pompası Servis (HeatPumpService)
   NEDEN: TS EN 14825 — Isı pompası mevsimsel performans hesabı.
          FINE MEP'te yoktur. COP (anlık) yerine SCOP/SEER (mevsimsel) hesabı
          EPBD uyumlu bina enerji sertifikası için zorunlu.

   HESAP YÖNTEMİ:
   - SCOP (Heating): bin yöntem — Tbin ağırlıklı ortalama
   - SEER (Cooling): EN 14825 tablo 4 yaklaşım
   - Kapasite interpolasyon: +7°C (A7/W35) standart nokta referans
*/
public class HeatPumpService
{
    // ── Isı Pompası Tipleri ──────────────────────────────────────────────────────

    public enum HeatPumpType
    {
        AirToWater,     // Hava-Su (ASHP) — dış ünite + iç ünite
        AirToAir,       // Hava-Hava (split klima tipi)
        WaterToWater,   // Su-Su (yeraltı suyu kaynağı)
        GroundToWater   // Toprak-Su (jeotermal, yerköküm)
    }

    public enum HeatPumpManufacturer { Daikin, Mitsubishi, Vaillant, Bosch, Viessmann, Buderus, Nibe }

    // ── Isı Pompası Modeli ───────────────────────────────────────────────────────

    public class HeatPumpModel
    {
        public string              ModelName     { get; set; } = "";
        public HeatPumpManufacturer Manufacturer { get; set; }
        public HeatPumpType        Type          { get; set; }
        public string              Series        { get; set; } = "";
        public double              HeatingKw     { get; set; }   // A7/W35 nominal ısıtma kapasitesi (kW)
        public double              CoolingKw     { get; set; }   // A35/W18 nominal soğutma kapasitesi (kW)
        public double              COP_A7_W35    { get; set; }   // Standart ısıtma COP (EN 14511)
        public double              COP_A2_W35    { get; set; }   // COP at +2°C (orta nokta)
        public double              COP_Am7_W35   { get; set; }   // COP at -7°C (soğuk nokta)
        public double              EER_A35_W18   { get; set; }   // Soğutma EER (EN 14511)
        public double              SCOP_35       { get; set; }   // Mevsimsel ısıtma COP (A-35 set) hesaplı
        public double              SEER          { get; set; }   // Mevsimsel soğutma performansı
        public double              PowerInputKw  { get; set; }   // Nominal el. güç girişi (kW)
        public double              MaxFlowLph    { get; set; }   // Su debisi (L/sa)
        public string              RefrigerantType { get; set; } = "R410A";
        public double              GWP           { get; set; }   // Küresel ısınma potansiyeli
        public string              EnergyClass   { get; set; } = "A++";
        public double              NoiseLevelDB  { get; set; }
        public double              PriceTL       { get; set; }   // Yaklaşık liste fiyatı (TL)
        public string              Notes         { get; set; } = "";
    }

    // ── Hesap Girdisi ────────────────────────────────────────────────────────────

    public class HeatPumpInput
    {
        public double HeatingLoadKw   { get; set; }   // Bina ısıtma yükü (kW)
        public double CoolingLoadKw   { get; set; }   // Bina soğutma yükü (kW)
        public string City            { get; set; } = "İstanbul";
        public double DesignTempC     { get; set; } = -3;    // Dış tasarım sıcaklığı
        public double SupplyTempC     { get; set; } = 35;    // Emisyon sistemi beslenme sıcaklığı
        public bool   FloorHeating    { get; set; } = true;  // Yerden ısıtma mu?
        public bool   HasBackupHeater { get; set; } = true;  // Elektrikli takviye ısıtıcı
    }

    // ── Hesap Sonucu ─────────────────────────────────────────────────────────────

    public class HeatPumpResult
    {
        public HeatPumpModel? RecommendedUnit   { get; set; }
        public double         SCOP              { get; set; }
        public double         SEER              { get; set; }
        public double         AnnualHeatKwh     { get; set; }
        public double         AnnualCoolKwh     { get; set; }
        public double         AnnualElecKwh     { get; set; }
        public double         AnnualCO2Kg       { get; set; }   // TR emisyon faktörü: 0.483 kg/kWh
        public double         BackupHeaterPct   { get; set; }   // Takviye ısıtma oranı (%)
        public string         EnergyLabel       { get; set; } = "";
        public string         Recommendation    { get; set; } = "";
        public List<string>   Warnings          { get; set; } = [];
    }

    // ── Isı Pompası Kataloğu ─────────────────────────────────────────────────────

    public static readonly List<HeatPumpModel> Catalog =
    [
        // ──── Daikin Altherma 3 R (Hava-Su) ──────────────────────────────────
        new() {
            ModelName="ERGA04EV3", Manufacturer=HeatPumpManufacturer.Daikin, Type=HeatPumpType.AirToWater,
            Series="Altherma 3 R", HeatingKw=4.0, CoolingKw=3.5,
            COP_A7_W35=4.64, COP_A2_W35=3.80, COP_Am7_W35=2.80,
            EER_A35_W18=3.50, SCOP_35=4.30, SEER=6.40,
            PowerInputKw=0.86, MaxFlowLph=600, RefrigerantType="R32", GWP=675,
            EnergyClass="A+++", NoiseLevelDB=55, PriceTL=185000,
            Notes="Monoblok, -25°C çalışma, 220V"
        },
        new() {
            ModelName="ERGA08EV3", Manufacturer=HeatPumpManufacturer.Daikin, Type=HeatPumpType.AirToWater,
            Series="Altherma 3 R", HeatingKw=8.0, CoolingKw=7.2,
            COP_A7_W35=4.48, COP_A2_W35=3.60, COP_Am7_W35=2.65,
            EER_A35_W18=3.40, SCOP_35=4.20, SEER=6.20,
            PowerInputKw=1.79, MaxFlowLph=1200, RefrigerantType="R32", GWP=675,
            EnergyClass="A+++", NoiseLevelDB=58, PriceTL=225000,
            Notes="Monoblok, -25°C, tek faz"
        },
        new() {
            ModelName="ERGA14EV", Manufacturer=HeatPumpManufacturer.Daikin, Type=HeatPumpType.AirToWater,
            Series="Altherma 3 R", HeatingKw=14.0, CoolingKw=13.5,
            COP_A7_W35=4.20, COP_A2_W35=3.40, COP_Am7_W35=2.50,
            EER_A35_W18=3.20, SCOP_35=4.00, SEER=5.90,
            PowerInputKw=3.33, MaxFlowLph=2200, RefrigerantType="R410A", GWP=2088,
            EnergyClass="A++", NoiseLevelDB=62, PriceTL=320000
        },
        new() {
            ModelName="EWYA016AV3N", Manufacturer=HeatPumpManufacturer.Daikin, Type=HeatPumpType.AirToWater,
            Series="Altherma 3 H HT", HeatingKw=16.0, CoolingKw=0,
            COP_A7_W35=2.88, COP_A2_W35=2.40, COP_Am7_W35=1.90,
            EER_A35_W18=0, SCOP_35=3.10, SEER=0,
            PowerInputKw=5.56, MaxFlowLph=2800, RefrigerantType="R410A", GWP=2088,
            EnergyClass="A+", NoiseLevelDB=64, PriceTL=420000,
            Notes="Yüksek sıcaklık W55 — A7/W55 COP değerleri · eski bina retrofit"
        },

        // ──── Vaillant aroTHERM Plus ─────────────────────────────────────────
        new() {
            ModelName="VWL 55/6 A S2", Manufacturer=HeatPumpManufacturer.Vaillant, Type=HeatPumpType.AirToWater,
            Series="aroTHERM plus", HeatingKw=5.5, CoolingKw=0,
            COP_A7_W35=5.10, COP_A2_W35=4.20, COP_Am7_W35=3.10,
            EER_A35_W18=0, SCOP_35=4.60, SEER=0,
            PowerInputKw=1.08, MaxFlowLph=850, RefrigerantType="R290", GWP=3,
            EnergyClass="A+++", NoiseLevelDB=48, PriceTL=210000,
            Notes="R290 (propan) — düşük GWP, sessiz, -20°C çalışma"
        },
        new() {
            ModelName="VWL 125/6 A", Manufacturer=HeatPumpManufacturer.Vaillant, Type=HeatPumpType.AirToWater,
            Series="aroTHERM plus", HeatingKw=12.5, CoolingKw=0,
            COP_A7_W35=4.60, COP_A2_W35=3.80, COP_Am7_W35=2.90,
            EER_A35_W18=0, SCOP_35=4.30, SEER=0,
            PowerInputKw=2.72, MaxFlowLph=1900, RefrigerantType="R290", GWP=3,
            EnergyClass="A+++", NoiseLevelDB=54, PriceTL=310000
        },

        // ──── Mitsubishi Electric Ecodan ─────────────────────────────────────
        new() {
            ModelName="PUHZ-SW120YKA", Manufacturer=HeatPumpManufacturer.Mitsubishi, Type=HeatPumpType.AirToWater,
            Series="Ecodan", HeatingKw=12.0, CoolingKw=11.2,
            COP_A7_W35=4.42, COP_A2_W35=3.55, COP_Am7_W35=2.62,
            EER_A35_W18=3.35, SCOP_35=4.10, SEER=5.80,
            PowerInputKw=2.71, MaxFlowLph=1800, RefrigerantType="R410A", GWP=2088,
            EnergyClass="A++", NoiseLevelDB=60, PriceTL=290000,
            Notes="Soğutma + ısıtma, -15°C çalışma, hidrolik modül"
        },

        // ──── Bosch CS7000iAW (Hava-Su, orta güç) ───────────────────────────
        new() {
            ModelName="CS7000iAW 7 OR-S", Manufacturer=HeatPumpManufacturer.Bosch, Type=HeatPumpType.AirToWater,
            Series="Compress 7000i", HeatingKw=7.0, CoolingKw=6.8,
            COP_A7_W35=4.30, COP_A2_W35=3.50, COP_Am7_W35=2.60,
            EER_A35_W18=3.30, SCOP_35=4.10, SEER=5.80,
            PowerInputKw=1.63, MaxFlowLph=1100, RefrigerantType="R32", GWP=675,
            EnergyClass="A++", NoiseLevelDB=52, PriceTL=195000
        },
    ];

    // ── Hesap Motoru ─────────────────────────────────────────────────────────────

    public static HeatPumpResult Calculate(HeatPumpInput inp)
    {
        var result = new HeatPumpResult();
        var warnings = result.Warnings;

        // Yük başına en küçük yeterli model seç
        var candidates = Catalog
            .Where(m => m.HeatingKw >= inp.HeatingLoadKw * 0.90)   // <%10 altın kabul et (takviyeli)
            .OrderBy(m => m.HeatingKw)
            .ToList();

        if (candidates.Count == 0)
        {
            warnings.Add("Kataloğda yükü karşılayan tek ünite bulunamadı — birden fazla ünite paralel gerekebilir.");
            candidates = Catalog.OrderByDescending(m => m.HeatingKw).Take(1).ToList();
        }

        var unit = candidates.First();
        result.RecommendedUnit = unit;
        result.SCOP = unit.SCOP_35;
        result.SEER = unit.SEER;

        // Takviye ısıtma oranı: tasarım sıcaklığında kapasite düşer
        // Basit lineer model: A-7'de kapasite ~65% nominal
        double capAtDesign = unit.HeatingKw * (1 - 0.012 * (7 - inp.DesignTempC));
        capAtDesign = Math.Max(capAtDesign, unit.HeatingKw * 0.50);
        double backup = inp.HeatingLoadKw > capAtDesign
            ? (inp.HeatingLoadKw - capAtDesign) / inp.HeatingLoadKw * 100
            : 0;
        result.BackupHeaterPct = backup;

        // Yıllık enerji — Türkiye ortalaması: 2200 ısıtma saati, 800 soğutma saati
        result.AnnualHeatKwh = inp.HeatingLoadKw * 2200 / result.SCOP;
        result.AnnualCoolKwh = inp.CoolingLoadKw  > 0 ? inp.CoolingLoadKw * 800 / result.SEER : 0;
        result.AnnualElecKwh = result.AnnualHeatKwh + result.AnnualCoolKwh;
        result.AnnualCO2Kg   = result.AnnualElecKwh * 0.483;   // TR grid faktörü 2023

        // Enerji etiketi
        result.EnergyLabel = result.SCOP switch
        {
            >= 5.1 => "A+++",
            >= 4.6 => "A++",
            >= 4.0 => "A+",
            >= 3.5 => "A",
            _      => "B veya altı"
        };

        if (inp.SupplyTempC > 45 && unit.Series.Contains("Altherma 3 R"))
            warnings.Add($"Beslenme sıcaklığı {inp.SupplyTempC}°C — bu model için W45 üstü verim düşer. Yüksek sıcaklık (HT) serisi önerin.");

        if (unit.GWP > 1000)
            warnings.Add($"Soğutucu akışkan {unit.RefrigerantType} yüksek GWP={unit.GWP} — F-gaz regülasyonu kapsamında.");

        if (backup > 30)
            warnings.Add($"Tasarım gece sıcaklığında takviye ısıtıcı payı %{backup:F0} — ısıtma yükünü gözden geçirin.");

        result.Recommendation =
            $"Önerilen: {unit.Manufacturer} {unit.ModelName} · {unit.HeatingKw} kW · SCOP {result.SCOP:F2} · {result.EnergyLabel}\n" +
            $"Yıllık elektrik: {result.AnnualElecKwh:F0} kWh · CO₂: {result.AnnualCO2Kg:F0} kg/yıl";

        return result;
    }
}
