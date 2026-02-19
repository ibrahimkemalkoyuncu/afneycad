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
                    message += $"• {p.Brand} {p.ModelName} ({p.Power}, {p.Connection})\n";
                }
                MessageBox.Show(message, "Profesyonel Pompa Seçimi (Mühendislik Modu)");
            }
            else
            {
                MessageBox.Show("Mevcut katalogda bu debi ve basıncı karşılayan pompa bulunamadı.", "Uyarı");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
