using System;
using System.Linq;
using ClosedXML.Excel;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Infrastructure.Export;

/*
   NE: Excel (.xlsx) Dışa Aktarma Servisi
   NEDEN: AfneyCAD verilerini (Metraj, Hesap Föyü, Özet) Microsoft Excel formatında
          dışa aktarmak için. FineSANI'nin xlsx çıktısıyla eşdeğer.

   SAYFALAR:
     1. Özet      — Proje bilgileri, sistem dağılımı, toplam boru uzunluğu
     2. Metraj    — Grup bazlı boru/armatör malzeme listesi (BOQ)
     3. Pis Su    — WasteWaterCalcSheetService.CalcRow tablosu
     4. Yağmur Suyu — RainWaterCalcSheetService.CalcRow tablosu
*/
public class ExcelExportService
{
    private readonly CadDatabase _database;

    // Tema renkleri
    private static readonly XLColor HeaderBg  = XLColor.FromHtml("#1565C0");
    private static readonly XLColor HeaderFg  = XLColor.White;
    private static readonly XLColor SubHdrBg  = XLColor.FromHtml("#1976D2");
    private static readonly XLColor AltRowBg  = XLColor.FromHtml("#EEF2FF");
    private static readonly XLColor WarnFg    = XLColor.FromHtml("#E65100");
    private static readonly XLColor OkFg      = XLColor.FromHtml("#2E7D32");

    public ExcelExportService(CadDatabase database) => _database = database;

    public void WriteToFile(string filePath,
        WasteWaterCalcSheetService.CalcSheetResult?  wasteResult  = null,
        RainWaterCalcSheetService.CalcSheetResult?   rainResult   = null,
        string projectName = "AfneyCAD Projesi",
        string engineer    = "")
    {
        using var wb = new XLWorkbook();

        AddSummarySheet(wb, projectName, engineer);
        AddBomSheet(wb);
        if (wasteResult is not null) AddWasteWaterSheet(wb, wasteResult);
        if (rainResult  is not null) AddRainWaterSheet(wb, rainResult);

        wb.SaveAs(filePath);
    }

    // ── 1. Özet Sayfası ───────────────────────────────────────────────────────
    private void AddSummarySheet(XLWorkbook wb, string projectName, string engineer)
    {
        var ws = wb.Worksheets.Add("Özet");

        // Başlık bloğu
        ws.Cell("A1").Value = "AfneyCAD — Proje Özeti";
        ws.Range("A1:E1").Merge().Style
            .Font.SetBold(true).Font.SetFontSize(16).Font.SetFontColor(HeaderFg)
            .Fill.SetBackgroundColor(HeaderBg)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        ws.Cell("A2").Value = "Proje Adı:";      ws.Cell("B2").Value = projectName;
        ws.Cell("A3").Value = "Mühendis:";       ws.Cell("B3").Value = engineer;
        ws.Cell("A4").Value = "Tarih:";          ws.Cell("B4").Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        ws.Cell("A5").Value = "Standart:";       ws.Cell("B5").Value = "TS EN 806, TS EN 12056";

        StyleLabelColumn(ws, 2, 5);

        // Sistem istatistikleri
        int row = 7;
        ws.Cell(row, 1).Value = "SİSTEM İSTATİSTİKLERİ";
        StyleSectionHeader(ws.Row(row));

        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();

        row++;
        ws.Cell(row, 1).Value = "Parametre"; ws.Cell(row, 2).Value = "Değer"; ws.Cell(row, 3).Value = "Birim";
        StyleTableHeader(ws.Range(row, 1, row, 3));

        var systemGroups = pipes.GroupBy(p => p.SystemType).OrderBy(g => g.Key.ToString());
        foreach (var g in systemGroups)
        {
            row++;
            ws.Cell(row, 1).Value = SystemTypeName(g.Key) + " — Boru Sayısı";
            ws.Cell(row, 2).Value = g.Count();
            ws.Cell(row, 3).Value = "adet";
            ws.Cell(row, 4).Value = SystemTypeName(g.Key) + " — Toplam Uzunluk";
            ws.Cell(row, 5).Value = Math.Round(g.Sum(p => p.Length / 1000.0), 2);
            ws.Cell(row, 6).Value = "m";
            if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.SetBackgroundColor(AltRowBg);
        }

        row += 2;
        ws.Cell(row, 1).Value = "Toplam Boru Adedi"; ws.Cell(row, 2).Value = pipes.Count; ws.Cell(row, 3).Value = "adet";
        row++;
        ws.Cell(row, 1).Value = "Toplam Boru Uzunluğu"; ws.Cell(row, 2).Value = Math.Round(pipes.Sum(p => p.Length / 1000.0), 2); ws.Cell(row, 3).Value = "m";
        row++;
        ws.Cell(row, 1).Value = "Toplam Armatür Adedi"; ws.Cell(row, 2).Value = fixtures.Count; ws.Cell(row, 3).Value = "adet";
        row++;
        ws.Cell(row, 1).Value = "Toplam Yükleme Birimi"; ws.Cell(row, 2).Value = Math.Round(fixtures.Sum(f => f.LoadUnits), 1); ws.Cell(row, 3).Value = "DU";

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 35;
    }

    // ── 2. Metraj (BOQ) Sayfası ───────────────────────────────────────────────
    private void AddBomSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Metraj (BOQ)");

        // Başlık
        ws.Cell("A1").Value = "MALZEME METRAJİ (BOQ)";
        ws.Range("A1:G1").Merge().Style
            .Font.SetBold(true).Font.SetFontSize(14).Font.SetFontColor(HeaderFg)
            .Fill.SetBackgroundColor(HeaderBg)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        ws.Cell("A2").Value = $"Tarih: {DateTime.Now:dd.MM.yyyy}";

        // Boru metrajı tablosu
        int row = 4;
        ws.Cell(row, 1).Value = "BORULAR";
        StyleSectionHeader(ws.Row(row));
        row++;

        string[] pipeHeaders = ["No", "Sistem", "Çap (mm)", "Adet", "Uzunluk (m)", "Ort. Eğim %", "Notlar"];
        for (int c = 0; c < pipeHeaders.Length; c++)
            ws.Cell(row, c + 1).Value = pipeHeaders[c];
        StyleTableHeader(ws.Range(row, 1, row, pipeHeaders.Length));

        var pipeGroups = _database.GetAllEntities().OfType<PipeEntity>()
            .GroupBy(p => (p.SystemType, Dn: Math.Round(p.InnerDiameter)))
            .OrderBy(g => g.Key.SystemType.ToString()).ThenBy(g => g.Key.Dn);

        int no = 1;
        foreach (var g in pipeGroups)
        {
            row++;
            ws.Cell(row, 1).Value = no++;
            ws.Cell(row, 2).Value = SystemTypeName(g.Key.SystemType);
            ws.Cell(row, 3).Value = g.Key.Dn;
            ws.Cell(row, 4).Value = g.Count();
            ws.Cell(row, 5).Value = Math.Round(g.Sum(p => p.Length / 1000.0), 2);
            ws.Cell(row, 6).Value = g.Average(p => p.Slope).ToString("F2");
            ws.Cell(row, 7).Value = $"DN{g.Key.Dn} {SystemTypeName(g.Key.SystemType)}";
            if (row % 2 == 0) ws.Range(row, 1, row, 7).Style.Fill.SetBackgroundColor(AltRowBg);
        }

        // Toplam satırı
        row++;
        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 4).Value = _database.GetAllEntities().OfType<PipeEntity>().Count();
        ws.Cell(row, 5).Value = Math.Round(_database.GetAllEntities().OfType<PipeEntity>().Sum(p => p.Length / 1000.0), 2);
        ws.Range(row, 1, row, 7).Style.Font.SetBold(true).Fill.SetBackgroundColor(SubHdrBg).Font.SetFontColor(HeaderFg);

        // Armatür tablosu
        row += 2;
        ws.Cell(row, 1).Value = "ARMATÜRler";
        StyleSectionHeader(ws.Row(row));
        row++;

        string[] fixHeaders = ["No", "Tip", "Sistem", "Adet", "Yükleme Birimi (DU)", "Toplam DU"];
        for (int c = 0; c < fixHeaders.Length; c++)
            ws.Cell(row, c + 1).Value = fixHeaders[c];
        StyleTableHeader(ws.Range(row, 1, row, fixHeaders.Length));

        var fixGroups = _database.GetAllEntities().OfType<SanitaryFixtureEntity>()
            .GroupBy(f => (f.FixtureType, f.SystemType))
            .OrderBy(g => g.Key.FixtureType);

        no = 1;
        foreach (var g in fixGroups)
        {
            row++;
            ws.Cell(row, 1).Value = no++;
            ws.Cell(row, 2).Value = g.Key.FixtureType;
            ws.Cell(row, 3).Value = SystemTypeName(g.Key.SystemType);
            ws.Cell(row, 4).Value = g.Count();
            ws.Cell(row, 5).Value = g.First().LoadUnits;
            ws.Cell(row, 6).Value = Math.Round(g.Sum(f => f.LoadUnits), 2);
            if (row % 2 == 0) ws.Range(row, 1, row, fixHeaders.Length).Style.Fill.SetBackgroundColor(AltRowBg);
        }

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = 22;
    }

    // ── 3. Pis Su Hesap Föyü ─────────────────────────────────────────────────
    private static void AddWasteWaterSheet(XLWorkbook wb, WasteWaterCalcSheetService.CalcSheetResult result)
    {
        var ws = wb.Worksheets.Add("Pis Su Hesabı");

        ws.Cell("A1").Value = "PİS SU HESAP FÖYÜ — TS EN 12056-2";
        ws.Range("A1:N1").Merge().Style
            .Font.SetBold(true).Font.SetFontSize(13).Font.SetFontColor(HeaderFg)
            .Fill.SetBackgroundColor(HeaderBg)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // Parametreler
        ws.Cell("A2").Value = $"Yöntem: {result.Options.Method}   Bina Tipi: {result.Options.BuildingType}   K: {result.Options.FrequencyFactor}   Manning n: {result.Options.RoughnessN}";

        string[] headers = ["No", "Segment ID", "Tip", "Uzunluk (m)", "DU", "Q (l/s)", "DN (mm)", "Eğim %", "V (m/s)", "Doluluk", "Q_dolu (l/s)", "Durum", "Uyarılar"];
        int hRow = 4;
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(hRow, c + 1).Value = headers[c];
        StyleTableHeader(ws.Range(hRow, 1, hRow, headers.Length));

        int row = hRow;
        foreach (var r in result.Rows)
        {
            row++;
            ws.Cell(row, 1).Value  = r.SegmentNo;
            ws.Cell(row, 2).Value  = r.SegmentId;
            ws.Cell(row, 3).Value  = r.PipeType;
            ws.Cell(row, 4).Value  = r.LengthM;
            ws.Cell(row, 5).Value  = r.LoadUnits;
            ws.Cell(row, 6).Value  = r.DesignFlowLs;
            ws.Cell(row, 7).Value  = r.DiameterMm;
            ws.Cell(row, 8).Value  = r.SlopePct;
            ws.Cell(row, 9).Value  = r.VelocityMs;
            ws.Cell(row, 10).Value = r.FillRatio.ToString("P0");
            ws.Cell(row, 11).Value = r.CapacityLs;
            ws.Cell(row, 12).Value = r.IsOk ? "✓" : "⚠";
            ws.Cell(row, 12).Style.Font.SetFontColor(r.IsOk ? OkFg : WarnFg);
            ws.Cell(row, 13).Value = r.Warnings;
            if (row % 2 == 0) ws.Range(row, 1, row, 13).Style.Fill.SetBackgroundColor(AltRowBg);
        }

        // Özet
        row += 2;
        ws.Cell(row, 1).Value = result.Summary;
        ws.Cell(row, 1).Style.Font.SetBold(true).Font.SetFontColor(result.WarningCount > 0 ? WarnFg : OkFg);

        ws.Columns().AdjustToContents();
    }

    // ── 4. Yağmur Suyu Hesap Föyü ────────────────────────────────────────────
    private static void AddRainWaterSheet(XLWorkbook wb, RainWaterCalcSheetService.CalcSheetResult result)
    {
        var ws = wb.Worksheets.Add("Yağmur Suyu");

        ws.Cell("A1").Value = "YAĞMUR SUYU HESAP FÖYÜ — TS EN 12056-3";
        ws.Range("A1:N1").Merge().Style
            .Font.SetBold(true).Font.SetFontSize(13).Font.SetFontColor(HeaderFg)
            .Fill.SetBackgroundColor(HeaderBg)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        ws.Cell("A2").Value = $"Konum: {result.Options.Location}   r: {result.Options.RainfallIntensity} l/s·m²   Eğim: %{result.Options.DefaultSlopePct}";

        string[] headers = ["No", "Alan Adı", "Yüzey", "A (m²)", "C", "r (l/s·m²)", "Q (l/s)", "DN (mm)", "Eğim %", "V (m/s)", "Doluluk", "Q_dolu (l/s)", "Durum", "Uyarılar"];
        int hRow = 4;
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(hRow, c + 1).Value = headers[c];
        StyleTableHeader(ws.Range(hRow, 1, hRow, headers.Length));

        int row = hRow;
        foreach (var r in result.Rows)
        {
            row++;
            ws.Cell(row, 1).Value  = r.RowNo;
            ws.Cell(row, 2).Value  = r.AreaName;
            ws.Cell(row, 3).Value  = r.SurfaceType;
            ws.Cell(row, 4).Value  = r.AreaM2;
            ws.Cell(row, 5).Value  = r.RunoffC;
            ws.Cell(row, 6).Value  = r.RainfallR;
            ws.Cell(row, 7).Value  = r.DesignFlowLs;
            ws.Cell(row, 8).Value  = r.DiameterMm;
            ws.Cell(row, 9).Value  = r.SlopePct;
            ws.Cell(row, 10).Value = r.VelocityMs;
            ws.Cell(row, 11).Value = r.FillRatio.ToString("P0");
            ws.Cell(row, 12).Value = r.CapacityLs;
            ws.Cell(row, 13).Value = r.IsOk ? "✓" : "⚠";
            ws.Cell(row, 13).Style.Font.SetFontColor(r.IsOk ? OkFg : WarnFg);
            ws.Cell(row, 14).Value = r.Warnings;
            if (row % 2 == 0) ws.Range(row, 1, row, 14).Style.Fill.SetBackgroundColor(AltRowBg);
        }

        row += 2;
        ws.Cell(row, 1).Value = result.Summary;
        ws.Cell(row, 1).Style.Font.SetBold(true).Font.SetFontColor(result.WarningCount > 0 ? WarnFg : OkFg);

        ws.Columns().AdjustToContents();
    }

    // ── Stil Yardımcıları ─────────────────────────────────────────────────────
    private static void StyleTableHeader(IXLRange range)
        => range.Style
            .Font.SetBold(true).Font.SetFontColor(HeaderFg)
            .Fill.SetBackgroundColor(SubHdrBg)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

    private static void StyleSectionHeader(IXLRow row)
        => row.Style.Font.SetBold(true).Font.SetFontSize(11)
            .Font.SetFontColor(HeaderFg).Fill.SetBackgroundColor(HeaderBg);

    private static void StyleLabelColumn(IXLWorksheet ws, int fromRow, int toRow)
    {
        for (int r = fromRow; r <= toRow; r++)
            ws.Cell(r, 1).Style.Font.SetBold(true).Font.SetFontColor(XLColor.FromHtml("#1565C0"));
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
