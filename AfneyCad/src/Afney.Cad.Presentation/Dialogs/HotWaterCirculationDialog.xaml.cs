using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class HotWaterCirculationDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly HotWaterCirculationService _svc;
        private readonly ObservableCollection<SegmentInputVm> _inputs = [];
        private List<ResultRowVm> _results = [];

        public HotWaterCirculationDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            _svc = new HotWaterCirculationService(database);

            SegmentGrid.ItemsSource = _inputs;
            AddDefaultSegments();
        }

        private void AddDefaultSegments()
        {
            _inputs.Add(new SegmentInputVm { Description = "Kolon 1 (zemin-1.kat)", LengthMStr = "4.0",  DiameterStr = "25", WallStr = "3", InsulationStr = "25", Material = "Steel" });
            _inputs.Add(new SegmentInputVm { Description = "Dağıtım Hattı A",       LengthMStr = "12.0", DiameterStr = "20", WallStr = "2.5",InsulationStr = "25", Material = "Steel" });
            _inputs.Add(new SegmentInputVm { Description = "Son Kullanıcı Hattı",   LengthMStr = "8.0",  DiameterStr = "15", WallStr = "2",  InsulationStr = "20", Material = "PPR" });
        }

        private void AddSegment_Click(object sender, RoutedEventArgs e)
        {
            _inputs.Add(new SegmentInputVm
            {
                Description  = $"Hat {_inputs.Count + 1}",
                LengthMStr   = "10.0",
                DiameterStr  = "20",
                WallStr      = "2.5",
                InsulationStr = "25",
                Material     = "Steel"
            });
        }

        private void DeleteSegment_Click(object sender, RoutedEventArgs e)
        {
            if (SegmentGrid.SelectedItem is SegmentInputVm row)
                _inputs.Remove(row);
        }

        private void LoadFromDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _svc.DesignFromDatabase(
                    GetDouble(TxtSupply.Text, 60),
                    GetDouble(TxtReturn.Text, 55),
                    GetDouble(TxtAmbient.Text, 20));

                if (result.Segments.Count == 0)
                {
                    MessageBox.Show("Projede sıcak su borusu bulunamadı. Önce tesisat çizin.", "Uyarı");
                    return;
                }

                _inputs.Clear();
                foreach (var seg in result.Segments)
                {
                    _inputs.Add(new SegmentInputVm
                    {
                        Description   = seg.Description,
                        LengthMStr    = seg.LengthM.ToString("F1"),
                        DiameterStr   = seg.PipeDiameterMm.ToString("F0"),
                        WallStr       = "3",
                        InsulationStr = seg.InsulationMm.ToString("F0"),
                        Material      = "Steel"
                    });
                }

                ShowResults(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yükleme hatası: {ex.Message}");
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var segments = BuildSegments();
                if (segments.Count == 0)
                {
                    MessageBox.Show("En az bir hat segmenti girin.", "Uyarı");
                    return;
                }

                var result = _svc.DesignCirculationLoop(
                    segments,
                    GetDouble(TxtSupply.Text, 60),
                    GetDouble(TxtReturn.Text, 55),
                    GetDouble(TxtAmbient.Text, 20));

                ShowResults(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesap hatası: {ex.Message}");
            }
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0)
            {
                MessageBox.Show("Önce hesap yapın.", "Uyarı");
                return;
            }

            try
            {
                var segments = BuildSegments();
                var result = _svc.DesignCirculationLoop(segments,
                    GetDouble(TxtSupply.Text, 60),
                    GetDouble(TxtReturn.Text, 55),
                    GetDouble(TxtAmbient.Text, 20));

                string html = _svc.ExportToHtml(result);

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "HTML Rapor Kaydet",
                    Filter     = "HTML (*.html)|*.html",
                    FileName   = $"Resirkülasyon_{DateTime.Now:yyyyMMdd_HHmm}.html",
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

        // ── YARDIMCI ────────────────────────────────────────────────────────────

        private List<CirculationSegment> BuildSegments()
        {
            var list = new List<CirculationSegment>();
            foreach (var vm in _inputs)
            {
                if (!double.TryParse(vm.LengthMStr,    out double len)    || len    <= 0) continue;
                if (!double.TryParse(vm.DiameterStr,   out double dia)    || dia    <= 0) continue;
                if (!double.TryParse(vm.WallStr,       out double wall))   wall = 3.0;
                if (!double.TryParse(vm.InsulationStr, out double insul))  insul = 25.0;

                list.Add(new CirculationSegment
                {
                    Id              = Guid.NewGuid().ToString(),
                    Description     = vm.Description,
                    LengthM         = len,
                    PipeDiameterMm  = dia,
                    WallThicknessMm = wall,
                    InsulationMm    = insul,
                    Material        = vm.Material
                });
            }
            return list;
        }

        private void ShowResults(CirculationLoopResult result)
        {
            _results = result.Segments.Select(s => new ResultRowVm(s)).ToList();
            ResultGrid.ItemsSource = _results;

            SummaryText.Text =
                $"Toplam Isı Kaybı: {result.TotalHeatLossW:F0} W  |  " +
                $"Toplam Resirkülasyon: {result.TotalRecircFlowLh:F1} lt/h  |  " +
                $"Pompa: Q={result.RecommendedPumpFlow:F1} lt/h  Hm={result.RecommendedPumpHeadMSS:F2} mSS  |  " +
                $"Kritik Hat ΔP: {result.CriticalPathPressurePa:F0} Pa";
        }

        private static double GetDouble(string text, double fallback)
            => double.TryParse(text, out double v) ? v : fallback;
    }

    // ── VIEW MODEL SINIFLARI ────────────────────────────────────────────────────

    public class SegmentInputVm
    {
        public string Description   { get; set; } = "";
        public string LengthMStr    { get; set; } = "10";
        public string DiameterStr   { get; set; } = "20";
        public string WallStr       { get; set; } = "3";
        public string InsulationStr { get; set; } = "25";
        public string Material      { get; set; } = "Steel";
    }

    public class ResultRowVm
    {
        private readonly CirculationSegmentResult _s;
        public ResultRowVm(CirculationSegmentResult s) => _s = s;

        public string Description      => _s.Description;
        public string HeatLossStr      => $"{_s.HeatLossW:F0}";
        public string FlowStr          => $"{_s.RecircFlowLh:F2}";
        public string ReturnDNStr      => $"DN {_s.ReturnPipeDN:F0}";
        public string PressureStr      => $"{_s.PressureDropPa:F0}";
        public string VelocityStr      => $"{_s.FlowVelocityMs:F3}";
        public string ValveStr         => $"{_s.ValveSettingPct:F0}";
        public bool   IsVelocityWarning => !_s.IsVelocityOK;
    }
}
