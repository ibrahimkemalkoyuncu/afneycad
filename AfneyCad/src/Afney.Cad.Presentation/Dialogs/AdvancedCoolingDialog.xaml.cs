using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AdvancedCoolingDialog
{
    public AdvancedCoolingDialog()
    {
        InitializeComponent();
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
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
