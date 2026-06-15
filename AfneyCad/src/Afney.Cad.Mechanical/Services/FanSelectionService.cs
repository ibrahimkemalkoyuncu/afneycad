using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Fan Seçim Servisi (FanSelectionService)
   NEDEN: FINE MEP'te fan kataloğu yoktur — mühendis dışarıdan bakıp elle girer.
          Bu servis Systemair, Halton, Soler & Palau katalog verilerini içerir;
          debi+basınç verildiğinde uygun fanı filtreler ve verim analizini yapar.

   FONKSİYONLAR:
   - FindFans(flowM3h, pressurePa, type?, manufacturer?) → sıralı liste
   - BestFan(flowM3h, pressurePa) → en verimli fan
*/
public class FanSelectionService
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    public enum FanType
    {
        Axial,          // Aksiyal — düşük basınç, yüksek debi
        Centrifugal,    // Santrifüj — yüksek basınç
        Mixed,          // Karışık akışlı
        Inline,         // In-line kanal fanı
        Roof,           // Çatı fanı
        BoxFan,         // Box fan / plug fan
        ERV             // Isı Geri Kazanım Ünitesi
    }

    public enum FanManufacturer { Systemair, Halton, SolerPalau, EBMPapst, Nicotra }

    // ── Fan Modeli ───────────────────────────────────────────────────────────────

    public class FanModel
    {
        public string          ModelName       { get; set; } = "";
        public FanManufacturer Manufacturer    { get; set; }
        public FanType         Type            { get; set; }
        public string          Series          { get; set; } = "";
        public double          MaxFlowM3h      { get; set; }   // Maks debi (m³/h)
        public double          NomPressurePa   { get; set; }   // Nominal statik basınç (Pa)
        public double          MaxPressurePa   { get; set; }   // Maks statik basınç (Pa)
        public double          PowerKw         { get; set; }   // Motor gücü (kW)
        public double          EfficiencyPct   { get; set; }   // Toplam verim (%)
        public double          NoiseDB         { get; set; }   // Ses seviyesi dB(A) @ 1m (LwA)
        public int             SpeedRPM        { get; set; }   // Hız (dev/dk)
        public string          ConnectionMM    { get; set; } = "";  // Bağlantı çapı / ebadı
        public string          Voltage         { get; set; } = "230V/1~/50Hz";
        public string          IPClass         { get; set; } = "IP44";
        public string          EnergyClass     { get; set; } = "A";
        public string          Application     { get; set; } = "";
        public bool            HasEC_Motor     { get; set; }   // EC motorlu mu?
        public double          PriceEur        { get; set; }   // Yaklaşık liste fiyatı
        public string          Notes           { get; set; } = "";
    }

    // ── Fan Hesap Sonucu ─────────────────────────────────────────────────────────

    public class FanSelectionResult
    {
        public FanModel Fan             { get; set; } = null!;
        public double   FlowMarginPct   { get; set; }   // Debi yedek payı (%)
        public double   PressureMarginPct { get; set; } // Basınç yedek payı (%)
        public double   SpecificFanPower { get; set; }  // SFP = P(W) / Q(m³/s) W/(m³/s)
        public string   SFPCategory     { get; set; } = ""; // SFP-1 .. SFP-5 (EN 13779)
    }

    // ── Fan Kataloğu ─────────────────────────────────────────────────────────────

    public static readonly List<FanModel> FanCatalog =
    [
        // ──── Systemair — Aksiyal Kanal Fanları (KV Serisi) ───────────────────
        new() {
            ModelName="KV 100 M", Manufacturer=FanManufacturer.Systemair, Type=FanType.Axial,
            Series="KV", MaxFlowM3h=130, NomPressurePa=70, MaxPressurePa=110,
            PowerKw=0.025, EfficiencyPct=42, NoiseDB=35, SpeedRPM=2800,
            ConnectionMM="DN100", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="B",
            HasEC_Motor=false, Application="Banyo/WC egzoz, çap 100mm", PriceEur=45
        },
        new() {
            ModelName="KV 125 M", Manufacturer=FanManufacturer.Systemair, Type=FanType.Axial,
            Series="KV", MaxFlowM3h=220, NomPressurePa=90, MaxPressurePa=140,
            PowerKw=0.040, EfficiencyPct=44, NoiseDB=37, SpeedRPM=2650,
            ConnectionMM="DN125", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="B",
            HasEC_Motor=false, Application="Banyo/Mutfak egzoz, çap 125mm", PriceEur=55
        },
        new() {
            ModelName="KV 160 M", Manufacturer=FanManufacturer.Systemair, Type=FanType.Axial,
            Series="KV", MaxFlowM3h=430, NomPressurePa=130, MaxPressurePa=200,
            PowerKw=0.090, EfficiencyPct=48, NoiseDB=44, SpeedRPM=2800,
            ConnectionMM="DN160", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="A",
            HasEC_Motor=false, Application="Konut/Büro genel havalandırma", PriceEur=85
        },
        new() {
            ModelName="KV 200 M", Manufacturer=FanManufacturer.Systemair, Type=FanType.Axial,
            Series="KV", MaxFlowM3h=750, NomPressurePa=150, MaxPressurePa=230,
            PowerKw=0.150, EfficiencyPct=50, NoiseDB=48, SpeedRPM=2750,
            ConnectionMM="DN200", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="A",
            HasEC_Motor=false, Application="Ofis havalandırma, çap 200mm", PriceEur=110
        },

        // ──── Systemair — Çatı Fanları (DVV Serisi) ─────────────────────────
        new() {
            ModelName="DVV 400D2-6-4", Manufacturer=FanManufacturer.Systemair, Type=FanType.Roof,
            Series="DVV", MaxFlowM3h=3200, NomPressurePa=180, MaxPressurePa=320,
            PowerKw=0.55, EfficiencyPct=52, NoiseDB=62, SpeedRPM=1450,
            ConnectionMM="DN400", Voltage="400V/3~/50Hz", IPClass="IP55", EnergyClass="A",
            HasEC_Motor=false, Application="Çatı egzoz, ticari bina", PriceEur=620
        },
        new() {
            ModelName="DVV 560D4-8-4", Manufacturer=FanManufacturer.Systemair, Type=FanType.Roof,
            Series="DVV", MaxFlowM3h=8000, NomPressurePa=250, MaxPressurePa=450,
            PowerKw=1.50, EfficiencyPct=58, NoiseDB=68, SpeedRPM=960,
            ConnectionMM="DN560", Voltage="400V/3~/50Hz", IPClass="IP55", EnergyClass="A",
            HasEC_Motor=false, Application="Büyük çatı egzoz — AVM/Fabrika", PriceEur=1250
        },

        // ──── Systemair — Isı Geri Kazanım (SAVE VTR Serisi) ────────────────
        new() {
            ModelName="SAVE VTR 150/B", Manufacturer=FanManufacturer.Systemair, Type=FanType.ERV,
            Series="SAVE VTR", MaxFlowM3h=150, NomPressurePa=100, MaxPressurePa=150,
            PowerKw=0.060, EfficiencyPct=85, NoiseDB=28, SpeedRPM=0,
            ConnectionMM="DN125", Voltage="230V/1~/50Hz", IPClass="IP21", EnergyClass="A+",
            HasEC_Motor=true, Application="Konut ısı geri kazanım — HR=%85", PriceEur=850,
            Notes="Bypass damper, filtre G4+F7"
        },
        new() {
            ModelName="SAVE VTR 300/B", Manufacturer=FanManufacturer.Systemair, Type=FanType.ERV,
            Series="SAVE VTR", MaxFlowM3h=300, NomPressurePa=150, MaxPressurePa=200,
            PowerKw=0.120, EfficiencyPct=86, NoiseDB=31, SpeedRPM=0,
            ConnectionMM="DN160", Voltage="230V/1~/50Hz", IPClass="IP21", EnergyClass="A+",
            HasEC_Motor=true, Application="Küçük ofis ısı geri kazanım", PriceEur=1400,
            Notes="EC fan, dijital kontrol, verimi EPBD uyumlu"
        },
        new() {
            ModelName="SAVE VTR 700/B", Manufacturer=FanManufacturer.Systemair, Type=FanType.ERV,
            Series="SAVE VTR", MaxFlowM3h=700, NomPressurePa=200, MaxPressurePa=300,
            PowerKw=0.280, EfficiencyPct=84, NoiseDB=36, SpeedRPM=0,
            ConnectionMM="DN250", Voltage="400V/3~/50Hz", IPClass="IP21", EnergyClass="A+",
            HasEC_Motor=true, Application="Orta boy ofis/otel ısı geri kazanım", PriceEur=3500
        },

        // ──── Halton — Ofis/Hastane Tavan Fanı (HFTC Serisi) ────────────────
        new() {
            ModelName="HFTC 315-2", Manufacturer=FanManufacturer.Halton, Type=FanType.Inline,
            Series="HFTC", MaxFlowM3h=1800, NomPressurePa=200, MaxPressurePa=350,
            PowerKw=0.37, EfficiencyPct=56, NoiseDB=50, SpeedRPM=1450,
            ConnectionMM="315x315", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="A",
            HasEC_Motor=false, Application="Ofis/hastane — sessiz in-line", PriceEur=480
        },
        new() {
            ModelName="HFTC 400-4", Manufacturer=FanManufacturer.Halton, Type=FanType.Inline,
            Series="HFTC", MaxFlowM3h=3500, NomPressurePa=280, MaxPressurePa=500,
            PowerKw=0.75, EfficiencyPct=60, NoiseDB=55, SpeedRPM=960,
            ConnectionMM="400x400", Voltage="400V/3~/50Hz", IPClass="IP44", EnergyClass="A",
            HasEC_Motor=false, Application="Hastane koridoru, büyük ofis AHU", PriceEur=760
        },

        // ──── Halton — Mutfak Egzoz (HKE Serisi) ────────────────────────────
        new() {
            ModelName="HKE 250", Manufacturer=FanManufacturer.Halton, Type=FanType.Centrifugal,
            Series="HKE", MaxFlowM3h=900, NomPressurePa=300, MaxPressurePa=600,
            PowerKw=0.25, EfficiencyPct=55, NoiseDB=58, SpeedRPM=2800,
            ConnectionMM="DN250", Voltage="230V/1~/50Hz", IPClass="IP55", EnergyClass="A",
            HasEC_Motor=false, Application="Endüstriyel mutfak egzoz, yağa dayanıklı", PriceEur=380,
            Notes="260°C / 2h yangına dayanıklı versiyon mevcut"
        },
        new() {
            ModelName="HKE 400", Manufacturer=FanManufacturer.Halton, Type=FanType.Centrifugal,
            Series="HKE", MaxFlowM3h=2800, NomPressurePa=400, MaxPressurePa=800,
            PowerKw=0.75, EfficiencyPct=60, NoiseDB=65, SpeedRPM=1450,
            ConnectionMM="DN400", Voltage="400V/3~/50Hz", IPClass="IP55", EnergyClass="A",
            HasEC_Motor=false, Application="AVM/Otel mutfak egzoz", PriceEur=720
        },

        // ──── Soler & Palau — Santrifüj Kanal Fanı (TD Serisi) ──────────────
        new() {
            ModelName="TD-800/200 N Silent", Manufacturer=FanManufacturer.SolerPalau, Type=FanType.Inline,
            Series="TD Silent", MaxFlowM3h=780, NomPressurePa=160, MaxPressurePa=280,
            PowerKw=0.125, EfficiencyPct=52, NoiseDB=33, SpeedRPM=1200,
            ConnectionMM="DN200", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="A",
            HasEC_Motor=false, Application="Sessiz konut/ofis kanal fanı", PriceEur=195,
            Notes="Titreşim izoleli, düşük gürültü"
        },
        new() {
            ModelName="TD-1300/250 N Silent", Manufacturer=FanManufacturer.SolerPalau, Type=FanType.Inline,
            Series="TD Silent", MaxFlowM3h=1200, NomPressurePa=180, MaxPressurePa=300,
            PowerKw=0.190, EfficiencyPct=55, NoiseDB=38, SpeedRPM=1350,
            ConnectionMM="DN250", Voltage="230V/1~/50Hz", IPClass="IP44", EnergyClass="A",
            HasEC_Motor=false, Application="Orta boy ofis havalandırma", PriceEur=260
        },

        // ──── EBM-Papst — EC Santrifüj (G3G Serisi) ─────────────────────────
        new() {
            ModelName="G3G200-GH33-01", Manufacturer=FanManufacturer.EBMPapst, Type=FanType.Centrifugal,
            Series="G3G", MaxFlowM3h=1600, NomPressurePa=300, MaxPressurePa=550,
            PowerKw=0.42, EfficiencyPct=72, NoiseDB=56, SpeedRPM=2400,
            ConnectionMM="Ø200 çıkış", Voltage="230-400V/1-3~/50Hz", IPClass="IP54", EnergyClass="A+",
            HasEC_Motor=true, Application="AHU iç fanı — yüksek verimli EC plug fan", PriceEur=480,
            Notes="0-10V hız kontrolü, IE5 eşdeğer verim"
        },
        new() {
            ModelName="G3G355-GN33-01", Manufacturer=FanManufacturer.EBMPapst, Type=FanType.Centrifugal,
            Series="G3G", MaxFlowM3h=8000, NomPressurePa=500, MaxPressurePa=900,
            PowerKw=2.10, EfficiencyPct=76, NoiseDB=70, SpeedRPM=1600,
            ConnectionMM="Ø355 çıkış", Voltage="400V/3~/50Hz", IPClass="IP54", EnergyClass="A+",
            HasEC_Motor=true, Application="Büyük AHU plug fan — endüstri", PriceEur=1650,
            Notes="Modbus/BACnet entegrasyon"
        },
    ];

    // ── Fan Arama ─────────────────────────────────────────────────────────────────

    public static List<FanSelectionResult> FindFans(
        double flowM3h, double pressurePa,
        FanType? type = null, FanManufacturer? manufacturer = null,
        double safetyFlow = 1.15, double safetyPressure = 1.20)
    {
        double reqFlow = flowM3h * safetyFlow;
        double reqPress = pressurePa * safetyPressure;

        var filtered = FanCatalog
            .Where(f => f.MaxFlowM3h   >= reqFlow)
            .Where(f => f.MaxPressurePa >= reqPress)
            .Where(f => type == null || f.Type == type)
            .Where(f => manufacturer == null || f.Manufacturer == manufacturer)
            .OrderByDescending(f => f.EfficiencyPct)
            .ThenBy(f => f.PowerKw)
            .ToList();

        return filtered.Select(f =>
        {
            double sfp = f.PowerKw * 1000.0 / (f.MaxFlowM3h / 3600.0);  // W/(m³/s)
            return new FanSelectionResult
            {
                Fan              = f,
                FlowMarginPct    = (f.MaxFlowM3h   / reqFlow   - 1) * 100,
                PressureMarginPct = (f.MaxPressurePa / reqPress - 1) * 100,
                SpecificFanPower = sfp,
                SFPCategory      = SfpCategory(sfp)
            };
        }).ToList();
    }

    public static FanSelectionResult? BestFan(double flowM3h, double pressurePa) =>
        FindFans(flowM3h, pressurePa).FirstOrDefault();

    private static string SfpCategory(double sfp) => sfp switch
    {
        <= 500  => "SFP-1 (Excellent)",
        <= 750  => "SFP-2 (Good)",
        <= 1250 => "SFP-3 (Average)",
        <= 2000 => "SFP-4 (Below average)",
        _       => "SFP-5 (Poor)"
    };

    // ── Ses Değerlendirmesi ───────────────────────────────────────────────────────

    public static string NoiseAssessment(double noiseDB, string application) =>
        noiseDB < 30 ? "Sessiz — konut uygulamaları için ideal" :
        noiseDB < 40 ? "Normal — ofis ve konut için kabul edilebilir" :
        noiseDB < 50 ? "Orta — ses yalıtım önlemleri önerilir" :
        noiseDB < 65 ? "Gürültülü — teknik hacim/servis odası önerilir" :
                       "Yüksek gürültü — akustik panel + vibrasyon izolasyonu zorunlu";
}
