using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Pompa Seçim Servisi (PumpSelectionService)
    NEDEN: Hesaplanan sistem debisi (Q) ve kritik hat basınç kaybına (Hm) göre uygun pompa modelini seçmek ve
           mühendise karşılaştırmalı analiz sunmak için.

    KATALOG MÜHENDİSLİĞİ:
    - Wilo: Stratos PICO, Stratos MAXO, CronoLine-IL, CronoTwin-DL, Helix FIRST serisi
    - Grundfos: MAGNA, ALPHA, CR, CM, TP serisi
    - Her pompa: MaxFlow, MaxHead, BEP, Verim, Güç, Bağlantı bilgisi
    - Seçim: BEP (Best Efficiency Point) yakınlık algoritması
*/
public class PumpSelectionService
{
    public class PumpModel
    {
        public string Brand { get; set; } = "";
        public string Series { get; set; } = "";
        public string ModelName { get; set; } = "";
        public double MaxFlow { get; set; }     // m³/h
        public double MaxHead { get; set; }     // mSS
        public double BepFlow { get; set; }     // En verimli debi noktası (m³/h)
        public double BepHead { get; set; }     // BEP'teki basma yüksekliği (mSS)
        public double Efficiency { get; set; }  // Verim (%0 - %1)
        public double PowerKW { get; set; }     // Motor gücü (kW)
        public string Connection { get; set; } = "";
        public string Application { get; set; } = ""; // Kullanım alanı

        public double Score { get; set; }  // Sıralama puanı (dahili)

        /// <summary>Motor elektrik tüketimi (kW)</summary>
        public double ElectricalPower => PowerKW > 0 && Efficiency > 0
            ? PowerKW / Efficiency
            : PowerKW;
    }

    // Genişletilmiş Pompa Kataloğu (Wilo + Grundfos + DAB)
    private readonly List<PumpModel> _catalog = new()
    {
        // --- WILO Sirkülasyon (Küçük Sistemler) ---
        new() { Brand = "Wilo",    Series = "Stratos PICO", ModelName = "Stratos PICO 15/1-4",  MaxFlow = 2.5,  MaxHead = 4,   BepFlow = 1.5,  BepHead = 2.5,  Efficiency = 0.72, PowerKW = 0.025, Connection = "Rp ½\"",  Application = "Sirkülasyon" },
        new() { Brand = "Wilo",    Series = "Stratos PICO", ModelName = "Stratos PICO 25/1-4",  MaxFlow = 3.5,  MaxHead = 4,   BepFlow = 2.0,  BepHead = 3.0,  Efficiency = 0.75, PowerKW = 0.04,  Connection = "Rp 1\"",  Application = "Sirkülasyon" },
        new() { Brand = "Wilo",    Series = "Stratos PICO", ModelName = "Stratos PICO 25/1-6",  MaxFlow = 4.0,  MaxHead = 6,   BepFlow = 2.5,  BepHead = 4.5,  Efficiency = 0.78, PowerKW = 0.06,  Connection = "Rp 1\"",  Application = "Sirkülasyon" },
        new() { Brand = "Wilo",    Series = "Stratos MAXO", ModelName = "Stratos MAXO 25/0.5-8",MaxFlow = 5.0,  MaxHead = 8,   BepFlow = 3.0,  BepHead = 5.5,  Efficiency = 0.80, PowerKW = 0.10,  Connection = "Rp 1\"",  Application = "Sirkülasyon" },
        new() { Brand = "Wilo",    Series = "Stratos MAXO", ModelName = "Stratos MAXO 30/0.5-12",MaxFlow = 8.0, MaxHead = 12,  BepFlow = 5.0,  BepHead = 8.0,  Efficiency = 0.82, PowerKW = 0.20,  Connection = "Rp 1¼\"", Application = "Sirkülasyon" },

        // --- WILO Basınçlandırma (Orta Sistemler) ---
        new() { Brand = "Wilo",    Series = "CronoLine-IL", ModelName = "CronoLine-IL 32/160",  MaxFlow = 12.0, MaxHead = 16,  BepFlow = 8.0,  BepHead = 12.0, Efficiency = 0.78, PowerKW = 1.10,  Connection = "DN 32",   Application = "Basınçlandırma" },
        new() { Brand = "Wilo",    Series = "CronoLine-IL", ModelName = "CronoLine-IL 40/200",  MaxFlow = 18.0, MaxHead = 20,  BepFlow = 12.0, BepHead = 16.0, Efficiency = 0.80, PowerKW = 2.20,  Connection = "DN 40",   Application = "Basınçlandırma" },
        new() { Brand = "Wilo",    Series = "CronoLine-IL", ModelName = "CronoLine-IL 50/150",  MaxFlow = 25.0, MaxHead = 30,  BepFlow = 15.0, BepHead = 22.0, Efficiency = 0.85, PowerKW = 3.00,  Connection = "DN 50",   Application = "Basınçlandırma" },
        new() { Brand = "Wilo",    Series = "CronoTwin",    ModelName = "CronoTwin-DL-E 40/170",MaxFlow = 18.0, MaxHead = 45,  BepFlow = 10.0, BepHead = 30.0, Efficiency = 0.80, PowerKW = 4.00,  Connection = "DN 40",   Application = "Basınçlandırma" },

        // --- WILO Hidrofor (Büyük Sistemler) ---
        new() { Brand = "Wilo",    Series = "Helix FIRST",  ModelName = "Helix FIRST V 2203",   MaxFlow = 8.0,  MaxHead = 50,  BepFlow = 5.0,  BepHead = 35.0, Efficiency = 0.82, PowerKW = 2.20,  Connection = "DN 40",   Application = "Hidrofor" },
        new() { Brand = "Wilo",    Series = "Helix FIRST",  ModelName = "Helix FIRST V 3604",   MaxFlow = 12.0, MaxHead = 65,  BepFlow = 8.0,  BepHead = 48.0, Efficiency = 0.83, PowerKW = 4.00,  Connection = "DN 50",   Application = "Hidrofor" },
        new() { Brand = "Wilo",    Series = "Helix FIRST",  ModelName = "Helix FIRST V 5206",   MaxFlow = 18.0, MaxHead = 80,  BepFlow = 12.0, BepHead = 60.0, Efficiency = 0.85, PowerKW = 7.50,  Connection = "DN 65",   Application = "Hidrofor" },

        // --- GRUNDFOS Sirkülasyon ---
        new() { Brand = "Grundfos", Series = "ALPHA",   ModelName = "ALPHA1 25-40",         MaxFlow = 2.8,  MaxHead = 4,   BepFlow = 1.8,  BepHead = 2.8,  Efficiency = 0.70, PowerKW = 0.025, Connection = "Rp 1\"",  Application = "Sirkülasyon" },
        new() { Brand = "Grundfos", Series = "ALPHA",   ModelName = "ALPHA2 25-60",         MaxFlow = 3.5,  MaxHead = 6,   BepFlow = 2.2,  BepHead = 4.2,  Efficiency = 0.74, PowerKW = 0.045, Connection = "Rp 1\"",  Application = "Sirkülasyon" },
        new() { Brand = "Grundfos", Series = "MAGNA",   ModelName = "MAGNA1 25-40",         MaxFlow = 3.8,  MaxHead = 4,   BepFlow = 2.2,  BepHead = 3.0,  Efficiency = 0.72, PowerKW = 0.05,  Connection = "Rp 1\"",  Application = "Sirkülasyon" },
        new() { Brand = "Grundfos", Series = "MAGNA",   ModelName = "MAGNA3 32-120F",       MaxFlow = 12.0, MaxHead = 12,  BepFlow = 7.0,  BepHead = 8.5,  Efficiency = 0.82, PowerKW = 0.25,  Connection = "DN 32",   Application = "Sirkülasyon" },
        new() { Brand = "Grundfos", Series = "MAGNA",   ModelName = "MAGNA3 40-150F",       MaxFlow = 16.0, MaxHead = 15,  BepFlow = 10.0, BepHead = 11.0, Efficiency = 0.84, PowerKW = 0.55,  Connection = "DN 40",   Application = "Sirkülasyon" },

        // --- GRUNDFOS Basınçlandırma ---
        new() { Brand = "Grundfos", Series = "TP",      ModelName = "TP 32-120/2",          MaxFlow = 12.5, MaxHead = 13,  BepFlow = 8.0,  BepHead = 10.0, Efficiency = 0.78, PowerKW = 0.75,  Connection = "DN 32",   Application = "Basınçlandırma" },
        new() { Brand = "Grundfos", Series = "TP",      ModelName = "TP 40-180/2",          MaxFlow = 18.0, MaxHead = 18,  BepFlow = 12.0, BepHead = 14.0, Efficiency = 0.80, PowerKW = 1.50,  Connection = "DN 40",   Application = "Basınçlandırma" },
        new() { Brand = "Grundfos", Series = "CM",      ModelName = "CM 3-4",               MaxFlow = 4.0,  MaxHead = 28,  BepFlow = 2.5,  BepHead = 20.0, Efficiency = 0.76, PowerKW = 0.50,  Connection = "Rp 1\"",  Application = "Basınçlandırma" },
        new() { Brand = "Grundfos", Series = "CM",      ModelName = "CM 5-5",               MaxFlow = 6.5,  MaxHead = 35,  BepFlow = 4.0,  BepHead = 25.0, Efficiency = 0.78, PowerKW = 1.10,  Connection = "Rp 1¼\"", Application = "Basınçlandırma" },

        // --- GRUNDFOS Hidrofor ---
        new() { Brand = "Grundfos", Series = "CR",      ModelName = "CR 3-12",              MaxFlow = 4.5,  MaxHead = 45,  BepFlow = 3.0,  BepHead = 32.0, Efficiency = 0.80, PowerKW = 1.10,  Connection = "DN 25",   Application = "Hidrofor" },
        new() { Brand = "Grundfos", Series = "CR",      ModelName = "CR 5-16",              MaxFlow = 8.0,  MaxHead = 58,  BepFlow = 5.5,  BepHead = 42.0, Efficiency = 0.82, PowerKW = 2.20,  Connection = "DN 32",   Application = "Hidrofor" },
        new() { Brand = "Grundfos", Series = "CR",      ModelName = "CR 10-12",             MaxFlow = 15.0, MaxHead = 65,  BepFlow = 10.0, BepHead = 48.0, Efficiency = 0.84, PowerKW = 4.00,  Connection = "DN 40",   Application = "Hidrofor" },
        new() { Brand = "Grundfos", Series = "CR",      ModelName = "CR 15-8",              MaxFlow = 22.0, MaxHead = 52,  BepFlow = 15.0, BepHead = 38.0, Efficiency = 0.85, PowerKW = 5.50,  Connection = "DN 50",   Application = "Hidrofor" },

        // --- DAB ---
        new() { Brand = "DAB",     Series = "EVOSTA",   ModelName = "EVOSTA 2 40-70/130",   MaxFlow = 3.0,  MaxHead = 7,   BepFlow = 2.0,  BepHead = 5.0,  Efficiency = 0.73, PowerKW = 0.04,  Connection = "Rp 1½\"", Application = "Sirkülasyon" },
        new() { Brand = "DAB",     Series = "KDN",      ModelName = "KDN 40-200",           MaxFlow = 18.0, MaxHead = 22,  BepFlow = 12.0, BepHead = 17.0, Efficiency = 0.79, PowerKW = 2.20,  Connection = "DN 40",   Application = "Basınçlandırma" },
    };

    /*
       NE: Pompa Önerisi (RecommendPumps)
       NEDEN: Verilen debi ve basma yüksekliğine göre en uygun 3-5 pompayı BEP yakınlık algoritmasıyla seçer.
       
       ALGORİTMA:
       1. Kapasite kontrolü (MaxFlow >= Q, MaxHead >= Hm)
       2. BEP sapma skoru = |BepFlow - Q| / MaxFlow + |BepHead - Hm| / MaxHead  (Normalize edilmiş)
       3. Verim bonusu (Yüksek verim, düşük skor)
       4. En düşük skora sahip pompalar önerilir
    */
    public List<PumpModel> RecommendPumps(double requiredFlow, double requiredHead, string? preferredBrand = null, string? application = null)
    {
        var candidates = _catalog
            .Where(p => p.MaxFlow >= requiredFlow && p.MaxHead >= requiredHead);

        // Marka filtresi (opsiyonel)
        if (!string.IsNullOrEmpty(preferredBrand))
            candidates = candidates.Where(p => p.Brand.Equals(preferredBrand, StringComparison.OrdinalIgnoreCase));

        // Uygulama filtresi (opsiyonel)
        if (!string.IsNullOrEmpty(application))
            candidates = candidates.Where(p => p.Application.Contains(application, StringComparison.OrdinalIgnoreCase));

        return candidates
            .Select(p =>
            {
                // BEP sapma skoru (0 = mükemmel eşleşme)
                double flowDeviation = Math.Abs(p.BepFlow - requiredFlow) / p.MaxFlow;
                double headDeviation = Math.Abs(p.BepHead - requiredHead) / p.MaxHead;

                // Verim bonusu (yüksek verim = düşük skor)
                double efficiencyPenalty = (1.0 - p.Efficiency) * 0.5;

                // Aşırı boyutlandırma cezası (çok büyük pompa seçilmesin)
                double oversizePenalty = 0;
                if (p.MaxFlow > requiredFlow * 3) oversizePenalty += 0.3;
                if (p.MaxHead > requiredHead * 3) oversizePenalty += 0.2;

                p.Score = flowDeviation + headDeviation + efficiencyPenalty + oversizePenalty;
                return p;
            })
            .OrderBy(p => p.Score)
            .Take(5)
            .ToList();
    }

    /*
       NE: Tüm Markaları Listele
       NEDEN: UI'da ComboBox için marka filtresi
    */
    public List<string> GetAvailableBrands() => _catalog.Select(p => p.Brand).Distinct().OrderBy(b => b).ToList();

    /*
       NE: Katalog İstatistikleri
       NEDEN: Kullanıcıya katalog kapsamını göstermek
    */
    public (int TotalModels, int Brands, string FlowRange, string HeadRange) GetCatalogStats()
    {
        return (
            _catalog.Count,
            _catalog.Select(p => p.Brand).Distinct().Count(),
            $"{_catalog.Min(p => p.MaxFlow):F1} - {_catalog.Max(p => p.MaxFlow):F1} m³/h",
            $"{_catalog.Min(p => p.MaxHead):F0} - {_catalog.Max(p => p.MaxHead):F0} mSS"
        );
    }
}
