using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ViewportCaptureDialog : Window
{
    private readonly CadDatabase _database;
    private readonly FloorSnapshotService _svc = new();
    private FloorSnapshotService.FloorRange[] _floors = [];

    public ViewportCaptureDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        LoadFloors();
        RbOneFloor.Checked   += (_, _) => CmbFloor.IsEnabled = true;
        RbAllFloors.Checked  += (_, _) => CmbFloor.IsEnabled = false;
    }

    private void LoadFloors()
    {
        _floors = _svc.DetectFloors(_database).ToArray();
        CmbFloor.Items.Clear();
        foreach (var f in _floors)
            CmbFloor.Items.Add(f);
        if (_floors.Length > 0) CmbFloor.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Blok adı boş bırakılamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Çakışma kontrolü
        string blockKey = "SNAP_" + name.ToUpperInvariant().Replace(" ", "_");
        if (_database.GetBlock(blockKey) != null)
        {
            var ans = MessageBox.Show($"'{name}' adlı blok zaten var.\nÜzerine yazmak istiyor musunuz?",
                "Üzerine Yaz", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans != MessageBoxResult.Yes) return;
        }

        double zMin = double.MinValue, zMax = double.MaxValue;
        if (RbOneFloor.IsChecked == true && CmbFloor.SelectedItem is FloorSnapshotService.FloorRange floor)
        {
            zMin = floor.ZMin;
            zMax = floor.ZMax;
        }

        MechanicalSystemType? sysFilter = null;
        if (RbSysCold.IsChecked == true)  sysFilter = MechanicalSystemType.DomesticColdWater;
        if (RbSysWaste.IsChecked == true) sysFilter = MechanicalSystemType.WasteWater;

        try
        {
            var block = _svc.CaptureToBlock(_database, name, sysFilter, zMin, zMax);

            StatusBorder.Visibility = Visibility.Visible;
            TxtStatus.Text = $"✅ '{name}' bloğu oluşturuldu.\n" +
                             $"{block.Entities.Count} nesne yakalandı.\n" +
                             $"Xref Yöneticisi'nden bu bloğu çizime ekleyebilirsiniz.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
