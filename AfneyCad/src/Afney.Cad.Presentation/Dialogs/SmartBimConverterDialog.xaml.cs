using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class SmartBimConverterDialog
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel _kernel;

    public SmartBimConverterDialog(CadDatabase database, MechanicalKernel kernel)
    {
        InitializeComponent();
        _database = database;
        _kernel   = kernel;
        Loaded += (_, _) => Scan_Click(this, new RoutedEventArgs());
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        var svc = new SmartBimConverterService(_database);
        var wallLayers = svc.DetectWallLayers();
        var allLayers  = _database.GetLayers().Select(l => l.Name).OrderBy(l => l).ToList();

        LayerList.Items.Clear();
        foreach (var layer in allLayers)
        {
            var item = new ListBoxItem { Content = layer };
            LayerList.Items.Add(item);
            if (wallLayers.Any(w => w.Equals(layer, System.StringComparison.OrdinalIgnoreCase)))
                item.IsSelected = true;
        }

        StatusText.Text = wallLayers.Count > 0
            ? $"✓ {wallLayers.Count} mimari layer otomatik tespit edildi."
            : "Otomatik tespit yok — layer'ları manuel seçin.";
    }

    private void Convert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected = LayerList.SelectedItems.Cast<ListBoxItem>()
                .Select(i => i.Content?.ToString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (selected.Count == 0)
            {
                StatusText.Text = "⚠ En az bir layer seçin.";
                return;
            }

            double thickness = ParseDouble(TxtThickness.Text, 200);
            double height    = ParseDouble(TxtHeight.Text, 3000);

            var obstacleType = (CboObstacleType.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "Döşeme" => ObstacleType.Slab,
                "Çatı"   => ObstacleType.Roof,
                "Kolon"  => ObstacleType.Column,
                _        => ObstacleType.Wall
            };

            var svc = new SmartBimConverterService(_database)
            {
                WallThicknessDefaultMm = thickness,
                WallHeightDefaultMm    = height
            };

            var result = svc.Convert(selected, obstacleType);

            // Kernel'a ekle
            _kernel.ArchitecturalObstacles.AddRange(result.Obstacles);

            LogText.Text = string.Join("\n", result.Log);
            StatusText.Text = $"✓ {result.WallCount} BIM nesnesi oluşturuldu | {result.SkippedCount} atlandı";
            DialogResult = result.WallCount > 0;
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
