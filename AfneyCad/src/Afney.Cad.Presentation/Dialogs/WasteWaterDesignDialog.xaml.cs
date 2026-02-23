using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class WasteWaterDesignDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly ObservableCollection<DrainageUnit> _drainageUnits = new();
        private readonly ObservableCollection<CatchmentArea> _catchmentAreas = new();

        public WasteWaterDesignDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            // Örnek verilerle başlat
            _drainageUnits.Add(new DrainageUnit { FixtureName = "WC (Rezervuarlı)", DU = 2.0, Count = 4 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Lavabo", DU = 0.5, Count = 6 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Duş", DU = 0.6, Count = 3 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Mutfak Eviyesi", DU = 0.8, Count = 2 });
            DrainageGrid.ItemsSource = _drainageUnits;

            _catchmentAreas.Add(new CatchmentArea { Name = "Düz Çatı", AreaM2 = 200, RunoffCoefficient = 1.0 });
            _catchmentAreas.Add(new CatchmentArea { Name = "Teras", AreaM2 = 50, RunoffCoefficient = 0.8 });
            CatchmentGrid.ItemsSource = _catchmentAreas;
        }

        private void CalcWaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var method = MethodCombo.SelectedIndex switch
                {
                    0 => WasteWaterDesignService.DesignMethod.System_I,
                    1 => WasteWaterDesignService.DesignMethod.System_II,
                    2 => WasteWaterDesignService.DesignMethod.System_III,
                    3 => WasteWaterDesignService.DesignMethod.System_IV,
                    _ => WasteWaterDesignService.DesignMethod.System_II
                };
                var service = new WasteWaterDesignService(_database);
                var result = service.CalculateWasteWaterFlow(new List<DrainageUnit>(_drainageUnits), method);
                WasteResultText.Text =
                    $"━━━ SONUÇLAR ━━━\n" +
                    $"Toplam DU: {result.TotalDU:F1}\n" +
                    $"Frekans Faktörü (K): {result.FrequencyFactor}\n" +
                    $"Pis Su Debisi (Q_ww): {result.WasteWaterFlow:F3} lt/s\n" +
                    $"Tasarım Debisi: {result.DesignFlow:F3} lt/s\n" +
                    $"Önerilen Boru: DN {result.RecommendedDN:F0}\n" +
                    $"Min. Eğim: %{result.MinimumSlope * 100:F1}\n" +
                    $"Doluluk Oranı: %{result.FillingRatio * 100:F0}\n" +
                    $"Standart: {result.Standard}";
            }
            catch (Exception ex)
            {
                WasteResultText.Text = $"Hata: {ex.Message}";
            }
        }

        private void CalcRain_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double rainfall = double.Parse(RainfallInput.Text);
                var service = new WasteWaterDesignService(_database);
                var result = service.CalculateRainwaterFlow(new List<CatchmentArea>(_catchmentAreas), rainfall);
                RainResultText.Text =
                    $"━━━ SONUÇLAR ━━━\n" +
                    $"Yağış Yoğunluğu: {result.RainfallIntensity} lt/s·ha\n" +
                    $"Toplam Alan: {result.TotalCatchmentArea:F1} m²\n" +
                    $"Toplam Debi: {result.TotalFlow:F3} lt/s\n" +
                    $"Önerilen Boru: DN {result.RecommendedDN:F0}\n" +
                    $"Min. Eğim: %{result.MinimumSlope * 100:F1}\n" +
                    $"Standart: {result.Standard}";
            }
            catch (Exception ex)
            {
                RainResultText.Text = $"Hata: {ex.Message}";
            }
        }
    }
}
