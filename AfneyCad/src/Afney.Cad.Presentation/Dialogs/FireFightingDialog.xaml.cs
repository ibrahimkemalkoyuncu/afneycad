using System;
using System.IO;
using System.Text;
using System.Windows;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class FireFightingDialog : Window
    {
        private readonly FireFightingService _svc = new();
        private FireFightingService.SprinklerDesignResult?   _sprinklerResult;
        private FireFightingService.HydrantDesignResult?     _hydrantResult;
        private FireFightingService.HoseReelDesignResult?    _reelResult;
        private FireFightingService.WaterSupplyAnalysisResult? _wsResult;

        public FireFightingDialog() => InitializeComponent();

        // ── SPRİNKLER ─────────────────────────────────────────────────────────
        private void SprinklerCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = new FireFightingService.SprinklerDesignInput
                {
                    ProtectedAreaM2       = GetDouble(AreaInput.Text,    500),
                    CeilingHeightM        = GetDouble(HeightInput.Text,  3.0),
                    FloorToSystemPressure = GetDouble(PressureInput.Text, 0),
                    IsWetSystem           = WetSystemCheck.IsChecked == true,
                    Hazard = HazardCombo.SelectedIndex switch
                    {
                        1 => FireFightingService.EN12845HazardClass.OrdinaryHazard_1,
                        2 => FireFightingService.EN12845HazardClass.OrdinaryHazard_2,
                        3 => FireFightingService.EN12845HazardClass.ExtraHazard,
                        _ => FireFightingService.EN12845HazardClass.LightHazard
                    }
                };

                _sprinklerResult = _svc.DesignSprinklerSystem(input);

                SprinklerResultText.Text =
                    $"━━━ SPRİNKLER SİSTEMİ ━━━\n" +
                    $"Tehlike Sınıfı      : {input.Hazard}\n" +
                    $"Sistem Tipi         : {(input.IsWetSystem ? "Islak (Wet Pipe)" : "Kuru (Dry Pipe)")}\n\n" +
                    $"━━━ BAŞLIK YERLEŞİMİ ━━━\n" +
                    $"Sprinkler Sayısı    : {_sprinklerResult.SprinklerCount} adet\n" +
                    $"Kapsama (başlık)    : {_sprinklerResult.CoverageAreaPerHead:F1} m²\n" +
                    $"Maks. Aralık        : {_sprinklerResult.MaxSpacing:F1} m\n" +
                    $"Tasarım Yoğunluğu  : {_sprinklerResult.DesignDensity:F2} mm/min\n" +
                    $"Tasarım Alanı       : {_sprinklerResult.DesignAreaM2:F0} m²\n\n" +
                    $"━━━ HİDROLİK ━━━\n" +
                    $"Gerekli Debi        : {_sprinklerResult.RequiredFlowLpm:F0} lt/dk\n" +
                    $"Gerekli Basınç      : {_sprinklerResult.RequiredPressureBar:F2} bar\n" +
                    $"Ana Boru            : DN{_sprinklerResult.MainPipeDN:F0}\n" +
                    $"Branş Boru          : DN{_sprinklerResult.BranchPipeDN:F0}\n\n" +
                    $"━━━ POMPA & DEPO ━━━\n" +
                    $"Pompa Kapasitesi    : {_sprinklerResult.PumpCapacityLpm:F0} lt/dk\n" +
                    $"Pompa Basma Yüks.   : {_sprinklerResult.PumpHeadM:F1} mSS\n" +
                    $"Su Deposu (30 dk)   : {_sprinklerResult.WaterTankVolumeM3:F1} m³\n\n" +
                    $"━━━ NOTLAR ━━━\n" +
                    string.Join("\n", _sprinklerResult.Notes) + "\n\n" +
                    $"Standart: {_sprinklerResult.Standard}";

                // Su talebi sekmesine otomatik aktar
                WsDemandSprinkler.Text = _sprinklerResult.RequiredFlowLpm.ToString("F0");
            }
            catch (Exception ex)
            {
                SprinklerResultText.Text = $"Hata: {ex.Message}";
            }
        }

        // ── HİDRANT ───────────────────────────────────────────────────────────
        private void HydrantCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = new FireFightingService.HydrantSystemInput
                {
                    BuildingAreaM2        = GetDouble(HydBuildingArea.Text,    1500),
                    NumberOfFloors        = GetInt(HydFloors.Text,               8),
                    FloorHeightM          = GetDouble(HydFloorHeight.Text,     3.5),
                    AvailablePressureBar  = GetDouble(HydAvailPressure.Text,   3.5),
                    HydrantType           = HydTypeCombo.SelectedIndex == 0
                        ? FireFightingService.HydrantType.Indoor
                        : FireFightingService.HydrantType.Outdoor,
                    Hazard = HydHazardCombo.SelectedIndex switch
                    {
                        1 => FireFightingService.EN12845HazardClass.OrdinaryHazard_1,
                        2 => FireFightingService.EN12845HazardClass.OrdinaryHazard_2,
                        3 => FireFightingService.EN12845HazardClass.ExtraHazard,
                        _ => FireFightingService.EN12845HazardClass.LightHazard
                    }
                };

                _hydrantResult = _svc.DesignHydrantSystem(input);

                HydrantResultText.Text =
                    $"━━━ HİDRANT SİSTEMİ ━━━\n" +
                    $"Tip                 : {(input.HydrantType == FireFightingService.HydrantType.Indoor ? "İç Hidrant (DN52)" : "Dış Hidrant (DN100)")}\n" +
                    $"Eş Zamanlı Sayı     : {_hydrantResult.SimultaneousCount:F0} hidrant\n\n" +
                    $"━━━ KAPASITE ━━━\n" +
                    $"Toplam Hidrant Sayısı: {_hydrantResult.HydrantCount} adet\n" +
                    $"Tekil Hidrant Debisi : {_hydrantResult.HydrantFlowLpm:F0} lt/dk\n" +
                    $"Toplam Debi          : {_hydrantResult.TotalFlowLpm:F0} lt/dk\n" +
                    $"Hortum Çapı          : DN{_hydrantResult.HoseDiameterMm:F0}\n\n" +
                    $"━━━ HİDROLİK ━━━\n" +
                    $"Riser Boru           : DN{_hydrantResult.RisserPipeDn:F0}\n" +
                    $"Gerekli Basınç       : {_hydrantResult.RequiredPressureBar:F2} bar\n\n" +
                    $"━━━ POMPA & DEPO ━━━\n" +
                    $"Pompa Kapasitesi     : {_hydrantResult.PumpCapacityLpm:F0} lt/dk\n" +
                    $"Pompa Basma Yüks.    : {_hydrantResult.PumpHeadMss:F1} mSS\n" +
                    $"Su Deposu (45 dk)    : {_hydrantResult.WaterTankM3:F0} m³\n\n" +
                    $"━━━ NOTLAR ━━━\n" +
                    string.Join("\n", _hydrantResult.Notes) + "\n\n" +
                    $"Standart: {_hydrantResult.Standard}";

                WsDemandHydrant.Text = _hydrantResult.TotalFlowLpm.ToString("F0");
            }
            catch (Exception ex)
            {
                HydrantResultText.Text = $"Hata: {ex.Message}";
            }
        }

        // ── HORTUM MAKARASI ────────────────────────────────────────────────────
        private void HoseReelCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double area  = GetDouble(ReelFloorArea.Text, 800);
                int    floors = GetInt(ReelFloors.Text, 8);

                _reelResult = _svc.DesignHoseReels(area, floors);

                HoseReelResultText.Text =
                    $"━━━ HORTUM MAKARASI ━━━\n" +
                    $"Toplam Makara Sayısı : {_reelResult.ReelCount} adet\n" +
                    $"Hortum Uzunluğu      : {_reelResult.HoseLength:F0} m\n" +
                    $"Kapsama Yarıçapı     : {_reelResult.CoverageRadiusM:F0} m\n\n" +
                    $"━━━ HİDROLİK ━━━\n" +
                    $"Tekil Debi           : {_reelResult.FlowPerReelLpm:F0} lt/dk\n" +
                    $"Çalışma Basıncı      : {_reelResult.WorkingPressureBar:F1} bar (min.)\n" +
                    $"Boru Çapı            : DN{_reelResult.PipeDn:F0}\n\n" +
                    $"━━━ NOTLAR ━━━\n" +
                    string.Join("\n", _reelResult.Notes) + "\n\n" +
                    $"Standart: {_reelResult.Standard}";

                // Eş zamanlı 2 makara debi olarak aktar
                WsDemandReel.Text = (_reelResult.FlowPerReelLpm * 2).ToString("F0");
            }
            catch (Exception ex)
            {
                HoseReelResultText.Text = $"Hata: {ex.Message}";
            }
        }

        // ── SU TEMİNİ ANALİZİ ─────────────────────────────────────────────────
        private void WaterSupplyAnalyze_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = new FireFightingService.WaterSupplyAnalysisInput
                {
                    SprinklerFlowLpm     = GetDouble(WsDemandSprinkler.Text,  0),
                    HydrantFlowLpm       = GetDouble(WsDemandHydrant.Text,    0),
                    HoseReelFlowLpm      = GetDouble(WsDemandReel.Text,       0),
                    AvailableFlowLpm     = GetDouble(WsSupplyFlow.Text,    1000),
                    AvailablePressureBar = GetDouble(WsSupplyPressure.Text,  3.5),
                    HasBoosterPump       = WsHasBooster.IsChecked == true,
                    BoosterPumpCapLpm    = GetDouble(WsBoosterFlow.Text,      0),
                    BoosterPumpHeadBar   = GetDouble(WsBoosterHead.Text,      0)
                };

                _wsResult = _svc.AnalyzeWaterSupply(input);

                string status = _wsResult.IsAdequate ? "✓ YETERLİ" : "⚠ YETERSİZ";
                string color  = _wsResult.IsAdequate ? "YETERLİ" : "YETERSİZ";

                WaterSupplyResultText.Text =
                    $"━━━ SU TALEBİ ANALİZİ ━━━\n\n" +
                    $"GENEL DURUM: {status}\n\n" +
                    $"━━━ TALEP ━━━\n" +
                    $"Sprinkler           : {input.SprinklerFlowLpm:F0} lt/dk\n" +
                    $"Hidrant             : {input.HydrantFlowLpm:F0} lt/dk\n" +
                    $"Hortum Makarası     : {input.HoseReelFlowLpm:F0} lt/dk\n" +
                    $"TOPLAM TALEP        : {_wsResult.TotalDemandLpm:F0} lt/dk\n\n" +
                    $"━━━ ARZ ━━━\n" +
                    $"Şebeke Kapasitesi   : {input.AvailableFlowLpm:F0} lt/dk\n" +
                    $"Şebeke Basıncı      : {input.AvailablePressureBar:F1} bar\n" +
                    (input.HasBoosterPump
                        ? $"Güçlendirme Pompası : {input.BoosterPumpCapLpm:F0} lt/dk\n"
                        : "") +
                    $"TOPLAM ARZ          : {_wsResult.TotalSupplyLpm:F0} lt/dk\n\n" +
                    $"━━━ DENGE ━━━\n" +
                    $"Marj                : {_wsResult.FlowMarginLpm:+0.0;-0.0;0} lt/dk ({_wsResult.FlowMarginPct:+0.0;-0.0;0}%)\n" +
                    $"Yangın Suyu Deposu  : min {_wsResult.ReservoirVolumeM3:F0} m³ (60 dk)\n\n" +
                    $"━━━ ÖNERİLER ━━━\n" +
                    string.Join("\n", _wsResult.Recommendations);
            }
            catch (Exception ex)
            {
                WaterSupplyResultText.Text = $"Hata: {ex.Message}";
            }
        }

        // ── HTML RAPOR ─────────────────────────────────────────────────────────
        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_sprinklerResult is null && _hydrantResult is null && _reelResult is null)
            {
                MessageBox.Show("Önce en az bir hesap yapın.", "Uyarı");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
                sb.AppendLine("<title>Yangın Söndürme Raporu — AfneyCAD</title>");
                sb.AppendLine("<style>body{font-family:Consolas,monospace;background:#1a1a2e;color:#eee;padding:24px;max-width:900px}");
                sb.AppendLine("h1{color:#EF5350}h2{color:#42A5F5;border-bottom:1px solid #333;padding-bottom:6px}");
                sb.AppendLine("table{border-collapse:collapse;width:100%;margin:12px 0}");
                sb.AppendLine("th{background:#B71C1C;color:white;padding:7px 12px;text-align:left}");
                sb.AppendLine("td{padding:6px 12px;border-bottom:1px solid #333}");
                sb.AppendLine("tr:nth-child(even){background:#252540}.ok{color:#66BB6A}.warn{color:#FF7043}</style></head><body>");
                sb.AppendLine("<h1>🔥 YANGIN SÖNDÜRME SİSTEMİ RAPORU</h1>");
                sb.AppendLine($"<p style='color:#888'>AfneyCAD — Standart: NFPA 13 / TS EN 12845 / TS EN 671-1/2 / BKY<br/>Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");

                if (_sprinklerResult is not null)
                {
                    sb.AppendLine("<h2>🔴 Sprinkler Sistemi</h2>");
                    sb.AppendLine("<table><tr><th>Parametre</th><th>Değer</th></tr>");
                    sb.AppendLine($"<tr><td>Sprinkler Sayısı</td><td>{_sprinklerResult.SprinklerCount} adet</td></tr>");
                    sb.AppendLine($"<tr><td>Tasarım Yoğunluğu</td><td>{_sprinklerResult.DesignDensity:F2} mm/min</td></tr>");
                    sb.AppendLine($"<tr><td>Gerekli Debi</td><td>{_sprinklerResult.RequiredFlowLpm:F0} lt/dk</td></tr>");
                    sb.AppendLine($"<tr><td>Gerekli Basınç</td><td>{_sprinklerResult.RequiredPressureBar:F2} bar</td></tr>");
                    sb.AppendLine($"<tr><td>Ana Boru</td><td>DN{_sprinklerResult.MainPipeDN:F0}</td></tr>");
                    sb.AppendLine($"<tr><td>Pompa Kapasitesi</td><td>{_sprinklerResult.PumpCapacityLpm:F0} lt/dk</td></tr>");
                    sb.AppendLine($"<tr><td>Su Deposu</td><td>{_sprinklerResult.WaterTankVolumeM3:F1} m³</td></tr>");
                    sb.AppendLine("</table>");
                    foreach (var n in _sprinklerResult.Notes)
                        sb.AppendLine($"<p class='{(n.StartsWith("⚠") ? "warn" : "ok")}'>{n}</p>");
                }

                if (_hydrantResult is not null)
                {
                    sb.AppendLine("<h2>🔵 Hidrant Sistemi</h2>");
                    sb.AppendLine("<table><tr><th>Parametre</th><th>Değer</th></tr>");
                    sb.AppendLine($"<tr><td>Hidrant Sayısı</td><td>{_hydrantResult.HydrantCount} adet</td></tr>");
                    sb.AppendLine($"<tr><td>Eş Zamanlı Çalışma</td><td>{_hydrantResult.SimultaneousCount:F0} hidrant</td></tr>");
                    sb.AppendLine($"<tr><td>Toplam Debi</td><td>{_hydrantResult.TotalFlowLpm:F0} lt/dk</td></tr>");
                    sb.AppendLine($"<tr><td>Riser DN</td><td>DN{_hydrantResult.RisserPipeDn:F0}</td></tr>");
                    sb.AppendLine($"<tr><td>Gerekli Basınç</td><td>{_hydrantResult.RequiredPressureBar:F2} bar</td></tr>");
                    sb.AppendLine($"<tr><td>Su Deposu (45 dk)</td><td>{_hydrantResult.WaterTankM3:F0} m³</td></tr>");
                    sb.AppendLine("</table>");
                    foreach (var n in _hydrantResult.Notes)
                        sb.AppendLine($"<p class='{(n.StartsWith("⚠") ? "warn" : "ok")}'>{n}</p>");
                }

                if (_reelResult is not null)
                {
                    sb.AppendLine("<h2>🟠 Hortum Makarası</h2>");
                    sb.AppendLine("<table><tr><th>Parametre</th><th>Değer</th></tr>");
                    sb.AppendLine($"<tr><td>Makara Sayısı</td><td>{_reelResult.ReelCount} adet</td></tr>");
                    sb.AppendLine($"<tr><td>Tekil Debi</td><td>{_reelResult.FlowPerReelLpm:F0} lt/dk @ {_reelResult.WorkingPressureBar:F1} bar</td></tr>");
                    sb.AppendLine($"<tr><td>Kapsama Yarıçapı</td><td>{_reelResult.CoverageRadiusM:F0} m</td></tr>");
                    sb.AppendLine($"<tr><td>Boru DN</td><td>DN{_reelResult.PipeDn:F0}</td></tr>");
                    sb.AppendLine("</table>");
                    foreach (var n in _reelResult.Notes)
                        sb.AppendLine($"<p class='ok'>{n}</p>");
                }

                if (_wsResult is not null)
                {
                    sb.AppendLine("<h2>💧 Su Talebi Analizi</h2>");
                    string cls = _wsResult.IsAdequate ? "ok" : "warn";
                    sb.AppendLine($"<p class='{cls}'><strong>Genel Durum: {(_wsResult.IsAdequate ? "✓ YETERLİ" : "⚠ YETERSİZ")}</strong></p>");
                    sb.AppendLine($"<p>Toplam Talep: {_wsResult.TotalDemandLpm:F0} lt/dk | Toplam Arz: {_wsResult.TotalSupplyLpm:F0} lt/dk | Marj: {_wsResult.FlowMarginPct:+0.0;-0.0;0}%</p>");
                    sb.AppendLine($"<p>Yangın Suyu Deposu: min <strong>{_wsResult.ReservoirVolumeM3:F0} m³</strong> (60 dk operasyon)</p>");
                }

                sb.AppendLine("</body></html>");

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "HTML Rapor Kaydet",
                    Filter     = "HTML (*.html)|*.html",
                    FileName   = $"YanginSondurme_{DateTime.Now:yyyyMMdd_HHmm}.html",
                    DefaultExt = ".html"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
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

        // ── YARDIMCI ──────────────────────────────────────────────────────────
        private static double GetDouble(string s, double fallback)
            => double.TryParse(s.Replace(',', '.'),
               System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;

        private static int GetInt(string s, int fallback)
            => int.TryParse(s, out int v) ? v : fallback;
    }
}
