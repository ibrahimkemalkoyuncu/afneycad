using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class SpecificationExportDialog : Window
    {
        private readonly CadDatabase _database;

        public SpecificationExportDialog(CadDatabase database) { InitializeComponent(); _database = database; }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new SpecificationExportService(_database);
                var doc = service.GenerateSpecification(ProjectNameInput.Text);
                string html = service.ExportToHtml(doc);
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Sartname_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                System.IO.File.WriteAllText(path, html, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                StatusText.Text = $"✅ Teknik şartname oluşturuldu:\n{path}\n\nBölüm sayısı: {doc.Sections.Count}";
            }
            catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
