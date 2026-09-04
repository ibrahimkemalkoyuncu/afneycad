using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class EnergyRecoveryDialog
{
    private readonly EnergyRecoveryService _service = new();

    public EnergyRecoveryDialog()
    {
        InitializeComponent();
    }

    private static ErvType SelectedErvType(int index) => index switch
    {
        0 => ErvType.PlateHeatExchanger,
        1 => ErvType.RotaryWheel,
        2 => ErvType.HeatPipe,
        3 => ErvType.RunAroundCoil,
        4 => ErvType.MembranePlate,
        _ => ErvType.PlateHeatExchanger
    };

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = new ErvInput
            {
                ErvType = SelectedErvType(CboErvType.SelectedIndex),
                OutdoorTempC = ParseDouble(TxtOutdoorT.Text, -12),
                OutdoorRH = ParseDouble(TxtOutdoorRH.Text, 80) / 100.0,
                IndoorTempC = ParseDouble(TxtIndoorT.Text, 22),
                IndoorRH = ParseDouble(TxtIndoorRH.Text, 50) / 100.0,
                AirFlowM3h = ParseDouble(TxtAirFlow.Text, 500),
                OperatingHoursPerYear = ParseDouble(TxtOperatingHours.Text, 4000),
                CustomEfficiency = string.IsNullOrWhiteSpace(TxtCustomEff.Text) ? 0 : ParseDouble(TxtCustomEff.Text, 0)
            };

            var r = _service.Calculate(input);

            ResEfficiency.Text = $"{r.Efficiency * 100:F0} %";
            ResSupplyTemp.Text = $"{r.SupplyOutletTempC:F1} °C";
            ResSensibleKW.Text = $"{r.SensibleRecoveryKW:F2} kW";
            ResPressureDrop.Text = $"{r.PressureDropPa:F0} Pa";
            ResAnnualSavings.Text = $"{r.AnnualSavingsKWh:F0} kWh/yıl";
            ResAnnualCO2.Text = $"{r.AnnualCO2SavingsKg:F0} kg/yıl";

            SummaryText.Text = $"{r.ErvTypeName} — {input.AirFlowM3h:F0} m³/h, ΔT(dış-iç)={input.IndoorTempC - input.OutdoorTempC:F0} K";
            StatusText.Text = $"✓ Yıllık tasarruf: {r.AnnualSavingsKWh:F0} kWh ({r.AnnualCO2SavingsKg:F0} kg CO2)";
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
