using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BackflowPreventerDialog
{
    public BackflowPreventerDialog() => InitializeComponent();

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int    risk   = CboRisk.SelectedIndex + 1;
            double flow   = ParseDouble(TxtFlow.Text, 1.0);
            int    dn     = (int)ParseDouble(TxtDN.Text, 25);

            var svc = new BackflowPreventerService();
            var r   = svc.Select(risk, flow, dn);

            ResType.Text = r.DeviceType;
            ResName.Text = r.DeviceName;
            ResDesc.Text = r.Description;
            ResDp.Text   = r.PressureLossBar > 0 ? $"{r.PressureLossBar:F3} bar" : "Uygulanamaz (hava boşluğu)";
            ResStd.Text  = r.Standard;
            StatusText.Text = "✓ Seçim tamamlandı.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
