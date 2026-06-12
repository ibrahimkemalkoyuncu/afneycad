using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class CoolingDesignDialog
{
    private readonly ObservableCollection<CoolingLoadService.Zone> _zones = [];
    private CoolingLoadService.CoolingResult? _lastResult;

    public CoolingDesignDialog()
    {
        InitializeComponent();

        foreach (var city in CoolingLoadService.CitySummerTemps.Keys.OrderBy(c => c))
            CboCity.Items.Add(new ComboBoxItem { Content = city });
        CboCity.SelectedIndex = 0;

        foreach (var zt in CoolingLoadService.ZoneTypeDefaults.Keys)
            CboZoneType.Items.Add(new ComboBoxItem { Content = zt });
        CboZoneType.SelectedIndex = 0;

        ZoneGrid.ItemsSource = _zones;
    }

    private void City_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (CboCity.SelectedItem is ComboBoxItem item &&
            CoolingLoadService.CitySummerTemps.TryGetValue(item.Content?.ToString() ?? "", out var temps))
        {
            TxtOutdoorDB.Text = temps.DB.ToString(CultureInfo.InvariantCulture);
            TxtOutdoorWB.Text = temps.WB.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void ZoneType_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Seçilen tip bilgisi AddZone_Click'te kullanılır
    }

    private void AddZone_Click(object sender, RoutedEventArgs e)
    {
        string type = (CboZoneType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ofis";
        CoolingLoadService.ZoneTypeDefaults.TryGetValue(type, out var defaults);

        _zones.Add(new CoolingLoadService.Zone
        {
            Name              = $"{type} {_zones.Count + 1}",
            ZoneType          = type,
            FloorAreaM2       = 30,
            HeightM           = 3.0,
            ExternalWallM2    = 12,
            WindowM2          = 4,
            RoofM2            = 0,
            SHGC              = 0.6,
            OccupantCount     = 4,
            ActivityLevel     = defaults.Activity,
            LightingWperm2    = defaults.Lighting > 0 ? defaults.Lighting : 12,
            EquipmentWperm2   = defaults.Equipment > 0 ? defaults.Equipment : 15,
            AirChangesPerHour = defaults.ACH > 0 ? defaults.ACH : 1.0,
            WallFacing        = CoolingLoadService.Orientation.Guney
        });
    }

    private void DeleteZone_Click(object sender, RoutedEventArgs e)
    {
        if (ZoneGrid.SelectedItem is CoolingLoadService.Zone z) _zones.Remove(z);
    }

    private void AddOfficeTemplate_Click(object sender, RoutedEventArgs e)
    {
        _zones.Clear();
        (string name, string type, double area, double h, double wall, double win, double roof, int occ, double light, double equip, double ach, bool shading)[] zones =
        [
            ("Açık Ofis",       "Ofis",            80,  3.0, 20, 12, 0,  20, 12, 20, 1.0, false),
            ("Toplantı Salonu", "Toplantı Salonu",  30,  3.0, 8,  4,  0,  12, 15, 10, 2.0, false),
            ("Müdür Odası",     "Ofis",             20,  3.0, 8,  3,  0,  2,  12, 15, 0.5, false),
            ("Lobi / Koridor",  "Ofis",             25,  3.0, 6,  2,  0,  0,  8,  5,  1.0, false),
            ("Sunucu Odası",    "Ofis",             10,  3.0, 0,  0,  0,  0,  8,  200, 6.0, false),
        ];
        foreach (var (name, type, area, h, wall, win, roof, occ, light, equip, ach, shade) in zones)
            _zones.Add(new CoolingLoadService.Zone
            {
                Name = name, ZoneType = type, FloorAreaM2 = area, HeightM = h,
                ExternalWallM2 = wall, WindowM2 = win, RoofM2 = roof,
                OccupantCount = occ, LightingWperm2 = light, EquipmentWperm2 = equip,
                AirChangesPerHour = ach, HasShading = shade, SHGC = 0.6,
                ActivityLevel = "Ofis Çalışması", WallFacing = CoolingLoadService.Orientation.Guney
            });
        StatusText.Text = "✓ Ofis katı şablonu yüklendi (5 bölge).";
    }

    private void AddResidentialTemplate_Click(object sender, RoutedEventArgs e)
    {
        _zones.Clear();
        (string name, double area, double wall, double win, int occ)[] zones =
        [
            ("Salon",        25, 12, 4, 3),
            ("Yatak Odası 1",14, 8,  2, 2),
            ("Yatak Odası 2",12, 8,  2, 2),
            ("Mutfak",       10, 5,  1, 1),
        ];
        foreach (var (name, area, wall, win, occ) in zones)
            _zones.Add(new CoolingLoadService.Zone
            {
                Name = name, ZoneType = "Oturma Odası", FloorAreaM2 = area, HeightM = 2.8,
                ExternalWallM2 = wall, WindowM2 = win, RoofM2 = 0,
                OccupantCount = occ, LightingWperm2 = 8, EquipmentWperm2 = 5,
                AirChangesPerHour = 0.5, SHGC = 0.6, ActivityLevel = "Oturma / Dinlenme",
                WallFacing = CoolingLoadService.Orientation.Guney
            });
        StatusText.Text = "✓ Konut şablonu yüklendi (4 bölge).";
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ZoneGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_zones.Count == 0) { StatusText.Text = "⚠ En az 1 bölge ekleyin."; return; }

            int peakHour = CboPeakHour.SelectedIndex switch { 0 => 12, 1 => 13, 2 => 14, 4 => 16, _ => 15 };

            var svc = new CoolingLoadService
            {
                OutdoorSummerTempC = ParseDouble(TxtOutdoorDB.Text, 34),
                OutdoorWetBulbC    = ParseDouble(TxtOutdoorWB.Text, 24),
                IndoorTempC        = ParseDouble(TxtIndoorTemp.Text, 24),
                IndoorRH           = ParseDouble(TxtIndoorRH.Text, 50),
                SafetyFactor       = ParseDouble(TxtSafety.Text, 1.15),
                PeakHour           = peakHour
            };

            _lastResult = svc.Calculate(_zones.ToList());

            ResultGrid.ItemsSource = _lastResult.Zones;

            ResTotalKw.Text   = $"{_lastResult.TotalCoolingKw:F2} kW";
            ResTotalTR.Text   = $"{_lastResult.TotalCoolingTR:F2} TR";
            ResChillerKw.Text = $"{_lastResult.ChillerCapacityKw:F1} kW";
            ResSHR.Text       = $"{_lastResult.SensibleHeatRatio:F3}";
            ResWarnings.Text  = _lastResult.WarningCount > 0 ? $"{_lastResult.WarningCount}" : "✓";
            ResChillerModel.Text = _lastResult.RecommendedChiller;
            ResSensLat.Text   = $"{_lastResult.TotalSensibleKw:F2} kW duyulur + {_lastResult.TotalLatentKw:F2} kW gizil";
            ResWarningText.Text  = _lastResult.Warnings.Count > 0
                ? string.Join("\n", _lastResult.Warnings)
                : "Uyarı yok";

            StatusText.Text = _lastResult.Summary;
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) { Calculate_Click(sender, e); if (_lastResult == null) return; }
        try
        {
            string html = BuildHtmlReport();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"SogutmaHesap_{DateTime.Now:yyyyMMdd_HHmm}.html");
            System.IO.File.WriteAllText(path, html, Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            StatusText.Text = $"✓ Rapor açıldı: {path}";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) { Calculate_Click(sender, e); if (_lastResult == null) return; }
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Excel Kaydet", Filter = "Excel|*.xlsx",
                FileName = $"SogutmaHesap_{DateTime.Now:yyyyMMdd}"
            };
            if (dlg.ShowDialog() != true) return;

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Soğutma Hesabı");
            string[] h = ["Bölge", "İletim W", "Güneş W", "İç Duy. W", "İç Gizil W",
                          "Hav.Duy. W", "Top.Duy. W", "Top.Gizil W", "Toplam kW", "TR", "Öneri"];
            for (int c = 0; c < h.Length; c++)
            {
                ws.Cell(1, c + 1).Value = h[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
                ws.Cell(1, c + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0D47A1");
                ws.Cell(1, c + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            }
            int row = 2;
            foreach (var r in _lastResult.Zones)
            {
                ws.Cell(row, 1).Value  = r.Zone.Name;
                ws.Cell(row, 2).Value  = r.TransmissionGainW;
                ws.Cell(row, 3).Value  = r.SolarGainW;
                ws.Cell(row, 4).Value  = r.InternalSensibleW;
                ws.Cell(row, 5).Value  = r.InternalLatentW;
                ws.Cell(row, 6).Value  = r.VentilationSensibleW;
                ws.Cell(row, 7).Value  = r.TotalSensibleW;
                ws.Cell(row, 8).Value  = r.TotalLatentW;
                ws.Cell(row, 9).Value  = r.TotalCoolingKw;
                ws.Cell(row, 10).Value = r.TotalCoolingTR;
                ws.Cell(row, 11).Value = r.RecommendedUnit;
                row++;
            }
            ws.Cell(row + 1, 1).Value  = "TOPLAM";
            ws.Cell(row + 1, 9).Value  = _lastResult.TotalCoolingKw;
            ws.Cell(row + 1, 10).Value = _lastResult.TotalCoolingTR;
            ws.Cell(row + 2, 1).Value  = "Chiller"; ws.Cell(row + 2, 2).Value = _lastResult.RecommendedChiller;
            ws.Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            StatusText.Text = "✓ Excel kaydedildi.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private string BuildHtmlReport()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Soğutma Yük Hesap Föyü</title>");
        sb.Append("<style>body{font-family:Arial;background:#0D1117;color:#ddd;padding:20px}");
        sb.Append("h1{color:#90CAF9}h2{color:#64B5F6}table{border-collapse:collapse;width:100%}");
        sb.Append("th{background:#0D3060;color:#90CAF9;padding:8px;text-align:left}");
        sb.Append("td{padding:6px;border:1px solid #222}tr:nth-child(even){background:#161B22}");
        sb.Append(".box{background:#0A2040;padding:12px;border-radius:4px;margin:10px 0}");
        sb.Append("</style></head><body>");
        sb.Append("<h1>❄️ Soğutma Yük Hesap Föyü</h1>");
        sb.Append($"<p>Tarih: {DateTime.Now:dd.MM.yyyy HH:mm} | Standart: ASHRAE HOF 2021 / TS EN 12831-3</p>");
        sb.Append($"<div class='box'><b>ÖZET:</b> {_lastResult!.Summary}<br/>");
        sb.Append($"Önerilen Soğutma Grubu: {_lastResult.RecommendedChiller}<br/>");
        sb.Append($"Duyulur: {_lastResult.TotalSensibleKw:F2} kW &nbsp;|&nbsp; Gizil: {_lastResult.TotalLatentKw:F2} kW &nbsp;|&nbsp; SHR: {_lastResult.SensibleHeatRatio:F3}</div>");
        sb.Append("<h2>Bölge Bazlı Soğutma Yükleri</h2><table>");
        sb.Append("<tr><th>Bölge</th><th>İletim (W)</th><th>Güneş (W)</th><th>İç Duy. (W)</th><th>İç Gizil (W)</th>");
        sb.Append("<th>Top. Duy. (W)</th><th>Toplam (kW)</th><th>TR</th><th>Öneri</th></tr>");
        foreach (var r in _lastResult.Zones)
            sb.Append($"<tr><td>{r.Zone.Name}</td><td>{r.TransmissionGainW:F0}</td><td>{r.SolarGainW:F0}</td>" +
                      $"<td>{r.InternalSensibleW:F0}</td><td>{r.InternalLatentW:F0}</td>" +
                      $"<td><b>{r.TotalSensibleW:F0}</b></td><td><b>{r.TotalCoolingKw:F2}</b></td>" +
                      $"<td>{r.TotalCoolingTR:F2}</td><td>{r.RecommendedUnit}</td></tr>");
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
