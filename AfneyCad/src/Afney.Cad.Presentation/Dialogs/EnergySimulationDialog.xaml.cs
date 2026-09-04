using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class EnergySimulationDialog
{
    private readonly EnergySimulationService _service = new();

    public EnergySimulationDialog()
    {
        InitializeComponent();
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = new EnergySimulationInput
            {
                City = (CboCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "İstanbul",
                FloorAreaM2 = ParseDouble(TxtFloorArea.Text, 200),
                BuildingUAValueWK = ParseDouble(TxtUAValue.Text, 400),
                HeatingSetpointC = ParseDouble(TxtHeatSetpoint.Text, 20),
                CoolingSetpointC = ParseDouble(TxtCoolSetpoint.Text, 26),
                HeatingCOP = ParseDouble(TxtHeatCOP.Text, 3.5),
                CoolingCOP = ParseDouble(TxtCoolCOP.Text, 3.0),
                DHW_COP = ParseDouble(TxtDhwCOP.Text, 2.5),
                DHWDemandLitersPerDay = ParseDouble(TxtDhwDemand.Text, 200),
                DHWTempC = ParseDouble(TxtDhwTemp.Text, 60),
                TotalWindowAreaM2 = ParseDouble(TxtWindowArea.Text, 30),
                SHGC = ParseDouble(TxtSHGC.Text, 0.4),
                InternalGainWm2 = ParseDouble(TxtInternalGain.Text, 20),
                LightingWm2 = ParseDouble(TxtLighting.Text, 10),
                FanPowerKW = ParseDouble(TxtFanPower.Text, 1.5),
                OccupiedHoursPerDay = ParseDouble(TxtOccupiedHours.Text, 12),
                ElectricityPriceTRYPerKWh = ParseDouble(TxtElecPrice.Text, 4.5)
            };

            var r = _service.Simulate(input);
            MonthlyGrid.ItemsSource = r.MonthlyData;

            ResTotalKWh.Text = $"{r.AnnualTotalKWh:F0} kWh/yıl";
            ResPrimaryKWh.Text = $"{r.AnnualPrimaryEnergyKWh:F0} kWh/yıl";
            ResSpecificKWhM2.Text = $"{r.SpecificEnergyKWhM2:F0} kWh/m²yıl";
            ResCO2Cost.Text = $"{r.AnnualCO2Tons:F1} t / {r.AnnualCostTRY:F0} TL";
            ResEnergyClass.Text = r.EnergyClass;

            SummaryText.Text = $"{r.City}: Isıtma={r.AnnualHeatingKWh:F0} kWh, Soğutma={r.AnnualCoolingKWh:F0} kWh, Sıcak Su={r.AnnualDHWKWh:F0} kWh, " +
                                $"Aydınlatma={r.AnnualLightingKWh:F0} kWh, Fan/Pompa={r.AnnualFanPumpKWh:F0} kWh";
            StatusText.Text = $"✓ Yıllık toplam enerji: {r.AnnualTotalKWh:F0} kWh, sınıf {r.EnergyClass}";
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
