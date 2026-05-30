using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class DepoHidroforDialog
{
    private readonly CadDatabase _database;

    public DepoHidroforDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int    persons = int.Parse(TxtPersons.Text);
            double lpd     = ParseDouble(TxtLpd.Text, 150);
            double days    = ParseDouble(TxtDays.Text, 1.5);
            double head    = ParseDouble(TxtHead.Text, 20);
            double safety  = ParseDouble(TxtSafety.Text, 1.2);

            var svc = new WaterTankService(_database)
            {
                LitersPerPersonPerDay = lpd,
                StorageDays           = days,
                StaticHeadM           = head,
                PumpSafetyFactor      = safety
            };

            var r = svc.Calculate(persons);

            ResDailyDemand.Text = $"{r.DailyDemandL:F0} L/gün  ({r.DailyDemandL / 1000:F2} m³/gün)";
            ResTankVol.Text     = $"{r.TankVolumeL:F0} L  ({r.TankVolumeM3:F2} m³)";
            ResTankModel.Text   = r.RecommendedTank;
            ResLU.Text          = $"{r.TotalLoadUnits:F1} LU  (Walther formülü uygulandı)";
            ResPeakFlow.Text    = $"{r.PeakFlowLs:F3} l/s";
            ResPumpFlow.Text    = $"{r.PumpFlowM3h:F2} m³/h";
            ResPumpHead.Text    = $"{r.PumpHeadM:F1} m";
            ResPumpModel.Text   = r.RecommendedPump;

            if (r.Warnings.Count > 0)
            {
                WarningText.Text    = string.Join("\n• ", r.Warnings).TrimStart();
                WarningPanel.Visibility = Visibility.Visible;
            }
            else
            {
                WarningPanel.Visibility = Visibility.Collapsed;
            }

            StatusText.Text = "✓ Hesap tamamlandı.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
