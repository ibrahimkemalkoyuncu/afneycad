using System;
using System.Linq;
using System.Windows;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Presentation.Dialogs
{
    /*
        NE: Basınç Kaybı Rapor Penceresi (PressureDropReportWindow)
        NEDEN: Hesaplanan hidrolik verileri kullanıcıya profesyonel bir tablo halinde sunmak için.
    */
    public partial class PressureDropReportWindow : Window
    {
        private CriticalPathReport _currentReport;

        public PressureDropReportWindow(CriticalPathReport report)
        {
            InitializeComponent();
            _currentReport = report;
            LoadReport(report);
        }

        private void LoadReport(CriticalPathReport report)
        {
            if (report == null) return;

            // Header Info (FindName used to bypass lint issues with generated fields)
            var systemTypeText = this.FindName("SystemTypeText") as System.Windows.Controls.TextBlock;
            var fixtureText = this.FindName("FixtureText") as System.Windows.Controls.TextBlock;
            var maxVelocityText = this.FindName("MaxVelocityText") as System.Windows.Controls.TextBlock;
            var totalLengthText = this.FindName("TotalLengthText") as System.Windows.Controls.TextBlock;
            var segmentsGrid = this.FindName("SegmentsGrid") as System.Windows.Controls.DataGrid;
            
            var linearLossText = this.FindName("LinearLossText") as System.Windows.Controls.TextBlock;
            var staticHeadText = this.FindName("StaticHeadText") as System.Windows.Controls.TextBlock;
            var residualPressureText = this.FindName("ResidualPressureText") as System.Windows.Controls.TextBlock;
            var totalPressureText = this.FindName("TotalPressureText") as System.Windows.Controls.TextBlock;

            if (systemTypeText != null) systemTypeText.Text = report.SystemType;
            if (fixtureText != null) fixtureText.Text = report.DisadvantagedFixture;
            if (maxVelocityText != null) maxVelocityText.Text = $"{report.MaxVelocity:F2} m/s";

            double totalLen = report.Segments.Sum(s => s.Length);
            if (totalLengthText != null) totalLengthText.Text = $"{totalLen:F1} m";

            // Grid Data
            if (segmentsGrid != null) segmentsGrid.ItemsSource = report.Segments;

            // Calculation Matrix
            if (linearLossText != null) linearLossText.Text = $"{report.TotalLinearLoss:F2} mSS";
            if (staticHeadText != null) staticHeadText.Text = $"{report.StaticHead:F2} m";
            if (residualPressureText != null) residualPressureText.Text = $"{report.RequiredResidualPressure:F2} mSS";
            if (totalPressureText != null) totalPressureText.Text = $"{report.TotalPressureRequired:F2} mSS ({(report.TotalPressureRequired / 10.0):F2} bar)";
        }

        private void PumpSuggest_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReport == null) return;

            // 1. Gereken Toplam Debi (Kritik hattın başlangıcındaki debi - Genelde en yüksek olan)
            double totalFlow = _currentReport.Segments.Max(s => s.FlowRate);
            double totalHead = _currentReport.TotalPressureRequired;

            // 2. Pompa Seçim Servisini Çağır
            var pumpService = new Afney.Cad.Mechanical.Services.PumpSelectionService();
            var recommendations = pumpService.RecommendPumps(totalFlow, totalHead);

            if (recommendations.Any())
            {
                string message = $"Hesaplanan Gereksinim: Q={totalFlow:F2} m³/h, Hm={totalHead:F2} mSS\n\n";
                message += "ÖNERİLEN POMPA MODELLERİ:\n";
                foreach (var p in recommendations)
                {
                    message += $"• {p.Brand} {p.ModelName} ({p.PowerKW:F2} kW, {p.Connection}, Verim: %{p.Efficiency * 100:F0})\n";
                }
                MessageBox.Show(message, "Profesyonel Pompa Seçimi (Mühendislik Modu)");
            }
            else
            {
                MessageBox.Show("Mevcut katalogda bu debi ve basıncı karşılayan pompa bulunamadı.", "Uyarı");
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReport == null) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'><title>Kritik Hat Raporu</title>");
                sb.AppendLine("<style>body{font-family:sans-serif;margin:20px;} table{width:100%;border-collapse:collapse;margin-top:20px;} th,td{border:1px solid #ddd;padding:8px;text-align:center;} th{background-color:#f2f2f2;} h2{color:#d35400;}</style>");
                sb.AppendLine("</head><body>");
                
                sb.AppendLine($"<h2>Kritik Hat Basınç Kaybı Raporu</h2>");
                sb.AppendLine($"<p>Sistem Tipi: <strong>{_currentReport.SystemType}</strong> | En Uç Nokta: <strong>{_currentReport.DisadvantagedFixture}</strong></p>");
                
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Boru ID</th><th>Çap (DN)</th><th>Boy (m)</th><th>Debi (m3/h)</th><th>Hız (m/s)</th><th>Kayıp (mSS)</th><th>Kümülatif (mSS)</th></tr>");
                
                foreach(var s in _currentReport.Segments)
                {
                    sb.AppendLine($"<tr><td>{s.PipeId}</td><td>{s.Diameter:F0}</td><td>{s.Length:F2}</td><td>{s.FlowRate:F2}</td><td>{s.Velocity:F2}</td><td>{s.PressureDrop:F3}</td><td>{s.CumulativeLoss:F3}</td></tr>");
                }
                sb.AppendLine("</table>");

                sb.AppendLine($"<h3>Analiz Sonucu</h3>");
                sb.AppendLine($"<p>Toplam Sürtünme: <strong>{_currentReport.TotalLinearLoss:F2} mSS</strong></p>");
                sb.AppendLine($"<p>Statik Yükseklik: <strong>{_currentReport.StaticHead:F2} m</strong></p>");
                sb.AppendLine($"<p style='color:red; font-size:18px;'>Gereken Pompa Basıncı: <strong>{_currentReport.TotalPressureRequired:F2} mSS</strong></p>");

                sb.AppendLine("</body></html>");

                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KritikHatRaporu_" + Guid.NewGuid() + ".html");
                System.IO.File.WriteAllText(tempPath, sb.ToString(), System.Text.Encoding.UTF8);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Dışa aktarma sırasında hata oluştu: {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
