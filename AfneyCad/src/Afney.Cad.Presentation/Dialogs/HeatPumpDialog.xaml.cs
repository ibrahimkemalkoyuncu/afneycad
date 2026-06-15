using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class HeatPumpDialog
{
    public HeatPumpDialog() { InitializeComponent(); LoadCatalog(); }

    private void LoadCatalog()
    {
        CatalogGrid.ItemsSource = HeatPumpService.Catalog;
        if (HeatPumpService.Catalog.Count > 0) CatalogGrid.SelectedIndex = 0;
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        double.TryParse(TxtHeatLoad.Text,    out double heat);
        double.TryParse(TxtCoolLoad.Text,    out double cool);
        double.TryParse(TxtDesignTemp.Text,  out double dtemp);
        double.TryParse(TxtSupplyTemp.Text,  out double supply);

        var inp = new HeatPumpService.HeatPumpInput
        {
            HeatingLoadKw  = heat,
            CoolingLoadKw  = cool,
            City           = (CboCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "İstanbul",
            DesignTempC    = dtemp,
            SupplyTempC    = supply,
            FloorHeating   = CboEmission.SelectedIndex == 0,
            HasBackupHeater = ChkBackup.IsChecked == true
        };

        var r = HeatPumpService.Calculate(inp);

        if (r.RecommendedUnit != null)
        {
            ResUnit.Text   = $"{r.RecommendedUnit.Manufacturer} {r.RecommendedUnit.ModelName}";
            ResSeries.Text = r.RecommendedUnit.Series + $" · {r.RecommendedUnit.HeatingKw} kW ısıtma · {r.RecommendedUnit.RefrigerantType}";
            ResClass.Text  = r.EnergyLabel;
            ResSCOP.Text   = r.SCOP.ToString("F2");
            ResSEER.Text   = r.SEER > 0 ? r.SEER.ToString("F2") : "N/A";
            ResCOP.Text    = r.RecommendedUnit.COP_A7_W35.ToString("F2");
            ResBackup.Text = r.BackupHeaterPct > 0 ? $"%{r.BackupHeaterPct:F0}" : "Gerekmiyor";
        }

        ResHeatKwh.Text = $"{r.AnnualHeatKwh:F0}";
        ResCoolKwh.Text = $"{r.AnnualCoolKwh:F0}";
        ResElec.Text    = $"{r.AnnualElecKwh:F0}";
        ResCO2.Text     = $"{r.AnnualCO2Kg:F0}";
        ResRecommendation.Text = r.Recommendation;

        if (r.Warnings.Count > 0)
        {
            ResWarnings.Text = string.Join("\n• ", r.Warnings).TrimStart('\n', '•');
            WarnBorder.Visibility = Visibility.Visible;
        }
        else WarnBorder.Visibility = Visibility.Collapsed;

        StatusText.Text = $"✓ Hesap tamamlandı — {r.RecommendedUnit?.ModelName ?? "?"} seçildi.";
    }

    private void CatalogGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is HeatPumpService.HeatPumpModel m)
            StatusText.Text = $"{m.ModelName} · {m.HeatingKw} kW · SCOP {m.SCOP_35} · {m.EnergyClass} · {m.RefrigerantType} (GWP={m.GWP})";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
