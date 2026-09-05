using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Tables;
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
    private readonly SheetIndexService? _sheetIndex;
    private readonly FloorSnapshotService _svc = new();
    private List<FloorSnapshotService.SnapshotInfo> _snapshots = [];
    private List<PlacedBlockRow> _placed = [];

    /*
       NE: sheetIndex parametresi (opsiyonel, geriye-uyumlu)
       NEDEN — GERÇEK HATA (Session #75 denetiminde bulundu): Bu dialogun kendi
              "Antet Ekle" butonu `new TitleBlockDialog(_database)` çağırıyordu —
              yeni sheetIndex parametresi verilmediğinden `TitleBlockDialog` sessizce
              paylaşılan statik `SheetIndexService.Instance`'a düşüyordu. Bu, Session
              #74'ün "paftalar birbirine karışıyor" hatasını çözmek için per-document
              yaptığı `CadDocumentContext.SheetIndex`'i BU çağrı yolunda atlıyordu —
              buradan eklenen bir pafta hem yanlış sayaçtan numara alıyor hem de
              `.sheetset.json`'a hiç kaydedilmiyordu (kaydet/yükle ile kayboluyordu).
    */
    public LayoutSheetDialog(CadDatabase database, SheetIndexService? sheetIndex = null)
    {
        InitializeComponent();
        _database = database;
        _sheetIndex = sheetIndex;
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
        var dialog = new TitleBlockDialog(_database, _sheetIndex) { Owner = this };
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

    // ── Tüm Katları Yakala & Izgara Düzende Paftaya Ekle ─────────────────────
    private void CaptureAll_Click(object sender, RoutedEventArgs e)
    {
        var floors = _svc.DetectFloors(_database);
        if (floors.Count == 0)
        {
            MessageBox.Show("Tespit edilebilir kat bulunamadı.", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ans = MessageBox.Show(
            $"{floors.Count} kat tespit edildi:\n" +
            string.Join("\n", floors.Select(f => $"  • {f.Name}")) +
            "\n\nTüm katlar snapshot bloğuna alınıp paftaya ızgara düzende yerleştirilsin mi?",
            "Tüm Katları Ekle", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ans != MessageBoxResult.Yes) return;

        // Izgara parametreleri — 2 sütun, satır/sütun arası 10 000 mm boşluk
        const double colSpacing = 10_000;
        const double rowSpacing = 10_000;
        const int columns = 2;

        int added = 0;
        for (int i = 0; i < floors.Count; i++)
        {
            var floor = floors[i];
            string safeName = floor.Name.Replace(" ", "_").Replace(".", "").ToUpperInvariant();

            // Snapshot bloğu oluştur (mevcut varsa günceller)
            _svc.CaptureToBlock(_database, safeName, null, floor.ZMin, floor.ZMax);

            // Izgara konumu
            int col = i % columns;
            int row = i / columns;
            double x = col * colSpacing;
            double y = -(row * rowSpacing); // Y aşağı gidiyor

            var block = _database.GetBlock("SNAP_" + safeName);
            if (block is null) continue;

            // Aynı isimli referans varsa ekleme
            bool alreadyPlaced = _database.GetAllEntities()
                .OfType<BlockReferenceEntity>()
                .Any(r => r.BlockName.Equals("SNAP_" + safeName, StringComparison.OrdinalIgnoreCase));

            if (!alreadyPlaced)
            {
                var refEnt = new BlockReferenceEntity("SNAP_" + safeName, new Vector3D(x, y, 0))
                {
                    Definition = block,
                    Scale = 1.0
                };
                _database.AddEntity(refEnt);
                added++;
            }
        }

        TxtStatus.Text = $"✅ {floors.Count} kat snapshot alındı, {added} yeni blok paftaya eklendi.";
        Refresh();
    }

    // ── Tümünü Patlat ────────────────────────────────────────────────────────
    private void ExplodeAll_Click(object sender, RoutedEventArgs e)
    {
        var refs = _database.GetAllEntities()
            .OfType<BlockReferenceEntity>()
            .Where(r => r.Layer != "EXPLODED")
            .ToList();

        if (refs.Count == 0)
        {
            MessageBox.Show("Patlatılacak blok referansı bulunamadı.", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ans = MessageBox.Show(
            $"{refs.Count} blok referansı patlatılacak.\n\n" +
            "⚠ Bu işlem geri alınamaz. Devam edilsin mi?",
            "Tümünü Patlat", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;

        int totalAdded = 0;
        foreach (var refEntity in refs)
        {
            if (refEntity.Definition is null) continue;

            var matrix = Matrix4x4.TranslationMatrix(
                             refEntity.Definition.BasePoint.X * -1,
                             refEntity.Definition.BasePoint.Y * -1,
                             refEntity.Definition.BasePoint.Z * -1)
                       * Matrix4x4.Scaling(refEntity.Scale, refEntity.Scale, refEntity.Scale)
                       * Matrix4x4.RotationZ(refEntity.Rotation * Math.PI / 180.0)
                       * Matrix4x4.TranslationMatrix(refEntity.Position.X, refEntity.Position.Y, refEntity.Position.Z);

            foreach (var src in refEntity.Definition.Entities)
            {
                var clone = src.Clone();
                clone.Transform(matrix);
                clone.Layer = "EXPLODED";
                _database.AddEntity(clone);
                totalAdded++;
            }
            _database.RemoveEntity(refEntity.Id);
        }

        TxtStatus.Text = $"✅ {refs.Count} blok patlatıldı — {totalAdded} nesne eklendi.";
        Refresh();
    }

    // ── DXF Merge: tüm blokları patlatarak tek DXF'e aktar ──────────────────
    private void ExportMerged_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Birleşik Pafta DXF Çıktısı",
            Filter     = "DXF|*.dxf",
            FileName   = $"AfneyCAD_BirlesikPafta_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".dxf"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            // Geçici DB klon: tüm referansları yerinde patlat
            var tempDb = new CadDatabase();
            foreach (var layer in _database.GetLayers())
            {
                if (tempDb.GetLayer(layer.Name) is null)
                    tempDb.AddLayer(new CadLayer(layer.Name) { Color = layer.Color });
            }

            // Referans olmayan entity'ler doğrudan kopyala
            foreach (var ent in _database.GetAllEntities().Where(e => e is not BlockReferenceEntity))
                tempDb.AddEntity(ent.Clone());

            // Blok referanslarını patlatarak ekle
            foreach (var refEntity in _database.GetAllEntities().OfType<BlockReferenceEntity>())
            {
                if (refEntity.Definition is null)
                {
                    tempDb.AddEntity(refEntity.Clone());
                    continue;
                }

                var matrix = Matrix4x4.TranslationMatrix(
                                 refEntity.Definition.BasePoint.X * -1,
                                 refEntity.Definition.BasePoint.Y * -1,
                                 refEntity.Definition.BasePoint.Z * -1)
                           * Matrix4x4.Scaling(refEntity.Scale, refEntity.Scale, refEntity.Scale)
                           * Matrix4x4.RotationZ(refEntity.Rotation * Math.PI / 180.0)
                           * Matrix4x4.TranslationMatrix(refEntity.Position.X, refEntity.Position.Y, refEntity.Position.Z);

                foreach (var src in refEntity.Definition.Entities)
                {
                    var clone = src.Clone();
                    clone.Transform(matrix);
                    tempDb.AddEntity(clone);
                }
            }

            var writer = new Afney.Cad.Infrastructure.Export.DxfWriterService(tempDb);
            writer.WriteToFile(dlg.FileName);

            TxtStatus.Text = $"✅ Birleşik DXF kaydedildi: {System.IO.Path.GetFileName(dlg.FileName)} " +
                             $"({tempDb.GetAllEntities().Count()} nesne)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DXF Merge hatası: {ex.Message}", "Hata",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
}
