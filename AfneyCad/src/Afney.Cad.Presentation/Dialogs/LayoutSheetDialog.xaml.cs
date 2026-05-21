using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class LayoutSheetDialog : Window
{
    // ── View-model for placed block references ────────────────────────────────
    public class PlacedBlockRow
    {
        public Guid   Id        { get; set; }
        public string BlockName { get; set; } = "";
        public string PosX      { get; set; } = "";
        public string PosY      { get; set; } = "";
        public string Scale     { get; set; } = "";
        public string Status    { get; set; } = "Bağlı";
    }

    private readonly CadDatabase _database;
    private readonly FloorSnapshotService _svc = new();
    private List<FloorSnapshotService.SnapshotInfo> _snapshots = [];
    private List<PlacedBlockRow> _placed = [];

    public LayoutSheetDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        Refresh();
    }

    private void Refresh()
    {
        // Sol: mevcut bloklar
        _snapshots = _svc.GetSnapshots(_database);
        BlockListGrid.ItemsSource = null;
        BlockListGrid.ItemsSource = _snapshots;

        // Sağ: paftadaki referanslar
        _placed = _database.GetAllEntities()
            .OfType<BlockReferenceEntity>()
            .Select(r => new PlacedBlockRow
            {
                Id        = r.Id,
                BlockName = r.BlockName.StartsWith("SNAP_", StringComparison.OrdinalIgnoreCase)
                                ? r.BlockName[5..] : r.BlockName,
                PosX      = $"{r.Position.X:F0}",
                PosY      = $"{r.Position.Y:F0}",
                Scale     = $"{r.Scale:F2}",
                Status    = r.Definition != null ? "Bağlı" : "Tanım Yok"
            })
            .ToList();

        PlacedGrid.ItemsSource = null;
        PlacedGrid.ItemsSource = _placed;
    }

    private void BlockListGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => BtnInsert.IsEnabled = BlockListGrid.SelectedItem is FloorSnapshotService.SnapshotInfo;

    private void PlacedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool has = PlacedGrid.SelectedItem is PlacedBlockRow;
        BtnExplode.IsEnabled   = has;
        BtnRemoveRef.IsEnabled = has;
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (BlockListGrid.SelectedItem is not FloorSnapshotService.SnapshotInfo info) return;

        var block = _database.GetBlock(info.BlockName);
        if (block is null)
        {
            MessageBox.Show("Blok tanımı bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        double x = ParseDouble(TxtOffsetX.Text);
        double y = ParseDouble(TxtOffsetY.Text);

        // Oto-offset: X = mevcut referansların sağına ekle
        if (TxtOffsetX.Text.Trim() == "0" && TxtOffsetY.Text.Trim() == "0")
        {
            var existing = _database.GetAllEntities().OfType<BlockReferenceEntity>().ToList();
            if (existing.Count > 0)
            {
                x = existing.Max(r =>
                {
                    try { return r.GetBoundingBox().Max.X + 5000; }
                    catch { return r.Position.X + 60_000; }
                });
            }
        }

        var refEntity = new BlockReferenceEntity(info.BlockName, new Vector3D(x, y, 0))
        {
            Definition = block,
            Scale      = 1.0
        };
        _database.AddEntity(refEntity);

        TxtStatus.Text = $"✅ '{info.DisplayName}' paftaya eklendi. Konum: X={x:F0}, Y={y:F0} mm";
        Refresh();
    }

    private void Explode_Click(object sender, RoutedEventArgs e)
    {
        if (PlacedGrid.SelectedItem is not PlacedBlockRow row) return;

        var refEntity = _database.GetAllEntities()
            .OfType<BlockReferenceEntity>()
            .FirstOrDefault(r => r.Id == row.Id);

        if (refEntity?.Definition is null)
        {
            MessageBox.Show("Blok tanımı bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Tek patlat kuralı: katman etiketiyle işaretle
        if (refEntity.Layer == "EXPLODED")
        {
            MessageBox.Show(
                "Bu blok zaten patlatılmış!\n\nBir bloğu iki kez patlatmak ciddi hataya yol açar.\nBu işlem engellendi.",
                "Uyarı — Tek Patlat Kuralı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ans = MessageBox.Show(
            $"'{row.BlockName}' bloğu {refEntity.Definition.Entities.Count} nesneye ayrılacak.\n\n" +
            "⚠ UYARI: Bu işlem yalnızca bir kez yapılmalıdır!\n" +
            "İkinci kez patlatma ciddi veri hatalarına yol açar.\n\nDevam edilsin mi?",
            "Patlat — Kritik Uyarı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;

        // Dönüşüm matrisi uygula
        var matrix = Matrix4x4.TranslationMatrix(
                         refEntity.Definition.BasePoint.X * -1,
                         refEntity.Definition.BasePoint.Y * -1,
                         refEntity.Definition.BasePoint.Z * -1)
                   * Matrix4x4.Scaling(refEntity.Scale, refEntity.Scale, refEntity.Scale)
                   * Matrix4x4.RotationZ(refEntity.Rotation * Math.PI / 180.0)
                   * Matrix4x4.TranslationMatrix(refEntity.Position.X, refEntity.Position.Y, refEntity.Position.Z);

        int addedCount = 0;
        foreach (var src in refEntity.Definition.Entities)
        {
            var clone = src.Clone();
            clone.Transform(matrix);
            clone.Layer = "EXPLODED"; // patlatılmış işareti
            _database.AddEntity(clone);
            addedCount++;
        }

        _database.RemoveEntity(refEntity.Id);

        TxtStatus.Text = $"✅ Patlatma tamamlandı — {addedCount} nesne eklendi. " +
                         "Artık her nesneyi ayrı ayrı düzenleyebilirsiniz.";
        Refresh();
    }

    private void RemoveRef_Click(object sender, RoutedEventArgs e)
    {
        if (PlacedGrid.SelectedItem is not PlacedBlockRow row) return;

        var ans = MessageBox.Show($"'{row.BlockName}' referansı kaldırılsın mı?\n(Blok tanımı silinmez.)",
            "Referansı Kaldır", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ans != MessageBoxResult.Yes) return;

        _database.RemoveEntity(row.Id);
        Refresh();
    }

    private void TitleBlock_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TitleBlockDialog(_database) { Owner = this };
        dialog.ShowDialog();
        Refresh();
    }

    private void ExportDxf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Pafta DXF Çıktısı",
            Filter     = "DXF|*.dxf",
            FileName   = $"AfneyCAD_Pafta_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".dxf"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var writer = new Afney.Cad.Infrastructure.Export.DxfWriterService(_database);
            writer.WriteToFile(dlg.FileName);
            TxtStatus.Text = $"✅ DXF kaydedildi: {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DXF hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
}
