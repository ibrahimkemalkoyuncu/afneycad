using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Pompa Seçim Servisi (PumpSelectionService)
    NEDEN: Hesaplanan sistem debisi (Q) ve kritik hat basınç kaybına (Hm) göre Wilo, Grundfos gibi standart markalardan uygun pompa modelini seçmek için.
*/
public class PumpSelectionService
{
    public class PumpModel
    {
        public string Brand { get; set; } = "";
        public string ModelName { get; set; } = "";
        public double MaxFlow { get; set; } // m3/h
        public double MaxHead { get; set; } // mSS
        public double BepFlow { get; set; } // En verimli olduğu debi
        public double Efficiency { get; set; } = 0.70; // %70 varsayılan verim
        public string Power { get; set; } = "";
        public string Connection { get; set; } = "";
    }

    private readonly List<PumpModel> _catalog = new()
    {
        new PumpModel { Brand = "Wilo", ModelName = "Stratos PICO 25/1-4", MaxFlow = 3.5, MaxHead = 4, BepFlow = 2.0, Efficiency = 0.75, Power = "0.04 kW", Connection = "Rp 1\"" },
        new PumpModel { Brand = "Wilo", ModelName = "Stratos PICO 25/1-6", MaxFlow = 4.0, MaxHead = 6, BepFlow = 2.5, Efficiency = 0.78, Power = "0.06 kW", Connection = "Rp 1\"" },
        new PumpModel { Brand = "Grundfos", ModelName = "MAGNA1 25-40", MaxFlow = 3.8, MaxHead = 4, BepFlow = 2.2, Efficiency = 0.72, Power = "0.05 kW", Connection = "Rp 1\"" },
        new PumpModel { Brand = "Grundfos", ModelName = "MAGNA3 32-120F", MaxFlow = 12.0, MaxHead = 12, BepFlow = 7.0, Efficiency = 0.82, Power = "0.25 kW", Connection = "DN 32" },
        new PumpModel { Brand = "Wilo", ModelName = "CronoLine-IL 50/150", MaxFlow = 25.0, MaxHead = 30, BepFlow = 15.0, Efficiency = 0.85, Power = "3.0 kW", Connection = "DN 50" },
        new PumpModel { Brand = "Wilo", ModelName = "CronoTwin-DL-E 40/170", MaxFlow = 18.0, MaxHead = 45, BepFlow = 10.0, Efficiency = 0.80, Power = "4.0 kW", Connection = "DN 40" }
    };

    public List<PumpModel> RecommendPumps(double requiredFlow, double requiredHead)
    {
        // Gelişmiş Seçim Algoritması: 
        // 1. Kapasiteyi karşılayanları bul
        // 2. BEP'e (En verimli nokta) en yakın olanları seç
        return _catalog
            .Where(p => p.MaxFlow >= requiredFlow && p.MaxHead >= requiredHead)
            .OrderBy(p => Math.Abs(p.BepFlow - requiredFlow)) // Verimlilik önceliği
            .Take(3)
            .ToList();
    }
}
