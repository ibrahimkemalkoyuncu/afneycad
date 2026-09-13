using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AdvancedCoolingDialog
{
    private readonly CadDatabase? _database;
    private string? _lastSummary;

    public AdvancedCoolingDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // İnfiltrasyon
            double roomVolume = ParseDouble(TxtRoomVolume.Text, 50);
            double ach = ParseDouble(TxtACH.Text, 0.5);
            double outdoorT = ParseDouble(TxtOutdoorT.Text, 34);
            double outdoorRH = ParseDouble(TxtOutdoorRH.Text, 50) / 100.0;
            double indoorT = ParseDouble(TxtIndoorT.Text, 24);
            double indoorRH = ParseDouble(TxtIndoorRH.Text, 50) / 100.0;

            var inf = AdvancedCoolingService.CalculateInfiltration(roomVolume, ach, outdoorT, indoorT, outdoorRH, indoorRH);

            // CLTD
            string orientation = (CboOrientation.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Guney";
            int hour = (int)ParseDouble(TxtHour.Text, 14);
            double cltd = AdvancedCoolingService.GetPeakCLTD(orientation, hour);

            // Ekipman
            string equipType = (CboEquipment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bilgisayar";
            int equipCount = (int)ParseDouble(TxtEquipCount.Text, 1);
            double equipGainW = AdvancedCoolingService.EquipmentHeatGain(equipType, equipCount);

            // Gölgeleme (Tag = servis anahtarı ile birebir eşleşen küçük harf literal;
            // Content'i ToLowerInvariant ile küçültmek Türkçe "İ" harfinde hatalı sonuç verir)
            var shadingItem = CboShading.SelectedItem as ComboBoxItem;
            string shadingType = shadingItem?.Tag?.ToString() ?? "yok";
            string shadingLabel = shadingItem?.Content?.ToString() ?? "Yok";
            double shadingFactor = AdvancedCoolingService.ShadingCorrectionFactor(shadingType);

            ResInfiltration.Text = $"{inf.SensibleW:F0} / {inf.LatentW:F0} W";
            ResCltd.Text = $"{cltd:F1} °C ({orientation}, saat {hour})";
            ResEquipment.Text = $"{equipGainW:F0} W ({equipCount}x {equipType})";
            ResTotalInf.Text = $"{inf.TotalW:F0} W  ({inf.AirFlowM3h:F0} m³/h)";
            ResShading.Text = $"{shadingFactor:F2}  ({shadingLabel})";

            SummaryText.Text = $"Oda hacmi={roomVolume:F0} m³, ACH={ach:F2}/h, dış/iç T={outdoorT:F0}/{indoorT:F0}°C";
            StatusText.Text = $"✓ Toplam infiltrasyon yükü: {inf.TotalW:F0} W, ekipman kazancı: {equipGainW:F0} W";

            _lastSummary = $"Gelişmiş Soğutma — İnfiltrasyon={inf.TotalW:F0} W, CLTD={cltd:F1}°C ({orientation}, saat {hour}), " +
                           $"Ekipman={equipGainW:F0} W ({equipCount}x {equipType}), Gölgeleme Faktörü={shadingFactor:F2} ({shadingLabel})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    /*
       NE: Çizime Ekle (AddToDrawing_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran hesap sonucunu
              hiçbir yere yazmıyordu. `TS825InsulationDialog` ile aynı desen.
    */
    private void AddToDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSummary is null) { Calculate_Click(sender, e); if (_lastSummary is null) return; }
        if (_database is null)
        {
            MessageBox.Show("Aktif çizim bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var te = new TextEntity(_lastSummary, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "GELISMIS_SOGUTMA_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Gelişmiş soğutma özeti çizime eklendi (katman: GELISMIS_SOGUTMA_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
