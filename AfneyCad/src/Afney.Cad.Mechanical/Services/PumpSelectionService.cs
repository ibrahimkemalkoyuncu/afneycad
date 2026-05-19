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
    private readonly List<PumpModel> _catalog = new();
    private readonly string _catalogFilePath;

    public PumpSelectionService()
    {
        string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Catalogs");
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        _catalogFilePath = System.IO.Path.Combine(dir, "Pumps.json");
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        if (System.IO.File.Exists(_catalogFilePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(_catalogFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<List<PumpModel>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    _catalog.Clear();
                    _catalog.AddRange(loaded);
                    return;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Pompa kataloğu JSON okuma hatası. Varsayılanlar yüklenecek.");
            }
        }

        // Eğer dosya yoksa veya hatalıysa varsayılanları yükle ve dosyaya yaz
        LoadDefaults();
        SaveCatalog();
    }

    private void SaveCatalog()
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(_catalog, options);
            System.IO.File.WriteAllText(_catalogFilePath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Pompa kataloğu JSON yazma hatası.");
        }
    }

    private void LoadDefaults()
    {
        _catalog.Clear();
        _catalog.AddRange(new List<PumpModel>
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
            new() { Brand = "DAB",     Series = "KDN",      ModelName = "KDN 40-200",           MaxFlow = 18.0, MaxHead = 22,  BepFlow = 12.0, BepHead = 17.0, Efficiency = 0.79, PowerKW = 2.20,  Connection = "DN 40",   Application = "Basınçlandırma" }
        });
    }

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

    /*
       NE: Pompa Q-H Karakteristik Eğrisi (GetPumpCurvePoints)
       NEDEN: FINE SANI'deki pompa eğrisi grafiğine eşdeğer.
              Gerçek pompa eğrisi katalog verisi gerektirir; burada BEP + MaxFlow/MaxHead
              üç noktalı ikinci dereceden parabol ile modellenir.

       FORMÜL: H = a*Q² + b*Q + c  (a<0, c = H_shutoff, BEP noktası geçiyor)
       DÖNDÜRÜR: (Q m³/h, H mSS) çift listesi — 20 nokta, 0..MaxFlow aralığı
    */
    public List<(double FlowM3h, double HeadMSS)> GetPumpCurvePoints(PumpModel pump, int pointCount = 20)
    {
        // Kapatma yüksekliği: MaxHead * 1.15 (tipik santrifüj pompa)
        double hShutoff = pump.MaxHead * 1.15;
        double hBep     = pump.BepHead;
        double qBep     = pump.BepFlow;
        double hZero    = 0.0;  // Q = MaxFlow'da H ≈ 0

        // Üç nokta: (0, hShutoff), (qBep, hBep), (qMax, 0)
        // H = a*Q² + b*Q + c  →  c = hShutoff
        // Diğer iki denklem:
        //   a*qBep² + b*qBep = hBep - hShutoff
        //   a*qMax² + b*qMax = -hShutoff
        double qMax = pump.MaxFlow;
        double c = hShutoff;
        // Matris çözümü (2x2):
        //  [qBep²  qBep ] [a]   [hBep - c]
        //  [qMax²  qMax ] [b] = [-c      ]
        double det = qBep * qBep * qMax - qMax * qMax * qBep;
        double a, b;
        if (Math.Abs(det) < 1e-9)
        {
            // Çözümsüz → basit lineer yaklaşım
            a = 0;
            b = qMax > 0 ? -hShutoff / qMax : 0;
        }
        else
        {
            a = (( hBep - c) * qMax - (-c) * qBep) / det;
            b = ((-c) * qBep * qBep - (hBep - c) * qMax * qMax) / det;
        }

        var points = new List<(double, double)>(pointCount);
        for (int i = 0; i <= pointCount; i++)
        {
            double q = qMax * i / pointCount;
            double h = a * q * q + b * q + c;
            if (h < 0) h = 0;
            points.Add((q, h));
        }
        return points;
    }

    /*
       NE: Sistem Eğrisi Noktaları (GetSystemCurvePoints)
       NEDEN: Boru sistemi direncinin Q'ya bağlı değişimini göstermek.

       FORMÜL: H_sistem = H_statik + R * Q²
         H_statik: Pompalama yüksekliği (statik head) — m
         R: Sistem direnci katsayısı — (H_tasarım - H_statik) / Q_tasarım²
    */
    public List<(double FlowM3h, double HeadMSS)> GetSystemCurvePoints(
        double staticHead, double designFlow, double designHead, int pointCount = 20)
    {
        double r = designFlow > 0 ? (designHead - staticHead) / (designFlow * designFlow) : 0;
        double qMax = designFlow * 1.5;
        var points = new List<(double, double)>(pointCount);
        for (int i = 0; i <= pointCount; i++)
        {
            double q = qMax * i / pointCount;
            double h = staticHead + r * q * q;
            points.Add((q, h));
        }
        return points;
    }

    /*
       NE: Çalışma Noktası Hesabı (CalculateDutyPoint)
       NEDEN: Pompa eğrisi ile sistem eğrisinin kesişimi = gerçek çalışma noktası.
              FINE SANI'deki "OP" (Operating Point) göstergesi.

       YÖNTEM: Binary search — pompa H > sistem H iken sol taraf, aksi halde sağ taraf
    */
    public (double FlowM3h, double HeadMSS, bool IsInRange) CalculateDutyPoint(
        PumpModel pump, double staticHead, double designFlow, double designHead)
    {
        var pumpCurve   = GetPumpCurvePoints(pump, 200);
        var systemCurve = GetSystemCurvePoints(staticHead, designFlow, designHead, 200);

        // Her Q değerinde (pompa H - sistem H) işaret değişimini bul
        double prevDiff = double.NaN;
        double opQ = 0, opH = 0;
        bool found = false;

        for (int i = 0; i < pumpCurve.Count && i < systemCurve.Count; i++)
        {
            double diff = pumpCurve[i].HeadMSS - systemCurve[i].HeadMSS;
            if (!double.IsNaN(prevDiff) && prevDiff * diff < 0)
            {
                // Lineer interpolasyon
                double q1 = pumpCurve[i - 1].FlowM3h, h1p = pumpCurve[i - 1].HeadMSS, h1s = systemCurve[i - 1].HeadMSS;
                double q2 = pumpCurve[i].FlowM3h,     h2p = pumpCurve[i].HeadMSS,     h2s = systemCurve[i].HeadMSS;
                double denom = (h1p - h1s) - (h2p - h2s);
                opQ = Math.Abs(denom) > 1e-9 ? q1 + (q1 - q2) * (h1p - h1s) / denom : (q1 + q2) / 2;
                opH = staticHead + (designHead - staticHead) / (designFlow * designFlow) * opQ * opQ;
                found = true;
                break;
            }
            prevDiff = diff;
        }

        bool inRange = found && opQ >= pump.BepFlow * 0.7 && opQ <= pump.BepFlow * 1.3;
        return (opQ, opH, inRange);
    }

    /*
       NE: Kavitasyon Kontrolü (CheckCavitation)
       NEDEN: NPSHa (Available) < NPSHr (Required) ise pompa kavite eder — gürültü ve hasar.

       FORMÜL:
         NPSHa = (P_atm + P_tank - P_vapor) / ρg  + z_s  - hf_s
           P_atm = 10.33 mSS (deniz seviyesi)
           P_vapor ≈ 0.24 mSS (20°C su)
           z_s = emme yüksekliği (negatif = serbest yüzey altında)
           hf_s = emme hattı basınç kaybı (mSS)
         NPSHr = katalog değeri (basit model: MaxHead * 0.03 + 0.5)
    */
    public CavitationCheckResult CheckCavitation(
        PumpModel pump, double suctionHeightM, double suctionLossMSS, double waterTempC = 20.0)
    {
        double pAtm    = 10.33;
        double pVapor  = WaterVaporPressureMSS(waterTempC);
        double npsHa   = pAtm - pVapor + suctionHeightM - suctionLossMSS;
        double npsHr   = pump.MaxHead * 0.03 + 0.5; // Basit katalog modeli
        double margin  = npsHa - npsHr;
        bool isSafe    = margin >= 0.5; // NPSH marjı ≥ 0.5 mSS önerilir

        return new CavitationCheckResult
        {
            NPSHa     = npsHa,
            NPSHr     = npsHr,
            Margin    = margin,
            IsSafe    = isSafe,
            WaterTemp = waterTempC,
            Recommendation = isSafe
                ? "Kavitasyon riski yok. Emme hattı uygun."
                : $"UYARI: NPSHa ({npsHa:F2} mSS) < NPSHr ({npsHr:F2} mSS) + 0.5 marj! Emme yüksekliği azaltın veya kayıpları düşürün."
        };
    }

    private static double WaterVaporPressureMSS(double tempC)
    {
        // Antoine yaklaşımı: P_sat (kPa), 0-100°C
        double logP = 8.07131 - 1730.63 / (233.426 + tempC);
        double pKPa = Math.Pow(10, logP) * 0.133322; // mmHg → kPa
        return pKPa / 9.80665; // kPa → mSS
    }
}

// --- POMPA HESAP SONUÇ MODELLERİ ---

public class CavitationCheckResult
{
    public double NPSHa { get; set; }
    public double NPSHr { get; set; }
    public double Margin { get; set; }
    public bool IsSafe { get; set; }
    public double WaterTemp { get; set; }
    public string Recommendation { get; set; } = "";
}
