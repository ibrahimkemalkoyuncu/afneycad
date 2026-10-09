using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ClashReportDialog : Window
{
    private List<ClashResult> _clashes;
    private readonly Func<double, List<ClashResult>>? _rescan;
    private readonly Afney.Cad.Mechanical.MechanicalKernel? _kernel;
    private readonly Afney.Cad.Database.Core.CadDatabase? _database;
    private readonly Afney.Cad.Application.Services.SelectionManager? _selectionManager;

    /*
       NE: TB-08 — Reflection Yerine Doğrudan Enjeksiyon
       NEDEN: Önceden `ApproveClash_Click`/`ClashesDataGrid_SelectionChanged`
              `Application.Current.MainWindow as MainWindow` üzerinden reflection ile
              `_mechanicalKernel`/`_selectionManager`/`_database` private alanlarına erişiyordu
              — bu alanlardan biri yeniden adlandırılırsa/taşınırsa derleme hatası vermeden
              SESSİZCE bozulurdu (`GetField` null döner, kod çalışmaya devam eder). Artık
              çağıran taraf (`MainWindow.OnClashDetectionClick`) bu üç referansı doğrudan,
              tip-güvenli parametre olarak veriyor.
    */
    public ClashReportDialog(List<ClashResult> clashes, Func<double, List<ClashResult>>? rescan = null,
        Afney.Cad.Mechanical.MechanicalKernel? kernel = null,
        Afney.Cad.Database.Core.CadDatabase? database = null,
        Afney.Cad.Application.Services.SelectionManager? selectionManager = null)
    {
        InitializeComponent();
        _clashes = clashes;
        _rescan = rescan;
        _kernel = kernel;
        _database = database;
        _selectionManager = selectionManager;

        LoadData();
    }

    /*
       NE: Tolerans ile Yeniden Tarama (Rescan_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: minimum boru-boru/vana aralığı
              (+25mm) kod içinde sabitti, kullanıcı değiştiremiyordu. `rescan` opsiyonel bir
              delege olarak verildiğinde (bkz. MainWindow.OnClashDetectionClick), kullanıcı
              burada gerçek bir toleransla yeniden tarama yaptırabiliyor. Delege verilmezse
              (örn. testte veya eski çağrı şeklinde) buton sessizce bilgi mesajı gösterir.
    */
    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        if (_rescan is null)
        {
            MessageBox.Show("Bu rapor yeniden taramayı desteklemiyor.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!double.TryParse(ToleranceInput.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double toleranceMm) || toleranceMm < 0)
        {
            MessageBox.Show("Geçerli bir tolerans (mm) girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _clashes = _rescan(toleranceMm);
        LoadData();
    }

    private void LoadData()
    {
        if (_clashes == null || _clashes.Count == 0)
        {
            SummaryTextBlock.Text = "Hata/Çakışma Bulunamadı!";
            SummaryTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 205, 50)); // LimeGreen
        }
        else
        {
            SummaryTextBlock.Text = $"Uyarı: {_clashes.Count} adet çakışma (Clash) tespit edildi!";
            SummaryTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 69, 0)); // OrangeRed
            
            ClashesDataGrid.ItemsSource = _clashes;
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_clashes == null || !_clashes.Any())
        {
            MessageBox.Show("Dışa aktarılacak çakışma kaydı bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"ClashReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Type,Severity,EntityA_Id,EntityB_Id,ObstacleId,PositionX,PositionY,PositionZ,Message");
                
                foreach (var clash in _clashes)
                {
                    sb.AppendLine($"{clash.Type},{clash.Severity},{clash.EntityA_Id},{clash.EntityB_Id},{clash.ObstacleId},{clash.Position.X},{clash.Position.Y},{clash.Position.Z},\"{clash.Message}\"");
                }
                
                System.IO.File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Rapor başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportBcf_Click(object sender, RoutedEventArgs e)
    {
        if (_clashes == null || !_clashes.Any())
        {
            MessageBox.Show("Dışa aktarılacak çakışma kaydı bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "BCF Dosyası (*.bcf;*.bcfzip)|*.bcf;*.bcfzip",
            FileName = $"ClashReport_{DateTime.Now:yyyyMMdd_HHmmss}.bcf"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                Afney.Cad.Mechanical.Services.BcfExportService.Export(_clashes, saveFileDialog.FileName);
                MessageBox.Show("BCF raporu başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BCF dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApproveClash_Click(object sender, RoutedEventArgs e)
    {
        if (ClashesDataGrid.SelectedItem is ClashResult selectedClash)
        {
            selectedClash.IsApproved = !selectedClash.IsApproved; // Toggle

            // Eğer Onaylandıysa, Kırmızı Vurguyu da kaldırmak için Entity'i bulup flag'i temizleyebiliriz
            var graph = _kernel?.TopologyGraph;
            if (graph != null)
            {
                var node = graph.GetNode(selectedClash.EntityA_Id);
                if (node != null && node.Entity is Afney.Cad.Mechanical.Entities.PipeEntity pipe)
                {
                    pipe.HasHydraulicViolation = !selectedClash.IsApproved;
                }
            }

            // Viewport'u yenile
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            mainWindow?.Viewport.InvalidateViewport();

            // Listeyi UI'da yenile
            ClashesDataGrid.Items.Refresh();
        }
        else
        {
            MessageBox.Show("Lütfen onaylamak veya yoksaymak için listeden bir çakışma seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClashesDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Listeden bir öğe seçildiğinde Viewport'u oraya odakla (Zoom/Pan)
        if (ClashesDataGrid.SelectedItem is ClashResult selectedClash)
        {
            // Seçili varlığı Highlight yapmak için SelectionManager'a ekle
            if (_selectionManager != null)
            {
                _selectionManager.ClearSelection();
                var entity = _database?.GetAllEntities().FirstOrDefault(en => en.Id == selectedClash.EntityA_Id);
                if (entity != null)
                {
                    _selectionManager.AddToSelection(entity);
                }
            }

            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            mainWindow?.Viewport.InvalidateViewport();
        }
    }
}
