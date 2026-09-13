using System.Collections.Generic;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class WaterMeterDialog
{
    private readonly CadDatabase _database;
    private WaterMeterService.MeterResult? _last;

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
            _last = r;

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

    /*
       NE: Çizime Ekle (AddToDrawing_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran (13 "rapor-sonu"
              hesap ekranından biri) hesap sonucunu hiçbir yere yazmıyordu — kullanıcı
              sonucu okuyup başka bir yere elle taşımak zorundaydı. `TS825InsulationDialog`
              ile aynı desen: özet bir `TextEntity` olarak, kendi katmanına ekleniyor.
    */
    private void AddToDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_last is null) { Calculate_Click(sender, e); if (_last is null) return; }

        var r = _last;
        string txt = $"Su Sayacı — DN{r.RecommendedDN} {r.MeterModel} — Qp={r.PeakFlowM3h:F2} m³/h Δp={r.PressureLossM:F2} mSS";

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "SU_SAYACI_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Su sayacı özeti çizime eklendi (katman: SU_SAYACI_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
