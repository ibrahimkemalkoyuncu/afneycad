using System;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class FireFightingDialog : Window
    {
        public FireFightingDialog() { InitializeComponent(); }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = new FireFightingService.SprinklerDesignInput
                {
                    ProtectedAreaM2 = double.Parse(AreaInput.Text),
                    CeilingHeightM = double.Parse(HeightInput.Text),
                    FloorToSystemPressure = double.Parse(PressureInput.Text),
                    IsWetSystem = WetSystemCheck.IsChecked == true,
                    Hazard = HazardCombo.SelectedIndex switch
                    {
                        0 => FireFightingService.HazardClass.LightHazard,
                        1 => FireFightingService.HazardClass.OrdinaryHazard_1,
                        2 => FireFightingService.HazardClass.OrdinaryHazard_2,
                        3 => FireFightingService.HazardClass.ExtraHazard,
                        _ => FireFightingService.HazardClass.LightHazard
                    }
                };
                var service = new FireFightingService();
                var result = service.DesignSprinklerSystem(input);
                ResultText.Text =
                    $"━━━ SPRİNKLER HESAP SONUÇLARI ━━━\n" +
                    $"Sprinkler Sayısı: {result.SprinklerCount}\n" +
                    $"Kapsama Alanı: {result.CoverageAreaPerHead:F1} m²/head\n" +
                    $"Max Aralık: {result.MaxSpacing:F1} m\n" +
                    $"Tasarım Yoğunluğu: {result.DesignDensity} mm/min\n\n" +
                    $"━━━ HİDROLİK DEĞERLER ━━━\n" +
                    $"Gerekli Debi: {result.RequiredFlowLpm:F0} lt/dk\n" +
                    $"Gerekli Basınç: {result.RequiredPressureBar:F2} bar\n" +
                    $"Ana Boru: DN{result.MainPipeDN:F0}\n" +
                    $"Branş Boru: DN{result.BranchPipeDN:F0}\n\n" +
                    $"━━━ POMPA & DEPO ━━━\n" +
                    $"Pompa Kapasitesi: {result.PumpCapacityLpm:F0} lt/dk\n" +
                    $"Pompa Basma Yüks.: {result.PumpHeadM:F1} mSS\n" +
                    $"Su Deposu: {result.WaterTankVolumeM3:F1} m³\n\n" +
                    $"Standart: {result.Standard}\n\n" +
                    string.Join("\n", result.Notes);
            }
            catch (Exception ex) { ResultText.Text = $"Hata: {ex.Message}"; }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
