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

    private void OnImport_Click(object sender, RoutedEventArgs e)
    {
        if (_previewEntities == null) return;

        var filtered = _previewEntities
            .Where(ent => _selectedLayers.Contains(ent.Layer ?? "0"))
            .ToList();

        double scaleFactor = 1.0;
        if (RbMilimetre.IsChecked == true) scaleFactor = 0.001;
        else if (RbSantimetre.IsChecked == true) scaleFactor = 0.01;
        else if (RbAutoScale.IsChecked == true)
        {
            double avgLen = filtered.OfType<LineEntity>().Take(100)
                .Select(l => l.GetLength()).Where(len => len > 0).DefaultIfEmpty(1).Average();
            if (avgLen > 1000) scaleFactor = 0.001;
            else if (avgLen > 50) scaleFactor = 0.01;
        }

        if (Math.Abs(scaleFactor - 1.0) > 1e-9)
        {
            foreach (var ent in filtered)
            {
                var scaleMatrix = Afney.Cad.Geometry.Primitives.Matrix4x4.CreateScale(scaleFactor);
                ent.Transform(scaleMatrix);
            }
        }

        ImportedEntities = filtered;
        TxtStatus.Text = $"{filtered.Count:N0} nesne import edildi.";
        DialogResult = true;
    }
}
