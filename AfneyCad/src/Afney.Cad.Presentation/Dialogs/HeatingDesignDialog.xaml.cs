using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class HeatingDesignDialog
{
    private readonly ObservableCollection<HeatingSystemService.Room> _rooms = [];
    private HeatingSystemService.HeatingResult? _lastResult;

    public HeatingDesignDialog()
    {
        InitializeComponent();

        // Şehirler
        foreach (var city in HeatingSystemService.CityDesignTemps.Keys.OrderBy(c => c))
            CboCity.Items.Add(new ComboBoxItem { Content = city });
        CboCity.SelectedIndex = 0;

        // Oda tipleri
        foreach (var type in HeatingSystemService.RoomDesignTemps.Keys)
            CboRoomType.Items.Add(new ComboBoxItem { Content = type });
        CboRoomType.SelectedIndex = 0;

        RoomGrid.ItemsSource = _rooms;
    }

    // ── Şehir Seçimi ─────────────────────────────────────────────────────────────

    private void City_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (CboCity.SelectedItem is ComboBoxItem item &&
            HeatingSystemService.CityDesignTemps.TryGetValue(item.Content?.ToString() ?? "", out double t))
            TxtOutdoorTemp.Text = t.ToString(CultureInfo.InvariantCulture);
    }

    // ── Oda Ekleme ────────────────────────────────────────────────────────────────

    private void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        string type = (CboRoomType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Oturma Odası";
        HeatingSystemService.RoomDesignTemps.TryGetValue(type, out double designTemp);
        _rooms.Add(new HeatingSystemService.Room
        {
            Name             = $"{type} {_rooms.Count + 1}",
            RoomType         = type,
            DesignTempC      = designTemp > 0 ? designTemp : 22,
            FloorAreaM2      = 15,
            HeightM          = 2.8,
            ExternalWallM2   = 8,
            WindowM2         = 2,
            ExternalRoofM2   = 0,
            PartitionWallM2  = 10
        });
    }

    private void DeleteRoom_Click(object sender, RoutedEventArgs e)
    {
        if (RoomGrid.SelectedItem is HeatingSystemService.Room room) _rooms.Remove(room);
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        // Standart 3+1 konut şablonu
        string[,] rooms = {
            {"Salon",       "Oturma Odası", "22", "25", "2.8", "12", "4",   "15"},
            {"Yatak Odası 1","Yatak Odası", "20", "14", "2.8", "8",  "2",   "12"},
            {"Yatak Odası 2","Yatak Odası", "20", "12", "2.8", "8",  "2",   "10"},
            {"Mutfak",       "Mutfak",      "20", "10", "2.8", "5",  "1.5", "8" },
            {"Banyo",        "Banyo",       "24", "5",  "2.8", "4",  "0.5", "5" },
            {"Hol",          "Hol / Koridor","18","8",  "2.8", "3",  "1",   "10"},
        };
        _rooms.Clear();
        for (int i = 0; i < rooms.GetLength(0); i++)
        {
            HeatingSystemService.RoomDesignTemps.TryGetValue(rooms[i, 1], out double dt);
            _rooms.Add(new HeatingSystemService.Room
            {
                Name             = rooms[i, 0],
                RoomType         = rooms[i, 1],
                DesignTempC      = double.Parse(rooms[i, 2], CultureInfo.InvariantCulture),
                FloorAreaM2      = double.Parse(rooms[i, 3], CultureInfo.InvariantCulture),
                HeightM          = double.Parse(rooms[i, 4], CultureInfo.InvariantCulture),
                ExternalWallM2   = double.Parse(rooms[i, 5], CultureInfo.InvariantCulture),
                WindowM2         = double.Parse(rooms[i, 6], CultureInfo.InvariantCulture),
                PartitionWallM2  = double.Parse(rooms[i, 7], CultureInfo.InvariantCulture),
            });
        }
        StatusText.Text = "✓ 3+1 konut şablonu yüklendi.";
    }

    // ── Hesaplama ────────────────────────────────────────────────────────────────

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RoomGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_rooms.Count == 0) { StatusText.Text = "⚠ En az 1 oda ekleyin."; return; }

            var svc = new HeatingSystemService
            {
                OutdoorDesignTempC = ParseDouble(TxtOutdoorTemp.Text, -12),
                SupplyTempC        = ParseDouble(TxtSupplyTemp.Text, 80),
                ReturnTempC        = ParseDouble(TxtReturnTemp.Text, 60),
                SafetyFactor       = ParseDouble(TxtSafety.Text, 1.2)
            };

            _lastResult = svc.Calculate(_rooms.ToList());

            // Sonuç grid
            ResultGrid.ItemsSource = _lastResult.Rooms;

            // Özet
            ResTotalKw.Text    = $"{_lastResult.TotalHeatKw:F2} kW";
            ResBoilerKw.Text   = $"{_lastResult.BoilerCapacityKw:F1} kW";
            ResFlowM3h.Text    = $"{_lastResult.SystemFlowM3h:F3} m³/h";
            ResWarnings.Text   = _lastResult.WarningCount > 0 ? $"{_lastResult.WarningCount}" : "✓";
            ResBoilerModel.Text = _lastResult.RecommendedBoiler;
            ResPumpModel.Text  = _lastResult.RecommendedPump;
            ResWarningText.Text = _lastResult.Warnings.Count > 0
                ? string.Join("\n", _lastResult.Warnings)
                : "Uyarı yok";

            StatusText.Text = _lastResult.Summary;
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    // ── Dışa Aktarma ─────────────────────────────────────────────────────────────

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) { Calculate_Click(sender, e); if (_lastResult == null) return; }
        try
        {
            string html = BuildHtmlReport();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"IsitmaHesap_{DateTime.Now:yyyyMMdd_HHmm}.html");
            System.IO.File.WriteAllText(path, html, System.Text.Encoding.UTF8);
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
                FileName = $"IsitmaHesap_{DateTime.Now:yyyyMMdd}"
            };
            if (dlg.ShowDialog() != true) return;

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Isıtma Hesabı");
            string[] h = ["Oda", "İletim W", "Hav. W", "Toplam W", "Toplam kW", "Radyatör", "Debi m³/h", "DN"];
            for (int c = 0; c < h.Length; c++)
            {
                ws.Cell(1, c + 1).Value = h[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
                ws.Cell(1, c + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#BF360C");
                ws.Cell(1, c + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            }
            int row = 2;
            foreach (var r in _lastResult.Rooms)
            {
                ws.Cell(row, 1).Value = r.Room.Name;
                ws.Cell(row, 2).Value = r.TransmissionLossW;
                ws.Cell(row, 3).Value = r.VentilationLossW;
                ws.Cell(row, 4).Value = r.TotalHeatLossW;
                ws.Cell(row, 5).Value = r.TotalHeatLossKw;
                ws.Cell(row, 6).Value = r.Radiator?.Model ?? "—";
                ws.Cell(row, 7).Value = r.RequiredFlowM3h;
                ws.Cell(row, 8).Value = r.RecommendedDN;
                row++;
            }
            // Özet satırı
            ws.Cell(row + 1, 1).Value = "TOPLAM";
            ws.Cell(row + 1, 5).Value = _lastResult.TotalHeatKw;
            ws.Cell(row + 2, 1).Value = "Kazan"; ws.Cell(row + 2, 2).Value = _lastResult.RecommendedBoiler;
            ws.Cell(row + 3, 1).Value = "Pompa"; ws.Cell(row + 3, 2).Value = _lastResult.RecommendedPump;
            ws.Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            StatusText.Text = $"✓ Excel kaydedildi.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── HTML Rapor ────────────────────────────────────────────────────────────────

    private string BuildHtmlReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Isıtma Hesap Föyü</title>");
        sb.Append("<style>body{font-family:Arial;background:#1e1e2e;color:#ddd;padding:20px}");
        sb.Append("h1{color:#FFCC80}h2{color:#FF9966}table{border-collapse:collapse;width:100%}");
        sb.Append("th{background:#5d1a00;color:#FFCC80;padding:8px}td{padding:6px;border:1px solid #333}");
        sb.Append("tr:nth-child(even){background:#252535}.box{background:#1b2b3b;padding:12px;border-radius:4px;margin:10px 0}");
        sb.Append("</style></head><body>");
        sb.Append("<h1>🔥 Isıtma Tesisat Hesap Föyü</h1>");
        sb.Append($"<p>Tarih: {DateTime.Now:dd.MM.yyyy HH:mm} | Standart: TS 825 / TS EN 12831</p>");
        sb.Append($"<div class='box'><b>ÖZET:</b> {_lastResult!.Summary}<br/>");
        sb.Append($"Kazan: {_lastResult.RecommendedBoiler}<br/>Pompa: {_lastResult.RecommendedPump}</div>");
        sb.Append("<h2>Oda Bazlı Isı Kaybı</h2><table>");
        sb.Append("<tr><th>Oda</th><th>İletim (W)</th><th>Hav. (W)</th><th>Toplam (W)</th><th>Toplam (kW)</th><th>Radyatör</th><th>DN</th></tr>");
        foreach (var r in _lastResult.Rooms)
            sb.Append($"<tr><td>{r.Room.Name}</td><td>{r.TransmissionLossW:F0}</td><td>{r.VentilationLossW:F0}</td>" +
                      $"<td><b>{r.TotalHeatLossW:F0}</b></td><td>{r.TotalHeatLossKw:F3}</td>" +
                      $"<td>{r.Radiator?.Model}</td><td>DN {r.RecommendedDN:F0}</td></tr>");
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
