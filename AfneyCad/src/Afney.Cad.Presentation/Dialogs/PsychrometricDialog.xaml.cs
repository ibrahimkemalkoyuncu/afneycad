using System;
using System.Globalization;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class PsychrometricDialog
{
    private readonly CadDatabase? _database;
    private PsychrometricState? _lastState;

    public PsychrometricDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
    }

    private void CalcState_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double t = ParseDouble(TxtState1T.Text, 24);
            double rh = ParseDouble(TxtState1RH.Text, 50) / 100.0;

            _lastState = PsychrometricService.CalculateState(t, rh);

            ResHumidityRatio.Text = $"{_lastState.HumidityRatio * 1000:F2}";
            ResEnthalpy.Text = $"{_lastState.EnthalpyKJkg:F2}";
            ResWetBulb.Text = $"{_lastState.WetBulbC:F1}";
            ResDewPoint.Text = $"{_lastState.DewPointC:F1}";
            ResSpecVolume.Text = $"{_lastState.SpecificVolumeM3kg:F4}";
            ResDensity.Text = $"{_lastState.DensityKgM3:F3}";

            StatusText.Text = $"✓ Durum noktası hesaplandı: {t:F1}°C / %{rh * 100:F0} RH";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void CalcProcess_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lastState == null)
            {
                // önce durum noktasını hesapla
                CalcState_Click(sender, e);
                if (_lastState == null) return;
            }

            double target = ParseDouble(TxtTargetTemp.Text, 18);
            var outState = PsychrometricService.SensibleProcess(_lastState, target);

            ResProcess.Text = $"Giriş: {_lastState.DryBulbC:F1}°C / %{_lastState.RelativeHumidity * 100:F0} RH  →  " +
                               $"Çıkış: {outState.DryBulbC:F1}°C / %{outState.RelativeHumidity * 100:F0} RH, " +
                               $"w={outState.HumidityRatio * 1000:F2} g/kg, h={outState.EnthalpyKJkg:F2} kJ/kg, " +
                               $"Twb={outState.WetBulbC:F1}°C, Tdp={outState.DewPointC:F1}°C";
            StatusText.Text = "✓ Duyulur ısıtma/soğutma prosesi hesaplandı.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void CalcMix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double t1 = ParseDouble(TxtMix1T.Text, 24);
            double rh1 = ParseDouble(TxtMix1RH.Text, 50) / 100.0;
            double flow1 = ParseDouble(TxtMix1Flow.Text, 1.0);

            double t2 = ParseDouble(TxtMix2T.Text, -3);
            double rh2 = ParseDouble(TxtMix2RH.Text, 80) / 100.0;
            double flow2 = ParseDouble(TxtMix2Flow.Text, 0.3);

            var s1 = PsychrometricService.CalculateState(t1, rh1);
            var s2 = PsychrometricService.CalculateState(t2, rh2);

            var mix = PsychrometricService.MixAirStreams(s1, flow1, s2, flow2);

            ResMix.Text = $"Karışım: {mix.DryBulbC:F1}°C / %{mix.RelativeHumidity * 100:F0} RH, " +
                          $"w={mix.HumidityRatio * 1000:F2} g/kg, h={mix.EnthalpyKJkg:F2} kJ/kg, " +
                          $"ρ={mix.DensityKgM3:F3} kg/m³ (toplam debi {flow1 + flow2:F2} kg/s)";
            StatusText.Text = "✓ Hava akımı karışımı hesaplandı.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    /*
       NE: Çizime Ekle (AddToDrawing_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran hesap sonucunu
              hiçbir yere yazmıyordu. `TS825InsulationDialog` ile aynı desen — burada son
              hesaplanan durum noktası (CalcState) özetleniyor.
    */
    private void AddToDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_lastState is null) { CalcState_Click(sender, e); if (_lastState is null) return; }
        if (_database is null)
        {
            MessageBox.Show("Aktif çizim bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var s = _lastState;
        string txt = $"Psikrometrik Durum — {s.DryBulbC:F1}°C / %{s.RelativeHumidity * 100:F0} RH, " +
                     $"w={s.HumidityRatio * 1000:F2} g/kg, h={s.EnthalpyKJkg:F2} kJ/kg, Twb={s.WetBulbC:F1}°C, Tdp={s.DewPointC:F1}°C";

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "PSIKROMETRIK_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Psikrometrik durum özeti çizime eklendi (katman: PSIKROMETRIK_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
