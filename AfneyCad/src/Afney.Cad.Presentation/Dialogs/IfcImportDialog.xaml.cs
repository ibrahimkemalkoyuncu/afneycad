using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Afney.Cad.Database.Core;
using Afney.Cad.Infrastructure.Import;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class IfcImportDialog : Window
    {
        private readonly CadDatabase _database;
        private string? _filePath;

        public IfcImportDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "IFC Dosyası Seç",
                Filter = "IFC Dosyaları (*.ifc)|*.ifc|Tüm Dosyalar (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _filePath = dlg.FileName;
                TxtFilePath.Text = _filePath;
                TxtFilePath.Foreground = System.Windows.Media.Brushes.White;
                BtnImport.IsEnabled = false;
                PreviewLog.Text = "Önizleme için 'Önizle' butonuna basın.";
                PreviewLog.Foreground = System.Windows.Media.Brushes.Gray;
                ImportProgress.Value = 0;
            }
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                PreviewLog.Text = "Lütfen geçerli bir IFC dosyası seçin.";
                return;
            }

            try
            {
                BtnImport.IsEnabled = false;
                BtnBrowse.IsEnabled = false;
                BtnPreview.IsEnabled = false;
                PreviewLog.Text = "Analiz ediliyor... Lütfen bekleyin.";
                PreviewLog.Foreground = System.Windows.Media.Brushes.LightCyan;

                var options = BuildOptions(previewOnly: true);
                var svc     = new IfcImportService(_database);

                // NE/NEDEN — GERÇEK HATA (Session #75 denetiminde bulundu): Preview_Click,
                // Import_Click'in aksine ağır AnalyzeFile çağrısını UI thread'inde senkron
                // çalıştırıyordu — büyük/karmaşık bir IFC dosyasında önizleme sırasında
                // arayüz donuyordu. Import_Click'teki aynı Task.Run deseni uygulandı.
                var result  = await System.Threading.Tasks.Task.Run(() => svc.AnalyzeFile(_filePath, options));

                var sb = new StringBuilder();
                sb.AppendLine($"📁 Dosya: {Path.GetFileName(_filePath)}");
                sb.AppendLine($"📏 Boyut: {new FileInfo(_filePath).Length / 1024:F0} KB");
                sb.AppendLine();
                sb.AppendLine("══ BULUNAN ELEMANLAR ══");
                sb.AppendLine($"  Duvar           : {result.WallCount,5} adet");
                sb.AppendLine($"  Döşeme          : {result.SlabCount,5} adet");
                sb.AppendLine($"  Pencere         : {result.WindowCount,5} adet");
                sb.AppendLine($"  Kapı            : {result.DoorCount,5} adet");
                sb.AppendLine($"  Mekan           : {result.SpaceCount,5} adet");
                sb.AppendLine($"  MEP (Boru/Kanal): {result.MepCount,5} adet");
                sb.AppendLine($"  ─────────────────────");
                sb.AppendLine($"  Toplam Entity   : {result.TotalCount,5} adet");
                sb.AppendLine();
                sb.AppendLine("══ OLUŞTURULACAK KATMANLAR ══");
                foreach (var layer in result.Layers)
                    sb.AppendLine($"  ■ {layer}");
                sb.AppendLine();
                if (result.Warnings.Count > 0)
                {
                    sb.AppendLine("⚠ UYARILAR:");
                    foreach (var w in result.Warnings)
                        sb.AppendLine($"  {w}");
                }
                else
                {
                    sb.AppendLine("✓ Herhangi bir uyarı yok. İçeri aktarmaya hazır.");
                }

                PreviewLog.Text = sb.ToString();
                PreviewLog.Foreground = System.Windows.Media.Brushes.LightCyan;
                BtnImport.IsEnabled = result.TotalCount > 0;
                ImportProgress.Value = 30;
            }
            catch (Exception ex)
            {
                PreviewLog.Text = $"Önizleme hatası: {ex.Message}";
                PreviewLog.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            finally
            {
                BtnBrowse.IsEnabled = true;
                BtnPreview.IsEnabled = true;
            }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath)) return;

            try
            {
                BtnImport.IsEnabled = false;
                BtnBrowse.IsEnabled = false;
                BtnPreview.IsEnabled = false;
                ImportProgress.Value = 50;
                PreviewLog.Text = "İçeri aktarılıyor... Lütfen bekleyin.";
                PreviewLog.Foreground = System.Windows.Media.Brushes.LightCyan;

                var options = BuildOptions(previewOnly: false);
                var svc     = new IfcImportService(_database);

                // UI donmasını önlemek için ağır işlemi arka plana at (DWG import ile aynı desen —
                // bkz. MainWindow.FileOps.cs LoadDwgInternal)
                var result = await System.Threading.Tasks.Task.Run(() => svc.Import(_filePath, options));

                ImportProgress.Value = 100;

                var sb = new StringBuilder();
                sb.AppendLine("✅ AKTARIM TAMAMLANDI");
                sb.AppendLine();
                sb.AppendLine($"  Eklenen entity sayısı : {result.ImportedCount}");
                sb.AppendLine($"  Atlanan eleman        : {result.SkippedCount}");
                if (result.Errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ Hatalar:");
                    foreach (var err in result.Errors)
                        sb.AppendLine($"  {err}");
                }

                PreviewLog.Text = sb.ToString();
                PreviewLog.Foreground = System.Windows.Media.Brushes.LightGreen;

                MessageBox.Show(
                    $"IFC aktarımı tamamlandı.\n{result.ImportedCount} eleman içeri alındı.",
                    "Aktarım Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                PreviewLog.Text = $"Aktarım hatası: {ex.Message}";
                PreviewLog.Foreground = System.Windows.Media.Brushes.OrangeRed;
                ImportProgress.Value = 0;
            }
            finally
            {
                BtnImport.IsEnabled = true;
                BtnBrowse.IsEnabled = true;
                BtnPreview.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private IfcImportOptions BuildOptions(bool previewOnly)
        {
            double scale = ScaleCombo.SelectedIndex switch
            {
                0 => 1000.0, // metre → mm
                1 => 1.0,    // mm → mm
                2 => 10.0,   // cm → mm
                _ => 1000.0
            };
            return new IfcImportOptions
            {
                ImportWalls   = ChkWalls.IsChecked   == true,
                ImportSlabs   = ChkSlabs.IsChecked   == true,
                ImportWindows = ChkWindows.IsChecked == true,
                ImportDoors   = ChkDoors.IsChecked   == true,
                ImportSpaces  = ChkSpaces.IsChecked  == true,
                ImportMep     = ChkMep.IsChecked     == true,
                ScaleFactor   = scale,
                PreviewOnly   = previewOnly
            };
        }
    }
}
