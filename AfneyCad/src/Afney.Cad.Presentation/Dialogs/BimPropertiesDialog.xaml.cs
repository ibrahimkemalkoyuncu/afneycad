using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BimPropertiesDialog
{
    private readonly ArchitecturalObstacle _obstacle;
    private readonly ObservableCollection<BimMaterialLayer> _layers;

    public BimPropertiesDialog(ArchitecturalObstacle obstacle)
    {
        InitializeComponent();
        _obstacle = obstacle;
        _layers   = new ObservableCollection<BimMaterialLayer>(obstacle.MaterialLayers);
        LayerGrid.ItemsSource = _layers;
        LoadFromObstacle();
    }

    private void LoadFromObstacle()
    {
        TxtName.Text         = _obstacle.Name;
        TxtHeight.Text       = _obstacle.Height.ToString(CultureInfo.InvariantCulture);
        TxtSoundDb.Text      = _obstacle.SoundReductionIndexDb.ToString(CultureInfo.InvariantCulture);
        TxtFireMinutes.Text  = _obstacle.FireResistanceMinutes.ToString();
        TxtUValue.Text       = _obstacle.UValueOverride?.ToString(CultureInfo.InvariantCulture) ?? "";
        CboType.SelectedIndex = (int)_obstacle.Type;
        CboFireClass.SelectedIndex = (int)_obstacle.FireRating;
        RefreshSummary();
    }

    // ── Kaydet ───────────────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        LayerGrid.CommitEdit(DataGridEditingUnit.Row, true);

        _obstacle.Name               = TxtName.Text.Trim();
        _obstacle.Height             = ParseDouble(TxtHeight.Text, 3000);
        _obstacle.SoundReductionIndexDb = ParseDouble(TxtSoundDb.Text, 0);
        _obstacle.FireResistanceMinutes = (int)ParseDouble(TxtFireMinutes.Text, 0);
        _obstacle.MaterialLayers     = _layers.ToList();
        _obstacle.FireRating         = (FireRatingClass)(CboFireClass.SelectedIndex >= 0 ? CboFireClass.SelectedIndex : 7);
        _obstacle.Type               = (ObstacleType)(CboType.SelectedIndex >= 0 ? CboType.SelectedIndex : 0);

        if (!string.IsNullOrWhiteSpace(TxtUValue.Text) &&
            double.TryParse(TxtUValue.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double uv))
            _obstacle.UValueOverride = uv;
        else
            _obstacle.UValueOverride = null;

        StatusText.Text = "Kaydedildi.";
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    // ── Katman İşlemleri ─────────────────────────────────────────────────────────

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        _layers.Add(new BimMaterialLayer { MaterialName = "Yeni Katman", ThicknessMm = 50, ThermalConductivity = 0.7 });
        RefreshSummary();
    }

    private void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayerGrid.SelectedItem is BimMaterialLayer layer) { _layers.Remove(layer); RefreshSummary(); }
    }

    private void CalcU_Click(object sender, RoutedEventArgs e)
    {
        LayerGrid.CommitEdit(DataGridEditingUnit.Row, true);
        RefreshSummary();
    }

    // ── Şablonlar ────────────────────────────────────────────────────────────────

    private void TemplateExtWall_Click(object sender, RoutedEventArgs e)
    {
        _layers.Clear();
        _layers.Add(new BimMaterialLayer { MaterialName = "Sıva (Dış)",      ThicknessMm = 20,  ThermalConductivity = 0.87 });
        _layers.Add(new BimMaterialLayer { MaterialName = "Tuğla (19cm)",    ThicknessMm = 190, ThermalConductivity = 0.45 });
        _layers.Add(new BimMaterialLayer { MaterialName = "EPS Yalıtım",     ThicknessMm = 60,  ThermalConductivity = 0.036 });
        _layers.Add(new BimMaterialLayer { MaterialName = "Sıva (İç)",       ThicknessMm = 20,  ThermalConductivity = 0.87 });
        TxtFireMinutes.Text = "60"; TxtSoundDb.Text = "45"; RefreshSummary();
    }

    private void TemplateIntWall_Click(object sender, RoutedEventArgs e)
    {
        _layers.Clear();
        _layers.Add(new BimMaterialLayer { MaterialName = "Sıva",            ThicknessMm = 15,  ThermalConductivity = 0.87 });
        _layers.Add(new BimMaterialLayer { MaterialName = "Bims Blok 10cm",  ThicknessMm = 100, ThermalConductivity = 0.27 });
        _layers.Add(new BimMaterialLayer { MaterialName = "Sıva",            ThicknessMm = 15,  ThermalConductivity = 0.87 });
        TxtFireMinutes.Text = "30"; TxtSoundDb.Text = "35"; RefreshSummary();
    }

    private void TemplateSlab_Click(object sender, RoutedEventArgs e)
    {
        _layers.Clear();
        _layers.Add(new BimMaterialLayer { MaterialName = "Seramik Kaplama",  ThicknessMm = 10,  ThermalConductivity = 1.0 });
        _layers.Add(new BimMaterialLayer { MaterialName = "Şap",              ThicknessMm = 60,  ThermalConductivity = 1.4 });
        _layers.Add(new BimMaterialLayer { MaterialName = "XPS Yalıtım",      ThicknessMm = 50,  ThermalConductivity = 0.033 });
        _layers.Add(new BimMaterialLayer { MaterialName = "Betonarme Plak",   ThicknessMm = 150, ThermalConductivity = 2.1 });
        TxtFireMinutes.Text = "90"; TxtSoundDb.Text = "52"; RefreshSummary();
    }

    // ── Özet Paneli ──────────────────────────────────────────────────────────────

    private void RefreshSummary()
    {
        double totalThick = _layers.Sum(l => l.ThicknessMm);
        const double Rsi = 0.13, Rse = 0.04;
        double rTotal = Rsi + Rse + _layers.Sum(l => l.ThermalResistance);
        double u = rTotal > 0 ? 1.0 / rTotal : 0;
        double r = rTotal - Rsi - Rse;

        ResTotalThick.Text = $"Toplam Kalınlık: {totalThick:F0} mm";
        ResUValue.Text     = $"U-Değeri: {u:F3} W/m²K";
        ResRValue.Text     = $"R-Değeri: {r:F3} m²K/W";
        ResFireRating.Text = $"Yangın: {TxtFireMinutes.Text} dk.";
        ResSoundDb.Text    = $"Ses: {TxtSoundDb.Text} dB";
        UValueResult.Text  = $"U = {u:F3} W/m²K";

        LayerGrid.Items.Refresh();
    }

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
