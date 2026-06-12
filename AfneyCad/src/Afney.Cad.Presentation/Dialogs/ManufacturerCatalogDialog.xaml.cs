using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ManufacturerCatalogDialog
{
    private readonly ManufacturerCatalogService _svc = new();

    // Seçilen pompa (dışarıdan okunabilir)
    public ManufacturerCatalogService.PumpModel? SelectedPump { get; private set; }

    public ManufacturerCatalogDialog()
    {
        InitializeComponent();
        RefreshPumps();
        RefreshPipes();
        RefreshValves();
    }

    // ── Pompa Tab ────────────────────────────────────────────────────────────────

    private void PumpFilter_Changed(object sender, RoutedEventArgs e) => RefreshPumps();
    private void PumpFilter_Changed(object sender, SelectionChangedEventArgs e) => RefreshPumps();

    private void RefreshPumps()
    {
        double flow = ParseDouble(TxtPumpFlow?.Text ?? "", 0);
        double head = ParseDouble(TxtPumpHead?.Text ?? "", 0);
        string mfrTxt = (CboPumpMfr?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tümü";

        ManufacturerCatalogService.PumpManufacturer? mfr = mfrTxt switch
        {
            "Grundfos" => ManufacturerCatalogService.PumpManufacturer.Grundfos,
            "Wilo"     => ManufacturerCatalogService.PumpManufacturer.Wilo,
            _          => null
        };

        var pumps = flow > 0 || head > 0
            ? _svc.FindPumps(flow, head, mfr).ToList()
            : (mfr.HasValue
                ? ManufacturerCatalogService.PumpCatalog.Where(p => p.Manufacturer == mfr.Value).ToList()
                : ManufacturerCatalogService.PumpCatalog);

        if (PumpGrid != null) PumpGrid.ItemsSource = pumps;

        if (flow > 0 || head > 0)
            StatusText.Text = $"✓ {pumps.Count()} pompa uygun (Q≥{flow:F1} m³/h, H≥{head:F1} m)";
    }

    private void PumpGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PumpGrid.SelectedItem is not ManufacturerCatalogService.PumpModel p) return;
        SelectedPump = p;
        PumpDetailName.Text   = $"{p.Manufacturer} {p.ModelName}";
        PumpDetailSeries.Text = p.Series;
        PumpDetailFlow.Text   = $"{p.MaxFlowM3h:F1} m³/h";
        PumpDetailHead.Text   = $"{p.MaxHeadM:F1} mSS";
        PumpDetailPower.Text  = $"{p.NomPowerKw:F3} kW";
        PumpDetailEff.Text    = $"{p.MaxEffPct:F0} %";
        PumpDetailRPM.Text    = $"{p.NomSpeedRPM:F0} d/dk";
        PumpDetailWeight.Text = $"{p.WeightKg:F1} kg";
        PumpCurveText.Text    = string.Join("  →  ",
            p.CurvePoints.Select(pt => $"Q={pt.Q:F1} H={pt.H:F1}"));

        // Çalışma noktası kontrolü
        double flow = ParseDouble(TxtPumpFlow?.Text ?? "", 0);
        double head = ParseDouble(TxtPumpHead?.Text ?? "", 0);
        if (flow > 0)
        {
            double hAtQ = p.GetHeadAtFlow(flow);
            StatusText.Text = $"Q={flow:F2} m³/h noktasında H={hAtQ:F2} m — " +
                              (hAtQ >= head ? "✓ Sistem gereksinimini karşılıyor" : "⚠ Yetersiz basma yüksekliği");
        }
    }

    // ── Boru Tab ─────────────────────────────────────────────────────────────────

    private void PipeFilter_Changed(object sender, RoutedEventArgs e) => RefreshPipes();
    private void PipeFilter_Changed(object sender, SelectionChangedEventArgs e) => RefreshPipes();

    private void RefreshPipes()
    {
        int minDN = (int)ParseDouble(TxtMinDN?.Text ?? "", 0);
        string mfrTxt = (CboPipeMfr?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tümü";
        string matTxt = (CboPipeMat?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tümü";

        ManufacturerCatalogService.PipeManufacturer? mfr = mfrTxt switch
        {
            "Valsir"  => ManufacturerCatalogService.PipeManufacturer.Valsir,
            "Wavin"   => ManufacturerCatalogService.PipeManufacturer.Wavin,
            "Geberit" => ManufacturerCatalogService.PipeManufacturer.Geberit,
            _         => null
        };
        ManufacturerCatalogService.PipeMaterial? mat = matTxt switch
        {
            "PPR"  => ManufacturerCatalogService.PipeMaterial.PPR,
            "PEX"  => ManufacturerCatalogService.PipeMaterial.PEX,
            "HDPE" => ManufacturerCatalogService.PipeMaterial.HDPE,
            _      => null
        };

        var pipes = ManufacturerCatalogService.PipeCatalog.AsEnumerable();
        if (mfr.HasValue) pipes = pipes.Where(p => p.Manufacturer == mfr.Value);
        if (mat.HasValue) pipes = pipes.Where(p => p.Material == mat.Value);
        if (minDN > 0)    pipes = pipes.Where(p => p.DN >= minDN);

        if (PipeGrid != null) PipeGrid.ItemsSource = pipes.OrderBy(p => p.DN).ToList();
    }

    // ── Vana Tab ─────────────────────────────────────────────────────────────────

    private void ValveFilter_Changed(object sender, RoutedEventArgs e) => RefreshValves();
    private void ValveFilter_Changed(object sender, SelectionChangedEventArgs e) => RefreshValves();

    private void RefreshValves()
    {
        double flow = ParseDouble(TxtValveFlow?.Text ?? "", 0);
        string mfrTxt  = (CboValveMfr?.SelectedItem  as ComboBoxItem)?.Content?.ToString() ?? "Tümü";
        string typeTxt = (CboValveType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tümü";

        ManufacturerCatalogService.ValveManufacturer? mfr = mfrTxt switch
        {
            "Honeywell" => ManufacturerCatalogService.ValveManufacturer.Honeywell,
            "Danfoss"   => ManufacturerCatalogService.ValveManufacturer.Danfoss,
            "Oventrop"  => ManufacturerCatalogService.ValveManufacturer.Oventrop,
            _           => null
        };
        string? type = typeTxt == "Tümü" ? null : typeTxt;

        var valves = ManufacturerCatalogService.ValveCatalog.AsEnumerable();
        if (mfr.HasValue) valves = valves.Where(v => v.Manufacturer == mfr.Value);
        if (type != null) valves = valves.Where(v => v.ValveType == type);

        // ΔP hesaplama için anonim tip oluştur
        var rows = valves.OrderBy(v => v.DN).Select(v => new
        {
            v.ModelName, v.Manufacturer, v.ValveType, v.DN, v.Kv, v.PN, v.TempMaxC, v.BodyMaterial,
            DeltaPKpa = flow > 0 ? v.PressureDropKpa(flow) : 0.0
        }).ToList();

        if (ValveGrid != null) ValveGrid.ItemsSource = rows;
    }

    // ── Seç / Kapat ──────────────────────────────────────────────────────────────

    private void SelectAndClose_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPump != null)
            StatusText.Text = $"✓ Seçildi: {SelectedPump.Manufacturer} {SelectedPump.ModelName}";
        DialogResult = true;
        Close();
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string html = BuildHtmlReport();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"EkipmanKatalogu_{DateTime.Now:yyyyMMdd_HHmm}.html");
            System.IO.File.WriteAllText(path, html, Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            StatusText.Text = $"✓ Rapor açıldı: {path}";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── HTML Rapor ───────────────────────────────────────────────────────────────

    private static string BuildHtmlReport()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Ekipman Kataloğu</title>");
        sb.Append("<style>body{font-family:Arial;background:#0D1117;color:#ddd;padding:20px}");
        sb.Append("h1{color:#A5D6A7}h2{color:#80CBC4}table{border-collapse:collapse;width:100%;margin-bottom:24px}");
        sb.Append("th{background:#1B3A1B;color:#A5D6A7;padding:7px;text-align:left}");
        sb.Append("td{padding:5px;border:1px solid #222}tr:nth-child(even){background:#161B22}</style></head><body>");
        sb.Append("<h1>📦 AfneyCAD — Üretici Ekipman Kataloğu</h1>");
        sb.Append($"<p>Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");

        sb.Append("<h2>🔄 Pompalar</h2><table>");
        sb.Append("<tr><th>Model</th><th>Üretici</th><th>Max Q (m³/h)</th><th>Max H (m)</th><th>Motor (kW)</th><th>Verim (%)</th><th>Bağlantı</th><th>Uygulama</th></tr>");
        foreach (var p in ManufacturerCatalogService.PumpCatalog)
            sb.Append($"<tr><td>{p.ModelName}</td><td>{p.Manufacturer}</td><td>{p.MaxFlowM3h:F1}</td>" +
                      $"<td>{p.MaxHeadM:F1}</td><td>{p.NomPowerKw:F3}</td><td>{p.MaxEffPct:F0}</td>" +
                      $"<td>{p.ConnectionDN}</td><td>{p.Application}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>🔧 Borular</h2><table>");
        sb.Append("<tr><th>Model</th><th>Üretici</th><th>Malzeme</th><th>DN</th><th>OD (mm)</th><th>Et (mm)</th><th>ID (mm)</th><th>PN</th><th>T_max</th><th>TL/m</th></tr>");
        foreach (var p in ManufacturerCatalogService.PipeCatalog.OrderBy(x => x.DN))
            sb.Append($"<tr><td>{p.ModelName}</td><td>{p.Manufacturer}</td><td>{p.Material}</td>" +
                      $"<td>{p.DN}</td><td>{p.OD:F1}</td><td>{p.WallThickMm:F1}</td><td>{p.ID:F1}</td>" +
                      $"<td>{p.PN:F0}</td><td>{p.TempMaxC:F0}°C</td><td>{p.PricePerMtr:F0}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>🔩 Vanalar</h2><table>");
        sb.Append("<tr><th>Model</th><th>Üretici</th><th>Tip</th><th>DN</th><th>Kv</th><th>PN</th><th>Gövde</th></tr>");
        foreach (var v in ManufacturerCatalogService.ValveCatalog)
            sb.Append($"<tr><td>{v.ModelName}</td><td>{v.Manufacturer}</td><td>{v.ValveType}</td>" +
                      $"<td>{v.DN}</td><td>{v.Kv:F1}</td><td>{v.PN:F0}</td><td>{v.BodyMaterial}</td></tr>");
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
