using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class SprinklerDesignDialog
{
    public SprinklerDesignDialog() { InitializeComponent(); UpdateHazardDesc(); }

    private void CboHazard_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHazardDesc();

    private void UpdateHazardDesc()
    {
        var h = GetHazard();
        TxtHazardDesc.Text = NFPA13SprinklerService.HazardDescription(h);
    }

    private NFPA13SprinklerService.HazardClass GetHazard() => CboHazard.SelectedIndex switch
    {
        0 => NFPA13SprinklerService.HazardClass.LightHazard,
        2 => NFPA13SprinklerService.HazardClass.OrdinaryHazard2,
        3 => NFPA13SprinklerService.HazardClass.ExtraHazard1,
        4 => NFPA13SprinklerService.HazardClass.ExtraHazard2,
        5 => NFPA13SprinklerService.HazardClass.EarlySuppressionFastResponse,
        _ => NFPA13SprinklerService.HazardClass.OrdinaryHazard1
    };

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        double.TryParse(TxtArea.Text,        out double area);
        double.TryParse(TxtCoverage.Text,    out double cov);
        double.TryParse(TxtMinPress.Text,    out double minP);
        double.TryParse(TxtStaticPress.Text, out double statP);

        double kFactor = CboKFactor.SelectedIndex switch { 1=>80, 2=>115, 3=>202, _=>57 };

        var inp = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard          = GetHazard(),
            AreaM2          = area   > 0 ? area  : 500,
            KFactor         = kFactor,
            MaxCoverageM2   = cov    > 0 ? cov   : 12,
            MinPressureBar  = minP   > 0 ? minP  : 0.70,
            StaticPressureBar = statP > 0 ? statP : 7.0,
            DryPipe         = ChkDryPipe.IsChecked == true
        };

        var r = NFPA13SprinklerService.Calculate(inp);

        ResDensity.Text    = $"{r.DesignDensityLpmdpm2:F1} L/(dak·m²)";
        ResDesignArea.Text = $"Tasarım alanı: {r.DesignAreaM2:F0} m²";
        ResActiveSpr.Text  = $"{r.ActiveSprinklerCount} eşzamanlı sprinkler";
        ResTotalSpr.Text   = $"Toplam: {r.TotalSprinklerCount} sprinkler";
        ResFlow.Text       = $"{r.TotalDesignFlowLpd:F0} L/dak";
        ResFlowM3h.Text    = $"= {r.TotalDesignFlowM3h:F2} m³/sa";
        ResSprFlow.Text    = $"Tek sprinkler: {r.SprinklerFlowLpd:F1} L/dak";
        ResMinPress.Text   = $"Gerekli: {r.MinPressureBarRequired:F2} bar";
        ResResidual.Text   = $"Artık basınç: {r.ResidualPressureBar:F2} bar";
        ResMainPipe.Text   = $"Besleme: {r.SupplyPipeSize}";
        ResBranchPipe.Text = $"Dal borusu: {r.BranchPipeSize}";
        ResCompliance.Text = string.Join("\n", r.Compliance);

        if (r.Warnings.Count > 0)
        {
            ResWarnings.Text = string.Join("\n", r.Warnings);
            WarnBorder.Visibility = Visibility.Visible;
        }
        else WarnBorder.Visibility = Visibility.Collapsed;

        StatusText.Text = $"✓ {r.TotalSprinklerCount} sprinkler · {r.TotalDesignFlowLpd:F0} L/dak · {r.SupplyPipeSize}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
