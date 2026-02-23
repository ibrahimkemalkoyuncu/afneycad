using System;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class SepticTankDialog : Window
    {
        public SepticTankDialog() { InitializeComponent(); }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = new SepticTankService.SepticTankInput
                {
                    PersonCount = int.Parse(PersonInput.Text),
                    UnitWaterConsumption = double.Parse(ConsumptionInput.Text),
                    RetentionTime = double.Parse(RetentionInput.Text),
                    Type = TankTypeCombo.SelectedIndex switch { 0 => SepticTankService.TankType.SingleChamber, 1 => SepticTankService.TankType.DoubleChamber, 2 => SepticTankService.TankType.TripleChamber, _ => SepticTankService.TankType.DoubleChamber }
                };
                var service = new SepticTankService();
                var result = service.CalculateSepticTank(input);
                ResultText.Text =
                    $"━━━ FOSSEPTİK HESAP SONUÇLARI ━━━\n" +
                    $"Gerekli Hacim: {result.RequiredVolume:F2} m³\n" +
                    $"Çamur Hacmi: {result.SludgeVolume:F2} m³\n" +
                    $"Toplam Hacim: {result.TotalVolume:F2} m³\n\n" +
                    $"━━━ BOYUTLAR ━━━\n" +
                    $"Uzunluk: {result.Length:F2} m\n" +
                    $"Genişlik: {result.Width:F2} m\n" +
                    $"Derinlik: {result.Depth:F2} m\n" +
                    $"Hazne Sayısı: {result.ChamberCount}\n\n" +
                    $"Standart: {result.Standard}\n\n" +
                    string.Join("\n", result.Notes);
            }
            catch (Exception ex) { ResultText.Text = $"Hata: {ex.Message}"; }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
