using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class RainWaterCalcDialog : Window
{
    private readonly CadDatabase _database;
    private readonly RainWaterCalcSheetService _svc = new();
    private RainWaterCalcSheetService.CalcSheetResult? _lastResult;

    public RainWaterCalcDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        Calculate_Click(this, new RoutedEventArgs());
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        var opts = BuildOptions();
        _lastResult = _svc.Calculate(_database, opts);

        CalcGrid.ItemsSource = null;
        CalcGrid.ItemsSource = _lastResult.Rows;

        PanelNotes.Children.Clear();
        foreach (var note in _lastResult.Notes)
            PanelNotes.Children.Add(new TextBlock
            {
                Text = "• " + note,
                Foreground = System.Windows.Media.Brushes.LightCyan,
                FontSize = 11,
                Margin = new Thickness(0, 1, 0, 1)
            });

        var summaryBlock = new TextBlock
        {
            Text = _lastResult.Summary,
            FontWeight = FontWeights.SemiBold,
            Foreground = _lastResult.WarningCount > 0
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.LightGreen,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };
        PanelNotes.Children.Add(summaryBlock);

        TxtStatus.Text = $"Hesap tamamlandı — {_lastResult.TotalAreas} alan, " +
                         $"{_lastResult.TotalAreaM2:F1} m², Q={_lastResult.TotalFlowLs:F3} l/s";
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null) { Calculate_Click(sender, e); }
        if (_lastResult is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Yağmur Suyu Hesap Raporu",
            Filter     = "HTML (*.html)|*.html",
            FileName   = $"YagmurSuyu_Hesap_{DateTime.Now:yyyyMMdd}",
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private RainWaterCalcSheetService.CalcOptions BuildOptions()
    {
        return new RainWaterCalcSheetService.CalcOptions
        {
            Location          = (CmbLocation.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "İstanbul",
            RainfallIntensity = ParseDouble(TxtRainfall.Text, 0.030),
            DefaultSlopePct   = ParseDouble(TxtSlope.Text, 1.0),
            PipeMaterial      = (CmbMaterial.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PVC",
            ManningN          = GetManningN((CmbMaterial.SelectedItem as ComboBoxItem)?.Content?.ToString()),
        };
    }

    private static double GetManningN(string? material) => material switch
    {
        "PP"           => 0.011,
        "Dökme Demir"  => 0.013,
        "Galvaniz"     => 0.015,
        _              => 0.011  // PVC
    };

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private static string BuildHtml(RainWaterCalcSheetService.CalcSheetResult res)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>Yağmur Suyu Hesap Föyü</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;background:#111;color:#eee;margin:24px}");
        sb.AppendLine("h1{color:#90CAF9}h2{color:#64B5F6;border-bottom:1px solid #444;padding-bottom:4px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:16px}");
        sb.AppendLine("th{background:#0D3060;color:#90CAF9;padding:6px 8px;text-align:left;font-size:12px}");
        sb.AppendLine("td{padding:5px 8px;font-size:12px;border-bottom:1px solid #333}");
        sb.AppendLine("tr:nth-child(even){background:#1a1a2e}.warn{color:#FF9800}.ok{color:#4CAF50}</style></head><body>");

        sb.AppendLine($"<h1>Yağmur Suyu Hesap Föyü</h1>");
        sb.AppendLine($"<p><b>Tarih:</b> {DateTime.Now:dd.MM.yyyy HH:mm} &nbsp;&nbsp; <b>Standart:</b> TS EN 12056-3</p>");

        // Parametreler
        sb.AppendLine("<h2>Hesap Parametreleri</h2><table><tr><th>Parametre</th><th>Değer</th></tr>");
        sb.AppendLine($"<tr><td>Konum</td><td>{res.Options.Location}</td></tr>");
        sb.AppendLine($"<tr><td>Yağış Yoğunluğu (r)</td><td>{res.Options.RainfallIntensity} l/s·m²</td></tr>");
        sb.AppendLine($"<tr><td>Boru Eğimi</td><td>%{res.Options.DefaultSlopePct}</td></tr>");
        sb.AppendLine($"<tr><td>Boru Malzemesi</td><td>{res.Options.PipeMaterial} (n={res.Options.ManningN})</td></tr>");
        sb.AppendLine("</table>");

        // Hesap tablosu
        sb.AppendLine("<h2>Hesap Tablosu</h2>");
        sb.AppendLine("<table><tr><th>No</th><th>Alan Adı</th><th>Yüzey</th><th>A (m²)</th><th>C</th>" +
                      "<th>r (l/s·m²)</th><th>Q (l/s)</th><th>DN (mm)</th><th>Eğim %</th>" +
                      "<th>V (m/s)</th><th>Doluluk</th><th>Q_dolu (l/s)</th><th>Durum</th><th>Uyarılar</th></tr>");
        foreach (var row in res.Rows)
        {
            string cls = row.IsOk ? "ok" : "warn";
            string durum = row.IsOk ? "✓" : "⚠";
            sb.AppendLine($"<tr><td>{row.RowNo}</td><td>{row.AreaName}</td><td>{row.SurfaceType}</td>" +
                          $"<td>{row.AreaM2:F1}</td><td>{row.RunoffC:F2}</td><td>{row.RainfallR:F3}</td>" +
                          $"<td>{row.DesignFlowLs:F3}</td><td>DN{row.DiameterMm}</td><td>{row.SlopePct:F1}</td>" +
                          $"<td>{row.VelocityMs:F2}</td><td>{row.FillRatio:P0}</td><td>{row.CapacityLs:F3}</td>" +
                          $"<td class='{cls}'>{durum}</td><td>{row.Warnings}</td></tr>");
        }
        sb.AppendLine("</table>");

        // Özet
        sb.AppendLine("<h2>Özet</h2><table><tr><th>Parametre</th><th>Değer</th></tr>");
        sb.AppendLine($"<tr><td>Toplam Alan Sayısı</td><td>{res.TotalAreas}</td></tr>");
        sb.AppendLine($"<tr><td>Toplam Çatı Alanı</td><td>{res.TotalAreaM2:F1} m²</td></tr>");
        sb.AppendLine($"<tr><td>Toplam Tasarım Debisi</td><td>{res.TotalFlowLs:F3} l/s</td></tr>");
        sb.AppendLine($"<tr><td>Uyarı Sayısı</td><td>{res.WarningCount}</td></tr>");
        sb.AppendLine("</table>");

        // Notlar
        if (res.Notes.Count > 0)
        {
            sb.AppendLine("<h2>Notlar</h2><ul>");
            foreach (var n in res.Notes) sb.AppendLine($"<li>{n}</li>");
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("<hr/><p style='font-size:10px;color:#666'>AfneyCAD — Yağmur Suyu Hesap Föyü | TS EN 12056-3</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
