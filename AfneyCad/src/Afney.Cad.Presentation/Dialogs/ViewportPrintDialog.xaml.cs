using System.Windows;
using Afney.Cad.Presentation.Views;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ViewportPrintDialog : Window
{
    private readonly CadViewport _viewport;

    public ViewportPrintDialog(CadViewport viewport)
    {
        InitializeComponent();
        _viewport = viewport;
    }

    private string GetSelectedScale()
    {
        if (RbFit.IsChecked == true) return "FIT";
        if (Rb100.IsChecked == true) return "1:100";
        if (Rb50.IsChecked == true) return "1:50";
        if (Rb200.IsChecked == true) return "1:200";
        return TxtCustomScale.Text;
    }

    private string GetPaperSize()
    {
        if (RbA4.IsChecked == true) return "A4";
        if (RbA2.IsChecked == true) return "A2";
        if (RbA1.IsChecked == true) return "A1";
        return "A3";
    }

    private void OnPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new Services.PrintViewportService();
            var options = new Services.PrintViewportService.PrintOptions
            {
                ProjectName   = $"{TxtFirma.Text} — {TxtProje.Text}",
                DrawingTitle  = TxtCizim.Text,
                DrawingNumber = TxtPaftaNo.Text,
                Scale       = GetSelectedScale(),
                Date        = DateTime.Now.ToString("dd.MM.yyyy")
            };

            if (svc.PrintViewport(_viewport, options))
            {
                MessageBox.Show("Yazdırma işlemi başarıyla tamamlandı.", "Yazdır",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yazdırma hatası: {ex.Message}", "Hata",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "PNG Olarak Kaydet",
            Filter = "PNG Dosyası (*.png)|*.png",
            DefaultExt = ".png",
            FileName = $"{TxtProje.Text}_{TxtPaftaNo.Text}"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var svc = new Services.PrintViewportService();
                var options = new Services.PrintViewportService.PrintOptions
                {
                    ProjectName   = $"{TxtFirma.Text} — {TxtProje.Text}",
                    DrawingTitle  = TxtCizim.Text,
                    DrawingNumber = TxtPaftaNo.Text,
                    Scale       = GetSelectedScale(),
                    Date        = DateTime.Now.ToString("dd.MM.yyyy")
                };
                svc.ExportToPng(_viewport, dlg.FileName, options);
                MessageBox.Show($"PNG kaydedildi:\n{dlg.FileName}", "Başarılı",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PNG kayıt hatası: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
