using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

public class TechnicalSpecConfig
{
    public string ProjectName    { get; set; } = "";
    public string CompanyName    { get; set; } = "";
    public string EngineerName   { get; set; } = "";
    public string Date           { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
    public string Standard       { get; set; } = "TS 11154 / TS EN 806";
    public bool   IncludeBOM     { get; set; } = true;
    public bool   IncludeMontaj  { get; set; } = true;
    public bool   IncludeCost    { get; set; } = true;
}

public class TechnicalSpecService
{
    private readonly CadDatabase _database;

    public TechnicalSpecService(CadDatabase database)
    {
        _database = database;
    }

    public string GenerateHtml(TechnicalSpecConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>Teknik Şartname</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;margin:40px;color:#333}");
        sb.AppendLine("h1{color:#0066CC;border-bottom:2px solid #0066CC;padding-bottom:8px}");
        sb.AppendLine("h2{color:#004499;margin-top:30px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:10px 0}");
        sb.AppendLine("th,td{border:1px solid #CCC;padding:6px 10px;text-align:left;font-size:13px}");
        sb.AppendLine("th{background:#E8F0FE;font-weight:bold}");
        sb.AppendLine(".header{display:flex;justify-content:space-between;margin-bottom:20px}");
        sb.AppendLine(".note{background:#FFF8E1;border-left:4px solid #FFC107;padding:10px;margin:10px 0;font-size:13px}");
        sb.AppendLine("</style></head><body>");

        WriteHeader(sb, cfg);
        WriteProjectSummary(sb, cfg);
        WritePipeSpec(sb);
        WriteFixtureSpec(sb);
        if (cfg.IncludeMontaj) WriteMontajNotes(sb);
        if (cfg.IncludeBOM) WriteBOM(sb);
        if (cfg.IncludeCost) WriteCostSummary(sb);
        WriteStandardReferences(sb, cfg);
        WriteFooter(sb, cfg);

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private void WriteHeader(StringBuilder sb, TechnicalSpecConfig cfg)
    {
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<div><h1>TEKNİK ŞARTNAME</h1><p><b>{cfg.ProjectName}</b></p></div>");
        sb.AppendLine($"<div style='text-align:right'><p>{cfg.CompanyName}</p><p>Tarih: {cfg.Date}</p><p>Mühendis: {cfg.EngineerName}</p></div>");
        sb.AppendLine("</div>");
    }

    private void WriteProjectSummary(StringBuilder sb, TechnicalSpecConfig cfg)
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
        double totalLength = pipes.Sum(p => p.GetLength()) / 1000.0;

        sb.AppendLine("<h2>1. PROJE ÖZETİ</h2>");
        sb.AppendLine("<table><tr><th>Parametre</th><th>Değer</th></tr>");
        sb.AppendLine($"<tr><td>Toplam Boru Uzunluğu</td><td>{totalLength:F1} metre</td></tr>");
        sb.AppendLine($"<tr><td>Boru Adedi</td><td>{pipes.Count}</td></tr>");
        sb.AppendLine($"<tr><td>Cihaz Adedi</td><td>{fixtures.Count}</td></tr>");
        sb.AppendLine($"<tr><td>Uygulanacak Standart</td><td>{cfg.Standard}</td></tr>");

        var systems = pipes.GroupBy(p => p.SystemType).Select(g => g.Key.ToString()).ToList();
        sb.AppendLine($"<tr><td>Sistem Tipleri</td><td>{string.Join(", ", systems)}</td></tr>");
        sb.AppendLine("</table>");
    }

    private void WritePipeSpec(StringBuilder sb)
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var groups = pipes.GroupBy(p => new { p.SystemType, DN = (int)p.InnerDiameter }).ToList();

        sb.AppendLine("<h2>2. BORU ÖZELLİKLERİ</h2>");
        sb.AppendLine("<table><tr><th>Sistem</th><th>DN (mm)</th><th>Adet</th><th>Toplam Uzunluk (m)</th></tr>");
        foreach (var g in groups.OrderBy(g => g.Key.SystemType.ToString()))
        {
            double len = g.Sum(p => p.GetLength()) / 1000.0;
            sb.AppendLine($"<tr><td>{g.Key.SystemType}</td><td>{g.Key.DN}</td><td>{g.Count()}</td><td>{len:F2}</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    private void WriteFixtureSpec(StringBuilder sb)
    {
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
        var groups = fixtures.GroupBy(f => f.FixtureType).ToList();

        sb.AppendLine("<h2>3. CİHAZ ÖZELLİKLERİ</h2>");
        sb.AppendLine("<table><tr><th>Cihaz Tipi</th><th>Adet</th><th>DU</th><th>Soğuk Su</th><th>Sıcak Su</th><th>Gider DN</th></tr>");
        foreach (var g in groups)
        {
            var sample = g.First();
            var ports = sample.GetPorts();
            string cold = ports.Any(p => p.Name == "ColdWater") ? "Var" : "-";
            string hot = ports.Any(p => p.Name == "HotWater") ? "Var" : "-";
            string drain = ports.FirstOrDefault(p => p.Name == "Drainage")?.Diameter.ToString("F0") ?? "-";
            sb.AppendLine($"<tr><td>{g.Key}</td><td>{g.Count()}</td><td>{sample.FixtureUnit:F1}</td><td>{cold}</td><td>{hot}</td><td>DN{drain}</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    private void WriteMontajNotes(StringBuilder sb)
    {
        sb.AppendLine("<h2>4. MONTAJ NOTLARI</h2>");
        sb.AppendLine("<div class='note'><b>Genel Kurallar:</b></div>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li>Tüm borular basınç testinden geçirilecektir (min. 10 bar, 30 dakika).</li>");
        sb.AppendLine("<li>PPRC borular termofizyonel kaynak ile birleştirilecektir.</li>");
        sb.AppendLine("<li>PVC pis su boruları yapıştırma veya contalı birleşim ile monte edilecektir.</li>");
        sb.AppendLine("<li>Yatay pis su boruları min. %2 eğimle döşenecektir.</li>");
        sb.AppendLine("<li>Kolon boruları her katta kelepçe ile sabitlenecektir.</li>");
        sb.AppendLine("<li>Sıcak su boruları 9mm kalınlığında elastomerik yalıtım ile kaplanacaktır.</li>");
        sb.AppendLine("<li>Yangın tesisatı boruları kırmızı renk ile boyanacaktır.</li>");
        sb.AppendLine("<li>Tüm vana ve armatürler bakım yapılabilir konumda monte edilecektir.</li>");
        sb.AppendLine("<li>Gömme tesisatta boru geçişleri manşon içinden yapılacaktır.</li>");
        sb.AppendLine("<li>Deprem bölgelerinde esnek bağlantı elemanları kullanılacaktır.</li>");
        sb.AppendLine("</ul>");
    }

    private void WriteBOM(StringBuilder sb)
    {
        sb.AppendLine("<h2>5. MALZEME LİSTESİ (BOM)</h2>");
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var groups = pipes.GroupBy(p => new { p.SystemType, DN = (int)p.InnerDiameter });

        sb.AppendLine("<table><tr><th>Malzeme</th><th>DN</th><th>Uzunluk (m)</th><th>Dirsek (adet)</th><th>Te (adet)</th></tr>");
        foreach (var g in groups)
        {
            double len = g.Sum(p => p.GetLength()) / 1000.0;
            int elbows = g.Count() - 1;
            int tees = Math.Max(0, elbows / 3);
            sb.AppendLine($"<tr><td>{g.Key.SystemType}</td><td>DN{g.Key.DN}</td><td>{len:F2}</td><td>{elbows}</td><td>{tees}</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    private void WriteCostSummary(StringBuilder sb)
    {
        var costSvc = new RealTimeCostService();
        var cost = costSvc.CalculateProjectCost(_database);

        sb.AppendLine("<h2>6. MALİYET ÖZETİ</h2>");
        sb.AppendLine("<table><tr><th>Kalem</th><th>Tutar (TRY)</th></tr>");
        sb.AppendLine($"<tr><td>Boru Malzemesi</td><td>{cost.PipeCost:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Fitting / Bağlantı</td><td>{cost.FittingCost:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Vitrifiye / Cihaz</td><td>{cost.FixtureCost:N0}</td></tr>");
        sb.AppendLine($"<tr><td>İşçilik (%{35})</td><td>{cost.LaborCost:N0}</td></tr>");
        sb.AppendLine($"<tr style='font-weight:bold;background:#E8F0FE'><td>GENEL TOPLAM</td><td>{cost.TotalCost:N0} TRY</td></tr>");
        sb.AppendLine("</table>");
    }

    private void WriteStandardReferences(StringBuilder sb, TechnicalSpecConfig cfg)
    {
        sb.AppendLine("<h2>7. STANDART REFERANSLARI</h2>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li><b>TS 11154:</b> Bina İçi Su Tesisatı — Tasarım ve Uygulama Kuralları</li>");
        sb.AppendLine("<li><b>TS EN 806:</b> Bina İçi İçme Suyu Dağıtım Tesisatı</li>");
        sb.AppendLine("<li><b>TS EN 12056:</b> Bina İçi Atık Su Tahliye Tesisatı</li>");
        sb.AppendLine("<li><b>TS 1258:</b> Su Tesisatında Kullanılan Bağlantı Parçaları</li>");
        sb.AppendLine("<li><b>TS EN 1717:</b> Geri Akış Önleme Cihazları</li>");
        sb.AppendLine("<li><b>TS EN 13831:</b> Genleşme Tankları</li>");
        sb.AppendLine("<li><b>TS EN 12845 / NFPA 13:</b> Otomatik Sprinkler Sistemleri</li>");
        sb.AppendLine("<li><b>DIN 1988-300:</b> İçme Suyu Tesisatı Hesaplama</li>");
        sb.AppendLine("</ul>");
    }

    private void WriteFooter(StringBuilder sb, TechnicalSpecConfig cfg)
    {
        sb.AppendLine("<hr/>");
        sb.AppendLine($"<p style='font-size:11px;color:#999'>Bu teknik şartname AfneyCAD v4.0.0 tarafından otomatik üretilmiştir. | {cfg.Date} | {cfg.CompanyName}</p>");
    }
}
