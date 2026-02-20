using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Hidrolik Hesap Raporu Servisi (HydraulicReportService)
   NEDEN: Projedeki tüm boruların (Debi, Hız, Kayıp) değerlerini TS 1258 / DIN 1988 formatında detaylı bir HTML tablosu olarak çıktı vermek.
   
   NASIL:
   Basınç kaybı servisi ile hesaplanmış olan boruları (`PressureDrop` bilgisiyle birlikte) dolaşır,
   hesaplanmamışsa önce hesaplar, ardından standart mühendislik föyüne döker.
*/
public class HydraulicReportService
{
    private readonly PressureDropService _pressureDropService;

    public HydraulicReportService(PressureDropService pressureDropService)
    {
        _pressureDropService = pressureDropService;
    }

    public string GenerateHtmlReport(IEnumerable<PipeEntity> pipes, string projectName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='tr'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='UTF-8'>");
        sb.AppendLine($"<title>Hidrolik Hesap Raporu - {projectName}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; color: #333; }");
        sb.AppendLine("h1 { color: #005A9C; border-bottom: 2px solid #005A9C; padding-bottom: 10px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 14px; }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 10px; text-align: center; }");
        sb.AppendLine("th { background-color: #f2f2f2; color: #333; font-weight: bold; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
        sb.AppendLine("tr:hover { background-color: #eef; }");
        sb.AppendLine(".footer { margin-top: 30px; font-size: 12px; color: #777; text-align: center; }");
        sb.AppendLine(".logo-area { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }");
        sb.AppendLine(".logo-area h2 { margin: 0; color: #555; }");
        sb.AppendLine(".highlight { background-color: #ffefc1; font-weight: bold; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("<div class='logo-area'>");
        sb.AppendLine($"<div><h1>PROJE HİDROLİK HESAP TABLOSU</h1><p>Proje: <strong>{projectName}</strong></p></div>");
        sb.AppendLine("<div><h2>AfneyCAD Engine</h2><p>TS 1258 / DIN 1988</p></div>");
        sb.AppendLine("</div>");

        // Table Start
        sb.AppendLine("<table>");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Boru ID</th>");
        sb.AppendLine("<th>Sistem Tipi</th>");
        sb.AppendLine("<th>Uzunluk (m)</th>");
        sb.AppendLine("<th>Çap (DN)</th>");
        sb.AppendLine("<th>Kümülatif LU</th>");
        sb.AppendLine("<th>Tasarım Debisi (l/s)</th>");
        sb.AppendLine("<th>Akış Hızı (m/s)</th>");
        sb.AppendLine("<th>Sürtünme Kaybı (mbar/m)</th>");
        sb.AppendLine("<th>Toplam Hat Kaybı (mSS)</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        double maxVelocity = 0;
        double maxPressureDrop = 0;

        foreach (var pipe in pipes.OrderBy(p => p.SystemType).ThenByDescending(p => p.FlowRate))
        {
            // Eğer daha önceden hesaplanmadıysa burada hesaplatalım
            if (pipe.PressureDrop <= 0 && pipe.FlowRate > 0)
            {
                pipe.PressureDrop = _pressureDropService.CalculatePipePressureDrop(pipe);
            }

            double lengthM = pipe.GetLength() / 1000.0;
            double flowLps = pipe.FlowRate / 3.6; // m³/h -> l/s
            double velocity = pipe.GetVelocity();
            
            // mSS toplam kaybı mbar/metre cinsine çevirelim (Yaklaşık birimsel gösterim)
            // Toplam Kayıp = hf (mSS).  1 mSS = 98.0665 mbar.  
            // Direnç R = (hf * 98.06) / L 
            double rMbarPerM = lengthM > 0 ? (pipe.PressureDrop * 98.0665) / lengthM : 0;

            if (velocity > maxVelocity) maxVelocity = velocity;
            if (pipe.PressureDrop > maxPressureDrop) maxPressureDrop = pipe.PressureDrop;

            // Hız limiti aşımını vurgula (Örn: 1.5 m/s üzeri)
            string velocityClass = velocity > 1.5 ? "class='highlight'" : "";

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>#{pipe.Id.ToString().Substring(0, 8)}</td>");
            sb.AppendLine($"<td>{pipe.SystemType}</td>");
            sb.AppendLine($"<td>{lengthM:F2}</td>");
            sb.AppendLine($"<td>DN {pipe.InnerDiameter}</td>");
            string luStr = pipe.LoadUnits > 0 ? pipe.LoadUnits.ToString("F1") : "-";
            sb.AppendLine($"<td>{luStr}</td>");
            sb.AppendLine($"<td>{flowLps:F2}</td>");
            sb.AppendLine($"<td {velocityClass}>{velocity:F2}</td>");
            sb.AppendLine($"<td>{rMbarPerM:F2}</td>");
            sb.AppendLine($"<td>{pipe.PressureDrop:F3}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");

        // Summary Statistics
        sb.AppendLine("<div style='margin-top: 20px; padding: 15px; background-color: #eef; border-radius: 5px;'>");
        sb.AppendLine("<h3>Özet Bilgiler</h3>");
        sb.AppendLine($"<p><strong>Toplam Analiz Edilen Boru Sayısı:</strong> {pipes.Count()}</p>");
        sb.AppendLine($"<p><strong>Sistemdeki Maksimum Akış Hızı:</strong> {maxVelocity:F2} m/s {(maxVelocity > 1.5 ? "<span style='color:red;'>(*Tavsiye edilen limit aşıldı)</span>" : "")}</p>");
        sb.AppendLine($"<p><strong>Tekil Bir Hatta Görülen Maksimum Direnç (mSS):</strong> {maxPressureDrop:F3} mSS</p>");
        sb.AppendLine("</div>");

        // Footer
        sb.AppendLine($"<div class='footer'>Rapor oluşturulma tarihi: {DateTime.Now:dd/MM/yyyy HH:mm} - AfneyCAD Engine tarafından otomatik oluşturulmuştur.</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
