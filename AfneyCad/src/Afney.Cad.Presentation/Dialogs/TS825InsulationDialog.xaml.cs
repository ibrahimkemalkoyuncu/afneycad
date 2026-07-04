using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: TS 825 Isı Yalıtım Hesap Diyaloğu
   NEDEN: Kullanıcının iklim bölgesi, yapı elemanı ve malzeme katmanlarını
          girerek TS 825'e göre U değeri, gerekli yalıtım kalınlığı, ısı kaybı
          ve yıllık ısıtma enerjisini hesaplaması için.
*/
public partial class TS825InsulationDialog : Window
{
    public class LayerRow
    {
        public string Malzeme  { get; set; } = "";
        public string Lambda   { get; set; } = "";
        public string Kalinlik { get; set; } = "";
    }

    private readonly CadDatabase? _database;
    private readonly TS825InsulationService _svc = new();
    private readonly ObservableCollection<LayerRow> _layers = new();
    private TS825InsulationService.InsulationResult? _last;

    public event EventHandler? DrawingChanged;

    public TS825InsulationDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
        LayerGrid.ItemsSource = _layers;
        OnFillSampleLayers(this, new RoutedEventArgs());
        UpdateLimitInfo();
    }

    // ── Enum çözümleme ────────────────────────────────────────────────────────
    private TS825InsulationService.ClimateZone SelectedZone() =>
        (TS825InsulationService.ClimateZone)(CmbZone.SelectedIndex + 1);

    private TS825InsulationService.ElementType SelectedElement() => CmbElement.SelectedIndex switch
    {
        0 => TS825InsulationService.ElementType.DisDuvar,
        1 => TS825InsulationService.ElementType.CatiTeras,
        2 => TS825InsulationService.ElementType.Doseme,
        3 => TS825InsulationService.ElementType.PencereKapi,
        _ => TS825InsulationService.ElementType.DisDuvar
    };

    private void OnZoneOrElementChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isWindow = SelectedElement() == TS825InsulationService.ElementType.PencereKapi;
        if (WindowUPanel != null)
            WindowUPanel.Visibility = isWindow ? Visibility.Visible : Visibility.Collapsed;
        UpdateLimitInfo();
    }

    private void UpdateLimitInfo()
    {
        if (TxtLimitInfo == null) return;
        var zone = SelectedZone();
        var el = SelectedElement();
        double uMax = TS825InsulationService.GetMaxU(zone, el);
        double te = TS825InsulationService.GetDesignOutdoorTemp(zone);
        double dd = TS825InsulationService.GetDegreeDays(zone);
        TxtLimitInfo.Text =
            $"TS 825 sınır U ({TS825InsulationService.ElementName(el)}, {(int)zone}. Bölge): {uMax:F2} W/m²K\n" +
            $"Dış tasarım sıcaklığı: {te:F0} °C · Isıtma derece-gün: {dd:F0}";
    }

    // ── Örnek / temizle ───────────────────────────────────────────────────────
    private void OnFillSampleLayers(object sender, RoutedEventArgs e)
    {
        _layers.Clear();
        // Tipik dış duvar: iç sıva + tuğla + dış sıva (yalıtımsız)
        _layers.Add(new LayerRow { Malzeme = "İç sıva (alçı)",       Lambda = "0.70", Kalinlik = "0.02" });
        _layers.Add(new LayerRow { Malzeme = "Yatay delikli tuğla",  Lambda = "0.45", Kalinlik = "0.19" });
        _layers.Add(new LayerRow { Malzeme = "Dış sıva (çimento)",   Lambda = "1.00", Kalinlik = "0.03" });
    }

    private void OnClearLayers(object sender, RoutedEventArgs e) => _layers.Clear();

    // ── Hesapla ───────────────────────────────────────────────────────────────
    private void OnCalculate(object sender, RoutedEventArgs e)
    {
        try
        {
            var zone = SelectedZone();
            var el = SelectedElement();
            double area = GetDouble(TxtArea.Text, 0);
            double indoor = GetDouble(TxtIndoor.Text, 20);
            double insLambda = GetDouble(TxtInsLambda.Text, 0.035);
            double? outdoor = string.IsNullOrWhiteSpace(TxtOutdoor.Text)
                ? null : GetDouble(TxtOutdoor.Text, 0);

            double directU = 0;
            var layers = _layers
                .Where(r => !string.IsNullOrWhiteSpace(r.Malzeme))
                .Select(r => new TS825InsulationService.Layer(
                    r.Malzeme, GetDouble(r.Lambda, 0), GetDouble(r.Kalinlik, 0)))
                .ToList();

            if (el == TS825InsulationService.ElementType.PencereKapi)
                directU = GetDouble(TxtWindowU.Text, 0);

            _last = _svc.Calculate(zone, el, area, layers, indoor, outdoor, insLambda, directU);
            RenderResults(_last);
            DrawingChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "TS 825 Hesap Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RenderResults(TS825InsulationService.InsulationResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("── TS 825 ISI YALITIM HESABI ──");
        sb.AppendLine();
        sb.AppendLine($"Bölge      : {(int)r.Zone}. Bölge ({TS825InsulationService.ZoneCity(r.Zone)})");
        sb.AppendLine($"Eleman     : {TS825InsulationService.ElementName(r.Element)}");
        sb.AppendLine($"Alan       : {r.AreaM2:F1} m²");
        sb.AppendLine();
        sb.AppendLine($"Rsi + Rse  : {r.Rsi:F2} + {r.Rse:F2} m²K/W");
        sb.AppendLine($"Katman R   : {r.RLayers:F3} m²K/W");
        sb.AppendLine($"Toplam R   : {r.RTotal:F3} m²K/W");
        sb.AppendLine();
        sb.AppendLine($"Mevcut U   : {r.UCurrent:F3} W/m²K");
        sb.AppendLine($"TS 825 Umax: {r.UMax:F2} W/m²K");
        sb.AppendLine($"Durum      : {(r.MeetsLimit ? "UYGUN" : "!! SINIR ASILDI")}");
        sb.AppendLine();
        if (r.Element != TS825InsulationService.ElementType.PencereKapi)
        {
            sb.AppendLine($"Gerekli yalıtım (λ={r.InsulationLambda:F3}):");
            sb.AppendLine($"   {r.RequiredInsulationThicknessM * 100:F1} cm " +
                          $"({r.RequiredInsulationThicknessM * 1000:F0} mm)");
            sb.AppendLine();
        }
        sb.AppendLine($"İç/Dış T   : {r.IndoorTempC:F0} / {r.OutdoorTempC:F0} °C");
        sb.AppendLine($"Isı kaybı Q: {r.HeatLossW:F0} W  ({r.HeatLossW / 1000.0:F2} kW)");
        sb.AppendLine($"Derece-gün : {r.DegreeDays:F0}");
        sb.AppendLine($"Yıllık E   : {r.AnnualEnergyKwh:F0} kWh/yıl");
        sb.AppendLine();
        sb.AppendLine("── AÇIKLAMALAR ──");
        foreach (var n in r.Notes) sb.AppendLine("• " + n);
        sb.AppendLine();
        sb.AppendLine($"Standart: {r.Standard}");

        TxtResults.Text = sb.ToString();
    }

    // ── HTML rapor ────────────────────────────────────────────────────────────
    private void OnHtmlReport(object sender, RoutedEventArgs e)
    {
        if (_last is null) { OnCalculate(sender, e); if (_last is null) return; }
        var r = _last;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'>");
        sb.AppendLine("<title>TS 825 Isı Yalıtım Raporu — AfneyCAD</title>");
        sb.AppendLine(@"<style>
body{font-family:'Segoe UI',sans-serif;margin:24px;color:#222}
h1{color:#0D47A1;border-bottom:3px solid #0D47A1;padding-bottom:6px}
table{border-collapse:collapse;margin-top:10px}
th,td{border:1px solid #ccc;padding:6px 12px;text-align:left}
th{background:#e8f0fb}
.ok{color:#2E7D32;font-weight:700}.bad{color:#C62828;font-weight:700}
.note{font-size:12px;color:#555;margin:3px 0}
.footer{margin-top:20px;font-size:11px;color:#888}
</style></head><body>");
        sb.AppendLine("<h1>TS 825 Isı Yalıtım ve Isı Kaybı Raporu</h1>");
        sb.AppendLine($"<p>Tarih: <b>{DateTime.Now:dd/MM/yyyy}</b> · Standart: <b>{r.Standard}</b></p>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><th>İklim Bölgesi</th><td>{(int)r.Zone}. Bölge ({TS825InsulationService.ZoneCity(r.Zone)})</td></tr>");
        sb.AppendLine($"<tr><th>Yapı Elemanı</th><td>{TS825InsulationService.ElementName(r.Element)}</td></tr>");
        sb.AppendLine($"<tr><th>Alan</th><td>{r.AreaM2:F1} m²</td></tr>");
        sb.AppendLine($"<tr><th>Toplam Isıl Direnç (R)</th><td>{r.RTotal:F3} m²K/W</td></tr>");
        sb.AppendLine($"<tr><th>Mevcut U</th><td>{r.UCurrent:F3} W/m²K</td></tr>");
        sb.AppendLine($"<tr><th>TS 825 Sınır U (max)</th><td>{r.UMax:F2} W/m²K</td></tr>");
        string durum = r.MeetsLimit ? "<span class='ok'>UYGUN</span>" : "<span class='bad'>SINIR AŞILDI</span>";
        sb.AppendLine($"<tr><th>Durum</th><td>{durum}</td></tr>");
        if (r.Element != TS825InsulationService.ElementType.PencereKapi)
            sb.AppendLine($"<tr><th>Gerekli Yalıtım (λ={r.InsulationLambda:F3})</th><td>{r.RequiredInsulationThicknessM * 100:F1} cm</td></tr>");
        sb.AppendLine($"<tr><th>Isı Kaybı (Q)</th><td>{r.HeatLossW:F0} W</td></tr>");
        sb.AppendLine($"<tr><th>Yıllık Enerji</th><td>{r.AnnualEnergyKwh:F0} kWh/yıl</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("<h3>Açıklamalar</h3>");
        foreach (var n in r.Notes) sb.AppendLine($"<p class='note'>• {System.Net.WebUtility.HtmlEncode(n)}</p>");
        sb.AppendLine("<div class='footer'>AfneyCAD — TS 825 Isı Yalıtım Hesabı (yaklaşık; onay projesi için resmî TS 825 hesap tablosu esastır)</div>");
        sb.AppendLine("</body></html>");

        try
        {
            string path = Path.Combine(Path.GetTempPath(), $"AfneyCAD_TS825_{DateTime.Now:yyyyMMddHHmm}.html");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"HTML rapor oluşturulamadı:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Çizime metin ekle ─────────────────────────────────────────────────────
    private void OnAddToDrawing(object sender, RoutedEventArgs e)
    {
        if (_last is null) { OnCalculate(sender, e); if (_last is null) return; }
        if (_database is null)
        {
            MessageBox.Show("Aktif çizim bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var r = _last;
        string txt =
            $"TS 825 — {TS825InsulationService.ElementName(r.Element)} ({(int)r.Zone}. Bölge)  " +
            $"U={r.UCurrent:F3} / Umax={r.UMax:F2} W/m²K  " +
            $"Yalıtım={r.RequiredInsulationThicknessM * 100:F1} cm  " +
            $"Q={r.HeatLossW:F0} W  E={r.AnnualEnergyKwh:F0} kWh/yıl";

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "TS825_HESAP"
        };
        _database.AddEntity(te);
        DrawingChanged?.Invoke(this, EventArgs.Empty);
        MessageBox.Show("TS 825 hesap özeti çizime eklendi (katman: TS825_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static double GetDouble(string s, double fallback)
        => double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
