using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class GutterDesignDialog
{
    private readonly ObservableCollection<GutterSizingService.RoofSection> _sections = [];

    public GutterDesignDialog()
    {
        InitializeComponent();

        foreach (var city in GutterSizingService.RainfallIntensity.Keys.OrderBy(c => c))
            CboCity.Items.Add(new ComboBoxItem { Content = city });
        CboCity.SelectedIndex = 0;

        foreach (var surf in GutterSizingService.RunoffCoefficients.Keys)
            CboSurface.Items.Add(new ComboBoxItem { Content = surf });
        CboSurface.SelectedIndex = 0;

        SectionGrid.ItemsSource = _sections;
    }

    private void City_Changed(object sender, SelectionChangedEventArgs e)
    {
        string city = (CboCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (GutterSizingService.RainfallIntensity.TryGetValue(city, out double r))
            TxtRainfall.Text = r.ToString("F4", CultureInfo.InvariantCulture);
    }

    private void AddSection_Click(object sender, RoutedEventArgs e)
    {
        string surf = (CboSurface.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Çatı (kiremit/metal)";
        _sections.Add(new GutterSizingService.RoofSection { Name = $"Bölüm {_sections.Count + 1}", AreaM2 = 50, SurfaceType = surf });
    }

    private void DeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (SectionGrid.SelectedItem is GutterSizingService.RoofSection s) _sections.Remove(s);
    }

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        _sections.Clear();
        _sections.Add(new GutterSizingService.RoofSection { Name = "Ön Cephe", AreaM2 = 80,  SurfaceType = "Çatı (kiremit/metal)" });
        _sections.Add(new GutterSizingService.RoofSection { Name = "Arka Cephe", AreaM2 = 80, SurfaceType = "Çatı (kiremit/metal)" });
        _sections.Add(new GutterSizingService.RoofSection { Name = "Teras",     AreaM2 = 40,  SurfaceType = "Düz Çatı (beton)" });
        StatusText.Text = "✓ Şablon yüklendi.";
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SectionGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_sections.Count == 0) { StatusText.Text = "⚠ Çatı bölümü ekleyin."; return; }

            string city = (CboCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Genel";
            var svc = new GutterSizingService
            {
                RainfallOverride = ParseDouble(TxtRainfall.Text, 0),
                GutterSlope      = ParseDouble(TxtSlope.Text, 0.5) / 100.0,
                ManningN         = ParseDouble(TxtManning.Text, 0.013)
            };

            var r = svc.Calculate(city, _sections.ToList());

            ResFlow.Text    = $"Q = {r.TotalFlowLs:F3} l/s";
            ResGutter.Text  = $"Oluk: Ø{r.GutterDiameterMm:F0}mm";
            ResDown.Text    = $"Dere: {r.DownpipeCount}×Ø{r.DownpipeDiameterMm:F0}mm";
            ResSpacing.Text = $"Aralık: ≤{r.DownpipeSpacingM:F1}m";
            StatusText.Text = r.Summary;
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
