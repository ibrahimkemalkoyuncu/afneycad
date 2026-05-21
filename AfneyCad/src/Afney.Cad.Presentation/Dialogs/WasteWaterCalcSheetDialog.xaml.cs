using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class WasteWaterCalcSheetDialog : Window
{
    // ── BOM row ───────────────────────────────────────────────────────────────
    public class BomRow
    {
        public string Material { get; set; } = "";
        public string Dn       { get; set; } = "";
        public string Length   { get; set; } = "";
        public string Count    { get; set; } = "";
        public string Unit     { get; set; } = "";
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly CadDatabase _database;
    private readonly WasteWaterCalcSheetService _svc = new();
    private WasteWaterCalcSheetService.CalcSheetResult? _lastResult;
    private readonly ObservableCollection<WasteWaterCalcSheetService.CalcRow> _calcRows = [];
    private readonly ObservableCollection<BomRow> _bomRows = [];

    // ── Constructor ───────────────────────────────────────────────────────────
    public WasteWaterCalcSheetDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        CalcGrid.ItemsSource = _calcRows;
        BomGrid.ItemsSource  = _bomRows;
        LoadReferenceTable();
    }

    // ── Reference table (TS EN 12056-2) ──────────────────────────────────────
    private void LoadReferenceTable()
    {
        var refData = new[]
        {
            new { DN = "50",  MinSlope = "2.50", MaxQ = "0.80", Usage = "Branşman — lavabo, duş" },
            new { DN = "75",  MinSlope = "1.50", MaxQ = "2.00", Usage = "Branşman — WC (sifon öncesi)" },
            new { DN = "100", MinSlope = "1.00", MaxQ = "5.20", Usage = "WC + yatay kolon" },
            new { DN = "125", MinSlope = "0.80", MaxQ = "8.40", Usage = "Orta kat kolektörü" },
            new { DN = "150", MinSlope = "0.70", MaxQ = "12.80",Usage = "Bina içi ana kolektör" },
            new { DN = "200", MinSlope = "0.50", MaxQ = "25.00",Usage = "Bina dışı bağlantı" },
            new { DN = "250", MinSlope = "0.40", MaxQ = "42.00",Usage = "Site / bölge kolektörü" },
            new { DN = "300", MinSlope = "0.30", MaxQ = "65.00",Usage = "Ana kolektör" },
        };

        // Find the reference DataGrid in Sekme 2 by Tag
        foreach (var obj in FindVisualChildren<DataGrid>(this))
        {
            if (obj.Tag?.ToString() == "RefTable")
            {
                obj.ItemsSource = refData;
                break;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var c in FindVisualChildren<T>(child)) yield return c;
        }
    }

    // ── Button Handlers ───────────────────────────────────────────────────────

    private void UpdateFromDrawing_Click(object sender, RoutedEventArgs e)
    {
        // Simulates "çizimden güncelle" — recalculate from current DB state
        Calculate_Click(sender, e);
        TxtCalcStatus.Text = $"Çizimden güncellendi — {DateTime.Now:HH:mm:ss}";
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var options = BuildOptions();
            _lastResult = _svc.CalculateFromDatabase(_database, options);

            _calcRows.Clear();
            foreach (var row in _lastResult.Rows)
                _calcRows.Add(row);

            TxtTotalSegments.Text = _lastResult.TotalSegments.ToString();
            TxtTotalLength.Text   = $"{_lastResult.TotalLengthM:F1} m";
            TxtWarningCount.Text  = _lastResult.WarningCount.ToString();
            TxtCalcStatus.Text    = $"Hesaplandı — {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hesaplama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AcceptSystem_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            MessageBox.Show("Önce 'Hesapla' butonuna basın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_lastResult.WarningCount > 0)
        {
            var ans = MessageBox.Show(
                $"{_lastResult.WarningCount} uyarı bulundu.\nYine de tesisatı kabul etmek istiyor musunuz?",
                "Tesisatı Kabul Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (ans != MessageBoxResult.Yes) return;
        }

        AcceptanceBar.Visibility = Visibility.Visible;
        TxtAcceptanceStatus.Text = $"✅ TESİSAT KABUL EDİLDİ — {DateTime.Now:dd.MM.yyyy HH:mm} | " +
                                   $"{_lastResult.TotalSegments} segment, {_lastResult.TotalLengthM:F1} m | " +
                                   $"Yöntem: {_lastResult.Options.Method}";
    }

    private void CalcSeptic_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = new WasteWaterCalcSheetService.SepticTankInput
            {
                PersonCount      = (int)GetDouble(TxtPersonCount.Text, 10),
                DailyWaterLiters = GetDouble(TxtDailyWater.Text, 150),
                RetentionDays    = GetDouble(TxtRetentionDays.Text, 3),
                SludgeFactor     = GetDouble(TxtSludgeFactor.Text, 1.5),
                TankType         = (CmbPitType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Foseptik"
            };

            var result = _svc.CalculateSepticTank(input);

            var sb = new StringBuilder();
            foreach (var n in result.Notes) sb.AppendLine(n);
            sb.AppendLine($"\nMinimum Hacim : {result.TotalVolumeM3:F2} m³");
            sb.AppendLine($"Önerilen Boyut: {result.RecommendedWidthM:F1} m × {result.RecommendedLengthM:F1} m × {result.RecommendedDepthM:F1} m");
            sb.AppendLine($"Standart      : {result.Standard}");

            TxtSepticResult.Text = sb.ToString();
            SepticResultPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Foseptik hesap hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CalcPump_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double inflow = GetDouble(TxtInflowLs.Text, 1.0);
            double staticH = GetDouble(TxtStaticH.Text, 8.0);
            double sumpVol = GetDouble(TxtSumpVolume.Text, 0);

            var result = _svc.CalculateSewagePump(inflow, staticH, sumpVol);

            TxtPumpResult.Text = $"Giriş Debisi   : {result.InflowLs:F2} l/s\n" +
                                 $"Pompa Debisi   : {result.RequiredFlowM3h:F1} m³/h\n" +
                                 $"Pompa Basma H  : {result.RequiredHeadM:F1} m\n" +
                                 $"Sump Hacmi     : {result.PitVolumeM3 * 1000:F0} lt\n" +
                                 $"Max Start/Saat : {result.CyclesPerHour}\n\n" +
                                 $"{result.Recommendation}";

            PumpResultPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Pompa hesap hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshBOM_Click(object sender, RoutedEventArgs e)
    {
        _bomRows.Clear();

        var pipes = _database.GetAllEntities()
            .OfType<PipeEntity>()
            .Where(p => p.SystemType == MechanicalSystemType.WasteWater ||
                        p.SystemType == MechanicalSystemType.RainWater)
            .GroupBy(p => (Dn: Math.Round(p.InnerDiameter * 1000), Type: p.SystemType.ToString()))
            .OrderBy(g => g.Key.Type)
            .ThenBy(g => g.Key.Dn);

        foreach (var g in pipes)
        {
            _bomRows.Add(new BomRow
            {
                Material = $"Pis Su Borusu — {g.Key.Type}",
                Dn       = $"DN{g.Key.Dn:F0}",
                Length   = $"{g.Sum(p => p.Length):F1}",
                Count    = $"{g.Count()}",
                Unit     = "m",
            });
        }

        var fixtures = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType)
            .OrderBy(g => g.Key);

        foreach (var g in fixtures)
        {
            _bomRows.Add(new BomRow
            {
                Material = g.Key,
                Dn       = "—",
                Length   = "—",
                Count    = $"{g.Count()}",
                Unit     = "adet",
            });
        }
    }

    private void ExportBomCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_bomRows.Count == 0) { RefreshBOM_Click(sender, e); }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Keşif Listesi CSV",
            Filter     = "CSV|*.csv",
            FileName   = $"AfneyCAD_KesfListesi_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog(this) != true) return;

        var sb = new StringBuilder("Malzeme;DN;Uzunluk (m);Adet;Birim\n");
        foreach (var r in _bomRows)
            sb.AppendLine($"{r.Material};{r.Dn};{r.Length};{r.Count};{r.Unit}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"CSV kaydedildi:\n{dlg.FileName}", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintContentDialog(_database, _lastResult) { Owner = this };
        dialog.ShowDialog();
    }

    private void RiserDiagram_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RiserDiagramExportDialog(_database) { Owner = this };
        dialog.ShowDialog();
    }

    private void UpdateDrawing_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DrawingUpdateDialog(_database, _lastResult) { Owner = this };
        dialog.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Helpers ───────────────────────────────────────────────────────────────
    private WasteWaterCalcSheetService.CircuitOptions BuildOptions()
    {
        return new WasteWaterCalcSheetService.CircuitOptions
        {
            BuildingType    = (CmbBuildingType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Konut",
            FrequencyFactor = GetDouble(TxtKFactor.Text, 0.5),
            PipeMaterial    = (CmbPipeMaterial.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PVC",
            RoughnessN      = GetDouble(TxtManningN.Text, 0.011),
            DefaultSlopePct = GetDouble(TxtDefaultSlope.Text, 2.0),
            MaxFillRatioBranch = GetDouble(TxtMaxFill.Text, 50) / 100.0,
            Method          = RbDIN.IsChecked == true
                              ? WasteWaterCalcSheetService.CalcMethod.DIN_Norm
                              : WasteWaterCalcSheetService.CalcMethod.DU_Sarfiyat,
        };
    }

    private static double GetDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
