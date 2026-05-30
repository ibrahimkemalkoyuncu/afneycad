using System.Collections.Generic;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class WaterMeterDialog
{
    private readonly CadDatabase _database;

    public WaterMeterDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        Calculate_Click(this, new RoutedEventArgs());
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new WaterMeterService(_database);
            var r = svc.Calculate();

            TxtPeakFlow.Text    = $"Pik debi: {r.PeakFlowLs:F3} l/s  ({r.PeakFlowM3h:F2} m³/h)";
            TxtRecommended.Text = $"Önerilen: DN {r.RecommendedDN} — {r.MeterModel}";
            TxtPressureLoss.Text = $"Kayıp Basınç: {r.PressureLossM:F2} mSS";

            OptionsGrid.ItemsSource = r.Options.ConvertAll(o => new
            {
                o.DN,
                o.QnomM3h,
                o.QmaxM3h,
                o.PressureLossM,
                SuitableText = o.Suitable ? "✓ Uygun" : "✗ Yetersiz"
            });

            StatusText.Text = "✓ Hesap tamamlandı.";
        }
        catch (System.Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
