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
            // NE: Temiz su / Pis su standart filtreleme
            // NEDEN: TS 1258, Türk ulusal standardı olarak hem temiz su hem pis su kurallarını
            //        kapsar (bkz. StandardSelectionService.GetMinWasteSlope varsayılan dalı).
            //        Önceden sadece "Temiz Su" listesine ekleniyordu — Türk mühendisler "Pis Su
            //        Standardı" listesinde hiç yerel seçenek bulamıyordu. Artık her iki listede de var.
            var standards = StandardSelectionService.GetAvailableStandards();
            var cleanWaterStandards = new[]
            {
                StandardSelectionService.DesignStandard.TS_1258,
                StandardSelectionService.DesignStandard.EN_806,
                StandardSelectionService.DesignStandard.DIN_1988,
                StandardSelectionService.DesignStandard.BS_6700,
                StandardSelectionService.DesignStandard.ASPE_UPC,
                StandardSelectionService.DesignStandard.ASHRAE_90_1,
                StandardSelectionService.DesignStandard.IPC_2021,
            };
            var wasteWaterStandards = new[]
            {
                StandardSelectionService.DesignStandard.TS_1258,
                StandardSelectionService.DesignStandard.EN_12056,
                StandardSelectionService.DesignStandard.DIN_1986,
                StandardSelectionService.DesignStandard.BS_6700,
                StandardSelectionService.DesignStandard.ASPE_UPC,
                StandardSelectionService.DesignStandard.IPC_2021,
            };

            foreach (var s in standards)
            {
                if (System.Array.IndexOf(cleanWaterStandards, s.Standard) >= 0)
                    CleanWaterCombo.Items.Add(new ComboBoxItem { Content = $"{s.Name} — {s.Description} ({s.Country})", Tag = s.Standard });

                if (System.Array.IndexOf(wasteWaterStandards, s.Standard) >= 0)
                    WasteWaterCombo.Items.Add(new ComboBoxItem { Content = $"{s.Name} — {s.Description} ({s.Country})", Tag = s.Standard });
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
