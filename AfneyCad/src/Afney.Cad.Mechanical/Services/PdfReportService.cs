using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

// PDF benzeri profesyonel rapor çıktısı — HTML/CSS print-ready format
public class PdfReportService
{
    public string GenerateFullReport(FullReportInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine($"<title>{input.ProjectName} — Mühendislik Raporu</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("@page { size: A4; margin: 20mm; }");
        sb.AppendLine("@media print { .no-print { display: none; } .page-break { page-break-before: always; } }");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11pt; color: #333; line-height: 1.5; }");
        sb.AppendLine("h1 { color: #1a3a5c; border-bottom: 2px solid #1a3a5c; padding-bottom: 5px; }");
        sb.AppendLine("h2 { color: #2a5a8c; margin-top: 20px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 10px 0; font-size: 10pt; }");
        sb.AppendLine("th { background: #1a3a5c; color: white; padding: 8px; text-align: left; }");
        sb.AppendLine("td { border: 1px solid #ddd; padding: 6px; }");
        sb.AppendLine("tr:nth-child(even) { background: #f5f5f5; }");
        sb.AppendLine(".header { text-align: center; border: 2px solid #1a3a5c; padding: 15px; margin-bottom: 20px; }");
        sb.AppendLine(".header h1 { border: none; margin: 0; }");
        sb.AppendLine(".summary-box { background: #e8f0fe; border: 1px solid #1a3a5c; padding: 15px; border-radius: 5px; margin: 10px 0; }");
        sb.AppendLine(".pass { color: #2e7d32; font-weight: bold; } .fail { color: #c62828; font-weight: bold; }");
        sb.AppendLine(".footer { text-align: center; font-size: 9pt; color: #888; border-top: 1px solid #ddd; padding-top: 10px; margin-top: 20px; }");
        sb.AppendLine("</style></head><body>");

        // Kapak sayfası
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<h1>{input.ProjectName}</h1>");
        sb.AppendLine($"<p><strong>MEKANİK TESİSAT MÜHENDİSLİK RAPORU</strong></p>");
        sb.AppendLine($"<p>Hazırlayan: {input.PreparedBy} | Tarih: {DateTime.Now:dd.MM.yyyy}</p>");
        sb.AppendLine($"<p>Proje No: {input.ProjectNo} | Revizyon: {input.Revision}</p>");
        sb.AppendLine("</div>");

        // 1. Proje Özeti
        sb.AppendLine("<h2>1. PROJE ÖZETİ</h2>");
        sb.AppendLine("<div class='summary-box'>");
        sb.AppendLine($"<p><strong>Bina Tipi:</strong> {input.BuildingType}</p>");
        sb.AppendLine($"<p><strong>Toplam Boru:</strong> {input.TotalPipeCount} adet | <strong>Toplam Cihaz:</strong> {input.TotalFixtureCount} adet</p>");
        sb.AppendLine($"<p><strong>Toplam Boru Uzunluğu:</strong> {input.TotalPipeLengthM:F1} m</p>");
        sb.AppendLine($"<p><strong>Standart:</strong> {input.StandardRef}</p>");
        sb.AppendLine("</div>");

        // 2. Hidrolik Hesap Tablosu
        if (input.HydraulicData.Any())
        {
            sb.AppendLine("<div class='page-break'></div>");
            sb.AppendLine("<h2>2. HİDROLİK HESAP TABLOSU</h2>");
            sb.AppendLine("<table><tr><th>Boru ID</th><th>DN (mm)</th><th>Uzunluk (m)</th><th>Debi (l/s)</th><th>Hız (m/s)</th><th>Basınç Kaybı (mSS)</th><th>Durum</th></tr>");
            foreach (var row in input.HydraulicData)
            {
                string status = row.Velocity > 2.0 ? "<span class='fail'>HIZ AŞIMI</span>" : "<span class='pass'>OK</span>";
                sb.AppendLine($"<tr><td>{row.PipeId}</td><td>{row.DiameterMm:F0}</td><td>{row.LengthM:F2}</td><td>{row.FlowRateLs:F3}</td><td>{row.Velocity:F2}</td><td>{row.PressureDropMSS:F4}</td><td>{status}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        // 3. Basınç Kaybı Grafiği (SVG)
        if (input.HydraulicData.Any())
        {
            sb.AppendLine("<h2>3. BASINÇ KAYBI GRAFİĞİ</h2>");
            var chartData = input.HydraulicData
                .Select(h => (Label: h.PipeId, Value: h.PressureDropMSS))
                .ToList();
            sb.AppendLine(SvgChartService.BarChart("Boru Basınç Kaybı Dağılımı (mSS)", chartData, " mSS", "#FF6600"));
        }

        // 4. Mevzuat Uyum
        if (input.ComplianceScore > 0)
        {
            sb.AppendLine("<div class='page-break'></div>");
            sb.AppendLine("<h2>4. MEVZUAT UYUM KONTROLÜ</h2>");
            string scoreClass = input.ComplianceScore >= 80 ? "pass" : "fail";
            sb.AppendLine($"<p>Uyum Skoru: <span class='{scoreClass}'>{input.ComplianceScore:F0}%</span></p>");
        }

        // 5. Metraj Özeti
        if (input.BomData.Any())
        {
            sb.AppendLine("<h2>5. METRAJ ÖZETİ</h2>");
            sb.AppendLine("<table><tr><th>Malzeme</th><th>Çap</th><th>Miktar</th><th>Birim</th><th>Birim Fiyat (TRY)</th><th>Toplam (TRY)</th></tr>");
            double grandTotal = 0;
            foreach (var item in input.BomData)
            {
                double total = item.Quantity * item.UnitPrice;
                grandTotal += total;
                sb.AppendLine($"<tr><td>{item.Description}</td><td>{item.Diameter}</td><td>{item.Quantity:F1}</td><td>{item.Unit}</td><td>{item.UnitPrice:N0}</td><td>{total:N0}</td></tr>");
            }
            sb.AppendLine($"<tr style='font-weight:bold'><td colspan='5'>GENEL TOPLAM</td><td>{grandTotal:N0} TRY</td></tr>");
            sb.AppendLine("</table>");
        }

        // Footer
        sb.AppendLine("<div class='footer'>");
        sb.AppendLine($"<p>Bu rapor AfneyCAD v2.0 tarafından otomatik oluşturulmuştur. | {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

public class FullReportInput
{
    public string ProjectName { get; set; } = "AfneyCAD Projesi";
    public string PreparedBy { get; set; } = "";
    public string ProjectNo { get; set; } = "";
    public string Revision { get; set; } = "R0";
    public string BuildingType { get; set; } = "Konut";
    public string StandardRef { get; set; } = "TS 1258 / DIN 1988";
    public int TotalPipeCount { get; set; }
    public int TotalFixtureCount { get; set; }
    public double TotalPipeLengthM { get; set; }
    public double ComplianceScore { get; set; }
    public List<HydraulicRowData> HydraulicData { get; set; } = new();
    public List<BomRowData> BomData { get; set; } = new();
}

public class HydraulicRowData
{
    public string PipeId { get; set; } = "";
    public double DiameterMm { get; set; }
    public double LengthM { get; set; }
    public double FlowRateLs { get; set; }
    public double Velocity { get; set; }
    public double PressureDropMSS { get; set; }
}

public class BomRowData
{
    public string Description { get; set; } = "";
    public string Diameter { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "m";
    public double UnitPrice { get; set; }
}
