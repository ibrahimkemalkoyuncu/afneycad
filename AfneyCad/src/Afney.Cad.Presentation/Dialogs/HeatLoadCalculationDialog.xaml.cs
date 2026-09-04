using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class HeatLoadCalculationDialog
{
    private readonly ObservableCollection<BuildingSurface> _surfaces = [];
    private readonly HeatLoadCalculationService _service = new();
    private HeatLoadResult? _lastResult;

    public HeatLoadCalculationDialog()
    {
        InitializeComponent();

        foreach (var name in HeatLoadCalculationService.DefaultUValues.Keys)
            CboDefaultU.Items.Add(new ComboBoxItem { Content = name });
        CboDefaultU.SelectedIndex = 0;

        SurfaceGrid.ItemsSource = _surfaces;

        // Basit varsayılan oda: 1 dış duvar + 1 pencere
        _surfaces.Add(new BuildingSurface { Name = "Dış Duvar", Area = 12, UValue = 0.40, LinearThermalBridge = 0.05, BridgeLength = 8 });
        _surfaces.Add(new BuildingSurface { Name = "Pencere", Area = 2.5, UValue = 1.60, LinearThermalBridge = 0.0, BridgeLength = 0 });
    }

    private void AddSurface_Click(object sender, RoutedEventArgs e)
    {
        string name = (CboDefaultU.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Yüzey";
        double u = HeatLoadCalculationService.DefaultUValues.TryGetValue(name, out double v) ? v : 1.0;
        _surfaces.Add(new BuildingSurface { Name = name, Area = 5, UValue = u, LinearThermalBridge = 0.05, BridgeLength = 0 });
    }

    private void DeleteSurface_Click(object sender, RoutedEventArgs e)
    {
        if (SurfaceGrid.SelectedItem is BuildingSurface s) _surfaces.Remove(s);
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SurfaceGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_surfaces.Count == 0) { StatusText.Text = "⚠ En az 1 yüzey ekleyin."; return; }

            var input = new HeatLoadInput
            {
                City = string.IsNullOrWhiteSpace(TxtCity.Text) ? "İstanbul" : TxtCity.Text,
                IndoorDesignTemp = ParseDouble(TxtIndoorTemp.Text, 22),
                RoomType = (CboRoomType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "oturma odası",
                RoomVolume = ParseDouble(TxtVolume.Text, 0),
                FloorArea = ParseDouble(TxtFloorArea.Text, 0),
                BuildingMass = (CboBuildingMass.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
                {
                    "Hafif" => BuildingMassType.Light,
                    "Ağır" => BuildingMassType.Heavy,
                    _ => BuildingMassType.Medium
                },
                Surfaces = [.. _surfaces]
            };

            _lastResult = _service.Calculate(input);

            ResTrans.Text = $"{_lastResult.TransmissionLossW:F0} W";
            ResVent.Text = $"{_lastResult.VentilationLossW:F0} W";
            ResReheat.Text = $"{_lastResult.ReheatAllowanceW:F0} W";
            ResTotal.Text = $"{_lastResult.TotalHeatLoadKW:F2} kW";

            string spec = _lastResult.SpecificHeatLoad > 0 ? $" | {_lastResult.SpecificHeatLoad:F1} W/m²" : "";
            SummaryText.Text = $"{input.City}: Dış T={_lastResult.OutdoorDesignTemp:F0}°C, İç T={_lastResult.IndoorDesignTemp:F0}°C, ΔT={_lastResult.IndoorDesignTemp - _lastResult.OutdoorDesignTemp:F0}K, " +
                                $"n={_lastResult.AirChangeRate:F1}/h, f_RH={_lastResult.ReheatFactor:F3}{spec}";
            StatusText.Text = $"✓ Toplam ısıtma yükü: {_lastResult.TotalHeatLoadKW:F2} kW";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
