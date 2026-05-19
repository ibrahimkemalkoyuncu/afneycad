using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class PipeCostDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly PipeCostService _svc = new();
        private readonly ObservableCollection<CostInputVm> _inputs = [];
        private PipeCostService.ProjectCostResult? _lastResult;

        public PipeCostDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            InputGrid.ItemsSource = _inputs;

            // Malzeme ComboBox seçeneklerini doldur
            var matCol = (DataGridComboBoxColumn)InputGrid.Columns[1];
            matCol.ItemsSource = Enum.GetValues<PipeCostService.PipeMaterial>();

            AddDefaultRows();
        }

        private void AddDefaultRows()
        {
            _inputs.Add(new CostInputVm { Description = "Sıcak Su Kolon",    Material = PipeCostService.PipeMaterial.PPR,       DiameterStr = "32",  LengthStr = "50",  System = "HotWater" });
            _inputs.Add(new CostInputVm { Description = "Soğuk Su Kolon",    Material = PipeCostService.PipeMaterial.Steel,     DiameterStr = "40",  LengthStr = "50",  System = "ColdWater" });
            _inputs.Add(new CostInputVm { Description = "Pis Su Kolonu",     Material = PipeCostService.PipeMaterial.CastIron,  DiameterStr = "100", LengthStr = "30",  System = "Drainage" });
        }

        private void LoadFromDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
                if (pipes.Count == 0)
                {
                    MessageBox.Show("Projede boru bulunamadı. Önce tesisat çizin.", "Uyarı");
                    return;
                }

                _inputs.Clear();
                foreach (var pipe in pipes)
                {
                    string sys = pipe.SystemType.ToString();
                    var mat = GuessUiMaterial(sys);
                    _inputs.Add(new CostInputVm
                    {
                        Description = $"{mat} - {sys}",
                        Material    = mat,
                        DiameterStr = (pipe.InnerDiameter * 1000).ToString("F0"),
                        LengthStr   = pipe.Length.ToString("F1"),
                        System      = sys
                    });
                }

                MessageBox.Show($"{pipes.Count} boru yüklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yükleme hatası: {ex.Message}");
            }
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            _inputs.Add(new CostInputVm
            {
                Description = $"Hat {_inputs.Count + 1}",
                Material    = PipeCostService.PipeMaterial.Steel,
                DiameterStr = "25",
                LengthStr   = "10",
                System      = ""
            });
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (InputGrid.SelectedItem is CostInputVm row)
                _inputs.Remove(row);
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pipes = BuildPipes();
                if (!pipes.Any())
                {
                    MessageBox.Show("En az bir satır girin.", "Uyarı");
                    return;
                }

                if (!double.TryParse(TxtContingency.Text, out double contingency))
                    contingency = 10;

                _lastResult = _svc.CalculateFromList(pipes, contingency);
                ShowResults(_lastResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesap hatası: {ex.Message}");
            }
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult is null)
            {
                MessageBox.Show("Önce hesap yapın.", "Uyarı");
                return;
            }

            try
            {
                string html = _svc.ExportToHtml(_lastResult);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "HTML Rapor Kaydet",
                    Filter     = "HTML (*.html)|*.html",
                    FileName   = $"BoreMaliyet_{DateTime.Now:yyyyMMdd_HHmm}.html",
                    DefaultExt = ".html"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Rapor kaydedildi:\n{dlg.FileName}", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor hatası: {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ── YARDIMCI ──────────────────────────────────────────────────────────

        private IEnumerable<(PipeCostService.PipeMaterial, double, double, string, string)> BuildPipes()
        {
            foreach (var vm in _inputs)
            {
                if (!double.TryParse(vm.DiameterStr, out double dia) || dia <= 0) continue;
                if (!double.TryParse(vm.LengthStr,   out double len) || len <= 0) continue;
                yield return (vm.Material, dia, len, vm.Description, vm.System);
            }
        }

        private void ShowResults(PipeCostService.ProjectCostResult result)
        {
            ResultGrid.ItemsSource = result.Items.Select(i => new CostResultVm(i)).ToList();

            SummaryText.Text =
                $"Malzeme: {result.TotalMaterialTl:N0} TL  |  " +
                $"İşçilik: {result.TotalLaborTl:N0} TL  |  " +
                $"Ek Parça: {result.TotalFittingTl:N0} TL  |  " +
                $"Ara Toplam: {result.TotalCostTl:N0} TL  |  " +
                $"Beklenmedik (%{result.ContingencyPct:F0}): {result.ContingencyTl:N0} TL  |  " +
                $"GENEL TOPLAM: {result.GrandTotalTl:N0} TL";

            NotesText.Text = string.Join(" | ", result.Notes);
        }

        private static PipeCostService.PipeMaterial GuessUiMaterial(string systemType)
        {
            string s = systemType.ToLowerInvariant();
            if (s.Contains("drain") || s.Contains("atık") || s.Contains("pis")) return PipeCostService.PipeMaterial.CastIron;
            if (s.Contains("hot")   || s.Contains("sıcak"))                      return PipeCostService.PipeMaterial.PPR;
            if (s.Contains("fire")  || s.Contains("yangın"))                     return PipeCostService.PipeMaterial.Galvanized;
            return PipeCostService.PipeMaterial.Steel;
        }
    }

    public class CostInputVm
    {
        public string Description                { get; set; } = "";
        public PipeCostService.PipeMaterial Material { get; set; } = PipeCostService.PipeMaterial.Steel;
        public string DiameterStr                { get; set; } = "25";
        public string LengthStr                  { get; set; } = "10";
        public string System                     { get; set; } = "";
    }

    public class CostResultVm
    {
        private readonly PipeCostService.PipeCostItem _i;
        public CostResultVm(PipeCostService.PipeCostItem i) => _i = i;

        public string Description  => _i.Description;
        public string LengthStr    => $"{_i.LengthM:F1}";
        public string MaterialStr  => $"{_i.MaterialCostTl:N0}";
        public string LaborStr     => $"{_i.LaborCostTl:N0}";
        public string FittingStr   => $"{_i.FittingCostTl:N0}";
        public string TotalStr     => $"{_i.TotalCostTl:N0}";
    }
}
