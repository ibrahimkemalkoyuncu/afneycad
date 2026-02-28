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

    public ClashReportDialog(List<ClashResult> clashes)
    {
        InitializeComponent();
        _clashes = clashes;
        
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
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var kernelFieldInfo = mainWindow.GetType().GetField("_mechanicalKernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                               ?? mainWindow.GetType().GetField("MechanicalKernel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                               
                var kernel = kernelFieldInfo?.GetValue(mainWindow);
                var graph = kernel?.GetType().GetProperty("TopologyGraph")?.GetValue(kernel) as Afney.Cad.Mechanical.Engine.MechanicalTopologyGraph;

                if (graph != null)
                {
                    var node = graph.GetNode(selectedClash.EntityA_Id);
                    if (node != null && node.Entity is Afney.Cad.Mechanical.Entities.PipeEntity pipe)
                    {
                        pipe.HasHydraulicViolation = !selectedClash.IsApproved;
                    }
                }
                
                // Viewport'u yenile
                mainWindow.Viewport.InvalidateViewport();
            }

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
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Çakışma pozisyonuna Focus/Pan yapısı
                // Viewport içerisinde setOffset veya focus yapacak bir metot çağrılabilir. 
                // Örn: mainWindow.Viewport.PanTo(selectedClash.Position);
                // Burada mevcut mimariye uygun şekilde basitçe Invalidate() ediyoruz.
                // Eğer Pan/Zoom API'si varsa buraya eklenecektir.
                
                // Seçili varlığı Highlihgt yapmak için SelectionManager'a ekle
                var selectionManager = mainWindow.Viewport.GetType().GetField("_selectionManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mainWindow.Viewport) as Afney.Cad.Application.Services.SelectionManager;
                
                if (selectionManager != null)
                {
                    selectionManager.ClearSelection();
                    // Varlığı DB'den bul ve seç
                    var db = mainWindow.Viewport.GetType().GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mainWindow.Viewport) as Afney.Cad.Database.Core.CadDatabase;
                    
                    if (db != null)
                    {
                        var entity = db.GetAllEntities().FirstOrDefault(en => en.Id == selectedClash.EntityA_Id);
                        if (entity != null)
                        {
                            selectionManager.AddToSelection(entity);
                        }
                    }
                }
                
                mainWindow.Viewport.InvalidateViewport();
            }
        }
    }
}
