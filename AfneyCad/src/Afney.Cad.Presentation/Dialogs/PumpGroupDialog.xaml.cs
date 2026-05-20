using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Afney.Cad.Presentation.Dialogs;

public partial class PumpGroupDialog : Window
{
    // ── State ─────────────────────────────────────────────────────────────────
    private readonly PumpSelectionService _svc = new();
    private List<PumpSelectionService.PumpModel> _allPumps = [];
    private PumpSelectionService.PumpModel? _selectedPump;
    private int   _pumpCount   = 1;
    private bool  _isParallel  = true;
    private double _staticHead  = 15;
    private double _designFlow  = 10;
    private double _designHead  = 30;

    // computed
    private List<(double Q, double H)> _singleCurve  = [];
    private List<(double Q, double H)> _groupCurve   = [];
    private List<(double Q, double H)> _systemCurve  = [];
    private (double Q, double H, bool Ok) _dutyPoint  = (0, 0, false);

    // ── Constructor ───────────────────────────────────────────────────────────
    public PumpGroupDialog()
    {
        InitializeComponent();
        LoadPumps();
        Recompute();
    }

    private void LoadPumps()
    {
        _allPumps = _svc.GetAllPumps();
        CmbPump.ItemsSource   = _allPumps.Select(p => p.ModelName).ToList();
        CmbPump.SelectedIndex = _allPumps.Count > 0 ? 0 : -1;
        if (_allPumps.Count > 0) _selectedPump = _allPumps[0];
    }

    // ── Event Handlers ────────────────────────────────────────────────────────
    private void CmbPump_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = CmbPump.SelectedIndex;
        _selectedPump = idx >= 0 && idx < _allPumps.Count ? _allPumps[idx] : null;
        Recompute();
    }

    private void Combination_Changed(object sender, RoutedEventArgs e)
    {
        _isParallel = RbParallel.IsChecked == true;
        Recompute();
    }

    private void PumpCount_Changed(object sender, RoutedEventArgs e)
    {
        _pumpCount = RbN3.IsChecked == true ? 3 : RbN2.IsChecked == true ? 2 : 1;
        Recompute();
    }

    private void SystemCurve_Changed(object sender, TextChangedEventArgs e) => ParseAndRecompute();

    private void ParseAndRecompute()
    {
        _staticHead = GetDouble(TxtStaticHead.Text, 15);
        _designFlow = GetDouble(TxtDesignFlow.Text, 10);
        _designHead = GetDouble(TxtDesignHead.Text, 30);
        Recompute();
    }

    // ── Computation ───────────────────────────────────────────────────────────
    private void Recompute()
    {
        if (_selectedPump is null) return;

        _singleCurve = _svc.GetPumpCurvePoints(_selectedPump, 60).Select(p => (p.FlowM3h, p.HeadMSS)).ToList();
        _groupCurve  = BuildGroupCurve(_singleCurve, _pumpCount, _isParallel);
        _systemCurve = _svc.GetSystemCurvePoints(_staticHead, _designFlow, _designHead, 60)
                           .Select(p => (p.FlowM3h, p.HeadMSS)).ToList();

        _dutyPoint   = FindDutyPoint(_groupCurve, _systemCurve);

        UpdateInfoPanels();
        ChartElement.InvalidateVisual();
    }

    private static List<(double Q, double H)> BuildGroupCurve(
        List<(double Q, double H)> single, int n, bool parallel)
    {
        if (n <= 1) return single;

        if (parallel)
        {
            // Paralel: Aynı H için Q * n → her H noktasında debiler toplanır
            // Yaklaşım: tüm noktalarda Q * n, H aynı kalır
            return single.Select(p => (p.Q * n, p.H)).ToList();
        }
        else
        {
            // Seri: Aynı Q için H * n → her Q noktasında basma toplanır
            return single.Select(p => (p.Q, p.H * n)).ToList();
        }
    }

    private static (double Q, double H, bool Ok) FindDutyPoint(
        List<(double Q, double H)> pump, List<(double Q, double H)> system)
    {
        if (pump.Count == 0 || system.Count == 0) return (0, 0, false);

        // Build system curve lookup (interpolate at same Q values as pump curve)
        double r = 0, hStat = 0;
        if (system.Count >= 2)
        {
            hStat = system[0].H;
            double qDes = system[^1].Q;
            double hDes = system[^1].H;
            r = qDes > 0 ? (hDes - hStat) / (qDes * qDes) : 0;
        }

        double prevDiff = double.NaN;
        for (int i = 1; i < pump.Count; i++)
        {
            double q  = pump[i].Q;
            double hp = pump[i].H;
            double hs = hStat + r * q * q;
            double diff = hp - hs;

            if (!double.IsNaN(prevDiff) && prevDiff * diff < 0)
            {
                // Lineer interpolasyon
                double q0  = pump[i - 1].Q, hp0 = pump[i - 1].H;
                double hs0 = hStat + r * q0 * q0;
                double d0  = hp0 - hs0;
                double denom = d0 - diff;
                double opQ  = Math.Abs(denom) > 1e-9 ? q0 - d0 * (q - q0) / denom : (q0 + q) / 2;
                double opH  = hStat + r * opQ * opQ;
                return (opQ, opH, true);
            }
            prevDiff = diff;
        }
        return (0, 0, false);
    }

    private void UpdateInfoPanels()
    {
        if (_selectedPump is null) return;

        // Operating point
        if (_dutyPoint.Ok)
        {
            TxtOpFlow.Text   = $"Q: {_dutyPoint.Q:F2} m³/h";
            TxtOpHead.Text   = $"H: {_dutyPoint.H:F1} mSS";
            bool inBep       = _dutyPoint.Q >= _selectedPump.BepFlow * _pumpCount * 0.6 &&
                               _dutyPoint.Q <= _selectedPump.BepFlow * _pumpCount * 1.4;
            TxtOpStatus.Text = inBep ? "✓ BEP bölgesinde — verimli çalışma" : "⚠ BEP dışında — verim düşük";
            TxtOpStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            TxtOpFlow.Text   = "Q: kesişim yok";
            TxtOpHead.Text   = "H: —";
            TxtOpStatus.Text = "Pompa eğrisi sistem eğrisini kesmedi — pompa kapasitesi yetersiz veya statik yük çok yüksek.";
        }

        // Group performance
        double groupMaxFlow = _isParallel ? _selectedPump.MaxFlow * _pumpCount : _selectedPump.MaxFlow;
        double groupMaxHead = _isParallel ? _selectedPump.MaxHead : _selectedPump.MaxHead * _pumpCount;
        double groupPower   = _selectedPump.PowerKW * _pumpCount;

        TxtGroupMaxFlow.Text = $"Max Q: {groupMaxFlow:F1} m³/h";
        TxtGroupMaxHead.Text = $"Max H: {groupMaxHead:F1} mSS";
        TxtGroupPower.Text   = $"Toplam Güç: {groupPower:F2} kW";
    }

    // ── SkiaSharp Chart ───────────────────────────────────────────────────────
    private void Chart_Paint(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(15, 15, 30));

        if (_selectedPump is null || _singleCurve.Count == 0) return;

        var info = e.Info;
        float pad = 55, right = 20, top = 20, bottom = 40;
        float w = info.Width - pad - right;
        float h = info.Height - top - bottom;

        // Axis ranges
        double maxQ = Math.Max(_groupCurve.Max(p => p.Q), _designFlow * 1.5);
        double maxH = Math.Max(
            Math.Max(_groupCurve.Max(p => p.H), _systemCurve.Max(p => p.H)),
            _designHead * 1.3) * 1.1;

        float ToX(double q) => pad + (float)(q / maxQ * w);
        float ToY(double hv) => top + h - (float)(hv / maxH * h);

        // Grid lines
        using var gridPaint = new SKPaint { Color = new SKColor(50, 50, 80), StrokeWidth = 1, IsAntialias = false };
        int gridLines = 5;
        for (int i = 0; i <= gridLines; i++)
        {
            float gy = top + h * i / gridLines;
            float gx = pad + w * i / gridLines;
            canvas.DrawLine(pad, gy, pad + w, gy, gridPaint);
            canvas.DrawLine(gx, top, gx, top + h, gridPaint);
        }

        // Axis
        using var axisPaint = new SKPaint { Color = new SKColor(150, 150, 180), StrokeWidth = 2 };
        canvas.DrawLine(pad, top, pad, top + h, axisPaint);
        canvas.DrawLine(pad, top + h, pad + w, top + h, axisPaint);

        // Axis labels
        using var labelPaint = new SKPaint { Color = new SKColor(180, 180, 200), TextSize = 10, IsAntialias = true };
        for (int i = 0; i <= gridLines; i++)
        {
            double qVal = maxQ * i / gridLines;
            float gx = pad + w * i / gridLines;
            canvas.DrawText($"{qVal:F1}", gx - 10, top + h + 14, labelPaint);

            double hVal = maxH * (gridLines - i) / gridLines;
            float gy = top + h * i / gridLines;
            canvas.DrawText($"{hVal:F0}", 4, gy + 4, labelPaint);
        }

        // Axis titles
        using var titlePaint = new SKPaint { Color = new SKColor(144, 202, 249), TextSize = 11, IsAntialias = true };
        canvas.DrawText("Q (m³/h)", pad + w / 2 - 20, info.Height - 4, titlePaint);
        canvas.Save();
        canvas.RotateDegrees(-90, 14, top + h / 2);
        canvas.DrawText("H (mSS)", 14 - 20, top + h / 2, titlePaint);
        canvas.Restore();

        // System curve (dashed orange)
        DrawCurve(canvas, _systemCurve, new SKColor(255, 112, 67), 2, ToX, ToY, dashed: true);

        // Single pump curve (blue)
        DrawCurve(canvas, _singleCurve, new SKColor(21, 101, 192), 2, ToX, ToY);

        // Group curve (yellow, thicker)
        DrawCurve(canvas, _groupCurve, new SKColor(255, 213, 79), 3, ToX, ToY);

        // Duty point marker
        if (_dutyPoint.Ok)
        {
            float dx = ToX(_dutyPoint.Q);
            float dy = ToY(_dutyPoint.H);
            using var dotPaint = new SKPaint { Color = new SKColor(105, 240, 174), IsAntialias = true };
            canvas.DrawCircle(dx, dy, 7, dotPaint);
            using var dotBorder = new SKPaint { Color = SKColors.White, IsAntialias = true, IsStroke = true, StrokeWidth = 1.5f };
            canvas.DrawCircle(dx, dy, 7, dotBorder);
            using var dpLabel = new SKPaint { Color = new SKColor(105, 240, 174), TextSize = 10, IsAntialias = true };
            canvas.DrawText($"OP ({_dutyPoint.Q:F1}, {_dutyPoint.H:F1})", dx + 9, dy - 4, dpLabel);
        }

        // Pump label
        using var pumpLabel = new SKPaint { Color = new SKColor(200, 200, 255), TextSize = 10, IsAntialias = true };
        string combo = _isParallel ? $"Paralel ×{_pumpCount}" : $"Seri ×{_pumpCount}";
        canvas.DrawText($"{_selectedPump.ModelName}  |  {combo}", pad + 6, top + 14, pumpLabel);
    }

    private static void DrawCurve(
        SKCanvas canvas,
        List<(double Q, double H)> pts,
        SKColor color,
        float strokeWidth,
        Func<double, float> toX,
        Func<double, float> toY,
        bool dashed = false)
    {
        if (pts.Count < 2) return;

        using var paint = new SKPaint
        {
            Color       = color,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
            Style       = SKPaintStyle.Stroke,
        };

        if (dashed)
            paint.PathEffect = SKPathEffect.CreateDash([6, 4], 0);

        using var path = new SKPath();
        bool first = true;
        foreach (var (q, hv) in pts)
        {
            float x = toX(q), y = toY(hv);
            if (first) { path.MoveTo(x, y); first = false; }
            else         path.LineTo(x, y);
        }
        canvas.DrawPath(path, paint);
    }

    // ── Export ────────────────────────────────────────────────────────────────
    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPump is null) return;

        bool isParallel = RbParallel.IsChecked == true;
        string comboTr  = isParallel ? "Paralel" : "Seri";

        var sb = new StringBuilder();
        sb.Append(
            "<!DOCTYPE html><html><head><meta charset='utf-8'/>" +
            "<title>Pompaj Grubu Raporu — AfneyCAD</title>" +
            "<style>body{font-family:Segoe UI,Arial;background:#1a1a2e;color:#eee;padding:24px}" +
            "h2{color:#90CAF9}h3{color:#80CBC4}" +
            "table{border-collapse:collapse;width:100%;margin-top:8px}" +
            "th{background:#0D47A1;color:#fff;padding:6px 10px;text-align:left}" +
            "td{padding:5px 10px;border-bottom:1px solid #333}" +
            "tr:nth-child(even){background:#252540}" +
            ".card{display:inline-block;background:#1A2A3A;border-radius:6px;padding:10px 16px;margin:6px;min-width:130px}" +
            ".lbl{font-size:10px;color:#90CAF9}.val{font-size:15px;font-weight:bold}" +
            "</style></head><body>");

        sb.AppendLine($"<h2>POMPAJ GRUBU ANALİZ RAPORU</h2>");
        sb.AppendLine($"<p style='color:#888'>AfneyCAD — {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine($"<p>Model: <b>{_selectedPump.ModelName}</b> | Kombinasyon: <b>{comboTr} ×{_pumpCount}</b></p>");

        double groupMaxFlow = isParallel ? _selectedPump.MaxFlow * _pumpCount : _selectedPump.MaxFlow;
        double groupMaxHead = isParallel ? _selectedPump.MaxHead : _selectedPump.MaxHead * _pumpCount;

        sb.AppendLine("<div>");
        Card(sb, "Max Q", $"{groupMaxFlow:F1} m³/h", "#90CAF9");
        Card(sb, "Max H", $"{groupMaxHead:F1} mSS", "#80CBC4");
        if (_dutyPoint.Ok)
        {
            Card(sb, "OP Debisi", $"{_dutyPoint.Q:F2} m³/h", "#FFD54F");
            Card(sb, "OP Basma",  $"{_dutyPoint.H:F1} mSS",  "#FFD54F");
        }
        Card(sb, "Toplam Güç", $"{_selectedPump.PowerKW * _pumpCount:F2} kW", "#EF9A9A");
        sb.AppendLine("</div>");

        sb.AppendLine("<h3>Tek Pompa Özellikleri</h3>");
        sb.AppendLine("<table><tr><th>Özellik</th><th>Değer</th></tr>");
        sb.AppendLine($"<tr><td>Marka</td><td>{_selectedPump.Brand}</td></tr>");
        sb.AppendLine($"<tr><td>Seri</td><td>{_selectedPump.Series}</td></tr>");
        sb.AppendLine($"<tr><td>Max Debi</td><td>{_selectedPump.MaxFlow:F1} m³/h</td></tr>");
        sb.AppendLine($"<tr><td>Max Basma</td><td>{_selectedPump.MaxHead:F1} mSS</td></tr>");
        sb.AppendLine($"<tr><td>BEP Debi</td><td>{_selectedPump.BepFlow:F1} m³/h</td></tr>");
        sb.AppendLine($"<tr><td>BEP Basma</td><td>{_selectedPump.BepHead:F1} mSS</td></tr>");
        sb.AppendLine($"<tr><td>Verim</td><td>{_selectedPump.Efficiency * 100:F0}%</td></tr>");
        sb.AppendLine($"<tr><td>Motor Gücü</td><td>{_selectedPump.PowerKW:F2} kW</td></tr>");
        sb.AppendLine($"<tr><td>Bağlantı</td><td>{_selectedPump.Connection}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h3>Sistem Eğrisi Parametreleri</h3>");
        sb.AppendLine("<table><tr><th>Parametre</th><th>Değer</th></tr>");
        sb.AppendLine($"<tr><td>Statik Yük</td><td>{_staticHead:F1} mSS</td></tr>");
        sb.AppendLine($"<tr><td>Tasarım Debisi</td><td>{_designFlow:F1} m³/h</td></tr>");
        sb.AppendLine($"<tr><td>Tasarım Basma</td><td>{_designHead:F1} mSS</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("</body></html>");

        string path = Path.Combine(Path.GetTempPath(), $"AfneyCAD_PumpGroup_{DateTime.Now:yyyyMMdd_HHmm}.html");
        File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static void Card(StringBuilder sb, string label, string value, string color)
        => sb.AppendLine($"<div class='card'><div class='lbl'>{label}</div><div class='val' style='color:{color}'>{value}</div></div>");

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double GetDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
