using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BackflowPreventerDialog
{
    private readonly CadDatabase? _database;
    private BackflowPreventerService.BackflowResult? _last;

    public BackflowPreventerDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int    risk   = CboRisk.SelectedIndex + 1;
            double flow   = ParseDouble(TxtFlow.Text, 1.0);
            int    dn     = (int)ParseDouble(TxtDN.Text, 25);

            var svc = new BackflowPreventerService();
            var r   = svc.Select(risk, flow, dn);
            _last = r;

            ResType.Text = r.DeviceType;
            ResName.Text = r.DeviceName;
            ResDesc.Text = r.Description;
            ResDp.Text   = r.PressureLossBar > 0 ? $"{r.PressureLossBar:F3} bar" : "Uygulanamaz (hava boşluğu)";
            ResStd.Text  = r.Standard;
            StatusText.Text = "✓ Seçim tamamlandı.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    /*
       NE: Çizime Ekle (AddToDrawing_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran seçim sonucunu
              hiçbir yere yazmıyordu. `TS825InsulationDialog` ile aynı desen.
    */
    private void AddToDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_last is null) { Select_Click(sender, e); if (_last is null) return; }
        if (_database is null)
        {
            MessageBox.Show("Aktif çizim bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var r = _last;
        string txt = $"Geri Akış Önleyici — {r.DeviceType} ({r.DeviceName}) — {r.Standard} — " +
                     (r.PressureLossBar > 0 ? $"Δp={r.PressureLossBar:F3} bar" : "Hava boşluğu");

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "GERI_AKIS_ONLEYICI_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Geri akış önleyici seçimi çizime eklendi (katman: GERI_AKIS_ONLEYICI_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
