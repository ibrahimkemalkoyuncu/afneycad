using System;
using System.Collections.Generic;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class RiserDiagramExportDialog : Window
    {
        private readonly CadDatabase _database;

        public RiserDiagramExportDialog(CadDatabase database) { InitializeComponent(); _database = database; }

        private void ExportSvg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int floorCount = int.Parse(FloorCountInput.Text);
                double floorHeight = double.Parse(FloorHeightInput.Text);
                double branchDN = double.Parse(BranchDNInput.Text);

                var floors = new List<RiserDiagramExportService.RiserFloor>();
                for (int i = 0; i < floorCount; i++)
                {
                    string name = i == 0 ? "Zemin Kat" : $"{i}. Kat";
                    floors.Add(new RiserDiagramExportService.RiserFloor
                    {
                        Name = name,
                        Elevation = i * floorHeight,
                        BranchDN = branchDN,
                        FixtureCount = 3
                    });
                }

                var service = new RiserDiagramExportService(_database);
                var diagram = service.GenerateRiserDiagram(floors);
                string svg = service.ExportToSvg(diagram);
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KolonSemasi_{DateTime.Now:yyyyMMdd_HHmmss}.svg");
                System.IO.File.WriteAllText(path, svg, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                StatusText.Text = $"✅ SVG kolon şeması oluşturuldu:\n{path}\n\n" +
                                  $"Kat: {floorCount} | Çizgi: {diagram.Lines.Count} | Etiket: {diagram.Labels.Count} | Sembol: {diagram.Symbols.Count}";
            }
            catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
