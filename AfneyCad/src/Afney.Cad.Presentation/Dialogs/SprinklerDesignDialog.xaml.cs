using System;
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
        // NOT: Bu dialog SADECE NFPA13SprinklerService kullanır (Amerikan NFPA 13
        // standardı). Pencere başlığı hem "NFPA 13" hem "TS EN 12845" içerdiğinden
        // (bu dialog EN 12845'i uygulamaz — o servis FireFightingDialog'dadır),
        // kullanıcının yanlış standart seçtiğini düşünmemesi için tehlike sınıfı
        // açıklamasının başına açıkça "[NFPA 13]" etiketi eklenir.
        var h = GetHazard();
        TxtHazardDesc.Text = "[NFPA 13] " + NFPA13SprinklerService.HazardDescription(h);
    }

    private NFPA13SprinklerService.NFPA13HazardClass GetHazard() => CboHazard.SelectedIndex switch
    {
        0 => NFPA13SprinklerService.NFPA13HazardClass.LightHazard,
        2 => NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard2,
        3 => NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard1,
        4 => NFPA13SprinklerService.NFPA13HazardClass.ExtraHazard2,
        5 => NFPA13SprinklerService.NFPA13HazardClass.EarlySuppressionFastResponse,
        _ => NFPA13SprinklerService.NFPA13HazardClass.OrdinaryHazard1
    };

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
        double.TryParse(TxtArea.Text,        out double area);
        double.TryParse(TxtCoverage.Text,    out double cov);
        double.TryParse(TxtMinPress.Text,    out double minP);
        double.TryParse(TxtStaticPress.Text, out double statP);

        // NOT (TB-07): Aşağıdaki > 0 kontrolleri ve varsayılan değerler ESKİ davranışla
        // BİREBİR aynı — tek fark, hangi alanın varsayılana düştüğü artık kullanıcıya
        // görünür (önceden tamamen sessizdi).
        var fieldWarnings = new System.Collections.Generic.List<string>();
        if (area  <= 0) fieldWarnings.Add("Koruma Alanı: boş/geçersiz → varsayılan 500 kullanıldı");
        if (cov   <= 0) fieldWarnings.Add("Max Kapsama: boş/geçersiz → varsayılan 12 kullanıldı");
        if (minP  <= 0) fieldWarnings.Add("Min Basınç: boş/geçersiz → varsayılan 0.70 kullanıldı");
        if (statP <= 0) fieldWarnings.Add("Statik Basınç: boş/geçersiz → varsayılan 7.0 kullanıldı");

        area  = area  > 0 ? area  : 500;
        cov   = cov   > 0 ? cov   : 12;
        minP  = minP  > 0 ? minP  : 0.70;
        statP = statP > 0 ? statP : 7.0;

        if (fieldWarnings.Count > 0)
            MessageBox.Show("⚠ Aşağıdaki alanlar boş/geçersiz, varsayılan değer kullanıldı:\n\n" + string.Join("\n", fieldWarnings),
                "Girdi Uyarısı", MessageBoxButton.OK, MessageBoxImage.Warning);

        double kFactor = CboKFactor.SelectedIndex switch { 1=>80, 2=>115, 3=>202, _=>57 };

        var inp = new NFPA13SprinklerService.SprinklerInput
        {
            Hazard          = GetHazard(),
            AreaM2          = area,
            KFactor         = kFactor,
            MaxCoverageM2   = cov,
            MinPressureBar  = minP,
            StaticPressureBar = statP,
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
        catch (Exception ex)
        {
            MessageBox.Show($"Hesaplama sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public event Action? OpenFireFightingRequested;
    private void OpenFireFightingDialog_Click(object sender, RoutedEventArgs e)
    {
        Close();
        OpenFireFightingRequested?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
