using Afney.Cad.Presentation.Dialogs;
using System;
using System.Windows;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow
    {
        #region -- MUHENDISLIK - HVAC EK HESAPLAR (ENGINEERING.HVACEXTRA) --
        // Session denetim raporu: EN 12831 Isıtma Yükü ve ASHRAE Psikrometri
        // servisleri koda tamdı ama arayüze bağlı değildi. Bu bölüm o bağlantıyı kurar.

        private void OnHeatLoadCalculation(object sender, RoutedEventArgs e)
        {
            try { new HeatLoadCalculationDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Isıtma yükü hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnPsychrometricAnalysis(object sender, RoutedEventArgs e)
        {
            try { new PsychrometricDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Psikrometrik hesap hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Session denetim raporu (devam): EnergyRecoveryService (ERV/HRV), AcousticAnalysisService,
        // EnergySimulationService (TS 825 Bin Method — NOT: TS825InsulationDialog FARKLI bir servis olan
        // TS825InsulationService'i kullanıyor, bu yüzden EnergySimulationService hâlâ ayrı/erişilemezdi) ve
        // AdvancedCoolingService de aynı şekilde koda tamdı ama arayüze bağlı değildi.

        private void OnEnergyRecoveryCommand(object sender, RoutedEventArgs e)
        {
            try { new EnergyRecoveryDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Isı geri kazanım hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnAcousticAnalysisCommand(object sender, RoutedEventArgs e)
        {
            try { new AcousticAnalysisDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Gürültü analizi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnEnergySimulationCommand(object sender, RoutedEventArgs e)
        {
            try { new EnergySimulationDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Yıllık enerji simülasyonu hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnAdvancedCoolingCommand(object sender, RoutedEventArgs e)
        {
            try { new AdvancedCoolingDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Gelişmiş soğutma analizi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion
    }
}
