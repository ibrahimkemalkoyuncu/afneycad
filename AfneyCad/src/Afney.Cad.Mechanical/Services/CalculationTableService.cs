using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Hesaplama Tablosu Servisi (CalculationTableService)
   NEDEN: FINE SANI standardında, her boru segmenti için ayrı ayrı mühendislik hesap satırı oluşturmak.
          Bu tablo profesyonel proje dosyalarında zorunlu olan "Hidrolik Hesap Föyü"nün veritabanıdır.
   
   TABLO SÜTUNLARI (TS 1258 / DIN 1988):
   - Hat No, Başlangıç-Bitiş, Sistem Tipi
   - Uzunluk (m), Vitrifiye Sayısı, Toplam LU
   - Tasarım Debisi Q (l/s), Seçilen Çap DN
   - Akış Hızı v (m/s), Sürtünme Kaybı R (mbar/m)
   - Toplam Hat Kaybı hf (mSS), Kümülatif Kayıp (mSS)
*/
public class CalculationTableService
{
    private readonly CadDatabase _database;
    private readonly PressureDropService _pressureDropService;

    public CalculationTableService(CadDatabase database, PressureDropService pressureDropService)
    {
        _database = database;
        _pressureDropService = pressureDropService;
    }

    /*
       NE: Tüm Borular İçin Hesap Tablosu Üret
       NEDEN: Projedeki her boru için mühendislik değerlerini tek tabloda toplamak.
    */
    public CalculationTable GenerateTable(string projectName = "AfneyCAD Projesi")
    {
        var table = new CalculationTable { ProjectName = projectName };
        var pipes = _database.GetAllEntities().OfType<PipeEntity>()
            .OrderBy(p => p.SystemType)
            .ThenByDescending(p => p.FlowRate)
            .ToList();

        int lineNo = 1;
        double cumulativeLoss = 0;

        foreach (var pipe in pipes)
        {
            // Basınç kaybı hesapla (henüz hesaplanmadıysa)
            if (pipe.PressureDrop <= 0 && pipe.FlowRate > 0)
            {
                pipe.PressureDrop = _pressureDropService.CalculatePipePressureDrop(pipe);
            }

            double lengthM = pipe.GetLength() / 1000.0;
            double flowLps = pipe.FlowRate / 3.6;
            double velocity = pipe.GetVelocity();
            double rMbarPerM = lengthM > 0 ? (pipe.PressureDrop * 98.0665) / lengthM : 0;
            cumulativeLoss += pipe.PressureDrop;

            var row = new CalculationRow
            {
                LineNo = lineNo++,
                PipeId = pipe.Id.ToString().Substring(0, 8),
                SystemType = GetSystemLabel(pipe.SystemType),
                From = $"({pipe.StartPoint.X / 1000:F1},{pipe.StartPoint.Y / 1000:F1})",
                To = $"({pipe.EndPoint.X / 1000:F1},{pipe.EndPoint.Y / 1000:F1})",
                LengthM = lengthM,
                TotalLU = pipe.LoadUnits,
                DesignFlowLps = flowLps,
                DiameterDN = pipe.InnerDiameter,
                VelocityMs = velocity,
                FrictionLossMbar = rMbarPerM,
                TotalLossMSS = pipe.PressureDrop,
                CumulativeLossMSS = cumulativeLoss,
                IsVelocityWarning = velocity > 1.5,
                Material = pipe.PipeMaterialType.ToString()
            };

            table.Rows.Add(row);
        }

        // Özet bilgiler
        table.TotalPipeCount = pipes.Count;
        table.TotalLength = pipes.Sum(p => p.GetLength() / 1000.0);
        table.MaxVelocity = table.Rows.Any() ? table.Rows.Max(r => r.VelocityMs) : 0;
        table.TotalPressureDrop = cumulativeLoss;
        table.GeneratedDate = DateTime.Now;

        return table;
    }

    /*
       NE: Hesap Tablosunu HTML Olarak Dışa Aktar
       NEDEN: Profesyonel baskı kalitesinde rapor üretmek.
    */
    public string ExportToHtml(CalculationTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'>");
        sb.AppendLine($"<title>Hidrolik Hesap Tablosu - {table.ProjectName}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;margin:20px;color:#333;}");
        sb.AppendLine("h1{color:#005A9C;border-bottom:2px solid #005A9C;padding-bottom:10px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-top:15px;font-size:13px;}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:8px;text-align:center;}");
        sb.AppendLine("th{background:#2c3e50;color:#fff;font-weight:bold;}");
        sb.AppendLine("tr:nth-child(even){background:#f9f9f9;}");
        sb.AppendLine("tr:hover{background:#eef;}");
        sb.AppendLine(".warn{background:#fff3cd;color:#856404;font-weight:bold;}");
        sb.AppendLine(".summary{margin-top:20px;padding:15px;background:#e8f4f8;border-radius:5px;}");
        sb.AppendLine(".footer{margin-top:30px;font-size:11px;color:#777;text-align:center;}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>HİDROLİK HESAP TABLOSU</h1>");
        sb.AppendLine($"<p>Proje: <strong>{table.ProjectName}</strong> | Tarih: {table.GeneratedDate:dd.MM.yyyy HH:mm}</p>");

        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine("<th>Hat No</th><th>Boru ID</th><th>Sistem</th>");
        sb.AppendLine("<th>Başlangıç</th><th>Bitiş</th>");
        sb.AppendLine("<th>Uzunluk (m)</th><th>LU</th>");
        sb.AppendLine("<th>Q (l/s)</th><th>DN</th>");
        sb.AppendLine("<th>v (m/s)</th><th>R (mbar/m)</th>");
        sb.AppendLine("<th>hf (mSS)</th><th>Küm. (mSS)</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var row in table.Rows)
        {
            string cls = row.IsVelocityWarning ? " class='warn'" : "";
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{row.LineNo}</td>");
            sb.AppendLine($"<td>#{row.PipeId}</td>");
            sb.AppendLine($"<td>{row.SystemType}</td>");
            sb.AppendLine($"<td>{row.From}</td>");
            sb.AppendLine($"<td>{row.To}</td>");
            sb.AppendLine($"<td>{row.LengthM:F2}</td>");
            sb.AppendLine($"<td>{(row.TotalLU > 0 ? row.TotalLU.ToString("F1") : "-")}</td>");
            sb.AppendLine($"<td>{row.DesignFlowLps:F3}</td>");
            sb.AppendLine($"<td>DN {row.DiameterDN:F0}</td>");
            sb.AppendLine($"<td{cls}>{row.VelocityMs:F2}</td>");
            sb.AppendLine($"<td>{row.FrictionLossMbar:F2}</td>");
            sb.AppendLine($"<td>{row.TotalLossMSS:F3}</td>");
            sb.AppendLine($"<td>{row.CumulativeLossMSS:F3}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");

        // Özet
        sb.AppendLine("<div class='summary'>");
        sb.AppendLine("<h3>Özet Bilgiler</h3>");
        sb.AppendLine($"<p><strong>Toplam Boru:</strong> {table.TotalPipeCount} adet</p>");
        sb.AppendLine($"<p><strong>Toplam Uzunluk:</strong> {table.TotalLength:F1} metre</p>");
        sb.AppendLine($"<p><strong>Max. Hız:</strong> {table.MaxVelocity:F2} m/s {(table.MaxVelocity > 1.5 ? "<span style='color:red;'>⚠ Limit aşıldı</span>" : "✓")}</p>");
        sb.AppendLine($"<p><strong>Toplam Basınç Kaybı:</strong> {table.TotalPressureDrop:F3} mSS ({table.TotalPressureDrop / 10:F2} bar)</p>");
        sb.AppendLine("</div>");

        sb.AppendLine($"<div class='footer'>AfneyCAD Engine — TS 1258 / DIN 1988 Hesap Tablosu — {table.GeneratedDate:dd.MM.yyyy}</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    /*
       NE: Hesap Tablosunu CSV Olarak Dışa Aktar
       NEDEN: Excel'de açılabilir format
    */
    public string ExportToCsv(CalculationTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hat No;Boru ID;Sistem;Uzunluk(m);LU;Q(l/s);DN;v(m/s);R(mbar/m);hf(mSS);Küm.(mSS)");

        foreach (var row in table.Rows)
        {
            sb.AppendLine($"{row.LineNo};#{row.PipeId};{row.SystemType};{row.LengthM:F2};" +
                          $"{(row.TotalLU > 0 ? row.TotalLU.ToString("F1") : "-")};{row.DesignFlowLps:F3};" +
                          $"DN{row.DiameterDN:F0};{row.VelocityMs:F2};{row.FrictionLossMbar:F2};" +
                          $"{row.TotalLossMSS:F3};{row.CumulativeLossMSS:F3}");
        }

        return sb.ToString();
    }

    private string GetSystemLabel(MechanicalSystemType type) => type switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater => "Sıcak Su",
        MechanicalSystemType.WasteWater => "Pis Su",
        _ => "Genel"
    };
}

// --- HESAP TABLOSU VERİ MODELLERİ ---

public class CalculationTable
{
    public string ProjectName { get; set; } = "";
    public List<CalculationRow> Rows { get; set; } = new();
    public int TotalPipeCount { get; set; }
    public double TotalLength { get; set; }
    public double MaxVelocity { get; set; }
    public double TotalPressureDrop { get; set; }
    public DateTime GeneratedDate { get; set; }
}

public class CalculationRow
{
    public int LineNo { get; set; }
    public string PipeId { get; set; } = "";
    public string SystemType { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public double LengthM { get; set; }
    public double TotalLU { get; set; }
    public double DesignFlowLps { get; set; }
    public double DiameterDN { get; set; }
    public double VelocityMs { get; set; }
    public double FrictionLossMbar { get; set; }
    public double TotalLossMSS { get; set; }
    public double CumulativeLossMSS { get; set; }
    public bool IsVelocityWarning { get; set; }
    public string Material { get; set; } = "";
}
