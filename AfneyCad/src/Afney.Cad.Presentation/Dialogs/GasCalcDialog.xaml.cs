using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class GasCalcDialog : Window
{
    private readonly CadDatabase _database;
    private readonly GasCalcSheetService _svc = new();
    private GasCalcSheetService.CalcSheetResult? _lastResult;

    // Editable view-models for DataGrid
    public class DeviceVm
    {
        public string Name           { get; set; } = "Kombi";
        public double NominalPowerKw { get; set; } = 24.0;
        public double LoadFactor     { get; set; } = 1.0;
    }

    public class SegmentVm
    {
        public string Name      { get; set; } = "Hat 1";
        public double LengthM   { get; set; } = 10.0;
        public string DeviceIds { get; set; } = "0"; // "0,1,2"
    }

    private readonly ObservableCollection<DeviceVm>  _devices  = [];
    private readonly ObservableCollection<SegmentVm> _segments = [];

    public GasCalcDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;

        DeviceGrid.ItemsSource  = _devices;
        SegmentGrid.ItemsSource = _segments;

        // Örnek veri
        _devices.Add(new DeviceVm  { Name = "Kombi (ısıtma)",  NominalPowerKw = 24.0, LoadFactor = 0.8 });
        _devices.Add(new DeviceVm  { Name = "Ankastre ocak",   NominalPowerKw = 8.0,  LoadFactor = 0.5 });
        _segments.Add(new SegmentVm { Name = "Sayaç → Dağıtım", LengthM = 5.0,  DeviceIds = "0,1" });
        _segments.Add(new SegmentVm { Name = "Dağıtım → Kombi", LengthM = 8.0,  DeviceIds = "0" });
        _segments.Add(new SegmentVm { Name = "Dağıtım → Ocak",  LengthM = 4.0,  DeviceIds = "1" });
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        var opts = BuildOptions();

        var devices = _devices.Select(d => new GasCalcSheetService.GasDevice
        {
            Name           = d.Name,
            NominalPowerKw = d.NominalPowerKw,
            LoadFactor     = d.LoadFactor
        }).ToList();

        var segments = _segments.Select(s =>
        {
            var ids = s.DeviceIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => double.TryParse(x.Trim(), out double idx) ? idx : -1.0)
                .ToArray();
            return (s.Name, s.LengthM, ids);
        }).ToList();

        _lastResult = _svc.Calculate(devices, segments, opts);

        ResultGrid.ItemsSource = null;
        ResultGrid.ItemsSource = _lastResult.Rows;

        PanelNotes.Children.Clear();
        foreach (var note in _lastResult.Notes)
            PanelNotes.Children.Add(new TextBlock
            {
                Text = "• " + note,
                Foreground = System.Windows.Media.Brushes.LightCyan,
                FontSize = 11,
                Margin = new Thickness(0, 1, 0, 1)
            });

        PanelNotes.Children.Add(new TextBlock
        {
            Text = _lastResult.Summary,
            FontWeight = FontWeights.SemiBold,
            Foreground = _lastResult.WarningCount > 0
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.LightGreen,
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0)
        });

        TxtStatus.Text = $"Hesap tamamlandı — {_lastResult.Rows.Count} segment, " +
                         $"Q={_lastResult.TotalFlowM3h:F3} m³/h, ΔP={_lastResult.TotalPressureDrop:F3} mbar";
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null) Calculate_Click(sender, e);
        if (_lastResult is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Doğalgaz Hesap Raporu",
            Filter = "HTML (*.html)|*.html",
            FileName = $"Dogalgaz_Hesap_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".html"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, BuildHtml(_lastResult), Encoding.UTF8);
            TxtStatus.Text = $"HTML rapor kaydedildi: {Path.GetFileName(dlg.FileName)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"HTML kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddDevice_Click(object sender, RoutedEventArgs e)
        => _devices.Add(new DeviceVm { Name = $"Cihaz {_devices.Count + 1}", NominalPowerKw = 10.0, LoadFactor = 1.0 });

    private void RemoveDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceGrid.SelectedItem is DeviceVm vm) _devices.Remove(vm);
    }

    private void AddSegment_Click(object sender, RoutedEventArgs e)
        => _segments.Add(new SegmentVm { Name = $"Hat {_segments.Count + 1}", LengthM = 5.0, DeviceIds = "0" });

    private void RemoveSegment_Click(object sender, RoutedEventArgs e)
    {
        if (SegmentGrid.SelectedItem is SegmentVm vm) _segments.Remove(vm);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private GasCalcSheetService.CalcOptions BuildOptions() => new()
    {
        SupplyPressureMbar = ParseDouble(TxtSupplyP.Text, 21.0),
        MinDevicePressure  = ParseDouble(TxtMinP.Text, 17.0),
        MaxVelocityMs      = ParseDouble(TxtMaxV.Text, 8.0),
        GasDensity         = ParseDouble(TxtDensity.Text, 0.72),
        PipeMaterial       = (CmbMaterial.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Çelik",
    };

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private static string BuildHtml(GasCalcSheetService.CalcSheetResult res)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Doğalgaz Hesap Föyü</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;background:#111;color:#eee;margin:24px}");
        sb.AppendLine("h1{color:#FFB74D}h2{color:#FFA726;border-bottom:1px solid #444;padding-bottom:4px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:16px}");
        sb.AppendLine("th{background:#5D4037;color:#FFCC80;padding:6px 8px;text-align:left;font-size:12px}");
        sb.AppendLine("td{padding:5px 8px;font-size:12px;border-bottom:1px solid #333}");
        sb.AppendLine("tr:nth-child(even){background:#1a1a1a}.warn{color:#FF9800}.ok{color:#4CAF50}</style></head><body>");
        sb.AppendLine("<h1>🔥 Doğalgaz Tesisat Hesap Föyü</h1>");
        sb.AppendLine($"<p><b>Tarih:</b> {DateTime.Now:dd.MM.yyyy HH:mm} &nbsp; <b>Standart:</b> TS EN 1775 / TS 7363</p>");

        // Parametreler
        sb.AppendLine("<h2>Hesap Parametreleri</h2><table>");
        sb.AppendLine($"<tr><th>Parametre</th><th>Değer</th></tr>");
        sb.AppendLine($"<tr><td>Besleme Basıncı</td><td>{res.Options.SupplyPressureMbar} mbar</td></tr>");
        sb.AppendLine($"<tr><td>Min. Cihaz Basıncı</td><td>{res.Options.MinDevicePressure} mbar</td></tr>");
        sb.AppendLine($"<tr><td>Maks. Hız</td><td>{res.Options.MaxVelocityMs} m/s</td></tr>");
        sb.AppendLine($"<tr><td>Gaz Yoğunluğu</td><td>{res.Options.GasDensity} kg/m³</td></tr>");
        sb.AppendLine($"<tr><td>Boru Malzemesi</td><td>{res.Options.PipeMaterial}</td></tr>");
        sb.AppendLine("</table>");

        // Cihazlar
        sb.AppendLine("<h2>Gaz Cihazları</h2><table>");
        sb.AppendLine("<tr><th>Cihaz</th><th>Güç (kW)</th><th>Yük Faktörü</th><th>Q (m³/h)</th></tr>");
        foreach (var d in res.Devices)
            sb.AppendLine($"<tr><td>{d.Name}</td><td>{d.NominalPowerKw}</td><td>{d.LoadFactor}</td><td>{d.FlowM3h:F3}</td></tr>");
        sb.AppendLine("</table>");

        // Hesap tablosu
        sb.AppendLine("<h2>Segment Hesap Tablosu</h2><table>");
        sb.AppendLine("<tr><th>No</th><th>Segment</th><th>L (m)</th><th>L_ekv (m)</th><th>Q (m³/h)</th>" +
                      "<th>DN (mm)</th><th>V (m/s)</th><th>ΔP (mbar)</th><th>P_kalan</th><th>Durum</th><th>Uyarılar</th></tr>");
        foreach (var r in res.Rows)
        {
            string cls = r.IsOk ? "ok" : "warn";
            sb.AppendLine($"<tr><td>{r.RowNo}</td><td>{r.SegmentName}</td><td>{r.LengthM:F2}</td><td>{r.EquivLengthM:F2}</td>" +
                          $"<td>{r.FlowM3h:F3}</td><td>DN{r.DiameterMm}</td><td>{r.VelocityMs:F2}</td>" +
                          $"<td>{r.PressureDropMbar:F3}</td><td>{r.RemainingPressureMbar:F2}</td>" +
                          $"<td class='{cls}'>{(r.IsOk ? "✓" : "⚠")}</td><td>{r.Warnings}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine($"<h2>Özet</h2><p style='font-weight:bold'>{res.Summary}</p>");
        sb.AppendLine("<h2>Notlar</h2><ul>");
        foreach (var n in res.Notes) sb.AppendLine($"<li>{n}</li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("<hr/><p style='font-size:10px;color:#666'>AfneyCAD — Doğalgaz Hesap Föyü | TS EN 1775:2007</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
