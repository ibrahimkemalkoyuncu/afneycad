using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Afney.Cad.Infrastructure.Export;

// Gelişmiş Excel Export — çoklu sayfa (CSV-based, XLSX alternatifi)
public class ExcelMultiSheetService
{
    private readonly CadDatabase _database;

    public ExcelMultiSheetService(CadDatabase database) => _database = database;

    public ExcelWorkbook GenerateWorkbook(string projectName = "AfneyCAD")
    {
        var workbook = new ExcelWorkbook { ProjectName = projectName };

        // Sheet 1: Boru Listesi
        var pipeSheet = new ExcelSheet("Boru Listesi");
        pipeSheet.Headers.AddRange(new[] { "ID", "Sistem", "Malzeme", "DN (mm)", "Uzunluk (m)", "Debi (l/s)", "Hız (m/s)", "Basınç Kaybı (mSS)", "Eğim (%)", "Katman" });

        foreach (var pipe in _database.GetAllEntities().OfType<PipeEntity>())
        {
            pipeSheet.Rows.Add(new List<string>
            {
                pipe.Id.ToString()[..8],
                pipe.SystemType.ToString(),
                pipe.PipeMaterialType.ToString(),
                pipe.InnerDiameter.ToString("F0"),
                (pipe.GetLength() / 1000.0).ToString("F2"),
                pipe.FlowRate.ToString("F3"),
                pipe.Velocity.ToString("F2"),
                pipe.PressureDrop.ToString("F4"),
                (pipe.Slope * 100).ToString("F1"),
                pipe.Layer ?? "0"
            });
        }
        workbook.Sheets.Add(pipeSheet);

        // Sheet 2: Cihaz Listesi
        var fixtureSheet = new ExcelSheet("Cihaz Listesi");
        fixtureSheet.Headers.AddRange(new[] { "ID", "Tip", "Yük Birimi (FU)", "Konum X", "Konum Y", "Katman" });

        foreach (var fix in _database.GetAllEntities().OfType<SanitaryFixtureEntity>())
        {
            fixtureSheet.Rows.Add(new List<string>
            {
                fix.Id.ToString()[..8],
                fix.FixtureType,
                fix.FixtureUnit.ToString("F1"),
                fix.Position.X.ToString("F0"),
                fix.Position.Y.ToString("F0"),
                fix.Layer ?? "0"
            });
        }
        workbook.Sheets.Add(fixtureSheet);

        // Sheet 3: Metraj Özeti
        var bomSheet = new ExcelSheet("Metraj Özeti");
        bomSheet.Headers.AddRange(new[] { "Malzeme", "Çap (mm)", "Toplam Uzunluk (m)", "Adet", "Birim", "Birim Fiyat (TL)", "Toplam (TL)" });

        var pipeGroups = _database.GetAllEntities().OfType<PipeEntity>()
            .GroupBy(p => $"{p.PipeMaterialType}_{p.InnerDiameter:F0}");

        foreach (var group in pipeGroups)
        {
            double totalLength = group.Sum(p => p.GetLength()) / 1000.0;
            double unitPrice = EstimateUnitPrice(group.First());
            bomSheet.Rows.Add(new List<string>
            {
                group.First().PipeMaterialType.ToString(),
                group.First().InnerDiameter.ToString("F0"),
                totalLength.ToString("F2"),
                group.Count().ToString(),
                "m",
                unitPrice.ToString("N0"),
                (totalLength * unitPrice).ToString("N0")
            });
        }
        workbook.Sheets.Add(bomSheet);

        // Sheet 4: Katman Özeti
        var layerSheet = new ExcelSheet("Katman Özeti");
        layerSheet.Headers.AddRange(new[] { "Katman Adı", "Entity Sayısı", "Renk" });

        var layerGroups = _database.GetAllEntities().GroupBy(e => e.Layer ?? "0");
        foreach (var group in layerGroups.OrderByDescending(g => g.Count()))
        {
            var layer = _database.GetLayer(group.Key);
            layerSheet.Rows.Add(new List<string>
            {
                group.Key,
                group.Count().ToString(),
                layer != null ? $"#{layer.Color:X6}" : "#FFFFFF"
            });
        }
        workbook.Sheets.Add(layerSheet);

        // Sheet 5: Proje Bilgileri
        var infoSheet = new ExcelSheet("Proje Bilgileri");
        infoSheet.Headers.AddRange(new[] { "Özellik", "Değer" });
        infoSheet.Rows.Add(new List<string> { "Proje Adı", projectName });
        infoSheet.Rows.Add(new List<string> { "Tarih", DateTime.Now.ToString("dd.MM.yyyy HH:mm") });
        infoSheet.Rows.Add(new List<string> { "Toplam Entity", _database.GetAllEntities().Count().ToString() });
        infoSheet.Rows.Add(new List<string> { "Toplam Boru", _database.GetAllEntities().OfType<PipeEntity>().Count().ToString() });
        infoSheet.Rows.Add(new List<string> { "Toplam Cihaz", _database.GetAllEntities().OfType<SanitaryFixtureEntity>().Count().ToString() });
        infoSheet.Rows.Add(new List<string> { "Katman Sayısı", _database.GetLayers().Count().ToString() });
        workbook.Sheets.Add(infoSheet);

        return workbook;
    }

    // Her sheet'i ayrı CSV olarak yaz
    public void ExportToCsvFolder(ExcelWorkbook workbook, string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        foreach (var sheet in workbook.Sheets)
        {
            string filePath = Path.Combine(folderPath, $"{sheet.Name}.csv");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", sheet.Headers));
            foreach (var row in sheet.Rows)
                sb.AppendLine(string.Join(";", row));
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }

    // Tek HTML dosyasında tüm sheet'ler (tab görünümü)
    public string ExportToHtml(ExcelWorkbook workbook)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine($"<title>{workbook.ProjectName} — Excel Çıktısı</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;font-size:10pt;margin:15px}");
        sb.AppendLine("h2{color:#1a5276;margin-top:25px}table{border-collapse:collapse;width:100%;margin:8px 0}");
        sb.AppendLine("th{background:#1a5276;color:white;padding:6px;text-align:left}td{border:1px solid #ddd;padding:5px}");
        sb.AppendLine("tr:nth-child(even){background:#f5f5f5}</style></head><body>");

        foreach (var sheet in workbook.Sheets)
        {
            sb.AppendLine($"<h2>{sheet.Name}</h2>");
            sb.AppendLine("<table><tr>");
            foreach (var h in sheet.Headers) sb.Append($"<th>{h}</th>");
            sb.AppendLine("</tr>");
            foreach (var row in sheet.Rows)
            {
                sb.Append("<tr>");
                foreach (var cell in row) sb.Append($"<td>{cell}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private double EstimateUnitPrice(PipeEntity pipe) => pipe.PipeMaterialType switch
    {
        Mechanical.Enums.PipeMaterial.PPRC_PN20 => pipe.InnerDiameter switch { <= 20 => 85, <= 25 => 110, <= 32 => 145, <= 40 => 195, _ => 280 },
        Mechanical.Enums.PipeMaterial.PVC_SN4 => pipe.InnerDiameter switch { <= 50 => 65, <= 75 => 95, <= 100 => 135, _ => 185 },
        Mechanical.Enums.PipeMaterial.Steel_Galvanized => pipe.InnerDiameter switch { <= 15 => 120, <= 20 => 155, _ => 195 },
        _ => 100
    };
}

public class ExcelWorkbook
{
    public string ProjectName { get; set; } = "";
    public List<ExcelSheet> Sheets { get; set; } = new();
}

public class ExcelSheet
{
    public string Name { get; set; }
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public ExcelSheet(string name) => Name = name;
}
