using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BOMDialog : Window
{
    // ── View-model rows ──────────────────────────────────────────────────────

    public class PipeBomCostRow
    {
        public string DiameterDisplay     { get; set; } = "";
        public string SystemType          { get; set; } = "";
        public string MaterialDisplay     { get; set; } = "";
        public string LengthDisplay       { get; set; } = "";
        public string UnitPriceDisplay    { get; set; } = "";
        public string MaterialCostDisplay { get; set; } = "";
        public string LaborCostDisplay    { get; set; } = "";
        public string FittingCostDisplay  { get; set; } = "";
        public string TotalCostDisplay    { get; set; } = "";
        public bool   IsHighCost          { get; set; }

        internal double RawLength       { get; set; }
        internal double RawUnitPrice    { get; set; }
        internal double RawMaterialCost { get; set; }
        internal double RawLabor        { get; set; }
        internal double RawFitting      { get; set; }
    }

    public class FixtureBomCostRow
    {
        public string Type             { get; set; } = "";
        public int    Count            { get; set; }
        public string UnitCostDisplay  { get; set; } = "";
        public string TotalCostDisplay { get; set; } = "";
        internal double RawUnitCost    { get; set; }
    }

    public class SystemSummaryRow
    {
        public string System   { get; set; } = "";
        public string Length   { get; set; } = "";
        public string Material { get; set; } = "";
        public string Labor    { get; set; } = "";
        public string Total    { get; set; } = "";
        public string SharePct { get; set; } = "";
    }

    // ── Fixture unit prices (approx. TL, 2024 Turkey) ────────────────────────
    private static readonly Dictionary<string, double> _fixturePrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Washbasin"]      = 2_500,
        ["Lavabo"]         = 2_500,
        ["WC"]             = 3_200,
        ["Toilet"]         = 3_200,
        ["Shower"]         = 4_800,
        ["Bathtub"]        = 8_500,
        ["Sink"]           = 2_200,
        ["KitchenSink"]    = 2_200,
        ["FloorDrain"]     = 850,
        ["WashingMachine"] = 12_000,
        ["DishWasher"]     = 14_000,
        ["Urinal"]         = 3_000,
        ["Bidet"]          = 2_000,
    };

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly CadDatabase _database;
    private readonly PipeCostService _svc = new();
    private PipeCostService.ProjectCostResult? _costResult;
    private readonly ObservableCollection<PipeBomCostRow>    _pipeRows    = [];
    private readonly ObservableCollection<FixtureBomCostRow> _fixtureRows = [];
    private readonly ObservableCollection<SystemSummaryRow>  _systemRows  = [];

    // ── Constructor ───────────────────────────────────────────────────────────
    public BOMDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;

        PipeGrid.ItemsSource    = _pipeRows;
        FixtureGrid.ItemsSource = _fixtureRows;
        SystemGrid.ItemsSource  = _systemRows;

        LoadData();
    }

    // ── Data loading ──────────────────────────────────────────────────────────
    private void LoadData()
    {
        double contingency = GetDouble(TxtContingency.Text, 10);
        _costResult = _svc.CalculateFromDatabase(_database, contingency);

        LoadPipeRows();
        LoadFixtureRows();
        LoadSystemRows();
        UpdateSummary();
    }

    private void LoadPipeRows()
    {
        _pipeRows.Clear();

        double highCostThreshold = _costResult!.Items.Count > 0
            ? _costResult.Items.Average(i => i.TotalCostTl) * 1.5
            : double.MaxValue;

        var grouped = _costResult.Items
            .GroupBy(i => (i.DiameterMm, i.Material, i.SystemType))
            .OrderBy(g => g.Key.SystemType)
            .ThenBy(g => g.Key.DiameterMm);

        foreach (var g in grouped)
        {
            double totalLen     = g.Sum(i => i.LengthM);
            double totalMat     = g.Sum(i => i.MaterialCostTl);
            double totalLabor   = g.Sum(i => i.LaborCostTl);
            double totalFitting = g.Sum(i => i.FittingCostTl);
            double total        = totalMat + totalLabor + totalFitting;
            double unitPrice    = totalLen > 0 ? totalMat / totalLen : 0;

            _pipeRows.Add(new PipeBomCostRow
            {
                DiameterDisplay     = $"DN{g.Key.DiameterMm:F0}",
                SystemType          = g.Key.SystemType,
                MaterialDisplay     = g.Key.Material.ToString(),
                LengthDisplay       = $"{totalLen:F1}",
                UnitPriceDisplay    = $"{unitPrice:N0}",
                MaterialCostDisplay = $"{totalMat:N0}",
                LaborCostDisplay    = $"{totalLabor:N0}",
                FittingCostDisplay  = $"{totalFitting:N0}",
                TotalCostDisplay    = $"{total:N0}",
                IsHighCost          = total > highCostThreshold,
                RawLength           = totalLen,
                RawUnitPrice        = unitPrice,
                RawMaterialCost     = totalMat,
                RawLabor            = totalLabor,
                RawFitting          = totalFitting,
            });
        }

        TxtPipeCount.Text = $"{_costResult.Items.Count} boru kalemi";
    }

    private void LoadFixtureRows()
    {
        _fixtureRows.Clear();

        var fixtures = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType)
            .OrderBy(g => g.Key);

        foreach (var g in fixtures)
        {
            double unitCost  = GetFixtureUnitCost(g.Key);
            double totalCost = unitCost * g.Count();

            _fixtureRows.Add(new FixtureBomCostRow
            {
                Type             = g.Key,
                Count            = g.Count(),
                UnitCostDisplay  = $"{unitCost:N0}",
                TotalCostDisplay = $"{totalCost:N0}",
                RawUnitCost      = unitCost,
            });
        }
    }

    private void LoadSystemRows()
    {
        _systemRows.Clear();

        double grandPipeCost = _costResult!.Items.Sum(i => i.TotalCostTl);

        var bySystem = _costResult.Items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.SystemType) ? "Diğer" : i.SystemType)
            .OrderByDescending(g => g.Sum(i => i.TotalCostTl));

        foreach (var g in bySystem)
        {
            double len   = g.Sum(i => i.LengthM);
            double mat   = g.Sum(i => i.MaterialCostTl);
            double labor = g.Sum(i => i.LaborCostTl);
            double total = g.Sum(i => i.TotalCostTl);
            double share = grandPipeCost > 0 ? total / grandPipeCost * 100 : 0;

            _systemRows.Add(new SystemSummaryRow
            {
                System   = g.Key,
                Length   = $"{len:F1} m",
                Material = $"{mat:N0} TL",
                Labor    = $"{labor:N0} TL",
                Total    = $"{total:N0} TL",
                SharePct = $"%{share:F1}",
            });
        }
    }

    private void UpdateSummary()
    {
        if (_costResult is null) return;

        double contingency = GetDouble(TxtContingency.Text, 10);
        double mat         = _costResult.TotalMaterialTl;
        double labor       = _costResult.TotalLaborTl;
        double fitting     = _costResult.TotalFittingTl;
        double sub         = mat + labor + fitting;
        double contAmt     = sub * contingency / 100.0;
        double fixtureTot  = _fixtureRows.Sum(r => r.RawUnitCost * r.Count);
        double grand       = sub + contAmt + fixtureTot;

        TxtMaterial.Text       = $"{mat:N0} TL";
        TxtLabor.Text          = $"{labor:N0} TL";
        TxtFitting.Text        = $"{fitting:N0} TL";
        TxtContingencyAmt.Text = $"{contAmt:N0} TL";
        TxtGrandTotal.Text     = $"{grand:N0} TL";
    }

    // ── Handlers ─────────────────────────────────────────────────────────────
    private void Contingency_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateSummary();

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_costResult is null) return;

        string html = BuildHtmlReport();
        string path = Path.Combine(Path.GetTempPath(), $"AfneyCAD_BOM_{DateTime.Now:yyyyMMdd_HHmm}.html");
        File.WriteAllText(path, html, Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_costResult is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "BOM CSV Kaydet",
            Filter     = "CSV Dosyası|*.csv",
            FileName   = $"AfneyCAD_BOM_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog(this) != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Çap;Sistem;Malzeme;Uzunluk (m);Birim Fiyat (TL/m);Malzeme TL;İşçilik TL;Ek Parça TL;Toplam TL");

        foreach (var r in _pipeRows)
            sb.AppendLine($"{r.DiameterDisplay};{r.SystemType};{r.MaterialDisplay};{r.LengthDisplay};{r.UnitPriceDisplay};{r.MaterialCostDisplay};{r.LaborCostDisplay};{r.FittingCostDisplay};{r.TotalCostDisplay}");

        sb.AppendLine();
        sb.AppendLine("Cihaz Tipi;Adet;Birim TL;Toplam TL");
        foreach (var r in _fixtureRows)
            sb.AppendLine($"{r.Type};{r.Count};{r.UnitCostDisplay};{r.TotalCostDisplay}");

        sb.AppendLine();
        sb.AppendLine($"GENEL TOPLAM;;{TxtGrandTotal.Text}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"CSV kaydedildi:\n{dlg.FileName}", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── HTML export ───────────────────────────────────────────────────────────
    private string BuildHtmlReport()
    {
        var sb = new StringBuilder();
        sb.Append(
            "<!DOCTYPE html><html><head><meta charset='utf-8'/>" +
            "<title>BOM Raporu — AfneyCAD</title>" +
            "<style>" +
            "body{font-family:Segoe UI,Arial,sans-serif;background:#1a1a2e;color:#eee;padding:24px;margin:0}" +
            "h2{color:#90CAF9;border-bottom:1px solid #1565C0;padding-bottom:8px}" +
            "h3{color:#80CBC4;margin-top:24px}" +
            "table{border-collapse:collapse;width:100%;margin-top:8px}" +
            "th{background:#0D47A1;color:white;padding:7px 10px;text-align:left;font-size:12px}" +
            "td{padding:5px 10px;border-bottom:1px solid #333;font-size:12px}" +
            "tr:nth-child(even){background:#252540}" +
            ".hi{color:#FFD54F}" +
            ".sum{background:#0A1A2A;color:#69F0AE;font-weight:bold}" +
            ".card{display:inline-block;background:#1A2A3A;border-radius:6px;padding:10px 16px;margin:6px;min-width:130px}" +
            ".card-lbl{font-size:10px;color:#90CAF9}" +
            ".card-val{font-size:16px;font-weight:bold}" +
            "</style></head><body>");

        sb.AppendLine($"<h2>MALZEME METRAJ ve MALİYET LİSTESİ (BOM)</h2>");
        sb.AppendLine($"<p style='color:#888'>AfneyCAD — {DateTime.Now:dd.MM.yyyy HH:mm} | Fiyatlar 2024 Türkiye piyasası (KDV hariç)</p>");

        sb.AppendLine("<div>");
        AppendCard(sb, "MALZEME",     TxtMaterial.Text,       "#90CAF9");
        AppendCard(sb, "İŞÇİLİK",    TxtLabor.Text,          "#80CBC4");
        AppendCard(sb, "EK PARÇA",   TxtFitting.Text,        "#FFCC80");
        AppendCard(sb, "BEKLENMEDİK", TxtContingencyAmt.Text, "#EF9A9A");
        AppendCard(sb, "GENEL TOPLAM", TxtGrandTotal.Text,    "#69F0AE");
        sb.AppendLine("</div>");

        sb.AppendLine("<h3>Boru Metrajı ve Maliyeti</h3>");
        sb.AppendLine("<table><tr><th>Çap</th><th>Sistem</th><th>Malzeme</th><th>Uzunluk (m)</th><th>Birim (TL/m)</th><th>Malzeme TL</th><th>İşçilik TL</th><th>Ek Parça TL</th><th>Toplam TL</th></tr>");
        foreach (var r in _pipeRows)
        {
            string cls = r.IsHighCost ? " class='hi'" : "";
            sb.AppendLine($"<tr{cls}><td>{r.DiameterDisplay}</td><td>{r.SystemType}</td><td>{r.MaterialDisplay}</td><td>{r.LengthDisplay}</td><td>{r.UnitPriceDisplay}</td><td>{r.MaterialCostDisplay}</td><td>{r.LaborCostDisplay}</td><td>{r.FittingCostDisplay}</td><td>{r.TotalCostDisplay}</td></tr>");
        }
        sb.AppendLine($"<tr class='sum'><td colspan='8'>ARA TOPLAM</td><td>{_costResult!.TotalCostTl:N0} TL</td></tr>");
        sb.AppendLine("</table>");

        if (_fixtureRows.Count > 0)
        {
            sb.AppendLine("<h3>Vitrifiye ve Cihazlar</h3>");
            sb.AppendLine("<table><tr><th>Cihaz Tipi</th><th>Adet</th><th>Birim (TL)</th><th>Toplam (TL)</th></tr>");
            foreach (var r in _fixtureRows)
                sb.AppendLine($"<tr><td>{r.Type}</td><td>{r.Count}</td><td>{r.UnitCostDisplay}</td><td>{r.TotalCostDisplay}</td></tr>");
            sb.AppendLine("</table>");
        }

        if (_systemRows.Count > 0)
        {
            sb.AppendLine("<h3>Sistem Bazlı Özet</h3>");
            sb.AppendLine("<table><tr><th>Sistem</th><th>Uzunluk</th><th>Malzeme</th><th>İşçilik</th><th>Toplam</th><th>Pay</th></tr>");
            foreach (var r in _systemRows)
                sb.AppendLine($"<tr><td>{r.System}</td><td>{r.Length}</td><td>{r.Material}</td><td>{r.Labor}</td><td>{r.Total}</td><td>{r.SharePct}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendCard(StringBuilder sb, string label, string value, string color)
        => sb.AppendLine($"<div class='card'><div class='card-lbl'>{label}</div><div class='card-val' style='color:{color}'>{value}</div></div>");

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static double GetDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private static double GetFixtureUnitCost(string fixtureType)
    {
        if (_fixturePrices.TryGetValue(fixtureType, out double price)) return price;
        foreach (var kv in _fixturePrices)
            if (fixtureType.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains(fixtureType, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return 2_000;
    }
}
