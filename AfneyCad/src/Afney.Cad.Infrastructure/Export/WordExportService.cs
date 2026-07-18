using System;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Infrastructure.Export;

/*
   NE: Word (.docx) Dışa Aktarma Servisi (WordExportService)
   NEDEN: 4M FineSANI'nin Word/Excel/PDF üçlü çıktı setinden AfneyCAD'de sadece Excel ve
          PDF vardı — Word (.docx) hesap raporu formatı hiç yoktu (haritalama denetiminde
          "Raporlama" kategorisinin en somut eksiği buydu). ExcelExportService ile aynı
          4 bölümü (Özet/Metraj/Pis Su/Yağmur Suyu) DocumentFormat.OpenXml SDK ile gerçek
          bir .docx dosyasına yazar — üçüncü parti Word/Office kurulumu gerektirmez.
*/
public class WordExportService
{
    private readonly CadDatabase _database;

    public WordExportService(CadDatabase database) => _database = database;

    public void WriteToFile(string filePath,
        WasteWaterCalcSheetService.CalcSheetResult? wasteResult = null,
        RainWaterCalcSheetService.CalcSheetResult?  rainResult  = null,
        string projectName = "AfneyCAD Projesi",
        string engineer    = "")
    {
        using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddTitle(body, "AfneyCAD — Proje Hesap Raporu");
        AddSummarySection(body, projectName, engineer);
        AddBomSection(body);
        if (wasteResult is not null) AddWasteWaterSection(body, wasteResult);
        if (rainResult  is not null) AddRainWaterSection(body, rainResult);

        mainPart.Document.Save();
    }

    // ── 1. Başlık ────────────────────────────────────────────────────────────
    private static void AddTitle(Body body, string text)
    {
        var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "200" }));
        var run = new Run(new Text(text));
        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "32" }, new Color { Val = "1565C0" });
        p.Append(run);
        body.Append(p);
    }

    private static void AddHeading(Body body, string text)
    {
        var p = new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { Before = "300", After = "150" }));
        var run = new Run(new Text(text));
        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "26" }, new Color { Val = "0D3060" });
        p.Append(run);
        body.Append(p);
    }

    private static void AddParagraph(Body body, string text, bool bold = false)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (bold) run.RunProperties = new RunProperties(new Bold());
        body.Append(new Paragraph(run));
    }

    // ── 2. Özet Bölümü ───────────────────────────────────────────────────────
    private void AddSummarySection(Body body, string projectName, string engineer)
    {
        AddHeading(body, "1. Proje Özeti");
        AddParagraph(body, $"Proje Adı: {projectName}");
        AddParagraph(body, $"Mühendis: {engineer}");
        AddParagraph(body, $"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
        AddParagraph(body, "Standart: TS EN 806, TS EN 12056");

        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();

        var table = CreateTable();
        AddTableRow(table, ["Sistem", "Boru Sayısı", "Toplam Uzunluk (m)"], isHeader: true);

        foreach (var g in pipes.GroupBy(p => p.SystemType).OrderBy(g => g.Key.ToString()))
        {
            AddTableRow(table, [
                SystemTypeName(g.Key),
                g.Count().ToString(),
                Math.Round(g.Sum(p => p.Length / 1000.0), 2).ToString("F2")
            ]);
        }
        body.Append(table);

        AddParagraph(body, "");
        AddParagraph(body, $"Toplam Boru Adedi: {pipes.Count}");
        AddParagraph(body, $"Toplam Boru Uzunluğu: {Math.Round(pipes.Sum(p => p.Length / 1000.0), 2):F2} m");
        AddParagraph(body, $"Toplam Armatür Adedi: {fixtures.Count}");
        AddParagraph(body, $"Toplam Yükleme Birimi: {Math.Round(fixtures.Sum(f => f.LoadUnits), 1):F1} DU");
    }

    // ── 3. Metraj (BOQ) Bölümü ───────────────────────────────────────────────
    private void AddBomSection(Body body)
    {
        AddHeading(body, "2. Malzeme Metrajı (BOQ)");

        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var pipeGroups = pipes
            .GroupBy(p => (p.SystemType, Dn: Math.Round(p.InnerDiameter)))
            .OrderBy(g => g.Key.SystemType.ToString()).ThenBy(g => g.Key.Dn);

        var table = CreateTable();
        AddTableRow(table, ["No", "Sistem", "Çap (mm)", "Adet", "Uzunluk (m)"], isHeader: true);

        int no = 1;
        foreach (var g in pipeGroups)
        {
            AddTableRow(table, [
                (no++).ToString(),
                SystemTypeName(g.Key.SystemType),
                g.Key.Dn.ToString("F0"),
                g.Count().ToString(),
                Math.Round(g.Sum(p => p.Length / 1000.0), 2).ToString("F2")
            ]);
        }
        AddTableRow(table, ["", "TOPLAM", "", pipes.Count.ToString(), Math.Round(pipes.Sum(p => p.Length / 1000.0), 2).ToString("F2")], isHeader: true);
        body.Append(table);

        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
        if (fixtures.Count > 0)
        {
            AddParagraph(body, "");
            AddParagraph(body, "Armatürler", bold: true);

            var fixTable = CreateTable();
            AddTableRow(fixTable, ["No", "Tip", "Sistem", "Adet", "Toplam DU"], isHeader: true);

            no = 1;
            foreach (var g in fixtures.GroupBy(f => (f.FixtureType, f.SystemType)).OrderBy(g => g.Key.FixtureType))
            {
                AddTableRow(fixTable, [
                    (no++).ToString(),
                    g.Key.FixtureType,
                    SystemTypeName(g.Key.SystemType),
                    g.Count().ToString(),
                    Math.Round(g.Sum(f => f.LoadUnits), 2).ToString("F2")
                ]);
            }
            body.Append(fixTable);
        }
    }

    // ── 4. Pis Su Hesap Föyü ─────────────────────────────────────────────────
    private static void AddWasteWaterSection(Body body, WasteWaterCalcSheetService.CalcSheetResult result)
    {
        AddHeading(body, "3. Pis Su Hesap Föyü — TS EN 12056-2");
        AddParagraph(body, $"Yöntem: {result.Options.Method}   Bina Tipi: {result.Options.BuildingType}   K: {result.Options.FrequencyFactor}   Manning n: {result.Options.RoughnessN}");

        var table = CreateTable();
        AddTableRow(table, ["No", "Segment", "Tip", "L (m)", "DU", "Q (l/s)", "DN (mm)", "Eğim %", "V (m/s)", "Durum"], isHeader: true);

        foreach (var r in result.Rows)
        {
            AddTableRow(table, [
                r.SegmentNo.ToString(), r.SegmentId, r.PipeType, r.LengthM.ToString("F2"),
                r.LoadUnits.ToString("F2"), r.DesignFlowLs.ToString("F2"), r.DiameterMm.ToString("F0"),
                r.SlopePct.ToString("F2"), r.VelocityMs.ToString("F2"), r.IsOk ? "✓" : "⚠ " + r.Warnings
            ]);
        }
        body.Append(table);
        AddParagraph(body, "");
        AddParagraph(body, result.Summary, bold: true);
    }

    // ── 5. Yağmur Suyu Hesap Föyü ────────────────────────────────────────────
    private static void AddRainWaterSection(Body body, RainWaterCalcSheetService.CalcSheetResult result)
    {
        AddHeading(body, "4. Yağmur Suyu Hesap Föyü — TS EN 12056-3");
        AddParagraph(body, $"Konum: {result.Options.Location}   r: {result.Options.RainfallIntensity} l/s·m²   Eğim: %{result.Options.DefaultSlopePct}");

        var table = CreateTable();
        AddTableRow(table, ["No", "Alan Adı", "Yüzey", "A (m²)", "C", "Q (l/s)", "DN (mm)", "Eğim %", "V (m/s)", "Durum"], isHeader: true);

        foreach (var r in result.Rows)
        {
            AddTableRow(table, [
                r.RowNo.ToString(), r.AreaName, r.SurfaceType, r.AreaM2.ToString("F2"),
                r.RunoffC.ToString("F2"), r.DesignFlowLs.ToString("F2"), r.DiameterMm.ToString("F0"),
                r.SlopePct.ToString("F2"), r.VelocityMs.ToString("F2"), r.IsOk ? "✓" : "⚠ " + r.Warnings
            ]);
        }
        body.Append(table);
        AddParagraph(body, "");
        AddParagraph(body, result.Summary, bold: true);
    }

    // ── Tablo Yardımcıları ───────────────────────────────────────────────────
    private static Table CreateTable()
    {
        var table = new Table();
        var props = new TableProperties(
            new TableBorders(
                new TopBorder     { Val = BorderValues.Single, Size = 6, Color = "999999" },
                new BottomBorder  { Val = BorderValues.Single, Size = 6, Color = "999999" },
                new LeftBorder    { Val = BorderValues.Single, Size = 6, Color = "999999" },
                new RightBorder   { Val = BorderValues.Single, Size = 6, Color = "999999" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" });
        table.AppendChild(props);
        return table;
    }

    private static void AddTableRow(Table table, string[] cells, bool isHeader = false)
    {
        var row = new TableRow();
        foreach (var cellText in cells)
        {
            var run = new Run(new Text(cellText) { Space = SpaceProcessingModeValues.Preserve });
            if (isHeader) run.RunProperties = new RunProperties(new Bold(), new Color { Val = "FFFFFF" });

            var cell = new TableCell(new Paragraph(run));
            if (isHeader)
            {
                cell.TableCellProperties = new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "1976D2" });
            }
            row.Append(cell);
        }
        table.Append(row);
    }

    private static string SystemTypeName(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => "Temiz Soğuk Su",
        MechanicalSystemType.DomesticHotWater  => "Temiz Sıcak Su",
        MechanicalSystemType.WasteWater        => "Pis Su",
        MechanicalSystemType.RainWater         => "Yağmur Suyu",
        MechanicalSystemType.FireProtection    => "Yangın",
        MechanicalSystemType.Gas               => "Doğalgaz",
        MechanicalSystemType.Ventilation       => "Havalandırma",
        _                                      => "Genel"
    };
}
