using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class StandardSelectionDialog : Window
    {
        private readonly MechanicalKernel _kernel;
        private readonly StandardSelectionService _service = new();

        public StandardSelectionDialog(MechanicalKernel kernel)
        {
            InitializeComponent();
            _kernel = kernel;
            LoadStandards();
        }

        private void LoadStandards()
        {
            var standards = StandardSelectionService.GetAvailableStandards();
            foreach (var s in standards)
            {
                var item = new ComboBoxItem { Content = $"{s.Name} — {s.Description} ({s.Country})", Tag = s.Standard };
                if (s.Standard == StandardSelectionService.DesignStandard.TS_1258 ||
                    s.Standard == StandardSelectionService.DesignStandard.EN_806 ||
                    s.Standard == StandardSelectionService.DesignStandard.DIN_1988)
                    CleanWaterCombo.Items.Add(item);
                else
                    WasteWaterCombo.Items.Add(item);
            }
            if (CleanWaterCombo.Items.Count > 0) CleanWaterCombo.SelectedIndex = 0;
            if (WasteWaterCombo.Items.Count > 0) WasteWaterCombo.SelectedIndex = 0;
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            InfoText.Text =
                $"Mevcut ayarlar:\n" +
                $"• Temiz Su: {_service.ActiveCleanWaterStandard}\n" +
                $"• Pis Su: {_service.ActiveWasteWaterStandard}\n\n" +
                $"Max Hız Limiti: {_service.GetMaxVelocity(_service.ActiveCleanWaterStandard)} m/s\n" +
                $"Min Hız Limiti: {_service.GetMinVelocity(_service.ActiveCleanWaterStandard)} m/s\n" +
                $"Basınç Kaybı Limiti: {_service.GetAllowablePressureLoss(_service.ActiveCleanWaterStandard)} mbar/m";
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (CleanWaterCombo.SelectedItem is ComboBoxItem cw && cw.Tag is StandardSelectionService.DesignStandard cwStd)
                _service.ActiveCleanWaterStandard = cwStd;
            if (WasteWaterCombo.SelectedItem is ComboBoxItem ww && ww.Tag is StandardSelectionService.DesignStandard wwStd)
                _service.ActiveWasteWaterStandard = wwStd;
            UpdateInfo();
            MessageBox.Show("Hesap standartları güncellendi.", "AfneyCAD");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
