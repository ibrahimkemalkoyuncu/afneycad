using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class FanSelectionDialog
{
    public FanSelectionService.FanModel? SelectedFan { get; private set; }

    public FanSelectionDialog() { InitializeComponent(); Search(); }

    private void Search_Click(object sender, RoutedEventArgs e) => Search();

    private void Search()
    {
        double.TryParse(TxtFlow.Text,         out double flow);
        double.TryParse(TxtPressure.Text,     out double press);
        double.TryParse(TxtSafetyFlow.Text,   out double sf);
        double.TryParse(TxtSafetyPress.Text,  out double sp);

        if (sf  <= 0) sf  = 15;
        if (sp  <= 0) sp  = 20;

        FanSelectionService.FanType? type = CboType.SelectedIndex switch
        {
            1 => FanSelectionService.FanType.Axial,
            2 => FanSelectionService.FanType.Centrifugal,
            3 => FanSelectionService.FanType.Inline,
            4 => FanSelectionService.FanType.Roof,
            5 => FanSelectionService.FanType.ERV,
            _ => (FanSelectionService.FanType?)null
        };

        FanSelectionService.FanManufacturer? mfr = CboManuf.SelectedIndex switch
        {
            1 => FanSelectionService.FanManufacturer.Systemair,
            2 => FanSelectionService.FanManufacturer.Halton,
            3 => FanSelectionService.FanManufacturer.SolerPalau,
            4 => FanSelectionService.FanManufacturer.EBMPapst,
            _ => (FanSelectionService.FanManufacturer?)null
        };

        var results = FanSelectionService.FindFans(flow, press, type, mfr, 1 + sf / 100, 1 + sp / 100);
        FanGrid.ItemsSource = results;

        StatusText.Text = $"{results.Count} model bulundu — Q={flow} m³/h · ΔP={press} Pa";
        SummaryText.Text = results.Count > 0
            ? $"En iyi öneri: {results[0].Fan.ModelName} ({results[0].Fan.Manufacturer}) — Verim: {results[0].Fan.EfficiencyPct}% · SFP: {results[0].SFPCategory}"
            : "Kriterleri karşılayan fan bulunamadı.";

        if (results.Count > 0) FanGrid.SelectedIndex = 0;
    }

    private void FanGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FanGrid.SelectedItem is not FanSelectionService.FanSelectionResult r) return;
        var f = r.Fan;

        DetModel.Text  = f.ModelName;
        DetMaker.Text  = $"{f.Manufacturer} — {f.Series} Serisi · {f.Type}";
        DetFlow.Text   = $"{f.MaxFlowM3h:F0} m³/h";
        DetPress.Text  = $"{f.MaxPressurePa:F0} Pa";
        DetPower.Text  = $"{f.PowerKw:F3} kW";
        DetEff.Text    = $"{f.EfficiencyPct:F0}%";
        DetNoise.Text  = $"{f.NoiseDB:F0} dB(A)";
        DetSFP.Text    = $"{r.SpecificFanPower:F0} W/(m³/s)\n{r.SFPCategory}";
        DetMargin.Text = $"Debi yedek: +{r.FlowMarginPct:F0}% · Basınç yedek: +{r.PressureMarginPct:F0}%";
        DetNoiseEval.Text = FanSelectionService.NoiseAssessment(f.NoiseDB, f.Application);
        DetApp.Text    = f.Application;
        DetNotes.Text  = string.IsNullOrEmpty(f.Notes) ? "" : $"ℹ {f.Notes}";
        DetConn.Text   = $"Bağlantı: {f.ConnectionMM} · {f.Voltage} · {f.IPClass} · Sınıf {f.EnergyClass}";
        DetEC.Text     = f.HasEC_Motor ? "✅ EC Motor — değişken hız, yüksek verim" : "";
    }

    private void SelectAndClose_Click(object sender, RoutedEventArgs e)
    {
        if (FanGrid.SelectedItem is not FanSelectionService.FanSelectionResult r)
        { StatusText.Text = "⚠ Lütfen bir fan seçin."; return; }
        SelectedFan = r.Fan;
        DialogResult = true; Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
