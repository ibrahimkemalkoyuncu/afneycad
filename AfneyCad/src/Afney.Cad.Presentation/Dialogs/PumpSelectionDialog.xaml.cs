using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class PumpSelectionDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly PumpSelectionService _svc;
        private readonly double _systemFlow;
        private readonly double _systemHead;

        private PumpSelectionService.PumpModel? _selectedPump;
        private List<PumpRowVm> _currentRows = [];

        public PumpSelectionDialog(CadDatabase database, double systemFlow, double systemHead)
        {
            InitializeComponent();
            _database   = database;
            _svc        = new PumpSelectionService();
            _systemFlow = systemFlow;
            _systemHead = systemHead;

            TxtFlow.Text = systemFlow.ToString("F2");
            TxtHead.Text = systemHead.ToString("F2");

            // Marka ComboBox'ını doldur
            foreach (var brand in _svc.GetAvailableBrands())
                BrandCombo.Items.Add(new ComboBoxItem { Content = brand });

            RunSelection();
        }

        private void SelectPump_Click(object sender, RoutedEventArgs e) => RunSelection();

        private void Inputs_Changed(object sender, EventArgs e) { /* anlık güncelleme isteğe bağlı */ }

        private void RunSelection()
        {
            if (!double.TryParse(TxtFlow.Text, out double q) || q <= 0) return;
            if (!double.TryParse(TxtHead.Text, out double h) || h <= 0) return;

            string? brand = (BrandCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (brand == "Tüm Markalar") brand = null;

            string? app = (AppCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (app == "Tümü") app = null;

            var pumps = _svc.RecommendPumps(q, h, brand, app);
            _currentRows = pumps.Select(p => new PumpRowVm(p)).ToList();
            PumpGrid.ItemsSource = _currentRows;

            if (pumps.Count > 0)
            {
                _selectedPump = pumps[0];
                PumpGrid.SelectedIndex = 0;
                RefreshChart();
                ChartPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                _selectedPump = null;
                PumpChart.InvalidateVisual();
                ChartPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void PumpGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PumpGrid.SelectedItem is PumpRowVm row)
            {
                _selectedPump = row.Model;
                RefreshChart();
            }
        }

        private void RefreshChart()
        {
            PumpChart.InvalidateVisual();

            if (_selectedPump == null) return;

            if (!double.TryParse(TxtFlow.Text, out double q)) q = _systemFlow;
            if (!double.TryParse(TxtHead.Text, out double h)) h = _systemHead;
            if (!double.TryParse(TxtStaticHead.Text, out double hs)) hs = h * 0.4;

            var (opQ, opH, inRange) = _svc.CalculateDutyPoint(_selectedPump, hs, q, h);
            string rangeInfo = inRange ? "✓ BEP bölgesinde" : "⚠ BEP dışında";
            DutyPointLabel.Text = $"Çalışma Noktası: Q={opQ:F2} m³/h  H={opH:F1} mSS  {rangeInfo}";
        }

        private void PumpChart_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(new SKColor(0x2D, 0x2D, 0x3F));

            if (_selectedPump == null) return;

            if (!double.TryParse(TxtFlow.Text, out double reqQ)) reqQ = _systemFlow;
            if (!double.TryParse(TxtHead.Text, out double reqH)) reqH = _systemHead;
            if (!double.TryParse(TxtStaticHead.Text, out double hs)) hs = reqH * 0.4;

            int w = e.Info.Width;
            int h = e.Info.Height;

            // Kenar boşlukları (grafik alanı)
            float padL = 55, padR = 20, padT = 20, padB = 40;
            float gW = w - padL - padR;
            float gH = h - padT - padB;

            // Eksen aralıkları
            double maxQ = _selectedPump.MaxFlow * 1.2;
            double maxH = _selectedPump.MaxHead * 1.3;

            SKPoint ToScreen(double q, double hh) =>
                new SKPoint(padL + (float)(q / maxQ * gW), padT + gH - (float)(hh / maxH * gH));

            // Izgara çizgileri
            using var gridPaint = new SKPaint { Color = new SKColor(80, 80, 100), StrokeWidth = 0.5f };
            for (int i = 0; i <= 5; i++)
            {
                float qX = padL + gW * i / 5;
                float hY = padT + gH * i / 5;
                canvas.DrawLine(qX, padT, qX, padT + gH, gridPaint);
                canvas.DrawLine(padL, hY, padL + gW, hY, gridPaint);
            }

            // Eksenler
            using var axisPaint = new SKPaint { Color = SKColors.Gray, StrokeWidth = 1.5f };
            canvas.DrawLine(padL, padT, padL, padT + gH, axisPaint);
            canvas.DrawLine(padL, padT + gH, padL + gW, padT + gH, axisPaint);

            // Eksen etiketleri
            using var labelPaint = new SKPaint
            {
                Color = new SKColor(180, 200, 220),
                TextSize = 11,
                IsAntialias = true
            };
            for (int i = 0; i <= 5; i++)
            {
                double qVal = maxQ * i / 5;
                double hVal = maxH * (5 - i) / 5;
                float qX = padL + gW * i / 5;
                float hY = padT + gH * i / 5;
                canvas.DrawText($"{qVal:F1}", qX - 8, padT + gH + 16, labelPaint);
                canvas.DrawText($"{hVal:F0}", 2, hY + 4, labelPaint);
            }

            // Eksen başlıkları
            using var titlePaint = new SKPaint { Color = new SKColor(160, 190, 220), TextSize = 12, IsAntialias = true };
            canvas.DrawText("Q (m³/h)", padL + gW / 2 - 25, h - 4, titlePaint);
            canvas.Save();
            canvas.RotateDegrees(-90, 12, padT + gH / 2);
            canvas.DrawText("H (mSS)", 12 - 20, padT + gH / 2 + 4, titlePaint);
            canvas.Restore();

            // Sistem eğrisi (gri noktalı)
            var sysCurve = _svc.GetSystemCurvePoints(hs, reqQ, reqH, 40);
            using var sysPaint = new SKPaint { Color = new SKColor(180, 180, 80), StrokeWidth = 1.5f, PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0) };
            DrawCurve(canvas, sysCurve, sysPaint, ToScreen);

            // Pompa Q-H eğrisi (mavi kalın)
            var pumpCurve = _svc.GetPumpCurvePoints(_selectedPump, 40);
            using var pumpPaint = new SKPaint { Color = new SKColor(80, 180, 255), StrokeWidth = 2.5f, IsAntialias = true };
            DrawCurve(canvas, pumpCurve, pumpPaint, ToScreen);

            // BEP noktası (yeşil daire)
            var bepPt = ToScreen(_selectedPump.BepFlow, _selectedPump.BepHead);
            using var bepPaint = new SKPaint { Color = new SKColor(80, 220, 80), IsAntialias = true };
            canvas.DrawCircle(bepPt, 6, bepPaint);
            canvas.DrawText("BEP", bepPt.X + 8, bepPt.Y - 4, new SKPaint { Color = new SKColor(80, 220, 80), TextSize = 11, IsAntialias = true });

            // Tasarım noktası (turuncu çarpı)
            var designPt = ToScreen(reqQ, reqH);
            using var designPaint = new SKPaint { Color = new SKColor(255, 140, 0), StrokeWidth = 2f, IsAntialias = true };
            canvas.DrawLine(designPt.X - 7, designPt.Y - 7, designPt.X + 7, designPt.Y + 7, designPaint);
            canvas.DrawLine(designPt.X - 7, designPt.Y + 7, designPt.X + 7, designPt.Y - 7, designPaint);
            canvas.DrawText("Tasarım", designPt.X + 8, designPt.Y + 4, new SKPaint { Color = new SKColor(255, 160, 40), TextSize = 11, IsAntialias = true });

            // Çalışma noktası (kırmızı kare)
            var (opQ2, opH2, _) = _svc.CalculateDutyPoint(_selectedPump, hs, reqQ, reqH);
            if (opQ2 > 0 && opQ2 <= maxQ && opH2 <= maxH)
            {
                var opPt = ToScreen(opQ2, opH2);
                using var opPaint = new SKPaint { Color = new SKColor(255, 80, 80), IsAntialias = true };
                canvas.DrawRect(opPt.X - 5, opPt.Y - 5, 10, 10, opPaint);
                canvas.DrawText("OP", opPt.X + 8, opPt.Y + 4, new SKPaint { Color = new SKColor(255, 100, 100), TextSize = 11, IsAntialias = true });
            }

            // Model adı + açıklama
            string modelLabel = $"{_selectedPump.Brand} — {_selectedPump.ModelName}";
            canvas.DrawText(modelLabel, padL + 6, padT + 16, new SKPaint { Color = new SKColor(200, 230, 255), TextSize = 13, FakeBoldText = true, IsAntialias = true });

            // Lejant
            float ly = h - padB - 5;
            DrawLegendItem(canvas, padL + 10, ly, new SKColor(80, 180, 255), "Pompa Q-H Eğrisi");
            DrawLegendItem(canvas, padL + 160, ly, new SKColor(180, 180, 80), "Sistem Eğrisi");
            DrawLegendItem(canvas, padL + 300, ly, new SKColor(80, 220, 80), "BEP");
            DrawLegendItem(canvas, padL + 360, ly, new SKColor(255, 80, 80), "Çalışma Noktası (OP)");
        }

        private static void DrawCurve(SKCanvas canvas,
            List<(double FlowM3h, double HeadMSS)> pts, SKPaint paint,
            Func<double, double, SKPoint> toScreen)
        {
            using var path = new SKPath();
            bool first = true;
            foreach (var (q, hh) in pts)
            {
                var pt = toScreen(q, hh);
                if (first) { path.MoveTo(pt); first = false; }
                else path.LineTo(pt);
            }
            canvas.DrawPath(path, paint);
        }

        private static void DrawLegendItem(SKCanvas canvas, float x, float y, SKColor color, string text)
        {
            using var p = new SKPaint { Color = color, StrokeWidth = 2.5f };
            canvas.DrawLine(x, y, x + 20, y, p);
            canvas.DrawText(text, x + 24, y + 4, new SKPaint { Color = new SKColor(200, 200, 200), TextSize = 10, IsAntialias = true });
        }

        private void CheckCavitation_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPump == null)
            {
                CavitationResult.Text = "Önce bir pompa seçin.";
                return;
            }

            if (!double.TryParse(TxtSuctionHeight.Text, out double zs)) zs = 0;
            if (!double.TryParse(TxtSuctionLoss.Text, out double hfs)) hfs = 0;
            if (!double.TryParse(TxtWaterTemp.Text, out double tempC)) tempC = 20;

            var result = _svc.CheckCavitation(_selectedPump, zs, hfs, tempC);
            CavitationResult.Text =
                $"NPSHa: {result.NPSHa:F2} mSS\n" +
                $"NPSHr: {result.NPSHr:F2} mSS\n" +
                $"Marj : {result.Margin:F2} mSS\n\n" +
                result.Recommendation;
            CavitationResult.Foreground = result.IsSafe
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.OrangeRed;
        }
    }

    // DataGrid için ViewModel satırı
    internal class PumpRowVm
    {
        public PumpSelectionService.PumpModel Model { get; }
        public string Brand       => Model.Brand;
        public string ModelName   => Model.ModelName;
        public string MaxFlowStr  => $"{Model.MaxFlow:F1} m³/h";
        public string MaxHeadStr  => $"{Model.MaxHead:F0} mSS";
        public string PowerStr    => $"{Model.PowerKW:F2}";
        public string EffStr      => $"%{Model.Efficiency * 100:F0}";
        public string Connection  => Model.Connection;
        public string Application => Model.Application;

        public PumpRowVm(PumpSelectionService.PumpModel m) => Model = m;
    }
}
