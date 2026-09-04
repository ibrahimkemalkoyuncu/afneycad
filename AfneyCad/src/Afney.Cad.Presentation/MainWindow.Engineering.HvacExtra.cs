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

        #endregion
    }
}
