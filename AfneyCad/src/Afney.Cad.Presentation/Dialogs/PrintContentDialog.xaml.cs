using System;
using System.IO;
using System.Text;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class PrintContentDialog : Window
{
    public class PrintOptions
    {
        public bool IncludeProjectInfo  { get; set; } = true;
        public bool IncludeCalcDate     { get; set; } = true;
        public bool IncludeEngineer     { get; set; } = true;
        public bool IncludeCalcTable    { get; set; } = true;
        public bool WarningsOnly        { get; set; }
        public bool IncludeNormRef      { get; set; } = true;
        public bool IncludeSummaryBar   { get; set; } = true;
        public bool IncludeSeptic       { get; set; }
        public bool IncludePump         { get; set; }
        public bool IncludeBom          { get; set; } = true;
        public string OutputFormat      { get; set; } = "HTML";
        public string ProjectName       { get; set; } = "AfneyCAD Projesi";
        public string EngineerName      { get; set; } = "";
    }

    private readonly CadDatabase _database;
    private readonly WasteWaterCalcSheetService.CalcSheetResult? _calcResult;

    public PrintContentDialog(CadDatabase database, WasteWaterCalcSheetService.CalcSheetResult? calcResult = null)
    {
        InitializeComponent();
        _database   = database;
        _calcResult = calcResult;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var opts = BuildPrintOptions();
            string content = BuildReport(opts);
            string ext     = opts.OutputFormat == "Word" ? "doc" : "html";
            string path    = Path.Combine(Path.GetTempPath(),
                $"AfneyCAD_{SanitizeFileName(opts.ProjectName)}_{DateTime.Now:yyyyMMdd_HHmm}.{ext}");

            File.WriteAllText(path, content, Encoding.UTF8);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true
            });

            StatusBorder.Visibility = Visibility.Visible;
            TxtStatus.Text = $"✅ Rapor oluşturuldu ve açıldı:\n{path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string BuildReport(PrintOptions opts)
    {
        bool isWord = opts.OutputFormat == "Word";
        var sb = new StringBuilder();

        // Word MIME header
        if (isWord)
        {
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office'");
            sb.AppendLine("      xmlns:w='urn:schemas-microsoft-com:office:word'");
            sb.AppendLine("      xmlns='http://www.w3.org/TR/REC-html40'>");
            sb.AppendLine("<head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'/>");
            sb.AppendLine("<!--[if gte mso 9]><xml><w:WordDocument><w:View>Print</w:View></w:WordDocument></xml><![endif]-->");
        }
        else
        {
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'/>");
        }

        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Calibri,Arial,sans-serif;font-size:11pt;margin:20mm;}");
        sb.AppendLine("h1{font-size:16pt;color:#0D47A1;border-bottom:2px solid #0D47A1;padding-bottom:4px;}");
        sb.AppendLine("h2{font-size:13pt;color:#1565C0;margin-top:18px;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:14px;}");
        sb.AppendLine("th{background:#0D47A1;color:white;padding:6px 8px;text-align:left;font-size:9.5pt;}");
        sb.AppendLine("td{border:1px solid #ccc;padding:5px 8px;font-size:9.5pt;}");
        sb.AppendLine("tr:nth-child(even){background:#f5f8ff;}");
        sb.AppendLine(".warn{background:#fff3e0 !important;color:#bf360c;}");
        sb.AppendLine(".card{display:inline-block;border:1px solid #90CAF9;border-radius:4px;padding:8px 18px;margin:4px;text-align:center;}");
        sb.AppendLine(".card span{display:block;font-size:18pt;font-weight:bold;color:#0D47A1;}");
        sb.AppendLine(".footer{margin-top:30px;font-size:8.5pt;color:#888;border-top:1px solid #ccc;padding-top:6px;}");
        sb.AppendLine(".sig-box{display:inline-block;width:180px;border-top:1px solid #333;text-align:center;padding-top:4px;margin:30px 20px 0 0;font-size:9pt;}");
        sb.AppendLine("</style></head><body>");

        // Project header
        if (opts.IncludeProjectInfo)
        {
            sb.AppendLine($"<h1>Pis Su Tesisat Hesap Raporu</h1>");
            sb.AppendLine($"<p><strong>Proje:</strong> {Esc(opts.ProjectName)}</p>");
        }
        if (opts.IncludeCalcDate)
            sb.AppendLine($"<p><strong>Tarih:</strong> {DateTime.Now:dd.MM.yyyy HH:mm} &nbsp;|&nbsp; <strong>AfneyCAD</strong> Otomatik Hesap Raporu</p>");

        // Summary bar
        if (opts.IncludeSummaryBar && _calcResult != null)
        {
            sb.AppendLine("<div style='margin:12px 0;'>");
            AppendCard(sb, "Segment Sayısı", _calcResult.TotalSegments.ToString());
            AppendCard(sb, "Toplam Uzunluk", $"{_calcResult.TotalLengthM:F1} m");
            AppendCard(sb, "Uyarı Sayısı", _calcResult.WarningCount.ToString());
            AppendCard(sb, "Hesap Yöntemi", _calcResult.Options.Method.ToString());
            sb.AppendLine("</div>");
        }

        // Calc table
        if (opts.IncludeCalcTable && _calcResult != null)
        {
            sb.AppendLine("<h2>Segment Hesap Föyü</h2>");
            sb.AppendLine("<table><tr>");
            foreach (var h in new[] { "Segment ID", "Boru DN (mm)", "Uzunluk (m)", "Eğim (%)", "DU",
                                      "Debi Q (l/s)", "Q_dolu (l/s)", "Doluluk (%)", "Hız (m/s)", "Boru Cinsi", "Uyarı" })
                sb.AppendLine($"<th>{h}</th>");
            sb.AppendLine("</tr>");

            foreach (var row in _calcResult.Rows)
            {
                if (opts.WarningsOnly && string.IsNullOrEmpty(row.Warnings)) continue;
                string cls = !string.IsNullOrEmpty(row.Warnings) ? " class='warn'" : "";
                sb.AppendLine($"<tr{cls}>" +
                    $"<td>{Esc(row.SegmentId)}</td>" +
                    $"<td>{row.DiameterMm:F0}</td>" +
                    $"<td>{row.LengthM:F2}</td>" +
                    $"<td>{row.SlopePct:F2}</td>" +
                    $"<td>{row.LoadUnits:F1}</td>" +
                    $"<td>{row.DesignFlowLs:F3}</td>" +
                    $"<td>{row.CapacityLs:F3}</td>" +
                    $"<td>{row.FillRatio:P0}</td>" +
                    $"<td>{row.VelocityMs:F2}</td>" +
                    $"<td>{Esc(row.PipeType)}</td>" +
                    $"<td>{Esc(row.Warnings)}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        // Norm reference table
        if (opts.IncludeNormRef)
        {
            sb.AppendLine("<h2>Norm Referans Tablosu (TS EN 12056-2)</h2>");
            sb.AppendLine("<table><tr><th>DN (mm)</th><th>Min. Eğim (%)</th><th>Max. Q (l/s)</th><th>Kullanım Yeri</th></tr>");
            var refData = new (string DN, string Slope, string MaxQ, string Usage)[]
            {
                ("50",  "2.50", "0.80",  "Branşman — lavabo, duş"),
                ("75",  "1.50", "2.00",  "Branşman — WC (sifon öncesi)"),
                ("100", "1.00", "5.20",  "WC + yatay kolon"),
                ("125", "0.80", "8.40",  "Orta kat kolektörü"),
                ("150", "0.70", "12.80", "Bina içi ana kolektör"),
                ("200", "0.50", "25.00", "Bina dışı bağlantı"),
                ("250", "0.40", "42.00", "Site / bölge kolektörü"),
                ("300", "0.30", "65.00", "Ana kolektör"),
            };
            foreach (var r in refData)
                sb.AppendLine($"<tr><td>{r.DN}</td><td>{r.Slope}</td><td>{r.MaxQ}</td><td>{r.Usage}</td></tr>");
            sb.AppendLine("</table>");
        }

        // BOM
        if (opts.IncludeBom)
        {
            sb.AppendLine("<h2>Keşif Listesi</h2>");
            sb.AppendLine("<table><tr><th>Malzeme</th><th>DN</th><th>Toplam Uzunluk (m)</th><th>Adet</th><th>Birim</th></tr>");

            var pipes = _database.GetAllEntities()
                .OfType<Afney.Cad.Mechanical.Entities.PipeEntity>()
                .Where(p => p.SystemType == Afney.Cad.Mechanical.Enums.MechanicalSystemType.WasteWater ||
                            p.SystemType == Afney.Cad.Mechanical.Enums.MechanicalSystemType.RainWater)
                .GroupBy(p => (Dn: Math.Round(p.InnerDiameter * 1000), Type: p.SystemType.ToString()))
                .OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Dn);

            foreach (var g in pipes)
                sb.AppendLine($"<tr><td>Pis Su Borusu — {g.Key.Type}</td><td>DN{g.Key.Dn:F0}</td><td>{g.Sum(p => p.Length):F1}</td><td>{g.Count()}</td><td>m</td></tr>");

            sb.AppendLine("</table>");
        }

        // Signature box
        if (opts.IncludeEngineer)
        {
            sb.AppendLine("<div style='margin-top:40px;'>");
            sb.AppendLine($"<div class='sig-box'>{Esc(opts.EngineerName)}<br/>Hazırlayan Mühendis</div>");
            sb.AppendLine($"<div class='sig-box'>&nbsp;<br/>Tarih: {DateTime.Now:dd.MM.yyyy}</div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine($"<div class='footer'>AfneyCAD — Mekanik Tesisat Tasarım Yazılımı &nbsp;|&nbsp; {DateTime.Now:dd.MM.yyyy HH:mm}</div>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendCard(StringBuilder sb, string label, string value)
        => sb.AppendLine($"<div class='card'>{Esc(label)}<span>{Esc(value)}</span></div>");

    private PrintOptions BuildPrintOptions() => new()
    {
        IncludeProjectInfo  = ChkProjectInfo.IsChecked  == true,
        IncludeCalcDate     = ChkCalcDate.IsChecked     == true,
        IncludeEngineer     = ChkEngineer.IsChecked     == true,
        IncludeCalcTable    = ChkCalcTable.IsChecked    == true,
        WarningsOnly        = ChkWarningsOnly.IsChecked == true,
        IncludeNormRef      = ChkNormRef.IsChecked      == true,
        IncludeSummaryBar   = ChkSummaryBar.IsChecked   == true,
        IncludeSeptic       = ChkSeptic.IsChecked       == true,
        IncludePump         = ChkPump.IsChecked         == true,
        IncludeBom          = ChkBom.IsChecked          == true,
        OutputFormat        = RbWord.IsChecked == true ? "Word" : "HTML",
        ProjectName         = TxtProjectName.Text.Trim(),
        EngineerName        = TxtEngineerName.Text.Trim(),
    };

    private static string Esc(string? s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
