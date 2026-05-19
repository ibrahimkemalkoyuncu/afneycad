using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class CalculationTableWindow : Window
    {
        private readonly CadDatabase _database;
        private readonly PressureDropService _pressureService;
        private CalculationTable? _currentTable;
        private WasteWaterCalcTable? _wasteTable;
        private readonly ObservableCollection<ManualCalcRow> _manualRows = new();

        // DN sütunu değiştiğinde MainWindow bu event ile AutoPipeLabeler'ı tetikler
        public event Action<string, double>? PipeDN_Changed;

        public CalculationTableWindow(CadDatabase database, PressureDropService pressureService)
        {
            InitializeComponent();
            _database = database;
            _pressureService = pressureService;
            ManualGrid.ItemsSource = _manualRows;
            LoadCleanWaterTable();
        }

        // ── TAB DEĞİŞİMİ ──────────────────────────────────────────────────────

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedIndex == 1 && _wasteTable == null)
                LoadWasteWaterTable();
        }

        // ── SEKME 1: TEMİZ SU ─────────────────────────────────────────────────

        private void LoadCleanWaterTable()
        {
            try
            {
                var svc = new CalculationTableService(_database, _pressureService);
                _currentTable = svc.GenerateTable("AfneyCAD Projesi");
                CalcGrid.ItemsSource = _currentTable.Rows;
                SummaryText.Text =
                    $"Toplam: {_currentTable.TotalPipeCount} boru | " +
                    $"{_currentTable.TotalLength:F1} m | " +
                    $"Max Hız: {_currentTable.MaxVelocity:F2} m/s | " +
                    $"Toplam ΔP: {_currentTable.TotalPressureDrop:F3} mSS";
                StatusText.Text = $"Temiz su tablosu yüklendi — {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tablo yükleme hatası: {ex.Message}");
            }
        }

        private void CalcGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Column.Header?.ToString() != "DN") return;
            if (e.Row.Item is not CalculationRow row) return;

            var textBox = e.EditingElement as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double newDN)) return;

            foreach (var entity in _database.GetAllEntities())
            {
                if (entity is Afney.Cad.Mechanical.Entities.PipeEntity pipe &&
                    pipe.Id.ToString().StartsWith(row.PipeId))
                {
                    if (Math.Abs(pipe.InnerDiameter - newDN) > 0.001)
                    {
                        pipe.InnerDiameter = newDN;
                        pipe.IsSizeLocked = true;
                        pipe.IsCalculationUpToDate = false;

                        // Viewport etiketi ve izometrik şemayı güncelle
                        PipeDN_Changed?.Invoke(row.PipeId, newDN);
                        StatusText.Text = $"DN güncellendi: #{row.PipeId} → DN {newDN:F0} | {DateTime.Now:HH:mm:ss}";
                    }
                    break;
                }
            }
        }

        // ── SEKME 2: PİS SU ───────────────────────────────────────────────────

        private void LoadWasteWaterTable()
        {
            try
            {
                double k = GetSelectedKFactor();
                var svc = new CalculationTableService(_database, _pressureService);
                _wasteTable = svc.GenerateWasteWaterTable("AfneyCAD Projesi", k);
                WasteCalcGrid.ItemsSource = _wasteTable.Rows;

                int warnings = _wasteTable.Rows.Count(r => r.IsWarning);
                WasteSummaryText.Text =
                    $"Toplam: {_wasteTable.TotalPipeCount} segment | " +
                    $"{_wasteTable.TotalLength:F1} m | " +
                    $"K={k} | " +
                    $"Uyarı: {(warnings > 0 ? $"{warnings} ⚠" : "Yok ✓")}";
                StatusText.Text = $"Pis su tablosu yüklendi — {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pis su tablosu hatası: {ex.Message}");
            }
        }

        private void KFactorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _wasteTable = null; // Zorla yenile
            if (MainTabControl?.SelectedIndex == 1)
                LoadWasteWaterTable();
        }

        private void RefreshWaste_Click(object sender, RoutedEventArgs e)
        {
            _wasteTable = null;
            LoadWasteWaterTable();
        }

        private double GetSelectedKFactor()
        {
            if (KFactorCombo?.SelectedItem is ComboBoxItem item &&
                double.TryParse(item.Tag?.ToString(), out double k))
                return k;
            return 0.5;
        }

        // ── SEKME 3: MANUEL GİRİŞ ─────────────────────────────────────────────

        private void ManualAddRow_Click(object sender, RoutedEventArgs e)
        {
            _manualRows.Add(new ManualCalcRow
            {
                LineNo = _manualRows.Count + 1,
                Description = $"Hat {_manualRows.Count + 1}",
                SystemType = "Pis Su",
                LengthM = 5.0,
                LoadValue = 2.0,
                DiameterDN = 100,
                SlopePct = 1.0
            });
            RecalcManual();
        }

        private void ManualDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (ManualGrid.SelectedItem is ManualCalcRow row)
            {
                _manualRows.Remove(row);
                // Satır numaralarını sıfırla
                for (int i = 0; i < _manualRows.Count; i++)
                    _manualRows[i].LineNo = i + 1;
                RecalcManual();
            }
        }

        private void ManualCalc_Click(object sender, RoutedEventArgs e)
        {
            // Grid'deki düzenleme tamamlanmadan hesap yapılmaması için focus'u kaldır
            ManualGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            RecalcManual();
        }

        private void RecalcManual()
        {
            double totalLength = 0;
            int warnings = 0;
            foreach (var row in _manualRows)
            {
                // Sistem tipine göre pis su veya temiz su hesabı
                bool isWaste = row.SystemType.Contains("Pis") || row.SystemType.Contains("Yağmur");
                double slope = row.SlopePct / 100.0;
                double dn = row.DiameterDN;
                double dM = dn / 1000.0;
                double n = 0.012;
                double R = dM / 4.0;
                double v = (1.0 / n) * Math.Pow(R, 2.0 / 3.0) * Math.Pow(Math.Max(slope, 0.001), 0.5);
                double area = Math.PI * (dM / 2.0) * (dM / 2.0);
                double qFullLps = v * area * 1000.0;

                double flow;
                if (isWaste)
                    flow = 0.5 * Math.Sqrt(Math.Max(row.LoadValue, 0));
                else
                    flow = row.LoadValue * 0.3; // Basit LU → Q

                double filling = qFullLps > 0 ? Math.Min(flow / qFullLps * 100.0, 100.0) : 0;

                row.FlowLps = flow;
                row.VelocityMs = v;
                row.FillingRatioPct = filling;
                row.Note = filling > 70 ? "⚠ Doluluk yüksek" : (v < 0.6 ? "⚠ Hız düşük" : "");

                totalLength += row.LengthM;
                if (filling > 70 || v < 0.6) warnings++;
            }
            ManualSummaryText.Text =
                $"Toplam: {_manualRows.Count} hat | {totalLength:F1} m | Uyarı: {(warnings > 0 ? $"{warnings} ⚠" : "Yok ✓")}";

            // DataGrid'i yenile (INotifyPropertyChanged olmadığı için)
            ManualGrid.Items.Refresh();
        }

        // ── EXPORT ────────────────────────────────────────────────────────────

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            var svc = new CalculationTableService(_database, _pressureService);
            string html;
            string title;

            if (MainTabControl.SelectedIndex == 1)
            {
                if (_wasteTable == null) LoadWasteWaterTable();
                html = svc.ExportWasteWaterToHtml(_wasteTable!);
                title = "PisSuHesapFoyu";
            }
            else
            {
                if (_currentTable == null) LoadCleanWaterTable();
                html = svc.ExportToHtml(_currentTable!);
                title = "HesapTablosu";
            }

            try
            {
                string path = Path.Combine(Path.GetTempPath(), $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(path, html, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = path, UseShellExecute = true });
                StatusText.Text = $"HTML raporu açıldı: {path}";
            }
            catch (Exception ex) { MessageBox.Show($"HTML export hatası: {ex.Message}"); }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null) LoadCleanWaterTable();
            try
            {
                var svc = new CalculationTableService(_database, _pressureService);
                string csv = svc.ExportToCsv(_currentTable!);
                string path = Path.Combine(Path.GetTempPath(), $"HesapTablosu_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(path, csv, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"CSV export hatası: {ex.Message}"); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    // ── MANUEL GİRİŞ VERİ MODELİ ─────────────────────────────────────────────

    public class ManualCalcRow
    {
        public int LineNo { get; set; }
        public string Description { get; set; } = "";
        public string SystemType { get; set; } = "Pis Su";
        public double LengthM { get; set; }
        public double LoadValue { get; set; }   // DU (pis su) veya LU (temiz su)
        public double DiameterDN { get; set; } = 100;
        public double SlopePct { get; set; } = 1.0;
        public double FlowLps { get; set; }
        public double VelocityMs { get; set; }
        public double FillingRatioPct { get; set; }
        public string Note { get; set; } = "";
    }
}
