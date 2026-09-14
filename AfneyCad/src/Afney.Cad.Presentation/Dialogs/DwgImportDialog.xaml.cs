using System.IO;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Infrastructure.Import;

namespace Afney.Cad.Presentation.Dialogs;

public partial class DwgImportDialog : Window
{
    private List<CadEntity>? _previewEntities;
    private readonly HashSet<string> _selectedLayers = new();

    public List<CadEntity>? ImportedEntities { get; private set; }
    public string? SelectedFilePath { get; private set; }
    public bool FlattenZ => ChkFlattenZ.IsChecked == true;
    public bool RemoveOutliers => ChkRemoveOutliers.IsChecked == true;
    public bool RemoveShortLines => ChkRemoveShortLines.IsChecked == true;

    public DwgImportDialog()
    {
        InitializeComponent();
    }

    private void OnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "DWG/DXF Dosyası Seçin",
            Filter = "AutoCAD Dosyaları (*.dwg;*.dxf)|*.dwg;*.dxf|DWG Dosyası (*.dwg)|*.dwg|DXF Dosyası (*.dxf)|*.dxf|Tüm Dosyalar (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            TxtFilePath.Text = dlg.FileName;
            SelectedFilePath = dlg.FileName;
            BtnAnalyze.IsEnabled = true;

            var fi = new FileInfo(dlg.FileName);
            TxtFormat.Text = fi.Extension.ToUpper().TrimStart('.');
            TxtFileSize.Text = fi.Length > 1024 * 1024
                ? $"{fi.Length / (1024.0 * 1024.0):F1} MB"
                : $"{fi.Length / 1024.0:F1} KB";
        }
    }

    private void OnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SelectedFilePath)) return;

        try
        {
            TxtProgress.Text = "Dosya analiz ediliyor...";
            ImportProgress.Visibility = Visibility.Visible;
            ImportProgress.IsIndeterminate = true;

            var importer = new CadImporter();
            _previewEntities = importer.Import(SelectedFilePath);

            int lines = 0, arcs = 0, polys = 0, texts = 0, inserts = 0, hatches = 0, other = 0;
            var layers = new HashSet<string>();
            var blocks = new HashSet<string>();

            foreach (var ent in _previewEntities)
            {
                if (!string.IsNullOrEmpty(ent.Layer)) layers.Add(ent.Layer);
                if (ent.ParentBlockName != null) blocks.Add(ent.ParentBlockName);

                switch (ent)
                {
                    case LineEntity: lines++; break;
                    case CircleEntity or ArcEntity: arcs++; break;
                    case LwPolylineEntity or SplineEntity: polys++; break;
                    case TextEntity: texts++; break;
                    case BlockReferenceEntity: inserts++; break;
                    case HatchEntity: hatches++; break;
                    default: other++; break;
                }
            }

            TxtEntityCount.Text = _previewEntities.Count.ToString("N0");
            TxtLayerCount.Text = layers.Count.ToString();
            TxtBlockCount.Text = blocks.Count.ToString();
            TxtVersion.Text = Path.GetExtension(SelectedFilePath)!.ToUpper().TrimStart('.');
            TxtLines.Text = lines.ToString("N0");
            TxtArcs.Text = arcs.ToString("N0");
            TxtPolylines.Text = polys.ToString("N0");
            TxtTexts.Text = texts.ToString("N0");
            TxtInserts.Text = inserts.ToString("N0");
            TxtHatches.Text = hatches.ToString("N0");
            TxtOther.Text = other.ToString("N0");

            LayerCheckList.Children.Clear();
            _selectedLayers.Clear();
            foreach (var layer in layers.OrderBy(l => l))
            {
                _selectedLayers.Add(layer);
                var cb = new CheckBox
                {
                    Content = layer,
                    IsChecked = true,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 2, 12, 2),
                    FontSize = 11
                };
                cb.Checked += (_, _) => _selectedLayers.Add(layer);
                cb.Unchecked += (_, _) => _selectedLayers.Remove(layer);
                LayerCheckList.Children.Add(cb);
            }

            ImportProgress.IsIndeterminate = false;
            ImportProgress.Value = 100;
            TxtProgress.Text = $"Analiz tamamlandı — {_previewEntities.Count:N0} nesne, {layers.Count} katman";
            TxtStatus.Text = "Hazır";
            BtnImport.IsEnabled = true;
        }
        catch (Exception ex)
        {
            TxtProgress.Text = $"Analiz hatası: {ex.Message}";
            ImportProgress.Visibility = Visibility.Collapsed;
        }
    }

    /*
       NE/NEDEN — GERÇEK HATA (bu turda bulundu, kullanıcının "en temel amaç: sıhhi
       tesisatı mimari üzerinde çizmek" önceliği üzerine araştırılırken): AfneyCAD'in
       dünya-koordinat birimi UYGULAMA GENELİNDE milimetredir — bu, `PipeCostService`,
       `PressureDropService`, `HydraulicReportService`, `CalculationTableService`,
       `RealTimeCostService`, `SelectionBomService` gibi 15'ten fazla servisin `pipe.Length`'i
       tutarlı şekilde "/1000.0" ile metreye çevirmesinden, `MepLevel`/`LevelManager`'ın kat
       yüksekliklerini mm cinsinden tutmasından (ör. 3000 = 3m) ve `BomService`'teki paralel
       "Kanal" (doğru) ile "Boru" (bu turda düzeltilen hatalı) satırlarının karşılaştırmasından
       kesin olarak doğrulandı. Ama bu ekranın ölçek seçenekleri TAM TERSİ bir varsayımla
       yazılmıştı — "Metre (ölçekleme yok)" ve "Milimetre (×0.001)" etiketleri, dünya biriminin
       METRE olduğunu varsayıyordu. Sonuç: milimetre cinsinden (Türkiye'de en yaygın mimari
       DWG kuralı) bir dosya "Milimetre" seçilerek içe aktarıldığında, tüm geometri yanlışlıkla
       1000 KAT küçültülüyordu — içe aktarılan mimari, üzerine çizilecek tesisata göre
       görünmez denecek kadar küçük kalıyordu. Şimdi her seçenek GERÇEKTEN uygulamanın mm
       dünyasına dönüştürüyor: Milimetre = ölçekleme yok, Metre = ×1000, Santimetre = ×10.
       Otomatik Algıla eşikleri de gerçekçi duvar/çizgi uzunluklarına göre (mm cinsinden en
       yaygın durum binlerce birim) yeniden kalibre edildi.
    */
    private void OnImport_Click(object sender, RoutedEventArgs e)
    {
        if (_previewEntities == null) return;

        var filtered = _previewEntities
            .Where(ent => _selectedLayers.Contains(ent.Layer ?? "0"))
            .ToList();

        double scaleFactor = 1.0;
        if (RbMetre.IsChecked == true) scaleFactor = 1000.0;       // m  -> mm
        else if (RbSantimetre.IsChecked == true) scaleFactor = 10.0;    // cm -> mm
        else if (RbMilimetre.IsChecked == true) scaleFactor = 1.0;      // mm -> mm (zaten uygulamanın birimi)
        else if (RbAutoScale.IsChecked == true)
        {
            double avgLen = filtered.OfType<LineEntity>().Take(100)
                .Select(l => l.GetLength()).Where(len => len > 0).DefaultIfEmpty(1000).Average();
            // Tipik bir mimari duvar/çizgi segmenti: metre cinsinde 0.1-9, santimetre
            // cinsinde 10-900, milimetre cinsinde (en yaygın Türkiye kuralı) yüzlerce-binlerce.
            if (avgLen < 50) scaleFactor = 1000.0;
            else if (avgLen < 500) scaleFactor = 10.0;
            // avgLen >= 500 -> muhtemelen zaten mm, ölçekleme yok.
        }

        if (Math.Abs(scaleFactor - 1.0) > 1e-9)
        {
            foreach (var ent in filtered)
            {
                var scaleMatrix = Afney.Cad.Geometry.Primitives.Matrix4x4.CreateScale(scaleFactor);
                ent.Transform(scaleMatrix);
            }
        }

        /*
           NE/NEDEN — GERÇEK BOŞLUK (bu turda bulundu): "Z koordinatlarını sıfırla",
           "Aşırı uzak nesneleri kaldır", "Çok kısa çizgileri kaldır" onay kutuları
           varsayılan olarak işaretliydi ama hiçbir çağıran (ör. `OnOpenFile`) bu
           bayrakları hiç okumuyordu — üçü de tamamen kozmetikti. Artık burada,
           `ImportedEntities` dışarı açılmadan ÖNCE gerçekten uygulanıyor.
        */
        if (FlattenZ)
        {
            foreach (var ent in filtered)
            {
                if (ent is LineEntity line)
                {
                    line.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(line.StartPoint.X, line.StartPoint.Y, 0);
                    line.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(line.EndPoint.X, line.EndPoint.Y, 0);
                }
            }
        }

        if (RemoveOutliers && filtered.Count > 0)
        {
            var centers = filtered.Select(ent => ent.GetBoundingBox().Center).ToList();
            double avgX = centers.Average(c => c.X);
            double avgY = centers.Average(c => c.Y);
            const double thresholdMm = 500000.0; // 500m yarıçap dışındaki nesneler "sapkın" kabul edilir
            double thresholdSq = thresholdMm * thresholdMm;

            filtered = filtered.Where(ent =>
            {
                var c = ent.GetBoundingBox().Center;
                double distSq = Math.Pow(c.X - avgX, 2) + Math.Pow(c.Y - avgY, 2);
                return distSq < thresholdSq;
            }).ToList();
        }

        if (RemoveShortLines)
        {
            // 10mm (1cm) altındaki çizgiler genellikle DWG dönüşüm artığı/gürültüdür.
            const double minLengthMm = 10.0;
            filtered = filtered.Where(ent => ent is not LineEntity line || line.GetLength() >= minLengthMm).ToList();
        }

        ImportedEntities = filtered;
        TxtStatus.Text = $"{filtered.Count:N0} nesne import edildi.";
        DialogResult = true;
    }
}
