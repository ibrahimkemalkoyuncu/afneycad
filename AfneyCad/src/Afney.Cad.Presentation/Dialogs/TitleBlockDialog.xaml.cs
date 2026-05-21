using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class TitleBlockDialog : Window
{
    private readonly CadDatabase _database;
    private readonly TitleBlockService _svc = new();

    public TitleBlockDialog(CadDatabase database)
    {
        InitializeComponent();
        _database  = database;
        TxtTarih.Text = DateTime.Now.ToString("dd.MM.yyyy");
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cfg = BuildConfig();

            double ox = ParseDouble(TxtOriginX.Text);
            double oy = ParseDouble(TxtOriginY.Text);
            var origin = new Vector3D(ox, oy, 0);

            // Katman yoksa ekle
            foreach (var layerName in new[] { "ANTET", "SINIR" })
            {
                if (_database.GetLayer(layerName) == null)
                    _database.AddLayer(new CadLayer(layerName) { Color = 0xFFFFFFFF });
            }

            var entities = _svc.Generate(cfg, origin);
            foreach (var ent in entities)
                _database.AddEntity(ent);

            MessageBox.Show(
                $"Antet eklendi ({entities.Count} nesne).\n" +
                $"Kağıt: {cfg.KagitBoyu} | Pafta No: {cfg.PaftaNo}\n" +
                $"Konum: X={ox:F0}, Y={oy:F0} mm",
                "Antet Eklendi", MessageBoxButton.OK, MessageBoxImage.Information);

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private TitleBlockService.TitleBlockConfig BuildConfig()
    {
        var paper = (CmbKagit.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "A2" => TitleBlockService.PaperSize.A2,
            "A1" => TitleBlockService.PaperSize.A1,
            "A0" => TitleBlockService.PaperSize.A0,
            _    => TitleBlockService.PaperSize.A3,
        };

        string olcek = (CmbOlcek.SelectedItem as ComboBoxItem)?.Content?.ToString()
                    ?? CmbOlcek.Text
                    ?? "1/100";

        return new TitleBlockService.TitleBlockConfig
        {
            FirmaAdi      = TxtFirma.Text.Trim(),
            ProjeAdi      = TxtProje.Text.Trim(),
            CizimAdi      = TxtCizim.Text.Trim(),
            Cizen         = TxtCizen.Text.Trim(),
            KontrolEden   = TxtKontrol.Text.Trim(),
            Tarih         = TxtTarih.Text.Trim(),
            PaftaNo       = TxtPaftaNo.Text.Trim(),
            Olcek         = olcek,
            Revizyon      = TxtRevizyon.Text.Trim(),
            KagitBoyu     = paper,
            DrawBorderFrame = ChkBorder.IsChecked == true,
        };
    }

    private static double ParseDouble(string s)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
