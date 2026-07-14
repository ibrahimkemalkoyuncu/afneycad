using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Presentation.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class PdfExportDialog
{
    private readonly CadDatabase _database;
    private readonly Services.UserSettingsService? _userSettings;

    public PdfExportDialog(CadDatabase database, Services.UserSettingsService? userSettings = null)
    {
        InitializeComponent();
        _database = database;
        _userSettings = userSettings;
        TxtLogoPath.Text = _userSettings?.Settings.CompanyLogoPath ?? "";
    }

    private void SelectLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Şirket Logosu Seç",
            Filter = "Resim Dosyaları (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog() == true)
        {
            TxtLogoPath.Text = dlg.FileName;
            if (_userSettings != null)
            {
                _userSettings.Settings.CompanyLogoPath = dlg.FileName;
                _userSettings.Save();
            }
        }
    }

    private void ClearLogo_Click(object sender, RoutedEventArgs e)
    {
        TxtLogoPath.Text = "";
        if (_userSettings != null)
        {
            _userSettings.Settings.CompanyLogoPath = "";
            _userSettings.Save();
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string name = TxtProjectName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "AfneyCAD Projesi";

            PdfExportService.TitleBlockInfo? tb = null;
            if (ChkTitleBlock.IsChecked == true)
            {
                tb = new PdfExportService.TitleBlockInfo
                {
                    ProjeAdi         = name,
                    ProjeNo          = TxtProjeNo.Text.Trim(),
                    Adres            = TxtAdres.Text.Trim(),
                    FirmaAdi         = TxtFirma.Text.Trim(),
                    MuhendisAdi      = TxtMuhendis.Text.Trim(),
                    MuhendisUnvan    = TxtUnvan.Text.Trim(),
                    OnayCizdiren     = TxtCizdiren.Text.Trim(),
                    OnayKontrolEden  = TxtKontrol.Text.Trim(),
                    Tarih            = DateTime.Now.ToString("dd.MM.yyyy"),
                    LogoPath         = string.IsNullOrWhiteSpace(TxtLogoPath.Text) ? null : TxtLogoPath.Text.Trim()
                };
            }

            var svc  = new PdfExportService(_database);
            string path = svc.ExportReport(name, tb);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });

            StatusText.Text = "✅ PDF oluşturuldu ve açıldı.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
