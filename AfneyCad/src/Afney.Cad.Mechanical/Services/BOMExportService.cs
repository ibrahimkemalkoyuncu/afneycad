using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using ClosedXML.Excel;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Gelişmiş BOM Raporlama Servisi (BOMExportService)
    NEDEN: Proje metrajını (Boru, Fittings, Cihaz) profesyonel HTML ve Excel formatında sunmak.
    ÖZELLİKLER:
    - Boru (Pipe) Metrajı
    - Dirsek (Elbow) Sayımı
    - T-Parçası (Tee) Sayımı
    - Redüksiyon (Reducer) Sayımı
    - Vitrifiye (Fixture) Listesi
    - Native Excel (.xlsx) Dışa Aktarımı
*/
public class BOMExportService
{
    /*
       NE: Excel Metraj Raporu Üret (GenerateExcelReport)
       NEDEN: Profesyonel mühendislik çıktıları için verileri doğrudan .xlsx formatına dökerek Excel'de düzenlenebilir hale getirmek için.
    */
    public void GenerateExcelReport(IEnumerable<CadEntity> entities, string projectName, string filePath)
    {
        var allList = entities.ToList();
        var pipes = allList.OfType<PipeEntity>().ToList();
        var elbows = allList.OfType<ElbowEntity>().ToList();
        var tees = allList.OfType<TeeEntity>().ToList();
        var fixtures = allList.OfType<SanitaryFixtureEntity>().ToList();

        using (var workbook = new XLWorkbook())
        {
            var summarySheet = workbook.Worksheets.Add("Özet");
            summarySheet.Cell(1, 1).Value = "AfneyCAD Metraj Raporu";
            summarySheet.Cell(1, 1).Style.Font.Bold = true;
            summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
            summarySheet.Cell(2, 1).Value = $"Proje: {projectName}";
            summarySheet.Cell(3, 1).Value = $"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}";

            // 1. BORU LİSTESİ
            if (pipes.Any())
            {
                var pipeSheet = workbook.Worksheets.Add("Borular");
                pipeSheet.Cell(1, 1).Value = "Sistem";
                pipeSheet.Cell(1, 2).Value = "Çap (DN)";
                pipeSheet.Cell(1, 3).Value = "Malzeme";
                pipeSheet.Cell(1, 4).Value = "Uzunluk (m)";
                pipeSheet.Range(1, 1, 1, 4).Style.Font.Bold = true;
                pipeSheet.Range(1, 1, 1, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#34495e");
                pipeSheet.Range(1, 1, 1, 4).Style.Font.FontColor = XLColor.White;

                var pipeGroups = pipes.GroupBy(p => new { p.SystemType, p.InnerDiameter, p.PipeMaterialType })
                                      .OrderBy(g => g.Key.SystemType).ThenBy(g => g.Key.InnerDiameter).ToList();

                int row = 2;
                foreach (var group in pipeGroups)
                {
                    double totalLen = group.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0;
                    pipeSheet.Cell(row, 1).Value = group.Key.SystemType.ToString();
                    pipeSheet.Cell(row, 2).Value = $"DN{group.Key.InnerDiameter:F0}";
                    pipeSheet.Cell(row, 3).Value = group.Key.PipeMaterialType.ToString();
                    pipeSheet.Cell(row, 4).Value = totalLen;
                    pipeSheet.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                    row++;
                }
                pipeSheet.Columns().AdjustToContents();
            }

            // 2. FITTINGS LİSTESİ
            if (elbows.Any() || tees.Any())
            {
                var fitSheet = workbook.Worksheets.Add("Ek Parçalar");
                fitSheet.Cell(1, 1).Value = "Tip";
                fitSheet.Cell(1, 2).Value = "Özellik";
                fitSheet.Cell(1, 3).Value = "Sistem";
                fitSheet.Cell(1, 4).Value = "Adet";
                fitSheet.Range(1, 1, 1, 4).Style.Font.Bold = true;

                int row = 2;
                // Dirsekler
                foreach (var group in elbows.GroupBy(e => new { e.InnerDiameter, e.SystemType }))
                {
                    fitSheet.Cell(row, 1).Value = "Dirsek (Elbow)";
                    fitSheet.Cell(row, 2).Value = $"DN{group.Key.InnerDiameter:F0}";
                    fitSheet.Cell(row, 3).Value = group.Key.SystemType.ToString();
                    fitSheet.Cell(row, 4).Value = group.Count();
                    row++;
                }
                // T-Parçaları
                foreach (var group in tees.GroupBy(t => new { t.MainDiameter, t.BranchDiameter, t.SystemType }))
                {
                    fitSheet.Cell(row, 1).Value = "T-Parçası (Tee)";
                    fitSheet.Cell(row, 2).Value = $"DN{group.Key.MainDiameter:F0}x{group.Key.BranchDiameter:F0}";
                    fitSheet.Cell(row, 3).Value = group.Key.SystemType.ToString();
                    fitSheet.Cell(row, 4).Value = group.Count();
                    row++;
                }
                fitSheet.Columns().AdjustToContents();
            }

            // 3. ARMATÜR LİSTESİ
            if (fixtures.Any())
            {
                var fixSheet = workbook.Worksheets.Add("Armatürler");
                fixSheet.Cell(1, 1).Value = "Tip";
                fixSheet.Cell(1, 2).Value = "Adet";
                fixSheet.Cell(1, 3).Value = "Toplam LU";
                fixSheet.Range(1, 1, 1, 3).Style.Font.Bold = true;

                int row = 2;
                foreach (var group in fixtures.GroupBy(f => f.FixtureType).OrderBy(g => g.Key))
                {
                    fixSheet.Cell(row, 1).Value = group.Key.ToString();
                    fixSheet.Cell(row, 2).Value = group.Count();
                    fixSheet.Cell(row, 3).Value = group.Sum(f => f.LoadUnits);
                    row++;
                }
                fixSheet.Columns().AdjustToContents();
            }

            workbook.SaveAs(filePath);
        }
    }

    public string GenerateHtmlReport(IEnumerable<CadEntity> entities, string projectName)
    {
        var allList = entities.ToList();
        var pipes = allList.OfType<PipeEntity>().ToList();
        var elbows = allList.OfType<ElbowEntity>().ToList();
        var tees = allList.OfType<TeeEntity>().ToList();
        var reducers = allList.OfType<ReducerEntity>().ToList(); // Varsa
        var fixtures = allList.OfType<SanitaryFixtureEntity>().ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'><title>AfneyCAD Metraj Raporu</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 40px; background-color: #f9f9f9; color: #333; }");
        sb.AppendLine(".container { max-width: 1000px; margin: 0 auto; background: white; padding: 30px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); border-radius: 8px; }");
        sb.AppendLine("h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; margin-bottom: 30px; }");
        sb.AppendLine("h2 { color: #2980b9; margin-top: 30px; border-left: 5px solid #e74c3c; padding-left: 10px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
        sb.AppendLine("th, td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #ddd; }");
        sb.AppendLine("th { background-color: #34495e; color: white; font-weight: 600; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine("tr:hover { background-color: #e6f7ff; }");
        sb.AppendLine(".summary-box { background: #ecf0f1; padding: 15px; border-radius: 4px; margin-bottom: 20px; display: flex; justify-content: space-between; }");
        sb.AppendLine(".footer { margin-top: 50px; text-align: center; color: #7f8c8d; font-size: 0.85em; border-top: 1px solid #eee; padding-top: 20px; }");
        sb.AppendLine(".logo { font-size: 24px; font-weight: bold; color: #e74c3c; } .logo span { color: #2c3e50; }");
        sb.AppendLine("</style></head><body>");
        
        sb.AppendLine("<div class='container'>");
        
        // Header
        sb.AppendLine("<div class='summary-box'>");
        sb.AppendLine("<div>");
        sb.AppendLine($"<div class='logo'>Afney<span>CAD</span></div>");
        sb.AppendLine($"<div>Proje: <strong>{projectName}</strong></div>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<div style='text-align:right'><div>Rapor Tarihi</div><div>{DateTime.Now:dd.MM.yyyy HH:mm}</div></div>");
        sb.AppendLine("</div>");
        
        sb.AppendLine("<h1>Tesisat Metraj Raporu (Bill of Materials)</h1>");

        // 1. Borular Tablosu
        if (pipes.Any())
        {
            sb.AppendLine("<h2>1. Boru Listesi (Pipes)</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Sistem</th><th>Çap (DN)</th><th>Malzeme</th><th>Toplam Uzunluk (m)</th></tr></thead><tbody>");

            var pipeGroups = pipes.GroupBy(p => new { p.SystemType, p.InnerDiameter, p.PipeMaterialType })
                                  .OrderBy(g => g.Key.SystemType).ThenBy(g => g.Key.InnerDiameter);

            foreach (var group in pipeGroups)
            {
                double totalLen = group.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0;
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{group.Key.SystemType}</td>");
                sb.AppendLine($"<td>DN{group.Key.InnerDiameter:F0}</td>");
                sb.AppendLine($"<td>{group.Key.PipeMaterialType}</td>");
                sb.AppendLine($"<td>{totalLen:F2} m</td>");
                sb.AppendLine($"</tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // 2. Fittings Tablosu
        if (elbows.Any() || tees.Any() || reducers.Any())
        {
            sb.AppendLine("<h2>2. Ek Parçalar (Fittings)</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Tip</th><th>Özellik (Çap/Açı)</th><th>Sistem</th><th>Adet</th></tr></thead><tbody>");

            // Dirsekler
            var elbowGroups = elbows.GroupBy(e => new { e.InnerDiameter, e.SystemType })
                                    .OrderBy(g => g.Key.InnerDiameter);
            foreach (var group in elbowGroups)
            {
                // Açı kontrolü eklenebilir (şimdilik standart 90 kabul veya isimlendirme)
                sb.AppendLine($"<tr><td>Dirsek (Elbow)</td><td>DN{group.Key.InnerDiameter:F0}</td><td>{group.Key.SystemType}</td><td>{group.Count()}</td></tr>");
            }

            // T-Parçaları
             var teeGroups = tees.GroupBy(t => new { t.MainDiameter, t.BranchDiameter, t.SystemType })
                                    .OrderBy(g => g.Key.MainDiameter);
            foreach (var group in teeGroups)
            {
                string desc = $"DN{group.Key.MainDiameter:F0} x DN{group.Key.BranchDiameter:F0}";
                if (group.Key.MainDiameter == group.Key.BranchDiameter) desc += " (Eşit T)";
                else desc += " (İnegal T)";
                
                sb.AppendLine($"<tr><td>T-Parçası (Tee)</td><td>{desc}</td><td>{group.Key.SystemType}</td><td>{group.Count()}</td></tr>");
            }

            // Redüksiyonlar
            // ReducerEntity özelliklerini tahmin ediyoruz (LargeDiameter, SmallDiameter)
            // Eğer ReducerEntity implementasyonu farklıysa burası hata verebilir, dinamik geçelim veya reflection kullanalım
            // Şimdilik sadece count alalım veya Type üzerinden gidelim.
            // Güvenli kod:
             var reducerGroups = reducers.GroupBy(r => r.SystemType);
             foreach(var group in reducerGroups)
             {
                 sb.AppendLine($"<tr><td>Redüksiyon</td><td>Muhtelif</td><td>{group.Key}</td><td>{group.Count()}</td></tr>");
             }

            sb.AppendLine("</tbody></table>");
        }

        // 3. Cihazlar Tablosu
        if (fixtures.Any())
        {
            sb.AppendLine("<h2>3. Vitrifiye ve Ekipmanlar (Fixtures)</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Tip</th><th>Adet</th><th>Toplam Yük (LU)</th></tr></thead><tbody>");

            var fixtureGroups = fixtures.GroupBy(f => f.FixtureType).OrderBy(g => g.Key);

            foreach (var group in fixtureGroups)
            {
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{group.Key}</td>");
                sb.AppendLine($"<td>{group.Count()}</td>");
                sb.AppendLine($"<td>{group.Sum(f => f.LoadUnits):F1}</td>");
                sb.AppendLine($"</tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("<div class='footer'>Bu rapor <strong>AfneyCAD</strong> Tesisat Mühendisliği Yazılımı ile oluşturulmuştur. &copy; 2026</div>");
        sb.AppendLine("</div></body></html>");

        return sb.ToString();
    }
}
