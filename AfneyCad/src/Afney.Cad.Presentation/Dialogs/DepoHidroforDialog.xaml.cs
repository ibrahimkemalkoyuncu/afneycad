using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class DepoHidroforDialog
{
    private readonly CadDatabase _database;
    private WaterTankService.TankResult? _last;

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
            _last = r;

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

    /*
       NE: Çizime Ekle (AddToDrawing_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran (13 "rapor-sonu"
              hesap ekranından biri) hesap sonucunu hiçbir yere yazmıyordu. `TS825InsulationDialog`
              ile aynı desen: özet bir `TextEntity` olarak, kendi katmanına ekleniyor.
    */
    private void AddToDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_last is null) { Calculate_Click(sender, e); if (_last is null) return; }

        var r = _last;
        string txt = $"Depo/Hidrofor — Depo: {r.RecommendedTank} ({r.TankVolumeM3:F2} m³), Pompa: {r.RecommendedPump} (Q={r.PumpFlowM3h:F2} m³/h, H={r.PumpHeadM:F1} m)";

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "DEPO_HIDROFOR_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Depo/hidrofor özeti çizime eklendi (katman: DEPO_HIDROFOR_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
