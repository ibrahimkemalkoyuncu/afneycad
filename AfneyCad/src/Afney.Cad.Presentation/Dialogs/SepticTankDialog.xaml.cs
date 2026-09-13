using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class SepticTankDialog : Window
    {
        private readonly CadDatabase? _database;
        private SepticTankService.SepticTankResult? _last;

        public SepticTankDialog(CadDatabase? database = null)
        {
            InitializeComponent();
            _database = database;
        }

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
                _last = result;
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

        /*
           NE: Çizime Ekle (AddToDrawing_Click)
           NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran hesap sonucunu
                  hiçbir yere yazmıyordu. `TS825InsulationDialog` ile aynı desen.
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
            string txt = $"Fosseptik — Toplam Hacim={r.TotalVolume:F2} m³, Boyut={r.Length:F1}x{r.Width:F1}x{r.Depth:F1} m, Hazne={r.ChamberCount}, {r.Standard}";

            var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
            {
                Color = 0xFF90CAF9,
                Layer = "FOSSEPTIK_HESAP"
            };
            _database.AddEntity(te);
            MessageBox.Show("Fosseptik özeti çizime eklendi (katman: FOSSEPTIK_HESAP, konum: 0,0).",
                "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
