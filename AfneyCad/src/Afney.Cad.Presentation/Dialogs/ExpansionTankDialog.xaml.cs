using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ExpansionTankDialog
{
    private readonly CadDatabase? _database;
    private ThermalExpansionService.ExpansionResult? _last;

    public ExpansionTankDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
    }

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
            _last = r;

            ResD.Text    = r.DeltaV.ToString("F4");
            ResVe.Text   = $"{r.ExpansionVolumeL:F2} L";
            ResPre.Text  = $"{r.PrechargeBar:F2} bar";
            ResTank.Text = $"{r.TankVolumeL:F1} L";
            ResModel.Text = r.RecommendedTank;
            StatusText.Text = "✓ Hesap tamamlandı.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
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
        if (_database is null)
        {
            MessageBox.Show("Aktif çizim bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var r = _last;
        string txt = $"Genleşme Deposu — {r.RecommendedTank} — Ve={r.ExpansionVolumeL:F1} L, Ön Basınç={r.PrechargeBar:F2} bar, Depo={r.TankVolumeL:F0} L";

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "GENLESME_DEPOSU_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Genleşme deposu özeti çizime eklendi (katman: GENLESME_DEPOSU_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
