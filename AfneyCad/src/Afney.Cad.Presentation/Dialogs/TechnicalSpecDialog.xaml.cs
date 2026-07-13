using System.IO;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class TechnicalSpecDialog : Window
{
    private readonly CadDatabase _database;

    public TechnicalSpecDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
    }

    private void OnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var cfg = new TechnicalSpecConfig
        {
            CompanyName  = TxtCompany.Text,
            ProjectName  = TxtProject.Text,
            EngineerName = TxtEngineer.Text,
            Standard     = (CbStandard.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "TS 11154",
            IncludeBOM   = ChkBOM.IsChecked == true,
            IncludeMontaj = ChkMontaj.IsChecked == true,
            IncludeCost  = ChkCost.IsChecked == true
        };

        var svc = new TechnicalSpecService(_database);
        string html = svc.GenerateHtml(cfg);

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Teknik Şartname Kaydet",
            Filter = "HTML Dosyası (*.html)|*.html",
            DefaultExt = ".html",
            FileName = $"TeknikSartname_{cfg.ProjectName.Replace(" ", "_")}"
        };

        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
            MessageBox.Show($"Teknik şartname oluşturuldu:\n{dlg.FileName}", "Başarılı",
                MessageBoxButton.OK, MessageBoxImage.Information);

            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
            catch (Exception exOpen) { Serilog.Log.Warning("[Rapor] Dosya kaydedildi ama açılamadı: {File} — {Error}", dlg.FileName, exOpen.Message); }

            DialogResult = true;
        }
    }
}
