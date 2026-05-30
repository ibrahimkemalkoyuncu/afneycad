using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Presentation.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class PdfExportDialog
{
    private readonly CadDatabase _database;

    public PdfExportDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
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
                    Tarih            = DateTime.Now.ToString("dd.MM.yyyy")
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
