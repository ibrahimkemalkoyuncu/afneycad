using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

// TS 1258 / Bayındırlık Birim Fiyat — Teknik Şartname Üreteci
public class TechnicalSpecificationService
{
    private readonly CadDatabase _database;

    // Bayındırlık poz numaraları ve teknik şartname maddeleri
    private static readonly Dictionary<string, SpecificationItem> PozTable = new()
    {
        ["PPR_DN20"] = new("04.401/1", "PP-R PN20 Ø20 mm boru", "m", 85.0,
            "PP-R PN20 basınç sınıfında, Ø20 mm dış çaplı temiz su borusu. " +
            "TS EN ISO 15874-2 standardına uygun, SDR 6, et kalınlığı 3.4 mm. " +
            "Maksimum çalışma sıcaklığı 70°C, çalışma basıncı 20 bar."),
        ["PPR_DN25"] = new("04.401/2", "PP-R PN20 Ø25 mm boru", "m", 110.0,
            "PP-R PN20 basınç sınıfında, Ø25 mm dış çaplı temiz su borusu. SDR 6, et kalınlığı 4.2 mm."),
        ["PPR_DN32"] = new("04.401/3", "PP-R PN20 Ø32 mm boru", "m", 145.0,
            "PP-R PN20 Ø32 mm. SDR 6, et kalınlığı 5.4 mm."),
        ["PPR_DN40"] = new("04.401/4", "PP-R PN20 Ø40 mm boru", "m", 195.0,
            "PP-R PN20 Ø40 mm. SDR 6, et kalınlığı 6.7 mm."),
        ["PPR_DN50"] = new("04.401/5", "PP-R PN20 Ø50 mm boru", "m", 280.0,
            "PP-R PN20 Ø50 mm. SDR 6, et kalınlığı 8.3 mm."),
        ["PPR_DN63"] = new("04.401/6", "PP-R PN20 Ø63 mm boru", "m", 380.0,
            "PP-R PN20 Ø63 mm. SDR 6, et kalınlığı 10.5 mm."),
        ["PVC_DN50"] = new("04.451/1", "PVC SN4 Ø50 mm pis su borusu", "m", 65.0,
            "Sert PVC pis su borusu, TS EN 1329-1, SN4, Ø50 mm. Contalı muf birleştirme."),
        ["PVC_DN75"] = new("04.451/2", "PVC SN4 Ø75 mm pis su borusu", "m", 95.0,
            "Sert PVC pis su borusu, TS EN 1329-1, SN4, Ø75 mm."),
        ["PVC_DN100"] = new("04.451/3", "PVC SN4 Ø100 mm pis su borusu", "m", 135.0,
            "Sert PVC pis su borusu, TS EN 1329-1, SN4, Ø100 mm."),
        ["PVC_DN125"] = new("04.451/4", "PVC SN4 Ø125 mm pis su borusu", "m", 185.0,
            "Sert PVC pis su borusu, TS EN 1329-1, SN4, Ø125 mm."),
        ["PVC_DN150"] = new("04.451/5", "PVC SN4 Ø150 mm pis su borusu", "m", 250.0,
            "Sert PVC pis su borusu, TS EN 1329-1, SN4, Ø150 mm."),
        ["GALV_DN15"] = new("04.301/1", "Galvaniz boru DN15", "m", 120.0,
            "Galvanizli çelik boru, TS EN 10255, DN15 (1/2\"), vidalı birleştirme."),
        ["GALV_DN20"] = new("04.301/2", "Galvaniz boru DN20", "m", 155.0,
            "Galvanizli çelik boru, TS EN 10255, DN20 (3/4\"), vidalı birleştirme."),
        ["GALV_DN25"] = new("04.301/3", "Galvaniz boru DN25", "m", 195.0,
            "Galvanizli çelik boru, TS EN 10255, DN25 (1\"), vidalı birleştirme."),
        ["LAVABO"] = new("04.601/1", "Lavabo (vitrifiye)", "ad", 850.0,
            "I. sınıf vitrifiye lavabo, TS 549, ayaklı veya asma tip. Sifon ve batarya dahil."),
        ["KLOZET"] = new("04.601/2", "Klozet (gömme rezervuarlı)", "ad", 1650.0,
            "I. sınıf vitrifiye klozet, gömme rezervuar dahil. TS 7834, 6 litre."),
        ["DUS_TEKNESI"] = new("04.601/3", "Duş teknesi (akrilik)", "ad", 1200.0,
            "Akrilik duş teknesi, 80x80 veya 90x90. TS EN 14527, sifon dahil."),
        ["KUVET"] = new("04.601/4", "Küvet (akrilik)", "ad", 2800.0,
            "Akrilik küvet, 160x70 veya 170x75. TS EN 14516, sifon dahil."),
        ["EVIYE"] = new("04.601/5", "Mutfak eviyesi (paslanmaz)", "ad", 950.0,
            "Paslanmaz çelik mutfak eviyesi, tek gözlü. TS EN 13310, sifon dahil."),
    };

    public TechnicalSpecificationService(CadDatabase database) => _database = database;

    public TechnicalSpecResult Generate(string projectName = "")
    {
        var result = new TechnicalSpecResult { ProjectName = projectName };
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();

        // Boruları grupla ve poz eşle
        var pipeGroups = pipes.GroupBy(p => GetPipePozKey(p));
        foreach (var group in pipeGroups)
        {
            if (PozTable.TryGetValue(group.Key, out var spec))
            {
                double totalLength = group.Sum(p => p.GetLength()) / 1000.0; // mm→m
                result.Items.Add(new SpecResultItem
                {
                    PozNo = spec.PozNo,
                    Description = spec.Description,
                    Unit = spec.Unit,
                    Quantity = Math.Round(totalLength, 2),
                    UnitPrice = spec.UnitPrice,
                    TotalPrice = Math.Round(totalLength * spec.UnitPrice, 2),
                    TechnicalSpec = spec.TechnicalText
                });
            }
        }

        // Cihazları grupla ve poz eşle
        var fixtureGroups = fixtures.GroupBy(f => GetFixturePozKey(f));
        foreach (var group in fixtureGroups)
        {
            if (PozTable.TryGetValue(group.Key, out var spec))
            {
                result.Items.Add(new SpecResultItem
                {
                    PozNo = spec.PozNo,
                    Description = spec.Description,
                    Unit = spec.Unit,
                    Quantity = group.Count(),
                    UnitPrice = spec.UnitPrice,
                    TotalPrice = group.Count() * spec.UnitPrice,
                    TechnicalSpec = spec.TechnicalText
                });
            }
        }

        result.GrandTotal = result.Items.Sum(i => i.TotalPrice);
        return result;
    }

    public string ExportToHtml(TechnicalSpecResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>Teknik Şartname</title>");
        sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;font-size:11pt;margin:20mm}");
        sb.AppendLine("h1{color:#1a3a5c;text-align:center}h2{color:#2a5a8c}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:10px 0}");
        sb.AppendLine("th{background:#1a3a5c;color:white;padding:8px}td{border:1px solid #ddd;padding:6px}");
        sb.AppendLine("tr:nth-child(even){background:#f5f5f5}.spec{font-size:10pt;color:#555;font-style:italic}</style></head><body>");

        sb.AppendLine($"<h1>TEKNİK ŞARTNAME — {result.ProjectName}</h1>");
        sb.AppendLine($"<p>Tarih: {DateTime.Now:dd.MM.yyyy} | Toplam Kalem: {result.Items.Count}</p>");

        sb.AppendLine("<h2>MALZEME LİSTESİ VE TEKNİK ÖZELLİKLER</h2>");
        sb.AppendLine("<table><tr><th>Poz No</th><th>Tanım</th><th>Birim</th><th>Miktar</th><th>B.Fiyat (TL)</th><th>Toplam (TL)</th></tr>");

        foreach (var item in result.Items)
        {
            sb.AppendLine($"<tr><td>{item.PozNo}</td><td>{item.Description}<br><span class='spec'>{item.TechnicalSpec}</span></td><td>{item.Unit}</td><td>{item.Quantity:F2}</td><td>{item.UnitPrice:N0}</td><td>{item.TotalPrice:N0}</td></tr>");
        }

        sb.AppendLine($"<tr style='font-weight:bold;background:#e8f0fe'><td colspan='5'>GENEL TOPLAM</td><td>{result.GrandTotal:N0} TL</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>GENEL TEKNİK ŞARTLAR</h2>");
        sb.AppendLine("<ol>");
        sb.AppendLine("<li>Tüm borular TS EN ISO 15874 (PP-R) veya TS EN 1329 (PVC) standartlarına uygun olacaktır.</li>");
        sb.AppendLine("<li>Birleştirme işlemlerinde polifüzyon kaynak (PP-R) veya contalı muf (PVC) yöntemi kullanılacaktır.</li>");
        sb.AppendLine("<li>Temiz su tesisatı minimum 10 bar basınç testine tabi tutulacak, 2 saat boyunca basınç düşüşü izlenecektir.</li>");
        sb.AppendLine("<li>Pis su tesisatı su ile doldurularak sızdırmazlık testi yapılacaktır.</li>");
        sb.AppendLine("<li>Sıcak su borularında TS EN ISO 11855 uyarınca yalıtım uygulanacaktır (min 13 mm elastomerik).</li>");
        sb.AppendLine("<li>Tüm vitrifiyelerde TS 549 / TS 7834 belgeli I. sınıf ürünler kullanılacaktır.</li>");
        sb.AppendLine("<li>İşçilik ve montaj, ilgili TSE ve Bayındırlık birim fiyat tarifelerine uygun yapılacaktır.</li>");
        sb.AppendLine("</ol>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string GetPipePozKey(PipeEntity pipe)
    {
        string material = pipe.PipeMaterialType switch
        {
            PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => "PPR",
            PipeMaterial.PVC_SN4 => "PVC",
            PipeMaterial.Steel_Galvanized => "GALV",
            _ => "PPR"
        };

        int dn = (int)Math.Round(pipe.InnerDiameter);
        // Standart DN'e yuvarla
        int[] sizes = { 15, 20, 25, 32, 40, 50, 63, 75, 100, 125, 150 };
        int closest = sizes.OrderBy(s => Math.Abs(s - dn)).First();

        return $"{material}_DN{closest}";
    }

    private string GetFixturePozKey(SanitaryFixtureEntity fixture)
    {
        string type = fixture.FixtureType?.ToUpperInvariant() ?? "";
        if (type.Contains("WC") || type.Contains("KLOZET") || type.Contains("TOILET")) return "KLOZET";
        if (type.Contains("LAVABO") || type.Contains("WASHBASIN")) return "LAVABO";
        if (type.Contains("DUŞ") || type.Contains("DUS") || type.Contains("SHOWER")) return "DUS_TEKNESI";
        if (type.Contains("KÜVET") || type.Contains("KUVET") || type.Contains("BATHTUB")) return "KUVET";
        if (type.Contains("EVİYE") || type.Contains("EVIYE") || type.Contains("SINK")) return "EVIYE";
        return "LAVABO";
    }
}

public record SpecificationItem(string PozNo, string Description, string Unit, double UnitPrice, string TechnicalText);

public class TechnicalSpecResult
{
    public string ProjectName { get; set; } = "";
    public List<SpecResultItem> Items { get; set; } = new();
    public double GrandTotal { get; set; }
}

public class SpecResultItem
{
    public string PozNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double TotalPrice { get; set; }
    public string TechnicalSpec { get; set; } = "";
}
