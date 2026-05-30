using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ExpansionTankDialog
{
    public ExpansionTankDialog() => InitializeComponent();

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new ThermalExpansionService
            {
                SystemVolumeL  = ParseDouble(TxtVolume.Text, 100),
                TempCold       = ParseDouble(TxtTCold.Text, 10),
                TempHot        = ParseDouble(TxtTHot.Text, 80),
                StaticHeadM    = ParseDouble(TxtHead.Text, 5),
                MaxPressureBar = ParseDouble(TxtPmax.Text, 3)
            };
            var r = svc.Calculate();

            ResD.Text    = r.DeltaV.ToString("F4");
            ResVe.Text   = $"{r.ExpansionVolumeL:F2} L";
            ResPre.Text  = $"{r.PrechargeBar:F2} bar";
            ResTank.Text = $"{r.TankVolumeL:F1} L";
            ResModel.Text = r.RecommendedTank;
            StatusText.Text = "✓ Hesap tamamlandı.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
