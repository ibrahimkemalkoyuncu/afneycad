using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class ReportExportDialog : Window
    {
        private readonly CadDatabase _database;

        public ReportExportDialog(CadDatabase database) { InitializeComponent(); _database = database; }

        private void ExportHtml_Click(object sender, RoutedEventArgs e) => Export(ReportExportService.ExportFormat.HTML);
        private void ExportCsv_Click(object sender, RoutedEventArgs e) => Export(ReportExportService.ExportFormat.CSV);
        private void ExportRtf_Click(object sender, RoutedEventArgs e) => Export(ReportExportService.ExportFormat.RTF);

        private void Export(ReportExportService.ExportFormat format)
        {
            try
            {
                var service = new ReportExportService(_database);
                var report = service.GenerateSystemReport(ProjectNameInput.Text);
                string ext = format switch { ReportExportService.ExportFormat.HTML => "html", ReportExportService.ExportFormat.CSV => "csv", _ => "rtf" };
                string content = format switch { ReportExportService.ExportFormat.HTML => service.ExportToHtml(report), ReportExportService.ExportFormat.CSV => service.ExportToCsv(report), _ => service.ExportToRtf(report) };
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Rapor_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
                System.IO.File.WriteAllText(path, content, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                StatusText.Text = $"✅ {format} rapor başarıyla oluşturuldu:\n{path}\n\nBoru sayısı: {report.Sections[0].Rows.Count}";
            }
            catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
