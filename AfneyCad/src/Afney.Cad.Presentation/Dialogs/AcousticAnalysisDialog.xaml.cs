using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AcousticAnalysisDialog
{
    private readonly AcousticAnalysisService _service = new();

    public AcousticAnalysisDialog()
    {
        InitializeComponent();
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = new AcousticInput
            {
                AirFlowM3h = ParseDouble(TxtAirFlow.Text, 1000),
                FanPressurePa = ParseDouble(TxtFanPressure.Text, 400),
                FanEfficiency = ParseDouble(TxtFanEff.Text, 0.7),
                DuctWidthMm = ParseDouble(TxtDuctWidth.Text, 400),
                DuctLengthM = ParseDouble(TxtDuctLength.Text, 10),
                IsDuctLined = ChkDuctLined.IsChecked == true,
                BranchCount = (int)ParseDouble(TxtBranchCount.Text, 2),
                ElbowCount = (int)ParseDouble(TxtElbowCount.Text, 3),
                SilencerInsertionLossDb = ParseDouble(TxtSilencerLoss.Text, 0),
                TerminalVelocityMs = ParseDouble(TxtTerminalVel.Text, 3.0),
                RoomVolumeM3 = ParseDouble(TxtRoomVolume.Text, 50),
                RoomType = (CboRoomType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ofis (Özel)"
            };

            var r = _service.AnalyzeSystem(input);

            ResFanLw.Text = $"{r.FanSoundPowerLw:F0} dB";
            ResDuctAtt.Text = $"{r.DuctAttenuationDb:F1} dB";
            ResTerminalLw.Text = $"{r.TerminalNoiseLw:F0} dB";
            ResRoomCorr.Text = $"{r.RoomCorrectionDb:F1} dB";
            ResRoomLp.Text = $"{r.RoomSoundPressureLp:F0} dBA";
            ResNrStatus.Text = $"NR {r.NRLimit} — {(r.NRCompliant ? "UYGUN" : "AŞILDI")}";
            ResRecommendation.Text = r.Recommendation;

            SummaryText.Text = $"{input.RoomType}: Debi={input.AirFlowM3h:F0} m³/h, Dallanma={input.BranchCount}, Dirsek={input.ElbowCount}";
            StatusText.Text = r.NRCompliant
                ? $"✓ Oda ses basıncı {r.RoomSoundPressureLp:F0} dBA, NR {r.NRLimit} sınırında uygun."
                : $"⚠ Oda ses basıncı {r.RoomSoundPressureLp:F0} dBA, NR {r.NRLimit} sınırını aşıyor.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
