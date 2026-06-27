using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

public class HvacBomItem
{
    public string Category    { get; set; } = "";
    public string Description { get; set; } = "";
    public string Size        { get; set; } = "";
    public double Quantity    { get; set; }
    public string Unit        { get; set; } = "m";
    public double UnitPrice   { get; set; }
    public double TotalPrice  => Quantity * UnitPrice;
}

public class HvacBomResult
{
    public List<HvacBomItem> Items { get; set; } = new();
    public double TotalDuctLength { get; set; }
    public double TotalInsulationArea { get; set; }
    public int DuctCount { get; set; }
    public int FittingCount { get; set; }
    public double TotalCost { get; set; }
}

public class HvacBomService
{
    private readonly CadDatabase _database;

    public HvacBomService(CadDatabase database)
    {
        _database = database;
    }

    public HvacBomResult Generate()
    {
        var result = new HvacBomResult();
        var ducts = _database.GetAllEntities().OfType<DuctEntity>().ToList();

        result.DuctCount = ducts.Count;

        var groups = ducts.GroupBy(d => new { d.Shape, d.Type, Size = d.GetSizeText() });

        foreach (var g in groups)
        {
            double totalLen = g.Sum(d => d.GetLength()) / 1000.0;
            double totalInsul = g.Sum(d => d.GetInsulationArea());
            int elbows = Math.Max(0, g.Count() - 1);
            int tees = Math.Max(0, elbows / 3);

            result.Items.Add(new HvacBomItem
            {
                Category = "Kanal",
                Description = $"{g.Key.Shape} — {g.First().GetTypeText()}",
                Size = g.Key.Size,
                Quantity = Math.Round(totalLen, 2),
                Unit = "m",
                UnitPrice = GetDuctPrice(g.Key.Shape, g.Key.Size)
            });

            if (totalInsul > 0 && g.Any(d => d.InsulationMm > 0))
            {
                result.Items.Add(new HvacBomItem
                {
                    Category = "Izolasyon",
                    Description = $"Kanal izolasyonu ({g.First().InsulationMm}mm)",
                    Size = g.Key.Size,
                    Quantity = Math.Round(totalInsul, 2),
                    Unit = "m2",
                    UnitPrice = 45.0
                });
            }

            if (elbows > 0)
            {
                result.Items.Add(new HvacBomItem
                {
                    Category = "Fitting",
                    Description = $"90° Dirsek — {g.Key.Shape}",
                    Size = g.Key.Size,
                    Quantity = elbows,
                    Unit = "adet",
                    UnitPrice = GetFittingPrice(g.Key.Size)
                });
                result.FittingCount += elbows;
            }

            if (tees > 0)
            {
                result.Items.Add(new HvacBomItem
                {
                    Category = "Fitting",
                    Description = $"Te Parçası — {g.Key.Shape}",
                    Size = g.Key.Size,
                    Quantity = tees,
                    Unit = "adet",
                    UnitPrice = GetFittingPrice(g.Key.Size) * 1.5
                });
                result.FittingCount += tees;
            }

            result.TotalDuctLength += totalLen;
            result.TotalInsulationArea += totalInsul;
        }

        // Menfez ve damper tahmini
        int grillCount = Math.Max(1, ducts.Count / 2);
        result.Items.Add(new HvacBomItem
        {
            Category = "Menfez",
            Description = "Besleme/Dönüş Menfezi",
            Size = "Çeşitli",
            Quantity = grillCount,
            Unit = "adet",
            UnitPrice = 180.0
        });

        int damperCount = Math.Max(1, ducts.Count / 4);
        result.Items.Add(new HvacBomItem
        {
            Category = "Damper",
            Description = "Ayar Damperi",
            Size = "Çeşitli",
            Quantity = damperCount,
            Unit = "adet",
            UnitPrice = 250.0
        });

        result.TotalCost = result.Items.Sum(i => i.TotalPrice);
        return result;
    }

    public string ExportToHtml(HvacBomResult bom, string projectName = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>HVAC Metraj</title>");
        sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;margin:30px;color:#333}");
        sb.AppendLine("h1{color:#2ECC71;border-bottom:2px solid #2ECC71;padding-bottom:6px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:12px 0}");
        sb.AppendLine("th,td{border:1px solid #CCC;padding:6px 10px;text-align:left;font-size:12px}");
        sb.AppendLine("th{background:#E8F5E9;font-weight:bold}");
        sb.AppendLine("tr:nth-child(even){background:#F9F9F9}");
        sb.AppendLine(".total{font-weight:bold;background:#E8F5E9}");
        sb.AppendLine(".summary{display:flex;gap:20px;margin:15px 0}");
        sb.AppendLine(".card{background:#F5F5F5;border-radius:6px;padding:12px 18px;min-width:120px}");
        sb.AppendLine(".card .val{font-size:22px;font-weight:bold;color:#2ECC71}</style></head><body>");

        sb.AppendLine($"<h1>HVAC KANAL METRAJ TABLOSU</h1>");
        if (!string.IsNullOrEmpty(projectName))
            sb.AppendLine($"<p><b>Proje:</b> {projectName} | <b>Tarih:</b> {DateTime.Now:dd.MM.yyyy}</p>");

        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<div class='card'><div>Kanal Uzunluğu</div><div class='val'>{bom.TotalDuctLength:F1} m</div></div>");
        sb.AppendLine($"<div class='card'><div>Kanal Adedi</div><div class='val'>{bom.DuctCount}</div></div>");
        sb.AppendLine($"<div class='card'><div>Fitting Adedi</div><div class='val'>{bom.FittingCount}</div></div>");
        sb.AppendLine($"<div class='card'><div>İzolasyon Alanı</div><div class='val'>{bom.TotalInsulationArea:F1} m²</div></div>");
        sb.AppendLine($"<div class='card'><div>Toplam Maliyet</div><div class='val'>{bom.TotalCost:N0} TRY</div></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table><tr><th>Kategori</th><th>Açıklama</th><th>Boyut</th><th>Miktar</th><th>Birim</th><th>Birim Fiyat</th><th>Toplam</th></tr>");
        foreach (var item in bom.Items)
        {
            sb.AppendLine($"<tr><td>{item.Category}</td><td>{item.Description}</td><td>{item.Size}</td><td>{item.Quantity:F1}</td><td>{item.Unit}</td><td>{item.UnitPrice:N0}</td><td>{item.TotalPrice:N0}</td></tr>");
        }
        sb.AppendLine($"<tr class='total'><td colspan='6'>GENEL TOPLAM</td><td>{bom.TotalCost:N0} TRY</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine($"<p style='font-size:10px;color:#999'>AfneyCAD v4.0.0 — HVAC Metraj | {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public string ExportToCsv(HvacBomResult bom)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Kategori;Açıklama;Boyut;Miktar;Birim;Birim Fiyat;Toplam");
        foreach (var item in bom.Items)
            sb.AppendLine($"{item.Category};{item.Description};{item.Size};{item.Quantity:F1};{item.Unit};{item.UnitPrice:N0};{item.TotalPrice:N0}");
        sb.AppendLine($";;;;;;;TOPLAM;{bom.TotalCost:N0} TRY");
        return sb.ToString();
    }

    private static double GetDuctPrice(DuctShape shape, string size)
    {
        double basePrice = shape == DuctShape.Circular ? 120.0 : 95.0;
        if (size.Contains("x"))
        {
            var parts = size.Split('x');
            if (double.TryParse(parts[0], out double w))
                basePrice *= 1.0 + (w - 200) / 1000.0;
        }
        else if (size.StartsWith("D") && double.TryParse(size[1..], out double d))
        {
            basePrice *= 1.0 + (d - 200) / 1000.0;
        }
        return Math.Max(basePrice, 50);
    }

    private static double GetFittingPrice(string size) => GetDuctPrice(DuctShape.Rectangular, size) * 0.8;
}
