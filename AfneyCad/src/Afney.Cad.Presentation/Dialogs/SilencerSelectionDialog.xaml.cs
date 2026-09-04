using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;
using static Afney.Cad.Mechanical.Services.SilencerSelectionService;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Susturucu Seçim Dialogu (SilencerSelectionDialog)
   NEDEN: SilencerSelectionService bir "seçim/hesap" servisidir (entity değil) — kanal ağına
          bir CAD elemanı olarak yerleştirilmez (Draw() override'ı yok, MechanicalEntity/
          CadEntity türevi değil). Denetim raporu bu servisin Presentation katmanından hiç
          erişilemediğini bulmuştu; bu basit dialog debi + hedef ekleme kaybı (Insertion Loss)
          girilerek uygun susturucuyu FindSilencers/BestSilencer ile listeler — ValveLibraryDialog
          ile aynı görsel dilde ama CAD'e ekleme adımı yok (servis kendisi bir çizim nesnesi
          üretmiyor).
*/
public partial class SilencerSelectionDialog : Window
{
    public SilencerSelectionDialog()
    {
        InitializeComponent();
        Search_Click(this, new RoutedEventArgs());
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        double flow = ParseDouble(TxtFlow.Text, 1000);
        double targetIL = ParseDouble(TxtTargetIL.Text, 20);
        int band = int.TryParse((BandCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out int b) ? b : 500;

        SilencerType? type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Rectangular" => SilencerType.Rectangular,
            "Circular"    => SilencerType.Circular,
            "Cellular"    => SilencerType.Cellular,
            _             => null
        };

        var results = FindSilencers(flow, targetIL, band, type);
        var rows = results.Select(r => new SilencerRowVm(r)).ToList();
        ResultGrid.ItemsSource = rows;

        TxtStatus.Text = rows.Count > 0
            ? $"{rows.Count} uygun susturucu bulundu."
            : "Kriterlere uygun susturucu bulunamadı — hedef IL veya debiyi gevşetin.";

        if (rows.Count > 0)
            ResultGrid.SelectedIndex = 0;
        else
            DtlText.Text = "Bir sonuç seçin — 8 oktav bant (63..8000 Hz) ekleme kaybı burada gösterilir.";
    }

    private void ResultGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultGrid.SelectedItem is not SilencerRowVm row) return;

        var s = row.Result.Silencer;
        var bands = OctaveBands.Zip(s.InsertionLossDb, (hz, il) => $"{hz}Hz:{il:F0}dB");
        DtlText.Text =
            $"{s.Manufacturer} {s.ModelName} — {s.Application}\n" +
            $"Oktav Bant Ekleme Kaybı (IL): {string.Join("  ", bands)}\n" +
            $"Kritik Bant ({row.Result.CriticalBandHz} Hz) IL: {row.Result.InsertionLossAtCriticalBandDb:F0} dB   " +
            $"Debi Marjı: %{row.Result.FlowMarginPct:F0}   Fiyat: {s.PriceEur:F0} EUR";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}

internal class SilencerRowVm
{
    public SilencerSelectionResult Result { get; }
    public string ModelName => Result.Silencer.ModelName;
    public string Manufacturer => Result.Silencer.Manufacturer.ToString();
    public string Type => Result.Silencer.Type.ToString();
    public string ConnectionMM => Result.Silencer.ConnectionMM;
    public string LengthMm => Result.Silencer.LengthMm.ToString("F0");
    public string PressureDropPa => Result.Silencer.PressureDropPa.ToString("F0");
    public string InsertionLossAtCriticalBandDb => Result.InsertionLossAtCriticalBandDb.ToString("F0");
    public string FlowMarginPct => Result.FlowMarginPct.ToString("F0");

    public SilencerRowVm(SilencerSelectionResult result) => Result = result;
}
