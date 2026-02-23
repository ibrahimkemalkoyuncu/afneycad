using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Rapor Dışa Aktarma Servisi (ReportExportService)
   NEDEN: FINE SANI, hesap tablolarını Word/Excel/PDF formatlarında dışa aktarır.
          Bu servis HTML → PDF ve CSV → Excel benzeri çıktılar üretir.
   
   DESTEKLENEN FORMATLAR:
   - HTML (tarayıcıda yazdırılabilir — PDF alternatifi)
   - CSV (Excel'de açılabilir)
   - RTF (Word'de açılabilir — zengin metin)
*/
public class ReportExportService
{
    private readonly CadDatabase _database;

    public ReportExportService(CadDatabase database) { _database = database; }

    public enum ExportFormat { HTML, CSV, RTF }

    public class ReportData
    {
        public string Title { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Engineer { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.Now;
        public List<ReportSection> Sections { get; set; } = new();
    }

    public class ReportSection
    {
        public string Title { get; set; } = "";
        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    /*
       NE: Profesyonel HTML rapor üretimi
       NEDEN: Yazdırılabilir, CSS ile formatlanmış mühendislik raporu
    */
    public string ExportToHtml(ReportData report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>" + report.Title + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; color: #333; }");
        sb.AppendLine("h1 { color: #005A9C; border-bottom: 3px solid #005A9C; padding-bottom: 10px; }");
        sb.AppendLine("h2 { color: #2E8B57; margin-top: 30px; }");
        sb.AppendLine(".meta { background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 15px 0; }");
        sb.AppendLine("th { background: #005A9C; color: white; padding: 10px; text-align: left; font-size: 13px; }");
        sb.AppendLine("td { padding: 8px; border: 1px solid #ddd; font-size: 12px; }");
        sb.AppendLine("tr:nth-child(even) { background: #f9f9f9; }");
        sb.AppendLine("@media print { .no-print { display: none; } }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>{report.Title}</h1>");
        sb.AppendLine("<div class=\"meta\">");
        sb.AppendLine($"<strong>Proje:</strong> {report.ProjectName}<br>");
        sb.AppendLine($"<strong>Mühendis:</strong> {report.Engineer}<br>");
        sb.AppendLine($"<strong>Tarih:</strong> {report.Date:dd.MM.yyyy}<br>");
        sb.AppendLine($"<strong>Program:</strong> AfneyCAD Mechanical v1.0");
        sb.AppendLine("</div>");

        foreach (var section in report.Sections)
        {
            sb.AppendLine($"<h2>{section.Title}</h2>");
            sb.AppendLine("<table><thead><tr>");
            foreach (var h in section.Headers) sb.AppendLine($"<th>{h}</th>");
            sb.AppendLine("</tr></thead><tbody>");
            foreach (var row in section.Rows)
            {
                sb.AppendLine("<tr>");
                foreach (var cell in row) sb.AppendLine($"<td>{cell}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("<p class=\"no-print\" style=\"color:#888;margin-top:40px;\">");
        sb.AppendLine("Bu rapor AfneyCAD Mechanical CAD yazılımı tarafından otomatik oluşturulmuştur.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /*
       NE: CSV export (Excel uyumlu)
       NEDEN: Tablo verilerini virgülle ayrılmış formatta dışa aktarır
    */
    public string ExportToCsv(ReportData report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {report.Title}");
        sb.AppendLine($"# Proje: {report.ProjectName} | Mühendis: {report.Engineer} | Tarih: {report.Date:dd.MM.yyyy}");
        sb.AppendLine();

        foreach (var section in report.Sections)
        {
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine(string.Join(";", section.Headers));
            foreach (var row in section.Rows)
            {
                sb.AppendLine(string.Join(";", row));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /*
       NE: RTF export (Word uyumlu)
       NEDEN: Zengin metin formatında rapor — Word'de açılabilir
    */
    public string ExportToRtf(ReportData report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"{\rtf1\ansi\deff0");
        sb.AppendLine(@"{\fonttbl{\f0 Segoe UI;}}");
        sb.AppendLine(@"\f0\fs24");
        sb.AppendLine($@"\b {report.Title}\b0\par");
        sb.AppendLine($@"Proje: {report.ProjectName}\par");
        sb.AppendLine($@"Mühendis: {report.Engineer}\par");
        sb.AppendLine($@"Tarih: {report.Date:dd.MM.yyyy}\par\par");

        foreach (var section in report.Sections)
        {
            sb.AppendLine($@"\b {section.Title}\b0\par");
            sb.AppendLine(string.Join(@"\tab ", section.Headers) + @"\par");
            foreach (var row in section.Rows)
            {
                sb.AppendLine(string.Join(@"\tab ", row) + @"\par");
            }
            sb.AppendLine(@"\par");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // Mevcut boru verilerinden otomatik rapor verisi üret
    public ReportData GenerateSystemReport(string projectName = "AfneyCAD Projesi")
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var report = new ReportData
        {
            Title = "Sıhhi Tesisat Hesap Raporu",
            ProjectName = projectName,
            Engineer = Environment.UserName,
            Date = DateTime.Now
        };

        // Boru listesi bölümü
        var pipeSection = new ReportSection { Title = "Boru Segmentleri" };
        pipeSection.Headers.AddRange(new[] { "No", "Sistem", "DN (mm)", "Uzunluk (m)", "Malzeme", "Hız (m/s)", "Debi (l/s)" });

        int idx = 1;
        foreach (var p in pipes)
        {
            double len = (p.EndPoint - p.StartPoint).Length() / 1000.0;
            pipeSection.Rows.Add(new List<string>
            {
                idx++.ToString(),
                p.SystemType.ToString(),
                p.InnerDiameter.ToString("F0"),
                len.ToString("F2"),
                p.PipeMaterialType.ToString(),
                p.Velocity.ToString("F2"),
                p.FlowRate.ToString("F3")
            });
        }
        report.Sections.Add(pipeSection);

        // Özet bölümü
        var summary = new ReportSection { Title = "Sistem Özeti" };
        summary.Headers.AddRange(new[] { "Parametre", "Değer" });
        summary.Rows.Add(new List<string> { "Toplam Boru Sayısı", pipes.Count.ToString() });
        summary.Rows.Add(new List<string> { "Toplam Uzunluk", $"{pipes.Sum(p => (p.EndPoint - p.StartPoint).Length() / 1000.0):F1} m" });
        summary.Rows.Add(new List<string> { "Max Hız", $"{(pipes.Any() ? pipes.Max(p => p.Velocity) : 0):F2} m/s" });
        report.Sections.Add(summary);

        return report;
    }
}
