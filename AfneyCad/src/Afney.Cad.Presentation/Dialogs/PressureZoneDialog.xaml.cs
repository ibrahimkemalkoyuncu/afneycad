using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class PressureZoneDialog : Window
    {
        private readonly PressureZoneService _svc = new();
        private PressureZoneService.PressureZoneDesignResult? _lastResult;

        public PressureZoneDialog()
        {
            InitializeComponent();
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = BuildInput();
                if (input is null) return;

                _lastResult = _svc.Design(input);
                ShowResults(_lastResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesap hatası: {ex.Message}", "Hata");
            }
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult is null)
            {
                MessageBox.Show("Önce hesap yapın.", "Uyarı");
                return;
            }

            try
            {
                string html = _svc.ExportToHtml(_lastResult);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "HTML Rapor Kaydet",
                    Filter     = "HTML (*.html)|*.html",
                    FileName   = $"BasincBolgesi_{DateTime.Now:yyyyMMdd_HHmm}.html",
                    DefaultExt = ".html"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Rapor kaydedildi:\n{dlg.FileName}", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor hatası: {ex.Message}", "Hata");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private PressureZoneService.ZoneDesignInput? BuildInput()
        {
            if (!double.TryParse(TxtHeight.Text, out double height) || height <= 0)
            { MessageBox.Show("Geçerli bir bina yüksekliği girin.", "Uyarı"); return null; }

            if (!double.TryParse(TxtSupplyPressure.Text, out double supply) || supply <= 0)
            { MessageBox.Show("Geçerli bir şebeke basıncı girin.", "Uyarı"); return null; }

            if (!int.TryParse(TxtFloors.Text, out int floors) || floors <= 0)
            { MessageBox.Show("Geçerli bir kat sayısı girin.", "Uyarı"); return null; }

            if (!double.TryParse(TxtFloorHeight.Text, out double floorH) || floorH <= 0)
                floorH = 3.0;

            if (!double.TryParse(TxtPrvSet.Text, out double prv) || prv <= 0)
                prv = 300.0;

            return new PressureZoneService.ZoneDesignInput(height, supply, floors, floorH, 500.0, prv);
        }

        private void ShowResults(PressureZoneService.PressureZoneDesignResult result)
        {
            ZoneGrid.ItemsSource = result.Zones.Select(z => new ZoneRowVm(z)).ToList();

            SummaryText.Text =
                $"Toplam Bölge: {result.TotalZones}  |  " +
                $"PRV Sayısı: {result.PrvCount}  |  " +
                $"Maks. Statik Basınç: {result.MaxStaticPressureKPa:F0} kPa  |  " +
                (result.BoosterPumpRequired
                    ? $"⚠ Güçlendirme Pompası Gerekli: Hm ≥ {result.BoosterPumpHeadMSS:F0} mSS"
                    : "✓ Şebeke basıncı yeterli");

            NotesText.Text = string.Join("\n", result.Notes);
            StandardText.Text = $"Standart: {result.Standard}  |  Maks. statik basınç: 500 kPa  |  PRV set: {TxtPrvSet.Text} kPa";
        }
    }

    internal class ZoneRowVm
    {
        private readonly PressureZoneService.PressureZone _z;
        public ZoneRowVm(PressureZoneService.PressureZone z) => _z = z;

        public string ZoneNumStr => $"Bölge {_z.ZoneNumber}";
        public string FloorsStr  => $"K{_z.StartFloor} – K{_z.EndFloor} ({_z.FloorCount} kat)";
        public string HeightStr  => $"{_z.ZoneBottomHeightM:F1} – {_z.ZoneTopHeightM:F1}";
        public string PBottomStr => $"{_z.StaticPressureBottomKPa:F0}";
        public string PTopStr    => $"{_z.StaticPressureTopKPa:F0}";
        public string PrvStr     => _z.RequiresPRV ? "✓ PRV" : "—";
        public string PrvInStr   => _z.RequiresPRV ? $"{_z.PrvInputPressureKPa:F0}" : "—";
        public string PrvOutStr  => _z.RequiresPRV ? $"{_z.PrvOutputPressureKPa:F0}" : "—";
        public bool   HasPrv     => _z.RequiresPRV;
    }
}
