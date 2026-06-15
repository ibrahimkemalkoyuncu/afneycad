using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class FloorHeatingDialog
{
    private readonly ObservableCollection<FloorHeatingService.FloorHeatingZone> _zones = [];

    public FloorHeatingDialog()
    {
        InitializeComponent();
        ZoneGrid.ItemsSource = _zones;
        AddDefaultZones();
    }

    private void AddDefaultZones()
    {
        _zones.Add(new() { Name = "Salon",        AreaM2 = 25, HeatingLoadW = 1500, MaxSpacingMm = 200 });
        _zones.Add(new() { Name = "Yatak Odası",  AreaM2 = 14, HeatingLoadW = 700,  MaxSpacingMm = 200 });
        _zones.Add(new() { Name = "Banyo",        AreaM2 = 5,  HeatingLoadW = 400,  MaxSpacingMm = 100 });
    }

    private void AddZone_Click(object sender, RoutedEventArgs e)
    {
        _zones.Add(new() { Name = $"Bölge {_zones.Count + 1}", AreaM2 = 20, HeatingLoadW = 1000, MaxSpacingMm = 200 });
    }

    private void RemoveZone_Click(object sender, RoutedEventArgs e)
    {
        if (ZoneGrid.SelectedItem is FloorHeatingService.FloorHeatingZone z)
            _zones.Remove(z);
    }

    private FloorHeatingService.FloorHeatingInput BuildInput()
    {
        double.TryParse(TxtSupply.Text, out double supply);
        double.TryParse(TxtReturn.Text, out double ret);
        double.TryParse(TxtRoom.Text,   out double room);
        double.TryParse(TxtMaxCircuit.Text, out double maxLen);

        (double od, double wall) = CboPipeDiam.SelectedIndex switch
        {
            1 => (20.0, 2.0),
            2 => (16.0, 2.0),
            3 => (20.0, 2.0),
            _ => (16.0, 2.0)
        };

        double rfFloor = FloorHeatingService.FloorCoveringResistance.TryGetValue(
            (CboFloor.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Seramik/Porselen",
            out double rf) ? rf : 0.0;

        return new FloorHeatingService.FloorHeatingInput
        {
            Zones              = [.. _zones],
            SupplyTempC        = supply > 0 ? supply : 35,
            ReturnTempC        = ret   > 0 ? ret    : 30,
            RoomTempC          = room  > 0 ? room   : 20,
            PipeDiameterMm     = od,
            PipeWallMm         = wall,
            MaxCircuitLengthM  = maxLen > 0 ? maxLen : 100,
            FloorResistanceM2K_W = rfFloor
        };
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        if (_zones.Count == 0) { StatusText.Text = "⚠ Lütfen bölge ekleyin."; return; }
        var result = FloorHeatingService.Calculate(BuildInput());

        ResultGrid.ItemsSource = result.Zones;
        SumPipe.Text     = $"{result.TotalPipeLenM:F0} m";
        SumFlow.Text     = $"{result.TotalFlowLph:F1} L/sa";
        SumDP.Text       = $"{result.MaxCircuitDP:F2} kPa";
        SumManifold.Text = result.ManifoldSize;

        string warn = result.Warnings.Count > 0 ? " · ⚠ " + string.Join(" | ", result.Warnings) : "";
        StatusText.Text = $"✓ {result.Zones.Count} bölge hesaplandı · Toplam {result.TotalPipeLenM:F0} m boru{warn}";
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (ResultGrid.ItemsSource is not System.Collections.Generic.List<FloorHeatingService.FloorHeatingZoneResult> zones || zones.Count == 0)
        { StatusText.Text = "⚠ Önce hesap yapın."; return; }

        var result = FloorHeatingService.Calculate(BuildInput());
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Yerden Isıtma Raporu</title>");
        sb.Append("<style>body{font-family:Arial;background:#0D1117;color:#ddd;padding:20px}");
        sb.Append("h1{color:#FFD740}h2{color:#90CAF9}table{border-collapse:collapse;width:100%}");
        sb.Append("th{background:#0D3060;color:#90CAF9;padding:6px;border:1px solid #333}");
        sb.Append("td{padding:5px;border:1px solid #333}.ok{color:#A5D6A7}.warn{color:#FF8A80}</style></head><body>");
        sb.Append("<h1>🌡️ Yerden Isıtma Tasarım Raporu</h1>");
        sb.Append($"<p>Toplam boru: <b>{result.TotalPipeLenM:F0} m</b> · Toplam debi: <b>{result.TotalFlowLph:F1} L/sa</b> · Max ΔP: <b>{result.MaxCircuitDP:F2} kPa</b> · {result.ManifoldSize}</p>");
        sb.Append("<h2>Bölge Sonuçları</h2><table>");
        sb.Append("<tr><th>Bölge</th><th>Isı Akısı (W/m²)</th><th>Aralık (mm)</th><th>Devre</th><th>Devre Boyu (m)</th><th>Toplam Boru (m)</th><th>Debi (L/sa)</th><th>ΔP (kPa)</th><th>Durum</th></tr>");
        foreach (var z in result.Zones)
        {
            string cls = z.Status.StartsWith("✓") ? "ok" : "warn";
            sb.Append($"<tr><td>{z.Zone.Name}</td><td>{z.HeatFluxWpm2:F1}</td><td>{z.SpacingMm}</td><td>{z.CircuitCount}</td><td>{z.CircuitLenM:F1}</td><td>{z.TotalPipeLenM:F1}</td><td>{z.FlowLph:F1}</td><td>{z.PressureDropKpa:F2}</td><td class='{cls}'>{z.Status}</td></tr>");
        }
        sb.Append("</table></body></html>");

        string path = Path.Combine(Path.GetTempPath(), $"YerdenIsitma_{DateTime.Now:yyyyMMdd_HHmm}.html");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
