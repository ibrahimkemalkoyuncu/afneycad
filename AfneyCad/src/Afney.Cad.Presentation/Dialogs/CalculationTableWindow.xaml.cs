using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class CalculationTableWindow : Window
    {
        private readonly CadDatabase _database;
        private readonly PressureDropService _pressureService;
        private CalculationTable? _currentTable;

        public CalculationTableWindow(CadDatabase database, PressureDropService pressureService)
        {
            InitializeComponent();
            _database = database;
            _pressureService = pressureService;
            LoadTable();
        }

        private void LoadTable()
        {
            try
            {
                var tableService = new CalculationTableService(_database, _pressureService);
                _currentTable = tableService.GenerateTable("AfneyCAD Projesi");
                CalcGrid.ItemsSource = _currentTable.Rows;
                SummaryText.Text = $"Toplam: {_currentTable.TotalPipeCount} boru | {_currentTable.TotalLength:F1} m | Max Hız: {_currentTable.MaxVelocity:F2} m/s | Toplam ΔP: {_currentTable.TotalPressureDrop:F3} mSS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tablo yükleme hatası: {ex.Message}");
            }
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null) return;
            try
            {
                var tableService = new CalculationTableService(_database, _pressureService);
                string html = tableService.ExportToHtml(_currentTable);
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"HesapTablosu_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                System.IO.File.WriteAllText(path, html, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"HTML export hatası: {ex.Message}"); }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null) return;
            try
            {
                var tableService = new CalculationTableService(_database, _pressureService);
                string csv = tableService.ExportToCsv(_currentTable);
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"HesapTablosu_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                System.IO.File.WriteAllText(path, csv, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"CSV export hatası: {ex.Message}"); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void CalcGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == System.Windows.Controls.DataGridEditAction.Commit)
            {
                if (e.Column.Header.ToString() == "DN")
                {
                    if (e.Row.Item is CalculationRow row)
                    {
                        var textBox = e.EditingElement as System.Windows.Controls.TextBox;
                        if (textBox != null && double.TryParse(textBox.Text, out double newDiameter))
                        {
                            // Veritabanındaki boruyu bul
                            var allPipes = _database.GetAllEntities();
                            foreach (var entity in allPipes)
                            {
                                if (entity is Afney.Cad.Mechanical.Entities.PipeEntity pipe && pipe.Id.ToString().StartsWith(row.PipeId))
                                {
                                    // Eğer çap gerçekten değiştiyse
                                    if (Math.Abs(pipe.InnerDiameter - newDiameter) > 0.001)
                                    {
                                        pipe.InnerDiameter = newDiameter;
                                        pipe.IsSizeLocked = true; // Mühendis çapı kilitledi
                                        pipe.IsCalculationUpToDate = false; // Sistem dengesi bozuldu, invalid işaretle
                                        
                                        // TODO: İdeal dünyada _mechanicalKernel referansı olsaydı
                                        // "InvalidateSystemCalculations" veya SyncLabel yapabilirdik.
                                        // Viewport'un tekrar çizilmesi MainWindow'da halledilecek.
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
