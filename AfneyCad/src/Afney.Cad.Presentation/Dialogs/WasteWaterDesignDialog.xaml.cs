using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class WasteWaterDesignDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly WasteWaterLayerService _layerService;

        private readonly ObservableCollection<DrainageUnit> _drainageUnits = new();
        private readonly ObservableCollection<CatchmentArea> _catchmentAreas = new();

        // Olaylar — MainWindow bu olayları dinleyerek viewport'a işlem yaptırır
        public event Action<MechanicalSystemType>? ModuleChanged;
        public event Action? DrawCatchmentAreaRequested;
        public event Action<bool>? PlaceOutletRequested;   // true = rain, false = sewer
        // lowerBottomM, lowerTopM, upperBottomM, upperTopM (metre)
        public event Action<double, double, double, double>? CreateSplitColumnRequested;
        public event Action? ValidateCopySelectionRequested;
        public event Action? FilterAndCopyRequested;
        public event Action? AcceptSystemRequested;

        public WasteWaterDesignDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            _layerService = new WasteWaterLayerService(database);
            _layerService.EnsureLayers();

            LoadSampleData();
        }

        private void LoadSampleData()
        {
            _drainageUnits.Add(new DrainageUnit { FixtureName = "WC (Rezervuarlı)", DU = 2.0, Count = 4 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Lavabo",           DU = 0.5, Count = 6 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Duş Teknesi",      DU = 0.6, Count = 3 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Mutfak Eviyesi",   DU = 0.8, Count = 2 });
            _drainageUnits.Add(new DrainageUnit { FixtureName = "Yer Süzgeci",      DU = 0.5, Count = 2 });
            DrainageGrid.ItemsSource = _drainageUnits;

            _catchmentAreas.Add(new CatchmentArea { Name = "Düz Çatı",    AreaM2 = 200, RunoffCoefficient = 1.0 });
            _catchmentAreas.Add(new CatchmentArea { Name = "Yeşil Çatı",  AreaM2 = 80,  RunoffCoefficient = 0.5 });
            _catchmentAreas.Add(new CatchmentArea { Name = "Teras/Döşeme", AreaM2 = 50,  RunoffCoefficient = 0.9 });
            CatchmentGrid.ItemsSource = _catchmentAreas;
        }

        // --- SEKME 1: KATMAN YÖNETİMİ ---

        private void ApplyLayers_Click(object sender, RoutedEventArgs e)
        {
            // Aktif modülü belirle
            MechanicalSystemType activeModule = MechanicalSystemType.WasteWater;
            if (RbColdWater.IsChecked  == true) activeModule = MechanicalSystemType.DomesticColdWater;
            if (RbHotWater.IsChecked   == true) activeModule = MechanicalSystemType.DomesticHotWater;
            if (RbWasteWater.IsChecked == true) activeModule = MechanicalSystemType.WasteWater;
            if (RbRainWater.IsChecked  == true) activeModule = MechanicalSystemType.RainWater;

            _layerService.SetActiveModule(activeModule);

            // Checkbox'lardan görünür katman listesi
            var visible = new List<MechanicalSystemType>();
            if (ChkShowCold.IsChecked  == true) visible.Add(MechanicalSystemType.DomesticColdWater);
            if (ChkShowHot.IsChecked   == true) visible.Add(MechanicalSystemType.DomesticHotWater);
            if (ChkShowWaste.IsChecked == true) visible.Add(MechanicalSystemType.WasteWater);
            if (ChkShowRain.IsChecked  == true) visible.Add(MechanicalSystemType.RainWater);
            if (ChkShowFire.IsChecked  == true) visible.Add(MechanicalSystemType.FireProtection);
            if (ChkShowGas.IsChecked   == true) visible.Add(MechanicalSystemType.Gas);

            _layerService.ShowSystems(visible);
            ModuleChanged?.Invoke(activeModule);

            MessageBox.Show(
                $"Aktif Modül: {activeModule}\nGörünür Katmanlar: {string.Join(", ", visible.Select(s => WasteWaterLayerService.GetLayerName(s)))}",
                "Katman Ayarları Uygulandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowAllLayers_Click(object sender, RoutedEventArgs e)
        {
            _layerService.ShowAllSystems();
            ChkShowCold.IsChecked  = true;
            ChkShowHot.IsChecked   = true;
            ChkShowWaste.IsChecked = true;
            ChkShowRain.IsChecked  = true;
            ChkShowFire.IsChecked  = true;
            ChkShowGas.IsChecked   = true;
        }

        // --- SEKME 2: PİS SU HESABI ---

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
                var svc    = new WasteWaterDesignService(_database);
                var result = svc.CalculateWasteWaterFlow(new List<DrainageUnit>(_drainageUnits), method);

                WasteResultText.Text =
                    $"━━━ PİS SU HESAP SONUÇLARI ━━━\n" +
                    $"Toplam DU            : {result.TotalDU:F1}\n" +
                    $"Frekans Faktörü K    : {result.FrequencyFactor}\n" +
                    $"Pis Su Debisi Q_ww   : {result.WasteWaterFlow:F3} lt/s\n" +
                    $"Sürekli Akış Q_c     : {result.ContinuousFlow:F3} lt/s\n" +
                    $"Tasarım Debisi Q_tot : {result.DesignFlow:F3} lt/s\n" +
                    $"Önerilen Boru        : DN {result.RecommendedDN:F0}\n" +
                    $"Min. Eğim            : %{result.MinimumSlope * 100:F1}\n" +
                    $"Max. Doluluk Oranı   : %{result.FillingRatio * 100:F0}\n" +
                    $"Standart             : {result.Standard}";
            }
            catch (Exception ex)
            {
                WasteResultText.Text = $"Hata: {ex.Message}";
            }
        }

        // --- SEKME 3: YAĞMUR SUYU ---

        private void DrawCatchment_Click(object sender, RoutedEventArgs e)
        {
            // Pencereyi küçülterek viewport'ta çizim moduna geçilmesini sağla
            DrawCatchmentAreaRequested?.Invoke();
            Hide();
        }

        private void CalcRain_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(RainfallInput.Text, out double rainfall) || rainfall <= 0)
                {
                    RainResultText.Text = "Hata: Geçerli bir yağış yoğunluğu girin.";
                    return;
                }

                var svc    = new WasteWaterDesignService(_database);
                var result = svc.CalculateRainwaterFlow(new List<CatchmentArea>(_catchmentAreas), rainfall);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("━━━ YAĞMUR SUYU HESAP SONUÇLARI ━━━");
                sb.AppendLine($"Yağış Yoğunluğu  : {result.RainfallIntensity} lt/s·ha");
                sb.AppendLine($"Toplam Alan      : {result.TotalCatchmentArea:F1} m²");
                sb.AppendLine($"Toplam Debi      : {result.TotalFlow:F3} lt/s");
                sb.AppendLine($"Önerilen Boru    : DN {result.RecommendedDN:F0}");
                sb.AppendLine($"Min. Eğim        : %{result.MinimumSlope * 100:F1}");
                sb.AppendLine($"Standart         : {result.Standard}");
                sb.AppendLine();
                sb.AppendLine("─── Alan Detayları ───");
                foreach (var d in result.AreaDetails)
                    sb.AppendLine($"  {d.AreaName,-20} {d.AreaM2,6:F1} m²  C={d.RunoffCoefficient:F1}  Q={d.FlowRate:F3} lt/s");

                RainResultText.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                RainResultText.Text = $"Hata: {ex.Message}";
            }
        }

        // --- SEKME 4: KOLON ARAÇLARI ---

        private void CreateSplitColumn_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtLowerBot.Text, out double lBot) ||
                !double.TryParse(TxtLowerTop.Text, out double lTop) ||
                !double.TryParse(TxtUpperBot.Text, out double uBot) ||
                !double.TryParse(TxtUpperTop.Text, out double uTop))
            {
                MessageBox.Show("Lütfen tüm kot değerlerini doğru girin.", "Giriş Hatası",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (lTop > uBot)
            {
                MessageBox.Show("Alt kolon tepe kotu, üst kolon taban kotundan büyük olamaz.", "Geometri Hatası",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Viewport'a bildir — MainWindow koordinat seçimi için komutu tetikler
            CreateSplitColumnRequested?.Invoke(lBot, lTop, uBot, uTop);
            Hide();
        }

        private void ValidateCopySelection_Click(object sender, RoutedEventArgs e)
        {
            ValidateCopySelectionRequested?.Invoke();
        }

        private void FilterAndCopy_Click(object sender, RoutedEventArgs e)
        {
            FilterAndCopyRequested?.Invoke();
        }

        private void PlaceSewerOutlet_Click(object sender, RoutedEventArgs e)
        {
            PlaceOutletRequested?.Invoke(false);
            Hide();
        }

        private void PlaceRainOutlet_Click(object sender, RoutedEventArgs e)
        {
            PlaceOutletRequested?.Invoke(true);
            Hide();
        }

        // --- SEKME 5: TESİSATI KABUL ET ---

        private void AcceptSystem_Click(object sender, RoutedEventArgs e)
        {
            AcceptSystemRequested?.Invoke();

            var log = new System.Text.StringBuilder();
            log.AppendLine($"[{DateTime.Now:HH:mm:ss}] Tesisatı Kabul Et başlatıldı...");

            // Temel doğrulama: veritabanındaki DrainageOutletEntity varlığını kontrol et
            var allEntities = _database.GetAllEntities();

            var wasteOutlets = allEntities.OfType<Afney.Cad.Mechanical.Entities.DrainageOutletEntity>()
                .Where(o => o.SystemType == MechanicalSystemType.WasteWater).ToList();
            var rainOutlets  = allEntities.OfType<Afney.Cad.Mechanical.Entities.DrainageOutletEntity>()
                .Where(o => o.SystemType == MechanicalSystemType.RainWater).ToList();
            var catchments   = allEntities.OfType<Afney.Cad.Mechanical.Entities.RainfallCatchmentEntity>().ToList();

            bool hasErrors = false;

            if (wasteOutlets.Count == 0)
            {
                log.AppendLine("  ✗ HATA: Pis su ağında boşaltma noktası (rögar) bulunamadı!");
                log.AppendLine("         → Kolon Araçları sekmesinden rögar noktası ekleyin.");
                hasErrors = true;
            }
            else
            {
                log.AppendLine($"  ✓ Pis su boşaltma noktaları: {wasteOutlets.Count} adet");
            }

            if (catchments.Count > 0 && rainOutlets.Count == 0)
            {
                log.AppendLine("  ✗ HATA: Yağmur düşme alanı tanımlı ancak boşaltma noktası yok!");
                hasErrors = true;
            }
            else if (rainOutlets.Count > 0)
            {
                log.AppendLine($"  ✓ Yağmur suyu boşaltma noktaları: {rainOutlets.Count} adet");
            }

            if (catchments.Count > 0)
                log.AppendLine($"  ✓ Yağmur düşme alanları: {catchments.Count} adet  ({catchments.Sum(c => c.AreaM2):F1} m² toplam)");

            log.AppendLine();

            if (hasErrors)
            {
                log.AppendLine("══════════════════════════════════════");
                log.AppendLine("SONUÇ: BAŞARISIZ — Yukarıdaki hataları düzeltin.");
                log.AppendLine("══════════════════════════════════════");
                ValidationLog.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                log.AppendLine("══════════════════════════════════════");
                log.AppendLine("SONUÇ: BAŞARILI — Tesisat kabul edildi.");
                log.AppendLine("Hesaplar modülüne geçebilirsiniz.");
                log.AppendLine("══════════════════════════════════════");
                ValidationLog.Foreground = System.Windows.Media.Brushes.LightGreen;
            }

            ValidationLog.Text = log.ToString();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            ValidationLog.Text = "Log temizlendi.";
            ValidationLog.Foreground = System.Windows.Media.Brushes.Gray;
        }

        // ValidationLog mesajı dışarıdan güncellenmek istenirse (MainWindow'dan)
        public void AppendValidationMessage(string message, bool isError = false)
        {
            ValidationLog.Text += $"\n{message}";
            ValidationLog.Foreground = isError
                ? System.Windows.Media.Brushes.OrangeRed
                : System.Windows.Media.Brushes.LightGreen;
        }

        public void SetCopyValidationResult(CopyValidationResult result)
        {
            if (result.IsValid)
            {
                CopyValidationResult.Text =
                    "✓ Seçimde kolon borusu bulunamadı. Kopyalama güvenli.";
                CopyValidationResult.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                CopyValidationResult.Text =
                    $"✗ {result.RiserPipeCount} adet kolon borusu tespit edildi.\n" +
                    $"  'Kolonları Çıkar ve Kopyala' butonu bunları otomatik dışarıda bırakır.";
                CopyValidationResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }
    }
}
