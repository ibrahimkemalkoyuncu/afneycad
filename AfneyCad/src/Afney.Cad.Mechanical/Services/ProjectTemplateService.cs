using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Proje Şablon Servisi (ProjectTemplateService)
   NEDEN: FINE MEP'te "yeni proje" açılırken tip seçilir ve bina tipine özgü
          varsayılan armatür yükü, kat yüksekliği, sistem tipleri önceden dolar.
          Mühendis her seferinde baştan girmek yerine şablondan başlar.

   ŞABLON TİPLERİ: Konut · Ofis · Otel · AVM · Hastane · Okul · Endüstri
*/
public class ProjectTemplateService
{
    // ── Bölge (Oda/Alan) Şablonu ─────────────────────────────────────────────────

    public class ZoneTemplate
    {
        public string Name             { get; set; } = "";
        public string SystemType       { get; set; } = "";
        public double FloorAreaM2      { get; set; }
        public int    FixtureUnits     { get; set; }    // Toplam armatür birimi (DU)
        public double PeakFlowLps      { get; set; }    // Pik debi (L/s)
        public double HeatingLoadW     { get; set; }    // Isıtma yükü (W)
        public double CoolingLoadW     { get; set; }    // Soğutma yükü (W)
        public int    OccupancyPersons { get; set; }    // Kişi sayısı
        public string Notes            { get; set; } = "";
    }

    // ── Proje Şablonu ────────────────────────────────────────────────────────────

    public class ProjectTemplate
    {
        public string             TemplateId        { get; set; } = "";
        public string             Name              { get; set; } = "";
        public string             Category          { get; set; } = "";
        public string             Description       { get; set; } = "";
        public string             Icon              { get; set; } = "";
        public double             TypicalFloorAreaM2 { get; set; }
        public double             FloorHeightM      { get; set; } = 3.0;
        public int                TypicalFloors     { get; set; } = 4;
        public List<string>       ActiveSystems     { get; set; } = [];   // Aktif sistem tipleri
        public List<ZoneTemplate> Zones             { get; set; } = [];
        public Dictionary<string, double> Standards { get; set; } = [];   // Standart değerler
        public string             Notes             { get; set; } = "";
    }

    // ── Şablon Kataloğu ──────────────────────────────────────────────────────────

    public static readonly List<ProjectTemplate> Templates =
    [
        // ─────────────── KONUT ───────────────────────────────────────────────────
        new()
        {
            TemplateId = "konut_3p1", Name = "3+1 Konut Dairesi", Category = "Konut",
            Icon = "🏠", Description = "Standart 3+1 konut dairesi — soğuk/sıcak su, pis su, ısıtma",
            TypicalFloorAreaM2 = 120, FloorHeightM = 2.8, TypicalFloors = 1,
            ActiveSystems = ["DomesticColdWater", "DomesticHotWater", "WasteWater", "RainWater", "Heating"],
            Zones =
            [
                new() { Name="Salon",          SystemType="Tüm",  FloorAreaM2=25, FixtureUnits=0,  HeatingLoadW=1800, CoolingLoadW=2500, OccupancyPersons=4 },
                new() { Name="Yatak Odası 1",  SystemType="Tüm",  FloorAreaM2=14, FixtureUnits=0,  HeatingLoadW=900,  CoolingLoadW=1100, OccupancyPersons=2 },
                new() { Name="Yatak Odası 2",  SystemType="Tüm",  FloorAreaM2=12, FixtureUnits=0,  HeatingLoadW=800,  CoolingLoadW=900,  OccupancyPersons=2 },
                new() { Name="Mutfak",         SystemType="Tüm",  FloorAreaM2=10, FixtureUnits=4,  HeatingLoadW=600,  CoolingLoadW=800,  OccupancyPersons=1 },
                new() { Name="Banyo",          SystemType="Tüm",  FloorAreaM2=5,  FixtureUnits=6,  HeatingLoadW=500,  CoolingLoadW=0,    OccupancyPersons=1 },
                new() { Name="WC",             SystemType="Tüm",  FloorAreaM2=3,  FixtureUnits=4,  HeatingLoadW=200,  CoolingLoadW=0,    OccupancyPersons=1 },
            ],
            Standards = new() { ["WaterDemandLps"]=0.3, ["HWRatioPercent"]=30, ["WasteFlowLs"]=0.6 },
            Notes = "TS 1258 Tablo 2 — bağımsız konut armatür birimleri"
        },

        new()
        {
            TemplateId = "konut_site", Name = "Konut Sitesi (Blok)", Category = "Konut",
            Icon = "🏘️", Description = "Çok katlı konut bloku — ortak hacimler dahil",
            TypicalFloorAreaM2 = 1500, FloorHeightM = 2.8, TypicalFloors = 8,
            ActiveSystems = ["DomesticColdWater", "DomesticHotWater", "WasteWater", "RainWater", "Heating", "FireProtection"],
            Zones =
            [
                new() { Name="Daire (Tipik)", SystemType="Tüm", FloorAreaM2=110, FixtureUnits=14, HeatingLoadW=4500, CoolingLoadW=5000, OccupancyPersons=3, Notes="Kat başına 4 daire" },
                new() { Name="Ortak Giriş",  SystemType="Tüm", FloorAreaM2=40,  FixtureUnits=2,  HeatingLoadW=800,  CoolingLoadW=1000, OccupancyPersons=0 },
                new() { Name="Sığınak",      SystemType="Temel",FloorAreaM2=60,  FixtureUnits=0,  HeatingLoadW=200,  CoolingLoadW=0,    OccupancyPersons=0 },
            ],
            Standards = new() { ["WaterDemandLps"]=2.5, ["FireReserveM3"]=30 },
            Notes = "TS 1258 — eşzamanlılık katsayısı uygulanır"
        },

        // ─────────────── OFİS ────────────────────────────────────────────────────
        new()
        {
            TemplateId = "ofis_kucuk", Name = "Küçük Ofis (500 m²)", Category = "Ofis",
            Icon = "🏢", Description = "Tek katlı küçük ofis — soğuk su, pis su, HVAC",
            TypicalFloorAreaM2 = 500, FloorHeightM = 3.2, TypicalFloors = 1,
            ActiveSystems = ["DomesticColdWater", "WasteWater", "Ventilation", "Cooling", "Heating"],
            Zones =
            [
                new() { Name="Açık Ofis",       FloorAreaM2=200, FixtureUnits=0,  HeatingLoadW=8000,  CoolingLoadW=12000, OccupancyPersons=25 },
                new() { Name="Toplantı Salonu",  FloorAreaM2=40,  FixtureUnits=0,  HeatingLoadW=2000,  CoolingLoadW=4000,  OccupancyPersons=15 },
                new() { Name="Mutfak/Dinlenme",  FloorAreaM2=30,  FixtureUnits=5,  HeatingLoadW=1200,  CoolingLoadW=2000,  OccupancyPersons=5  },
                new() { Name="Tuvalet (Kat)",    FloorAreaM2=20,  FixtureUnits=10, HeatingLoadW=600,   CoolingLoadW=0,     OccupancyPersons=0  },
                new() { Name="Server Odası",     FloorAreaM2=15,  FixtureUnits=0,  HeatingLoadW=0,     CoolingLoadW=6000,  OccupancyPersons=0, Notes="Sürekli soğutma" },
            ],
            Standards = new() { ["VentLPS_kisi"]=10, ["AcPh"]=6, ["LightingWpm2"]=12 }
        },

        new()
        {
            TemplateId = "ofis_buyuk", Name = "Çok Katlı Ofis Binası", Category = "Ofis",
            Icon = "🏙️", Description = "Çok katlı ofis — merkezi HVAC, yangın, hidrant",
            TypicalFloorAreaM2 = 4000, FloorHeightM = 3.5, TypicalFloors = 10,
            ActiveSystems = ["DomesticColdWater", "DomesticHotWater", "WasteWater", "Ventilation", "Cooling", "Heating", "FireProtection"],
            Zones =
            [
                new() { Name="Ofis Katı (Tipik)",   FloorAreaM2=350, FixtureUnits=8,  HeatingLoadW=14000, CoolingLoadW=20000, OccupancyPersons=40 },
                new() { Name="Lobi/Resepsiyon",     FloorAreaM2=120, FixtureUnits=2,  HeatingLoadW=5000,  CoolingLoadW=8000,  OccupancyPersons=10 },
                new() { Name="Tuvalet Bloğu (Kat)", FloorAreaM2=30,  FixtureUnits=16, HeatingLoadW=1000,  CoolingLoadW=0,     OccupancyPersons=0  },
                new() { Name="Restoran/Kafeterya",  FloorAreaM2=200, FixtureUnits=20, HeatingLoadW=8000,  CoolingLoadW=15000, OccupancyPersons=80 },
                new() { Name="Otopark",             FloorAreaM2=500, FixtureUnits=0,  HeatingLoadW=0,     CoolingLoadW=0,     OccupancyPersons=0, Notes="Havalandırma kritik — CO sensörü" },
            ],
            Standards = new() { ["FireReserveM3"]=100, ["SprinklerAreaM2"]=12 }
        },

        // ─────────────── OTEL ────────────────────────────────────────────────────
        new()
        {
            TemplateId = "otel_butik", Name = "Butik Otel (30 Oda)", Category = "Otel",
            Icon = "🏨", Description = "Butik otel — sıcak su yoğun kullanım, HVAC",
            TypicalFloorAreaM2 = 1200, FloorHeightM = 3.0, TypicalFloors = 4,
            ActiveSystems = ["DomesticColdWater", "DomesticHotWater", "WasteWater", "Heating", "Cooling", "FireProtection"],
            Zones =
            [
                new() { Name="Standart Oda (Tipik)",FloorAreaM2=28, FixtureUnits=8,  HeatingLoadW=1200, CoolingLoadW=1500, OccupancyPersons=2, Notes="Banyolu, çift lavabo" },
                new() { Name="Süit Oda",            FloorAreaM2=55, FixtureUnits=14, HeatingLoadW=2200, CoolingLoadW=2800, OccupancyPersons=2 },
                new() { Name="Restoran",             FloorAreaM2=80, FixtureUnits=15, HeatingLoadW=4000, CoolingLoadW=7000, OccupancyPersons=60 },
                new() { Name="Mutfak (Endüstriyel)",FloorAreaM2=40, FixtureUnits=25, HeatingLoadW=2000, CoolingLoadW=5000, OccupancyPersons=8, Notes="Yağ tutucu gerekli" },
                new() { Name="Lobi/Resepsiyon",     FloorAreaM2=60, FixtureUnits=3,  HeatingLoadW=2500, CoolingLoadW=4000, OccupancyPersons=10 },
                new() { Name="SPA/Havuz",           FloorAreaM2=80, FixtureUnits=30, HeatingLoadW=5000, CoolingLoadW=3000, OccupancyPersons=20, Notes="Yüksek nem — özel HVAC" },
            ],
            Standards = new() { ["HWUsageLperGun_kisi"]=200, ["HWPeakRatio"]=0.25 },
            Notes = "SHW pik saati 06:00-09:00 — özel bekleme tankı gerekli"
        },

        // ─────────────── AVM ─────────────────────────────────────────────────────
        new()
        {
            TemplateId = "avm", Name = "Alışveriş Merkezi (AVM)", Category = "Ticari",
            Icon = "🛒", Description = "AVM — yoğun tesisat, sprinkler, yangın hidrant",
            TypicalFloorAreaM2 = 15000, FloorHeightM = 4.5, TypicalFloors = 3,
            ActiveSystems = ["DomesticColdWater", "WasteWater", "FireProtection", "Ventilation", "Cooling"],
            Zones =
            [
                new() { Name="Mağaza Katı (Tipik)",  FloorAreaM2=3000, FixtureUnits=0,  HeatingLoadW=0,     CoolingLoadW=60000, OccupancyPersons=500 },
                new() { Name="Gıda Katı/Food Court", FloorAreaM2=1200, FixtureUnits=80, HeatingLoadW=0,     CoolingLoadW=40000, OccupancyPersons=400, Notes="Yağ tutucu + yüksek debi" },
                new() { Name="Tuvalet Bloğu",        FloorAreaM2=100,  FixtureUnits=60, HeatingLoadW=2000,  CoolingLoadW=3000,  OccupancyPersons=0 },
                new() { Name="Sinema Salonu",        FloorAreaM2=800,  FixtureUnits=5,  HeatingLoadW=5000,  CoolingLoadW=25000, OccupancyPersons=350 },
                new() { Name="Otopark (Bodrum)",     FloorAreaM2=4000, FixtureUnits=0,  HeatingLoadW=0,     CoolingLoadW=0,     OccupancyPersons=0, Notes="CO/LPG sensörlü havalandırma" },
            ],
            Standards = new() { ["SprinklerAreaM2"]=9, ["FireReserveM3"]=500, ["HydrantCount"]=8 }
        },

        // ─────────────── HASTANE ─────────────────────────────────────────────────
        new()
        {
            TemplateId = "hastane", Name = "Hastane / Sağlık Tesisi", Category = "Sağlık",
            Icon = "🏥", Description = "Hastane — steril su, tıbbi gaz, yoğun bakım HVAC, yangın",
            TypicalFloorAreaM2 = 8000, FloorHeightM = 3.5, TypicalFloors = 6,
            ActiveSystems = ["DomesticColdWater", "DomesticHotWater", "WasteWater", "FireProtection", "Ventilation", "Cooling", "Heating"],
            Zones =
            [
                new() { Name="Hasta Odası (2 Kişilik)",FloorAreaM2=22, FixtureUnits=6,  HeatingLoadW=900,  CoolingLoadW=1000, OccupancyPersons=3, Notes="Lavabo + tuvalet + duş" },
                new() { Name="Ameliyathane",           FloorAreaM2=40, FixtureUnits=4,  HeatingLoadW=2000, CoolingLoadW=4000, OccupancyPersons=8, Notes="Steril HVAC — HEPA H14" },
                new() { Name="Yoğun Bakım Ünitesi",   FloorAreaM2=60, FixtureUnits=10, HeatingLoadW=3000, CoolingLoadW=5000, OccupancyPersons=10, Notes="24/7 soğutma kritik" },
                new() { Name="Bekleme/Koridor",        FloorAreaM2=200,FixtureUnits=0,  HeatingLoadW=6000, CoolingLoadW=8000, OccupancyPersons=50 },
                new() { Name="Mutfak/Sterilizasyon",  FloorAreaM2=80, FixtureUnits=30, HeatingLoadW=4000, CoolingLoadW=6000, OccupancyPersons=10, Notes="Softener + RO suyu" },
                new() { Name="Acil Servis",           FloorAreaM2=150,FixtureUnits=20, HeatingLoadW=7000, CoolingLoadW=9000, OccupancyPersons=30, Notes="Sürekli temiz hava" },
            ],
            Standards = new() { ["HWUsageLperYatak"]=250, ["SterilWaterLpd"]=50, ["AcPhAmeliyat"]=20 },
            Notes = "TS EN 13779 Sınıf IV havalandırma / Ameliyathane pozitif basınç"
        },

        // ─────────────── OKUL ────────────────────────────────────────────────────
        new()
        {
            TemplateId = "okul", Name = "İlk/Ortaöğretim Okulu", Category = "Eğitim",
            Icon = "🏫", Description = "K-12 okul — ısıtma ağırlıklı, sınıf HVAC",
            TypicalFloorAreaM2 = 3000, FloorHeightM = 3.2, TypicalFloors = 3,
            ActiveSystems = ["DomesticColdWater", "WasteWater", "Heating", "Ventilation"],
            Zones =
            [
                new() { Name="Derslik (30 Kişi)",  FloorAreaM2=55, FixtureUnits=0, HeatingLoadW=3000, CoolingLoadW=4000, OccupancyPersons=31 },
                new() { Name="Spor Salonu",        FloorAreaM2=400,FixtureUnits=0, HeatingLoadW=8000, CoolingLoadW=0,    OccupancyPersons=100 },
                new() { Name="Yemekhane",          FloorAreaM2=200,FixtureUnits=15,HeatingLoadW=6000, CoolingLoadW=8000, OccupancyPersons=150 },
                new() { Name="Soyunma/Duş",        FloorAreaM2=40, FixtureUnits=20,HeatingLoadW=2000, CoolingLoadW=0,    OccupancyPersons=30 },
                new() { Name="İdari Ofis",         FloorAreaM2=60, FixtureUnits=3, HeatingLoadW=2000, CoolingLoadW=2500, OccupancyPersons=10 },
            ],
            Standards = new() { ["VentLPS_kisi"]=8, ["AcPh"]=3 }
        },

        // ─────────────── ENDÜSTRİ ────────────────────────────────────────────────
        new()
        {
            TemplateId = "fabrika", Name = "Üretim Tesisi / Fabrika", Category = "Endüstri",
            Icon = "🏭", Description = "Fabrika — proses suyu, yangın, havalandırma, atık su",
            TypicalFloorAreaM2 = 5000, FloorHeightM = 7.0, TypicalFloors = 1,
            ActiveSystems = ["DomesticColdWater", "WasteWater", "FireProtection", "Ventilation"],
            Zones =
            [
                new() { Name="Üretim Halli",      FloorAreaM2=3000, FixtureUnits=0,  HeatingLoadW=30000, CoolingLoadW=0,    OccupancyPersons=50, Notes="Endüstriyel havalandırma" },
                new() { Name="Soyunma/Duş",       FloorAreaM2=100,  FixtureUnits=40, HeatingLoadW=4000,  CoolingLoadW=0,    OccupancyPersons=50 },
                new() { Name="Ofis/İdari",        FloorAreaM2=300,  FixtureUnits=8,  HeatingLoadW=8000,  CoolingLoadW=10000,OccupancyPersons=30 },
                new() { Name="Yemekhane",         FloorAreaM2=150,  FixtureUnits=15, HeatingLoadW=5000,  CoolingLoadW=6000, OccupancyPersons=60 },
                new() { Name="Kompresör/Teknik",  FloorAreaM2=80,   FixtureUnits=0,  HeatingLoadW=0,     CoolingLoadW=15000,OccupancyPersons=2,  Notes="Ekipman soğutması" },
            ],
            Standards = new() { ["FireReserveM3"]=200, ["ProcessWaterLpd"]=5000 }
        },
    ];

    // ── Şablon Arama ─────────────────────────────────────────────────────────────

    public static ProjectTemplate? FindById(string id) =>
        Templates.Find(t => t.TemplateId == id);

    public static IEnumerable<ProjectTemplate> ByCategory(string category) =>
        Templates.FindAll(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public static string[] Categories =>
        [.. System.Linq.Enumerable.Distinct(Templates.ConvertAll(t => t.Category))];

    // ── Özet Metrikleri ──────────────────────────────────────────────────────────

    public static (double totalHeatingKw, double totalCoolingKw, int totalDU, int totalPersons)
        SummarizeTemplate(ProjectTemplate tpl)
    {
        double heat = 0, cool = 0; int du = 0, persons = 0;
        foreach (var z in tpl.Zones)
        {
            heat    += z.HeatingLoadW;
            cool    += z.CoolingLoadW;
            du      += z.FixtureUnits;
            persons += z.OccupancyPersons;
        }
        return (heat / 1000.0, cool / 1000.0, du, persons);
    }
}
