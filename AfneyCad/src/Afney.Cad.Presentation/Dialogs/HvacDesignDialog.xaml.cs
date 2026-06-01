using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class HvacDesignDialog
{
    private readonly ObservableCollection<DuctSizingService.Zone> _zones = [];
    private DuctSizingService.HvacResult? _lastResult;

    // View-model için ek property
    private class SegmentRow
    {
        public DuctSizingService.DuctSegment S { get; set; } = null!;
        public string ZoneName        => S.ZoneName;
        public double AirFlowM3h      => S.AirFlowM3h;
        public double VelocityMs      => S.VelocityMs;
        public double DiameterMm      => S.DiameterMm;
        public string RectLabel       => S.WidthMm != S.DiameterMm ? $"{S.WidthMm:F0}×{S.HeightMm:F0}" : "—";
        public double FrictionPaPer1m => S.FrictionPaPer1m;
        public string Note            => S.Note;
    }

    public HvacDesignDialog()
    {
        InitializeComponent();

        foreach (var t in DuctSizingService.DefaultAirChanges.Keys)
            CboZoneType.Items.Add(new ComboBoxItem { Content = t });
        CboZoneType.SelectedIndex = 0;

        ZoneGrid.ItemsSource = _zones;
    }

    private void AddZone_Click(object sender, RoutedEventArgs e)
    {
        string type = (CboZoneType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ofis";
        DuctSizingService.DefaultAirChanges.TryGetValue(type, out double n);
        _zones.Add(new DuctSizingService.Zone { Name = $"{type} {_zones.Count + 1}", ZoneType = type, FloorAreaM2 = 20, HeightM = 3.0, AirChanges = n > 0 ? n : 4 });
    }

    private void DeleteZone_Click(object sender, RoutedEventArgs e)
    {
        if (ZoneGrid.SelectedItem is DuctSizingService.Zone z) _zones.Remove(z);
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        _zones.Clear();
        string[,] t = {
            {"Genel Ofis",     "Ofis",      "150", "3.0", "5"},
            {"Toplantı Odası", "Toplantı",  "30",  "3.0", "8"},
            {"Yemekhane",      "Yemekhane", "50",  "3.0", "8"},
            {"Koridor",        "Koridor",   "40",  "3.0", "2"},
            {"WC",             "WC",        "20",  "3.0", "10"},
        };
        for (int i = 0; i < t.GetLength(0); i++)
            _zones.Add(new DuctSizingService.Zone { Name = t[i,0], ZoneType = t[i,1],
                FloorAreaM2 = double.Parse(t[i,2], CultureInfo.InvariantCulture),
                HeightM     = double.Parse(t[i,3], CultureInfo.InvariantCulture),
                AirChanges  = double.Parse(t[i,4], CultureInfo.InvariantCulture) });
        StatusText.Text = "✓ Ofis şablonu yüklendi.";
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ZoneGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_zones.Count == 0) { StatusText.Text = "⚠ En az 1 zon ekleyin."; return; }

            var svc = new DuctSizingService
            {
                MaxVelocityMainMs   = ParseDouble(TxtMaxV.Text, 6),
                MaxVelocityBranchMs = ParseDouble(TxtBranchV.Text, 4)
            };
            bool rect = (CboDuctType.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Dikdörtgen";
            double len = ParseDouble(TxtLength.Text, 20);

            _lastResult = svc.Calculate(_zones.ToList(), rect, len);

            ResultGrid.ItemsSource = _lastResult.Segments.Select(s => new SegmentRow { S = s }).ToList();
            SummaryText.Text = _lastResult.Summary + (_lastResult.Warnings.Count > 0 ? $"\n⚠ {string.Join("; ", _lastResult.Warnings)}" : "");
            StatusText.Text = $"✓ {_lastResult.Segments.Count} kanal segmenti hesaplandı | Fan: {_lastResult.FanPowerW:F0} W";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) { Calculate_Click(sender, e); if (_lastResult == null) return; }
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Excel Kaydet", Filter = "Excel|*.xlsx", FileName = $"HvacHesap_{DateTime.Now:yyyyMMdd}" };
            if (dlg.ShowDialog() != true) return;
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("HVAC");
            string[] h = ["Zon", "Q (m³/h)", "v (m/s)", "Ø (mm)", "G×Y", "ΔP Pa/m", "Not"];
            for (int c = 0; c < h.Length; c++) { ws.Cell(1, c+1).Value = h[c]; ws.Cell(1, c+1).Style.Font.Bold = true; ws.Cell(1, c+1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#006064"); ws.Cell(1, c+1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White; }
            int row = 2;
            foreach (var s in _lastResult.Segments)
            {
                ws.Cell(row, 1).Value = s.ZoneName; ws.Cell(row, 2).Value = s.AirFlowM3h; ws.Cell(row, 3).Value = s.VelocityMs;
                ws.Cell(row, 4).Value = s.DiameterMm; ws.Cell(row, 5).Value = $"{s.WidthMm:F0}×{s.HeightMm:F0}";
                ws.Cell(row, 6).Value = s.FrictionPaPer1m; ws.Cell(row, 7).Value = s.Note; row++;
            }
            ws.Cell(row+1, 1).Value = "ÖZET"; ws.Cell(row+1, 2).Value = _lastResult.Summary;
            ws.Cell(row+2, 1).Value = "Fan Önerisi"; ws.Cell(row+2, 2).Value = _lastResult.RecommendedFan;
            ws.Columns().AdjustToContents(); wb.SaveAs(dlg.FileName);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            StatusText.Text = "✓ Excel kaydedildi.";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
