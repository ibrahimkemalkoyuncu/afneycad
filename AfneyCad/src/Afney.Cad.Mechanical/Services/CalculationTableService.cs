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

    /*
       NE: Pis Su ve Yağmur Suyu Hesap Tablosu Üret (TS EN 12056-2 / TS EN 12056-3)
       NEDEN: Gravitasyonel pis su sistemleri basınç tablosu ile değil, DU tabanlı debi +
              Manning eğim ile hesaplanır. Temiz su tablosundan ayrı bir föy gerektirir.
    */
    public WasteWaterCalcTable GenerateWasteWaterTable(string projectName = "AfneyCAD Projesi", double kFactor = 0.5)
    {
        var table = new WasteWaterCalcTable { ProjectName = projectName, FrequencyFactor = kFactor };

        var wastePipes = _database.GetAllEntities().OfType<PipeEntity>()
            .Where(p => p.SystemType == MechanicalSystemType.WasteWater ||
                        p.SystemType == MechanicalSystemType.RainWater)
            .OrderBy(p => p.SystemType)
            .ThenByDescending(p => p.LoadUnits)
            .ToList();

        int lineNo = 1;
        foreach (var pipe in wastePipes)
        {
            double lengthM = pipe.GetLength() / 1000.0;
            double totalDU = pipe.LoadUnits;
            double qWw = totalDU > 0 ? kFactor * Math.Sqrt(totalDU) : 0;
            double slopePct = MinimumSlopePct(pipe.InnerDiameter);
            double slopeM = slopePct / 100.0;
            double vManning = EstimateVelocityManning(pipe.InnerDiameter, slopeM);
            double fillingRatio = EstimateFillingRatio(qWw, pipe.InnerDiameter, slopeM);

            var row = new WasteWaterCalcRow
            {
                LineNo = lineNo++,
                PipeId = pipe.Id.ToString().Substring(0, 8),
                SystemType = GetSystemLabel(pipe.SystemType),
                LengthM = lengthM,
                TotalDU = totalDU,
                FrequencyFactor = kFactor,
                FlowQww = qWw,
                DiameterDN = pipe.InnerDiameter,
                SlopePct = slopePct,
                FillingRatioPct = fillingRatio * 100.0,
                VelocityMs = vManning,
                Material = pipe.PipeMaterialType.ToString(),
                IsWarning = fillingRatio > 0.7 || vManning < 0.6
            };
            table.Rows.Add(row);
        }

        table.TotalPipeCount = wastePipes.Count;
        table.TotalLength = wastePipes.Sum(p => p.GetLength() / 1000.0);
        table.GeneratedDate = DateTime.Now;
        return table;
    }

    /*
       NE: Pis Su Hesap Föyü HTML Raporu
       NEDEN: TS EN 12056-2 standardında mühendislik onay belgesi olarak sunulacak.
    */
    public string ExportWasteWaterToHtml(WasteWaterCalcTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'>");
        sb.AppendLine($"<title>Pis Su Hesap Föyü - {table.ProjectName}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;margin:20px;color:#333;}");
        sb.AppendLine("h1{color:#8B4513;border-bottom:3px solid #8B4513;padding-bottom:8px;}");
        sb.AppendLine("h2{color:#555;font-size:14px;margin-top:0;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-top:12px;font-size:12px;}");
        sb.AppendLine("th,td{border:1px solid #ccc;padding:6px 8px;text-align:center;}");
        sb.AppendLine("th{background:#5D4037;color:#fff;font-weight:bold;}");
        sb.AppendLine("tr:nth-child(even){background:#fdf8f5;}");
        sb.AppendLine(".warn{background:#fff3cd;color:#856404;font-weight:bold;}");
        sb.AppendLine(".ok{color:#2E7D32;}");
        sb.AppendLine(".summary{margin-top:18px;padding:12px;background:#efebe9;border-radius:4px;}");
        sb.AppendLine(".footer{margin-top:28px;font-size:10px;color:#999;text-align:center;}");
        sb.AppendLine(".legend{margin-top:15px;font-size:11px;color:#666;}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h1>PİS SU TESİSATI HİDROLİK HESAP FÖYÜ</h1>");
        sb.AppendLine($"<h2>TS EN 12056-2 — Cazibeli Pis Su Sistemleri (Eş Zamanlılık Sistemi II, K={table.FrequencyFactor})</h2>");
        sb.AppendLine($"<p>Proje: <strong>{table.ProjectName}</strong> &nbsp;|&nbsp; Tarih: {table.GeneratedDate:dd.MM.yyyy HH:mm}</p>");

        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine("<th>Hat No</th><th>Boru ID</th><th>Sistem</th><th>Uzunluk (m)</th>");
        sb.AppendLine("<th>ΣDU</th><th>K</th><th>Q<sub>ww</sub> (lt/s)</th>");
        sb.AppendLine("<th>DN (mm)</th><th>Eğim (%)</th><th>Doluluk (%)</th><th>v (m/s)</th><th>Malzeme</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var row in table.Rows)
        {
            string cls = row.IsWarning ? " class='warn'" : "";
            string fillCls = row.FillingRatioPct > 70 ? " style='color:red;font-weight:bold'" : " class='ok'";
            string vClass = row.VelocityMs < 0.6 ? " style='color:orange'" : "";
            sb.AppendLine($"<tr{cls}>");
            sb.AppendLine($"<td>{row.LineNo}</td>");
            sb.AppendLine($"<td>#{row.PipeId}</td>");
            sb.AppendLine($"<td>{row.SystemType}</td>");
            sb.AppendLine($"<td>{row.LengthM:F2}</td>");
            sb.AppendLine($"<td>{(row.TotalDU > 0 ? row.TotalDU.ToString("F1") : "—")}</td>");
            sb.AppendLine($"<td>{row.FrequencyFactor:F1}</td>");
            sb.AppendLine($"<td>{row.FlowQww:F3}</td>");
            sb.AppendLine($"<td>DN {row.DiameterDN:F0}</td>");
            sb.AppendLine($"<td>{row.SlopePct:F1}</td>");
            sb.AppendLine($"<td{fillCls}>{row.FillingRatioPct:F0}</td>");
            sb.AppendLine($"<td{vClass}>{row.VelocityMs:F2}</td>");
            sb.AppendLine($"<td>{row.Material}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class='legend'>");
        sb.AppendLine("⚠ Sarı = Doluluk %70 aşıldı veya v &lt; 0.6 m/s (öz-temizleme hızı yetersiz) &nbsp;|&nbsp;");
        sb.AppendLine("Formül: Q<sub>ww</sub> = K × √(ΣDU)");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='summary'><h3>Özet</h3>");
        sb.AppendLine($"<p><strong>Toplam Boru Segmenti:</strong> {table.TotalPipeCount} adet</p>");
        sb.AppendLine($"<p><strong>Toplam Uzunluk:</strong> {table.TotalLength:F1} metre</p>");
        sb.AppendLine($"<p><strong>Frekans Faktörü K:</strong> {table.FrequencyFactor} (Sistem II — Konut)</p>");
        int warnings = table.Rows.Count(r => r.IsWarning);
        sb.AppendLine($"<p><strong>Uyarı Sayısı:</strong> {(warnings > 0 ? $"<span style='color:red'>{warnings} ⚠</span>" : "<span style='color:green'>0 ✓</span>")}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine($"<div class='footer'>AfneyCAD Engine — TS EN 12056-2 Pis Su Hesap Föyü — {table.GeneratedDate:dd.MM.yyyy}</div>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static double MinimumSlopePct(double dn) => dn switch
    {
        <= 50  => 2.5,
        <= 75  => 2.0,
        <= 100 => 1.0,
        <= 125 => 0.8,
        <= 150 => 0.7,
        <= 200 => 0.5,
        _      => 0.3
    };

    private static double EstimateVelocityManning(double dn, double slope)
    {
        double dM = dn / 1000.0;
        double n = 0.012; // PVC Manning katsayısı
        double R = dM / 4.0; // Tam dolu için hidrolik yarıçap
        return (1.0 / n) * Math.Pow(R, 2.0 / 3.0) * Math.Pow(Math.Max(slope, 0.001), 0.5);
    }

    private static double EstimateFillingRatio(double qLps, double dn, double slope)
    {
        double dM = dn / 1000.0;
        double area = Math.PI * (dM / 2.0) * (dM / 2.0);
        double n = 0.012;
        double R = dM / 4.0;
        double qFullLps = (1.0 / n) * area * Math.Pow(R, 2.0 / 3.0) * Math.Pow(Math.Max(slope, 0.001), 0.5) * 1000.0;
        return qFullLps > 0 ? Math.Min(qLps / qFullLps, 1.0) : 0;
    }

    private string GetSystemLabel(MechanicalSystemType type) => type switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater => "Sıcak Su",
        MechanicalSystemType.WasteWater => "Pis Su",
        MechanicalSystemType.RainWater => "Yağmur Suyu",
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

// --- PİS SU HESAP TABLOSU VERİ MODELLERİ (TS EN 12056-2) ---

public class WasteWaterCalcTable
{
    public string ProjectName { get; set; } = "";
    public double FrequencyFactor { get; set; } = 0.5;
    public List<WasteWaterCalcRow> Rows { get; set; } = [];
    public int TotalPipeCount { get; set; }
    public double TotalLength { get; set; }
    public DateTime GeneratedDate { get; set; }
}

public class WasteWaterCalcRow
{
    public int LineNo { get; set; }
    public string PipeId { get; set; } = "";
    public string SystemType { get; set; } = "";
    public double LengthM { get; set; }
    public double TotalDU { get; set; }
    public double FrequencyFactor { get; set; }
    public double FlowQww { get; set; }       // lt/s
    public double DiameterDN { get; set; }
    public double SlopePct { get; set; }      // %
    public double FillingRatioPct { get; set; } // %
    public double VelocityMs { get; set; }
    public string Material { get; set; } = "";
    public bool IsWarning { get; set; }
}
