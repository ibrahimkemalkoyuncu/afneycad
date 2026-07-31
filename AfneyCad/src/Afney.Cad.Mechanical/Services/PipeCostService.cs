using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Boru Maliyeti Analizi Servisi (PipeCostService)
   NEDEN: Proje maliyetini erken aşamada tahmin etmek ve BOM'a birim fiyat eklemek için.
          Malzeme + işçilik + ek parça maliyetlerini içerir.

   FİYAT KAYNAĞI: Türkiye piyasası yaklaşık birim fiyatları (2024, KDV hariç)
   GÜNCELLEME: Fiyatlar enflasyona göre değişir; harici fiyat listesiyle override edilebilir.
*/
public class PipeCostService
{
    public enum PipeMaterial { Steel, Galvanized, PPR, CPVC, CopperType_K, CopperType_L, HDPE, CastIron }

    public class UnitPrice
    {
        public PipeMaterial Material   { get; set; }
        public double DiameterMm       { get; set; }  // nominal iç çap
        public double PricePerMeterTl  { get; set; }  // TL/m (malzeme)
        public double LaborPerMeterTl  { get; set; }  // TL/m (işçilik)
        public double FittingFactorPct { get; set; } = 25; // ek parçalar malzeme bedelinin %25'i
    }

    public class PipeCostItem
    {
        public string Id               { get; set; } = "";
        public string Description      { get; set; } = "";
        public PipeMaterial Material   { get; set; }
        public double DiameterMm       { get; set; }
        public double LengthM          { get; set; }
        public double MaterialCostTl   { get; set; }
        public double LaborCostTl      { get; set; }
        public double FittingCostTl    { get; set; }
        public double TotalCostTl      => MaterialCostTl + LaborCostTl + FittingCostTl;
        public string SystemType       { get; set; } = "";
    }

    public class ProjectCostResult
    {
        public List<PipeCostItem> Items        { get; set; } = [];
        public double TotalMaterialTl          { get; set; }
        public double TotalLaborTl             { get; set; }
        public double TotalFittingTl           { get; set; }
        public double TotalCostTl              { get; set; }
        public double ContingencyPct           { get; set; } = 10;
        public double ContingencyTl            { get; set; }
        public double GrandTotalTl             { get; set; }
        public Dictionary<string, double> BySystem { get; set; } = [];
        public List<string> Notes              { get; set; } = [];
    }

    // ── BİRİM FİYAT KATALOGU ───────────────────────────────────────────────────
    private readonly List<UnitPrice> _catalog = BuildDefaultCatalog();

    private static List<UnitPrice> BuildDefaultCatalog() =>
    [
        // Çelik boru (Sch 40, standart)
        new() { Material = PipeMaterial.Steel,      DiameterMm = 15,  PricePerMeterTl = 85,   LaborPerMeterTl = 45  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 20,  PricePerMeterTl = 110,  LaborPerMeterTl = 50  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 25,  PricePerMeterTl = 140,  LaborPerMeterTl = 55  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 32,  PricePerMeterTl = 180,  LaborPerMeterTl = 60  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 40,  PricePerMeterTl = 225,  LaborPerMeterTl = 70  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 50,  PricePerMeterTl = 290,  LaborPerMeterTl = 80  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 65,  PricePerMeterTl = 380,  LaborPerMeterTl = 95  },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 80,  PricePerMeterTl = 470,  LaborPerMeterTl = 110 },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 100, PricePerMeterTl = 620,  LaborPerMeterTl = 130 },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 125, PricePerMeterTl = 820,  LaborPerMeterTl = 155 },
        new() { Material = PipeMaterial.Steel,      DiameterMm = 150, PricePerMeterTl = 1050, LaborPerMeterTl = 185 },

        // PPR (PN20 sıcak su)
        new() { Material = PipeMaterial.PPR,        DiameterMm = 20,  PricePerMeterTl = 45,   LaborPerMeterTl = 35  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 25,  PricePerMeterTl = 65,   LaborPerMeterTl = 40  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 32,  PricePerMeterTl = 95,   LaborPerMeterTl = 45  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 40,  PricePerMeterTl = 135,  LaborPerMeterTl = 50  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 50,  PricePerMeterTl = 195,  LaborPerMeterTl = 60  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 63,  PricePerMeterTl = 290,  LaborPerMeterTl = 75  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 75,  PricePerMeterTl = 400,  LaborPerMeterTl = 90  },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 90,  PricePerMeterTl = 550,  LaborPerMeterTl = 105 },
        new() { Material = PipeMaterial.PPR,        DiameterMm = 110, PricePerMeterTl = 750,  LaborPerMeterTl = 125 },

        // Galvanizli çelik (soğuk su)
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 15,  PricePerMeterTl = 95,   LaborPerMeterTl = 50  },
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 20,  PricePerMeterTl = 125,  LaborPerMeterTl = 55  },
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 25,  PricePerMeterTl = 160,  LaborPerMeterTl = 60  },
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 32,  PricePerMeterTl = 210,  LaborPerMeterTl = 68  },
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 50,  PricePerMeterTl = 340,  LaborPerMeterTl = 85  },
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 80,  PricePerMeterTl = 560,  LaborPerMeterTl = 120 },
        new() { Material = PipeMaterial.Galvanized, DiameterMm = 100, PricePerMeterTl = 740,  LaborPerMeterTl = 145 },

        // HDPE (dış hat / atık su)
        new() { Material = PipeMaterial.HDPE,       DiameterMm = 50,  PricePerMeterTl = 55,   LaborPerMeterTl = 30  },
        new() { Material = PipeMaterial.HDPE,       DiameterMm = 63,  PricePerMeterTl = 75,   LaborPerMeterTl = 35  },
        new() { Material = PipeMaterial.HDPE,       DiameterMm = 90,  PricePerMeterTl = 130,  LaborPerMeterTl = 45  },
        new() { Material = PipeMaterial.HDPE,       DiameterMm = 110, PricePerMeterTl = 175,  LaborPerMeterTl = 55  },
        new() { Material = PipeMaterial.HDPE,       DiameterMm = 160, PricePerMeterTl = 340,  LaborPerMeterTl = 80  },
        new() { Material = PipeMaterial.HDPE,       DiameterMm = 200, PricePerMeterTl = 520,  LaborPerMeterTl = 100 },

        // Dökme demir (pis su kolonları)
        new() { Material = PipeMaterial.CastIron,   DiameterMm = 50,  PricePerMeterTl = 180,  LaborPerMeterTl = 65  },
        new() { Material = PipeMaterial.CastIron,   DiameterMm = 100, PricePerMeterTl = 380,  LaborPerMeterTl = 95  },
        new() { Material = PipeMaterial.CastIron,   DiameterMm = 150, PricePerMeterTl = 650,  LaborPerMeterTl = 135 },
    ];

    // ── API ────────────────────────────────────────────────────────────────────

    public List<UnitPrice> GetCatalog() => _catalog;

    /// <summary>Sistem tipi + iç çap (mm) için m başına malzeme+işçilik birim fiyatı (TL/m) döner.</summary>
    public double GetUnitPriceForPipe(string systemType, double innerDiameterMm)
    {
        var mat   = GuessMaterial(systemType);
        var price = FindClosestPrice(mat, innerDiameterMm);
        return price.PricePerMeterTl + price.LaborPerMeterTl;
    }

    /// <summary>Cihaz tipi için adet birim fiyatı (TL/adet) döner.</summary>
    public double GetUnitPriceForFixture(string fixtureType)
    {
        string s = fixtureType.ToLowerInvariant();
        if (s.Contains("klozet") || s.Contains("wc"))     return 3500;
        if (s.Contains("lavabo"))                          return 2200;
        if (s.Contains("duş")  || s.Contains("banyo"))    return 2800;
        if (s.Contains("evye") || s.Contains("mutfak"))   return 1800;
        if (s.Contains("pisuar"))                         return 2500;
        if (s.Contains("küvet"))                          return 4500;
        return 2000;
    }

    public void UpdatePrice(PipeMaterial material, double diameterMm, double pricePerMeter, double laborPerMeter)
    {
        var existing = _catalog.FirstOrDefault(p => p.Material == material &&
                                                     Math.Abs(p.DiameterMm - diameterMm) < 0.5);
        if (existing is not null)
        {
            existing.PricePerMeterTl = pricePerMeter;
            existing.LaborPerMeterTl = laborPerMeter;
        }
        else
        {
            _catalog.Add(new UnitPrice { Material = material, DiameterMm = diameterMm, PricePerMeterTl = pricePerMeter, LaborPerMeterTl = laborPerMeter });
        }
    }

    /*
       NE: Veritabanındaki tüm borulardan maliyet hesapla
       NEDEN: Tek tıkla proje boru maliyeti özeti üretmek için
    */
    public ProjectCostResult CalculateFromDatabase(CadDatabase database, double contingencyPct = 10)
    {
        var pipes = database.GetAllEntities().OfType<PipeEntity>().ToList();
        var items = new List<PipeCostItem>();

        foreach (var pipe in pipes)
        {
            var mat = GuessMaterial(pipe.SystemType.ToString());
            double dia = pipe.InnerDiameter; // mm
            var price = FindClosestPrice(mat, dia);

            /*
               MÜHENDİSLİK: `pipe.Length` (== GetLength() == StartPoint.DistanceTo(EndPoint))
               uygulama genelindeki evrensel iç birimle (mm) aynı — TIPKI BillOfMaterialsService'in
               satır 72'de `/ 1000.0` yaptığı gibi burada da m'ye çevrilmesi gerekiyordu, ama
               unutulmuştu. Sonuç: PricePerMeterTl (TL/METRE) doğrudan mm cinsinden bir uzunlukla
               çarpılıyordu — her maliyet 1000 KAT şişik çıkıyordu (ve Description'daki
               "{pipe.Length:F1} m" de 12.5m'lik bir boruyu "12500.0 m" olarak gösteriyordu).
               Bu, gerçekten UI'a bağlı (BOMDialog.xaml.cs → CalculateFromDatabase) canlı bir
               "sessizce yanlış sonuç" hatasıydı, dead code değil.
            */
            double lengthM = pipe.Length / 1000.0;

            double materialCost = price.PricePerMeterTl * lengthM;
            double laborCost    = price.LaborPerMeterTl  * lengthM;
            double fittingCost  = materialCost * price.FittingFactorPct / 100.0;

            items.Add(new PipeCostItem
            {
                Id           = pipe.Id.ToString(),
                Description  = $"{mat} DN{dia:F0} — {lengthM:F1} m",
                Material     = mat,
                DiameterMm   = dia,
                LengthM      = lengthM,
                MaterialCostTl = materialCost,
                LaborCostTl    = laborCost,
                FittingCostTl  = fittingCost,
                SystemType   = pipe.SystemType.ToString()
            });
        }

        return BuildResult(items, contingencyPct);
    }

    /*
       NE: Manuel boru listesinden maliyet hesapla
       NEDEN: Kullanıcı dialog'dan elle girilen borular için
    */
    public ProjectCostResult CalculateFromList(
        IEnumerable<(PipeMaterial Material, double DiameterMm, double LengthM, string Description, string System)> pipes,
        double contingencyPct = 10)
    {
        var items = new List<PipeCostItem>();
        int idx = 1;

        foreach (var (mat, dia, len, desc, sys) in pipes)
        {
            var price = FindClosestPrice(mat, dia);
            double materialCost = price.PricePerMeterTl * len;
            double laborCost    = price.LaborPerMeterTl  * len;
            double fittingCost  = materialCost * price.FittingFactorPct / 100.0;

            items.Add(new PipeCostItem
            {
                Id           = idx++.ToString(),
                Description  = string.IsNullOrWhiteSpace(desc) ? $"{mat} DN{dia:F0}" : desc,
                Material     = mat,
                DiameterMm   = dia,
                LengthM      = len,
                MaterialCostTl = materialCost,
                LaborCostTl    = laborCost,
                FittingCostTl  = fittingCost,
                SystemType   = sys
            });
        }

        return BuildResult(items, contingencyPct);
    }

    public string ExportToHtml(ProjectCostResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>Boru Maliyet Raporu — AfneyCAD</title>");
        sb.AppendLine("<style>body{font-family:Consolas,monospace;background:#1a1a2e;color:#eee;padding:20px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:12px}");
        sb.AppendLine("th{background:#005A9C;color:white;padding:6px 10px;text-align:left}");
        sb.AppendLine("td{padding:5px 10px;border-bottom:1px solid #444}");
        sb.AppendLine("tr:nth-child(even){background:#252540}.total{color:#FFD700;font-weight:bold}</style></head><body>");
        sb.AppendLine("<h2>BORU MALİYET ANALİZİ — AfneyCAD</h2>");
        sb.AppendLine("<table><tr><th>Açıklama</th><th>Sistem</th><th>Uzunluk (m)</th>");
        sb.AppendLine("<th>Malzeme (TL)</th><th>İşçilik (TL)</th><th>Ek Parça (TL)</th><th>Toplam (TL)</th></tr>");

        foreach (var item in result.Items)
        {
            sb.AppendLine($"<tr><td>{item.Description}</td><td>{item.SystemType}</td>");
            sb.AppendLine($"<td>{item.LengthM:F1}</td><td>{item.MaterialCostTl:N0}</td>");
            sb.AppendLine($"<td>{item.LaborCostTl:N0}</td><td>{item.FittingCostTl:N0}</td>");
            sb.AppendLine($"<td>{item.TotalCostTl:N0}</td></tr>");
        }

        sb.AppendLine($"<tr class='total'><td colspan='3'>ARA TOPLAM</td>");
        sb.AppendLine($"<td>{result.TotalMaterialTl:N0}</td><td>{result.TotalLaborTl:N0}</td>");
        sb.AppendLine($"<td>{result.TotalFittingTl:N0}</td><td>{result.TotalCostTl:N0}</td></tr>");
        sb.AppendLine($"<tr class='total'><td colspan='6'>Beklenmedik Giderler (%{result.ContingencyPct:F0})</td>");
        sb.AppendLine($"<td>{result.ContingencyTl:N0}</td></tr>");
        sb.AppendLine($"<tr class='total'><td colspan='6'>GENEL TOPLAM (KDV Hariç)</td>");
        sb.AppendLine($"<td>{result.GrandTotalTl:N0} TL</td></tr>");
        sb.AppendLine("</table>");

        foreach (var note in result.Notes)
            sb.AppendLine($"<p>{note}</p>");

        // Sistem bazlı özet
        if (result.BySystem.Count > 0)
        {
            sb.AppendLine("<h3>Sistem Bazlı Dağılım</h3><table><tr><th>Sistem</th><th>Toplam (TL)</th></tr>");
            foreach (var kv in result.BySystem.OrderByDescending(x => x.Value))
                sb.AppendLine($"<tr><td>{kv.Key}</td><td>{kv.Value:N0}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ── YARDIMCI ──────────────────────────────────────────────────────────────

    private ProjectCostResult BuildResult(List<PipeCostItem> items, double contingencyPct)
    {
        var result = new ProjectCostResult
        {
            Items            = items,
            TotalMaterialTl  = items.Sum(i => i.MaterialCostTl),
            TotalLaborTl     = items.Sum(i => i.LaborCostTl),
            TotalFittingTl   = items.Sum(i => i.FittingCostTl),
            ContingencyPct   = contingencyPct
        };

        result.TotalCostTl   = result.TotalMaterialTl + result.TotalLaborTl + result.TotalFittingTl;
        result.ContingencyTl = result.TotalCostTl * contingencyPct / 100.0;
        result.GrandTotalTl  = result.TotalCostTl + result.ContingencyTl;

        result.BySystem = items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.SystemType) ? "Diğer" : i.SystemType)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.TotalCostTl));

        result.Notes.Add($"Fiyatlar 2024 Türkiye piyasası yaklaşık değerleridir (KDV hariç).");
        result.Notes.Add($"İşçilik maliyeti bölgesel farklılık gösterebilir ±%30.");
        if (result.GrandTotalTl > 1_000_000)
            result.Notes.Add($"⚠ Büyük proje: KDV (%20) eklenerek {result.GrandTotalTl * 1.2:N0} TL toplam bütçe öngörülür.");

        return result;
    }

    private UnitPrice FindClosestPrice(PipeMaterial material, double diameterMm)
    {
        var candidates = _catalog.Where(p => p.Material == material).ToList();
        if (candidates.Count == 0)
            candidates = _catalog.Where(p => p.Material == PipeMaterial.Steel).ToList();

        return candidates.MinBy(p => Math.Abs(p.DiameterMm - diameterMm))
               ?? new UnitPrice { PricePerMeterTl = 200, LaborPerMeterTl = 60 };
    }

    private static PipeMaterial GuessMaterial(string systemType)
    {
        if (string.IsNullOrEmpty(systemType)) return PipeMaterial.Steel;
        string s = systemType.ToLowerInvariant();
        if (s.Contains("drain") || s.Contains("atık") || s.Contains("pis"))  return PipeMaterial.CastIron;
        if (s.Contains("hot")   || s.Contains("sıcak"))                       return PipeMaterial.PPR;
        if (s.Contains("gas")   || s.Contains("gaz")  || s.Contains("steam")) return PipeMaterial.Steel;
        if (s.Contains("fire")  || s.Contains("yangın"))                      return PipeMaterial.Galvanized;
        return PipeMaterial.Steel;
    }
}
