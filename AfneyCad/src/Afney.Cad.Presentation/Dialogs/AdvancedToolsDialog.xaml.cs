using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AdvancedToolsDialog
{
    private readonly CadDatabase _database;

    public AdvancedToolsDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;

        // Yağmur Oluğu
        foreach (var c in GutterSizingService.RainfallIntensity.Keys.OrderBy(x => x))
            GtrCity.Items.Add(new ComboBoxItem { Content = c });
        GtrCity.SelectedIndex = 0;
        foreach (var s in GutterSizingService.RunoffCoefficients.Keys)
            GtrSurface.Items.Add(new ComboBoxItem { Content = s });
        GtrSurface.SelectedIndex = 0;

        // Genleşme Kompansatörü
        foreach (var m in ExpansionLoopService.AlphaPerK.Keys)
            ExpMaterial.Items.Add(new ComboBoxItem { Content = m });
        ExpMaterial.SelectedIndex = 0;

        // Yaşlanma
        var agMaterials = new[] { "Çelik (galvaniz)→0.005", "Çelik (siyah)→0.003", "Dökme Demir→0.008", "PP-R/PVC (yaşlanmaz)→0" };
        foreach (var m in agMaterials) AgMaterial.Items.Add(new ComboBoxItem { Content = m });
        AgMaterial.SelectedIndex = 0;

        // Gürültü
        NoiseMaterial.Items.Add(new ComboBoxItem { Content = "Plastik (PP-R/PVC) — 5 dB", IsSelected = true });
        NoiseMaterial.Items.Add(new ComboBoxItem { Content = "Çelik — 10 dB" });
    }

    // ── Yağmur Oluğu ─────────────────────────────────────────────────────────────

    private void GtrCity_Changed(object sender, SelectionChangedEventArgs e)
    {
        string city = (GtrCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (GutterSizingService.RainfallIntensity.TryGetValue(city, out double r))
            GtrRain.Text = r.ToString("F4", CultureInfo.InvariantCulture);
    }

    private void GtrCalc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string city = (GtrCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Genel";
            string surf = (GtrSurface.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Çatı (kiremit/metal)";
            double area = ParseDouble(GtrArea.Text, 100);
            double rain = ParseDouble(GtrRain.Text, 0.025);
            double slope = ParseDouble(GtrSlope.Text, 0.5);

            var svc = new GutterSizingService { RainfallOverride = rain, GutterSlope = slope / 100.0 };
            var r = svc.Calculate(city, [new GutterSizingService.RoofSection { Name = "Çatı", AreaM2 = area, SurfaceType = surf }]);

            GtrResult.Text = $"Q = {r.TotalFlowLs:F3} l/s  |  {r.GutterLabel}  |  {r.DownpipeCount}×Ø{r.DownpipeDiameterMm:F0}mm dere (aralık ≤{r.DownpipeSpacingM:F1}m)";
            GtrNotes.Text  = string.Join("\n", r.Notes);
        }
        catch (Exception ex) { GtrResult.Text = $"Hata: {ex.Message}"; }
    }

    // ── Genleşme Kompansatörü ─────────────────────────────────────────────────────

    private void ExpCalc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string mat = (ExpMaterial.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Çelik (St/galvaniz)";
            var svc = new ExpansionLoopService
            {
                Material       = mat,
                DiameterMm     = ParseDouble(ExpDN.Text, 50),
                PipeLengthM    = ParseDouble(ExpLength.Text, 10),
                TempInstallC   = ParseDouble(ExpTInst.Text, 20),
                TempOperatingC = ParseDouble(ExpTOper.Text, 80)
            };
            var r = svc.Calculate();

            ExpDeltaL.Text = $"{r.DeltaLMm:F2} mm";
            ExpULoop.Text  = r.ULoopLabel;
            ExpZLoop.Text  = r.ZLoopLabel;
            ExpLLoop.Text  = r.LLoopLabel;
            ExpRec.Text    = r.Recommendation;
        }
        catch (Exception ex) { ExpDeltaL.Text = $"Hata: {ex.Message}"; }
    }

    // ── Boru Yaşlanması ───────────────────────────────────────────────────────────

    private void AgCalc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double roughBase = ParseDouble(AgRough.Text, 0.007);
            double rate      = ParseDouble(AgRate.Text, 0.003);
            int    maxAge    = Math.Max(int.TryParse(AgAge.Text, out int a) ? a : 0, 30);

            // D=50mm, v=1.5 m/s örnek boru için basınç kaybı değişimi
            const double D = 0.05, v = 1.5, nu = 1e-6;
            double Re = v * D / nu;

            var rows = new List<object>();
            double dp0 = 0;
            for (int yr = 0; yr <= Math.Min(maxAge, 50); yr += 5)
            {
                double eps = roughBase + rate * yr;
                double f   = 0.25 / Math.Pow(Math.Log10(eps / 1000 / (3.7 * D) + 5.74 / Math.Pow(Re, 0.9)), 2);
                double dp  = f * (1.0 / D) * v * v / (2 * 9.81); // mSS/m
                if (yr == 0) dp0 = dp;
                double pct = dp0 > 0 ? (dp - dp0) / dp0 * 100 : 0;
                string status = pct < 10 ? "✓ İyi" : pct < 30 ? "⚠ Dikkat" : "❌ Ciddi";
                rows.Add(new { Year = yr, Roughness = eps, FrictionFactor = f, DropIncreasePct = pct, Status = status });
            }
            AgGrid.ItemsSource = rows;
            AgResult.Text = $"Başlangıç pürüzlülüğü: {roughBase:F5} mm  |  {maxAge} yılda: {roughBase + rate * maxAge:F5} mm  (+{rate * maxAge / roughBase * 100:F0}%)";
        }
        catch (Exception ex) { AgResult.Text = $"Hata: {ex.Message}"; }
    }

    // ── Gürültü Analizi ───────────────────────────────────────────────────────────

    private void NoiseAnalyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double kBase = (NoiseMaterial.SelectedIndex == 1) ? 10 : 5;
            var svc = new PipeNoiseService(_database) { MaterialFactor = kBase };
            var r   = svc.Analyze();

            NoiseGrid.ItemsSource = r.Pipes;
            NoiseSummary.Text     = r.Summary;
        }
        catch (Exception ex) { NoiseSummary.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
