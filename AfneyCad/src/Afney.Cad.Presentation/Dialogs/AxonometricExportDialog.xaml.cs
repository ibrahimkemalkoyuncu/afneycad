using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Presentation.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AxonometricExportDialog
{
    private readonly CadDatabase _database;
    private readonly AxonometricExportService _svc = new();

    public AxonometricExportDialog(CadDatabase database, string projectName)
    {
        InitializeComponent();
        _database = database;
        TxtProjectName.Text = projectName;
        TxtSavePath.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"Axo_{projectName}_{DateTime.Now:yyyyMMdd}.html");
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Axonometrik Şema Kaydet",
            Filter = CboFormat.SelectedIndex == 1
                ? "SVG Dosyası|*.svg"
                : "HTML Dosyası|*.html",
            FileName = Path.GetFileName(TxtSavePath.Text)
        };
        if (dlg.ShowDialog() == true)
            TxtSavePath.Text = dlg.FileName;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string projectName  = TxtProjectName.Text.Trim();
            int    floors       = (CboFloors.SelectedIndex + 1) switch { 7 => 8, 8 => 10, int i => i };
            double floorHeight  = ParseDouble(TxtFloorHeight.Text, 3.0);
            string savePath     = TxtSavePath.Text.Trim();

            if (string.IsNullOrEmpty(savePath))
            {
                savePath = Path.Combine(Path.GetTempPath(),
                    $"Axo_{projectName}_{DateTime.Now:yyyyMMdd_HHmm}.html");
            }

            string html = _svc.Export(_database, projectName, floors, floorHeight);

            // SVG format seçildiyse HTML wrapper'dan SVG'yi çıkar
            if (CboFormat.SelectedIndex == 1 && !savePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                // Sadece SVG bloğunu kaydet
                int svgStart = html.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
                int svgEnd   = html.IndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
                if (svgStart >= 0 && svgEnd > svgStart)
                    html = html[svgStart..(svgEnd + 6)];
            }

            File.WriteAllText(savePath, html, System.Text.Encoding.UTF8);
            StatusText.Text = $"✓ Kaydedildi: {Path.GetFileName(savePath)}";

            if (ChkAutoOpen.IsChecked == true)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = savePath,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
