using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class XrefManagerDialog : Window
{
    private readonly CadDatabase _database;
    private readonly FloorSnapshotService _svc = new();
    private List<FloorSnapshotService.SnapshotInfo> _snapshots = [];

    public XrefManagerDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        Refresh();
    }

    private void Refresh()
    {
        _snapshots = _svc.GetSnapshots(_database);
        XrefGrid.ItemsSource = null;
        XrefGrid.ItemsSource = _snapshots;

        TxtInfo.Text = _snapshots.Count == 0
            ? "Henüz hiç ekran çizimi kaydedilmedi. 'Ekran Çizimi' dialogunu kullanın."
            : $"{_snapshots.Count} blok mevcut — {_snapshots.Count(s => s.IsReferenced)} bağlı.";
    }

    private void XrefGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = XrefGrid.SelectedItem is FloorSnapshotService.SnapshotInfo;
        BtnAttach.IsEnabled = hasSelection;
        BtnDetach.IsEnabled = hasSelection &&
            (XrefGrid.SelectedItem as FloorSnapshotService.SnapshotInfo)?.IsReferenced == true;
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (XrefGrid.SelectedItem is not FloorSnapshotService.SnapshotInfo info) return;

        var block = _database.GetBlock(info.BlockName);
        if (block is null)
        {
            MessageBox.Show("Blok tanımı bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Bounding box üzerinden bir sonraki boş konuma yerleştir
        double offsetX = CalculateNextInsertOffset();
        var refEntity = new BlockReferenceEntity(info.BlockName, new Vector3D(offsetX, 0, 0))
        {
            Definition = block,
            Scale      = 1.0
        };

        _database.AddEntity(refEntity);
        Refresh();

        MessageBox.Show(
            $"'{info.DisplayName}' bloğu çizime eklendi.\n" +
            $"Konum: X={offsetX:F0} mm  (pafta düzeninde konumlandırın).",
            "Bağlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Detach_Click(object sender, RoutedEventArgs e)
    {
        if (XrefGrid.SelectedItem is not FloorSnapshotService.SnapshotInfo info) return;

        var refs = _database.GetAllEntities()
            .OfType<BlockReferenceEntity>()
            .Where(r => r.BlockName == info.BlockName)
            .ToList();

        if (refs.Count == 0) { Refresh(); return; }

        var ans = MessageBox.Show(
            $"'{info.DisplayName}' için {refs.Count} referans silinecek.\nDevam edilsin mi?",
            "Ayır", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;

        foreach (var r in refs)
            _database.RemoveEntity(r.Id);

        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // Mevcut blok referanslarının sağına yerleştir
    private double CalculateNextInsertOffset()
    {
        var existingRefs = _database.GetAllEntities().OfType<BlockReferenceEntity>().ToList();
        if (existingRefs.Count == 0) return 0;
        return existingRefs.Max(r =>
        {
            try { return r.GetBoundingBox().Max.X + 2000; }
            catch { return r.Position.X + 50_000; }
        });
    }
}
