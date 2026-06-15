using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AHUDesignDialog
{
    public AHUDesignDialog() => InitializeComponent();

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        double.TryParse(TxtFlow.Text,       out double flow);
        double.TryParse(TxtReturn.Text,     out double ret);
        double.TryParse(TxtOutSummer.Text,  out double outS);
        double.TryParse(TxtOutWinter.Text,  out double outW);
        double.TryParse(TxtOutHum.Text,     out double outH);
        double.TryParse(TxtSupplyTemp.Text, out double supT);
        double.TryParse(TxtRoomTemp.Text,   out double roomT);
        double.TryParse(TxtRoomHum.Text,    out double roomH);
        double.TryParse(TxtPressure.Text,   out double press);
        double.TryParse(TxtHREff.Text,      out double hrEff);

        var inp = new AHUDesignService.AHUInput
        {
            SupplyAirflowM3h    = flow   > 0 ? flow  : 5000,
            ReturnAirRatioPct   = ret    > 0 ? ret   : 70,
            OutdoorTempSummerC  = outS  != 0 ? outS  : 32,
            OutdoorTempWinterC  = outW  != 0 ? outW  : -3,
            OutdoorHumidityPct  = outH   > 0 ? outH  : 60,
            SupplyTempC         = supT  != 0 ? supT  : 18,
            RoomTempC           = roomT  > 0 ? roomT : 22,
            RoomHumidityPct     = roomH  > 0 ? roomH : 50,
            StaticPressurePa    = press  > 0 ? press : 500,
            HasHeatRecovery     = ChkHR.IsChecked == true,
            HREfficiencyPct     = hrEff  > 0 ? hrEff : 75,
            FilterClass         = (CboFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ePM1 60%"
        };

        var r = AHUDesignService.Calculate(inp);

        ResSupply.Text    = $"{r.SupplyAirflowM3h:F0}";
        ResFresh.Text     = $"{r.FreshAirflowM3h:F0}";
        ResReturn.Text    = $"{r.ReturnAirflowM3h:F0}";

        ResHR.Text        = $"{r.HRSavingsKw:F1}";
        ResPreHeat.Text   = $"{r.WinterPreheatKw:F1}";
        ResHeat.Text      = $"{r.WinterHeatKw:F1}";

        ResSensible.Text  = $"{r.SummerCoolKw:F1}";
        ResLatent.Text    = $"{r.SummerLatentKw:F1}";
        ResCoolTotal.Text = $"{r.TotalCoolKw:F1}";

        ResFan.Text       = $"{r.FanPowerKw:F2} kW";
        ResSFP.Text       = $"SFP: {r.SFP:F0} W/(m³/s)";
        ResHumid.Text     = r.HumidLoadKgph > 0 ? $"{r.HumidLoadKgph:F1} kg/sa" : "Gerekmiyor";
        ResSize.Text      = r.AHUSize;
        ResFilter.Text    = r.FilterRecommendation;

        if (r.Notes.Count > 0)
        {
            ResNotes.Text = string.Join("\n", r.Notes);
            NotesBorder.Visibility = Visibility.Visible;
        }
        else NotesBorder.Visibility = Visibility.Collapsed;

        StatusText.Text = $"✓ Isıtma {r.WinterHeatKw:F1} kW · Soğutma {r.TotalCoolKw:F1} kW · Fan {r.FanPowerKw:F2} kW";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
