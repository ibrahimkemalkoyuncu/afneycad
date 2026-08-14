using Afney.Cad.Application.Services;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Render.Engines;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Input;
using Serilog;
using Afney.Cad.Mechanical.Entities; // Eklendi

namespace Afney.Cad.Presentation.Views;

    /*
       NE: CAD Görüntüleyici (Viewport)
       NEDEN: 2B ve 3B Çizimlerin, Mühendislik donanımlarının SkiaSharp kütüphanesi kullanılarak yüksek performansla ekranda gösterilmesi.
    */
    public partial class CadViewport : UserControl, IDisposable
    {

        /*
           NE: Surface Boyama (Ana Çizim Metodu)
           NEDEN: Her karede (frame) gerçekleşen; varlıkları, grid'i, komut önizlemelerini ve seçim kutusunu ekrana çizen SkiaSharp metodudur.
        */
        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (_database == null) return;
            var canvas = e.Surface.Canvas;
            canvas.Clear(new SKColor(0x21, 0x28, 0x30)); // AutoCAD 2026 Dark Grey (#212830)

            float density = (float)(e.Info.Width / (CadCanvas.ActualWidth > 0 ? CadCanvas.ActualWidth : 1.0));
            canvas.Scale(density);

            var pixelSize = _zoom > 0 ? 1.0 / _zoom : 1.0;
            // Tek instance: sadece canvas/pixelSize/kamera güncellenir, paint cache'leri korunur.
            if (_renderContext == null)
                _renderContext = new SkiaRenderContext(canvas, pixelSize);
            else
                _renderContext.SetCanvas(canvas, pixelSize);
            var renderContext = _renderContext;
            renderContext.IsIsometric = _isIsometric;
            renderContext.SetCamera(_offset, _zoom);

            // Grid
            DrawInfiniteGrid(canvas, e.Info.Width, e.Info.Height);

            // --- B Çözümü: Otomatik Hizalama Kılavuzu (Auto-Align Origin Guide) ---
            // Kullanıcının mimariyi üst üste dizebilmesi için 0,0 noktasına devasa lazerler çiz
            var originScreen = WorldToScreen(new Vector3D(0, 0, 0));
            var originProjected = new SKPoint((float)originScreen.X, (float)originScreen.Y);
            
            // Origin guide — cached paint (no allocation per frame)
            {
                float w = e.Info.Width;
                float h = e.Info.Height;
                canvas.DrawLine(0, originProjected.Y, w, originProjected.Y, _originXPaint);
                canvas.DrawLine(originProjected.X, 0, originProjected.X, h, _originYPaint);
                canvas.DrawText("ORIGIN (0,0,0)", originProjected.X + 8, originProjected.Y - 8, _originTxtPaint);
                canvas.DrawCircle(originProjected.X, originProjected.Y, 5, _originXPaint);
            }

            // Entities
            var left = (float)(-_offset.X / _zoom);
            var top = (float)(-_offset.Y / _zoom);
            var right = left + (float)(CadCanvas.ActualWidth / _zoom);
            var bottom = top + (float)(CadCanvas.ActualHeight / _zoom);
            
            // BoundingBox her zaman Min -> Max (Sol Alt -> Sağ Üst) şeklinde tanımlanmalı
            var minX = Math.Min(left, right);
            var maxX = Math.Max(left, right);
            var minY = Math.Min(top, bottom);
            var maxY = Math.Max(top, bottom);

            var visibleBox = new CadBoundingBox(new Vector3D(minX, minY, -5000), new Vector3D(maxX, maxY, 5000));

            foreach (var entity in _database.QueryEntities(visibleBox))
            {
                // Katman Görünürlük Kontrolü (Layer Filter)
                if (HiddenLayers.Contains(entity.Layer)) continue;
                
                entity.Draw(renderContext);
            }

            // Overlays
            DrawHighlight(canvas);
            
            // Hover (Glow) Efekti (Faz 26)
            if (_hoveredEntity != null && _selectionManager != null && !_selectionManager.IsSelected(_hoveredEntity.Id))
            {
                // Cached hover paint — no per-frame allocation
                var hb = _hoveredEntity.GetBoundingBox();
                var pBase = WorldToScreen(hb.Min);
                var pTop  = WorldToScreen(hb.Max);
                canvas.DrawRect((float)Math.Min(pBase.X, pTop.X) - 2,
                                (float)Math.Min(pBase.Y, pTop.Y) - 2,
                                (float)Math.Abs(pTop.X - pBase.X) + 4,
                                (float)Math.Abs(pTop.Y - pBase.Y) + 4, _hoverBoxPaint);
            }
            
            // Selection Highlighting (Glow) — hidden layer filter prevents invisible entities from appearing
            _selectionManager?.DrawSelection(renderContext, HiddenLayers);
            
            // Grip Noktaları (Mavi Kareler)
            _selectionManager?.DrawGrips(canvas, vec => {
                var p = WorldToScreen(vec);
                return new SKPoint((float)p.X, (float)p.Y);
            });
            
            if (_lastMouseWorldPos.HasValue) DrawFullScreenCrosshair(canvas, e.Info.Width, e.Info.Height);
            if (_activeSnap.HasValue) DrawSnapMarker(canvas, _activeSnap.Value);
            _activeCommand?.Draw(renderContext);

            if (_isSelecting) DrawSelectionBox(canvas);

            // Custom overlay (flow animation, clash highlight, vb.)
            OverlayRenderer?.Invoke(canvas, e.Info.Width / density, e.Info.Height / density);

            // UCS İkonu — Sol Alt Köşe
            DrawUCSIcon(canvas, e.Info.Width / density, e.Info.Height / density);
        }

        // Dışarıdan eklenen overlay render callback (PipeFlowAnimationService vb. kullanır)
        public Action<SkiaSharp.SKCanvas, float, float>? OverlayRenderer { get; set; }

        /*
           NE: Sonsuz Grid Çizme
           NEDEN: Kullanıcının derinlik ve mesafe algısını kolaylaştıran, zoom seviyesine göre dinamik olarak ölçeklenen bir ızgara yapısı çizer.
        */
        /*
           NE: Sonsuz Grid Çizme (İyileştirilmiş)
           NASIL:
             - Log10 tabanlı adım hesabı: Zoom ne olursa olsun grid çizgi yoğunluğu sabit.
             - Cached SKPaint: Her frame yeni nesne oluşturulmuyor.
             - Ekranda görünür aralıkta kalan çizgiler MAX_LINES ile sınırlandırıldı.
        */
        private void DrawInfiniteGrid(SKCanvas canvas, float width, float height)
        {
            var tl = ScreenToWorld(new Point(0, 0));
            var br = ScreenToWorld(new Point(width, height));

            double rawStep = Math.Pow(10.0, Math.Ceiling(Math.Log10(200.0 / Math.Max(_zoom, 1e-9))));
            double minorStep = rawStep;
            double majorStep  = minorStep * 10.0;

            if (_gridDotMode)
            {
                const int MaxDots = 2500;
                int count = 0;
                double yMin = Math.Min(tl.Y, br.Y);
                double yMax = Math.Max(tl.Y, br.Y);
                for (double x = Math.Floor(tl.X / minorStep) * minorStep; x <= br.X && count < MaxDots; x += minorStep)
                {
                    for (double y = Math.Floor(yMin / minorStep) * minorStep; y <= yMax && count < MaxDots; y += minorStep, count++)
                    {
                        var p = WorldToScreen(new Vector3D(x, y, 0));
                        canvas.DrawPoint((float)p.X, (float)p.Y, _gridDotPaint);
                    }
                }
                return;
            }

            const int MaxLines = 400;
            int vCount = 0;
            for (double x = Math.Floor(tl.X / minorStep) * minorStep; x <= br.X && vCount < MaxLines; x += minorStep, vCount++)
            {
                var p1 = WorldToScreen(new Vector3D(x, tl.Y, 0));
                var p2 = WorldToScreen(new Vector3D(x, br.Y, 0));
                bool isMajor = (Math.Abs(x % majorStep) < minorStep * 0.1) ||
                               (Math.Abs(x % majorStep) > majorStep - minorStep * 0.1);
                canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y,
                                isMajor ? _gridMajorPaint : _gridMinorPaint);
            }

            int hCount = 0;
            double yMin2 = Math.Min(tl.Y, br.Y);
            double yMax2 = Math.Max(tl.Y, br.Y);
            for (double y = Math.Floor(yMin2 / minorStep) * minorStep; y <= yMax2 && hCount < MaxLines; y += minorStep, hCount++)
            {
                var p1 = WorldToScreen(new Vector3D(tl.X, y, 0));
                var p2 = WorldToScreen(new Vector3D(br.X, y, 0));
                bool isMajor = (Math.Abs(y % majorStep) < minorStep * 0.1) ||
                               (Math.Abs(y % majorStep) > majorStep - minorStep * 0.1);
                canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y,
                                isMajor ? _gridMajorPaint : _gridMinorPaint);
            }
        }

        /*
           NE: Tam Ekran Artı Göstergesi (Crosshair)
           NEDEN: AutoCAD benzeri bir deneyim için fare imlecinin hizasını ekran boyunca dikey ve yatay çizgilerle göstermek için.
        */
        private void DrawFullScreenCrosshair(SKCanvas canvas, float width, float height)
        {
            if (!_lastMouseWorldPos.HasValue) return;
            var p = WorldToScreen(_lastMouseWorldPos.Value);
            canvas.DrawLine(0, (float)p.Y, width, (float)p.Y, _crosshairPaint);
            canvas.DrawLine((float)p.X, 0, (float)p.X, height, _crosshairPaint);
            float pb = 8.0f;
            canvas.DrawRect((float)p.X - pb, (float)p.Y - pb, pb * 2, pb * 2, _crosshairPaint);
        }

        private System.Collections.Generic.List<Vector3D>? _highlightPolyline;
        private SKPaint? _highlightFill;
        private SKPaint? _highlightStroke;

        /*
           NE: Geçici Vurgulama Göster (ShowHighlight)
           NEDEN: Seçilen bir odayı (Mahal) veya alanı kullanıcıya görsel olarak turuncu renkte parlatarak geri bildirim vermek için.
        */
        public void ShowHighlight(System.Collections.Generic.IEnumerable<Vector3D> points)
        {
            _highlightPolyline = points.ToList();
            
            _highlightFill?.Dispose();
            _highlightStroke?.Dispose();
            
            _highlightFill = new SKPaint { Color = new SKColor(230, 126, 34, 100), Style = SKPaintStyle.Fill, IsAntialias = true }; // Turuncu Yarı Şeffaf
            _highlightStroke = new SKPaint { Color = SKColors.OrangeRed, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true, PathEffect = SKPathEffect.CreateDash(new float[] {10, 5}, 0) };
            
            InvalidateViewport();
        }

        /*
           NE: Vurgulamayı Temizle
           NEDEN: İşlem bittiğinde veya iptal edildiğinde turuncu vurgu alanını ekrandan kaldırmak için.
        */
        public void ClearHighlight()
        {
            _highlightPolyline = null;
            
            _highlightFill?.Dispose();
            _highlightFill = null;
            
            _highlightStroke?.Dispose();
            _highlightStroke = null;
            
            InvalidateViewport();
        }

        /*
           NE: Vurgu Alanını Çiz
           NEDEN: ShowHighlight ile set edilen poligon verisini Skia kanvasına turuncu tarama ve kesikli çizgi olarak dökmek için.
        */
        private void DrawHighlight(SKCanvas canvas)
        {
            if (_highlightPolyline == null || _highlightPolyline.Count < 3) return;

            using var path = new SKPath();
            var start = WorldToScreen(_highlightPolyline[0]);
            path.MoveTo((float)start.X, (float)start.Y);

            for (int i = 1; i < _highlightPolyline.Count; i++)
            {
                var p = WorldToScreen(_highlightPolyline[i]);
                path.LineTo((float)p.X, (float)p.Y);
            }
            path.Close();

            if (_highlightFill != null) canvas.DrawPath(path, _highlightFill);
            if (_highlightStroke != null) canvas.DrawPath(path, _highlightStroke);
        }
        
        /*
           NE: Snap Marker Çizici (DrawSnapMarker) — AutoCAD 2026 Style
           NEDEN: Fare bir noktanın üzerine geldiğinde yakalanan yerin tipine göre sembol çizmek için.
           Endpoint=Yeşil Kare, Midpoint=Cyan Üçgen, Center=Sarı Daire, Intersection=Beyaz X, Quadrant=Mor Baklava
        */
        private void DrawSnapMarker(SKCanvas canvas, SnapPoint snap)
        {
            var rawP = WorldToScreen(snap.Position);
            var p = new SKPoint((float)rawP.X, (float)rawP.Y);
            float size = 14f; // AutoCAD'den büyük (daha görünür)

            // Snap tipine göre renk — AutoCAD 2026 renkleri
            SKColor markerColor = snap.Type switch
            {
                SnapPointType.Endpoint => new SKColor(0, 255, 0),         // Yeşil
                SnapPointType.Midpoint => new SKColor(0, 255, 255),       // Cyan
                SnapPointType.Center => new SKColor(255, 255, 0),         // Sarı
                SnapPointType.Quadrant => new SKColor(180, 100, 255),     // Mor
                SnapPointType.Intersection => new SKColor(255, 255, 255), // Beyaz
                SnapPointType.Perpendicular => new SKColor(255, 128, 0),  // Turuncu
                _ => new SKColor(255, 165, 0)                             // Varsayılan turuncu
            };

            using var paint = new SKPaint { Color = markerColor, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
            using var glow = new SKPaint { Color = markerColor.WithAlpha(60), Style = SKPaintStyle.Fill, IsAntialias = true };

            // Glow efekti (arkada hafif parıltı)
            canvas.DrawCircle(p.X, p.Y, size, glow);

            switch (snap.Type)
            {
                case SnapPointType.Endpoint: // ■ Kare
                    canvas.DrawRect(p.X - size/2, p.Y - size/2, size, size, paint);
                    break;
                case SnapPointType.Midpoint: // △ Üçgen
                    using (var path = new SKPath())
                    {
                        path.MoveTo(p.X, p.Y - size * 0.7f);
                        path.LineTo(p.X - size * 0.7f, p.Y + size * 0.5f);
                        path.LineTo(p.X + size * 0.7f, p.Y + size * 0.5f);
                        path.Close();
                        canvas.DrawPath(path, paint);
                    }
                    break;
                case SnapPointType.Center: // ○ Daire
                    canvas.DrawCircle(p.X, p.Y, size * 0.6f, paint);
                    canvas.DrawLine(p.X - size, p.Y, p.X + size, p.Y, paint);
                    canvas.DrawLine(p.X, p.Y - size, p.X, p.Y + size, paint);
                    break;
                case SnapPointType.Quadrant: // ◇ Baklava
                    using (var path = new SKPath())
                    {
                        path.MoveTo(p.X, p.Y - size * 0.7f);
                        path.LineTo(p.X + size * 0.7f, p.Y);
                        path.LineTo(p.X, p.Y + size * 0.7f);
                        path.LineTo(p.X - size * 0.7f, p.Y);
                        path.Close();
                        canvas.DrawPath(path, paint);
                    }
                    break;
                case SnapPointType.Intersection: // ✕ Çapraz
                    canvas.DrawLine(p.X - size * 0.6f, p.Y - size * 0.6f, p.X + size * 0.6f, p.Y + size * 0.6f, paint);
                    canvas.DrawLine(p.X - size * 0.6f, p.Y + size * 0.6f, p.X + size * 0.6f, p.Y - size * 0.6f, paint);
                    break;
                case SnapPointType.Perpendicular: // ⊥ Dik açı
                    using (var path = new SKPath())
                    {
                        path.MoveTo(p.X - size * 0.6f, p.Y + size * 0.6f);
                        path.LineTo(p.X - size * 0.6f, p.Y - size * 0.4f);
                        path.LineTo(p.X + size * 0.6f, p.Y - size * 0.4f);
                        path.MoveTo(p.X - size * 0.3f, p.Y - size * 0.4f);
                        path.LineTo(p.X - size * 0.3f, p.Y + size * 0.1f);
                        path.LineTo(p.X + size * 0.1f, p.Y + size * 0.1f);
                        canvas.DrawPath(path, paint);
                    }
                    break;
                default:
                    canvas.DrawRect(p.X - size/2, p.Y - size/2, size, size, paint);
                    break;
            }

            // Snap tipi etiketi
            using var textPaint = new SKPaint { Color = markerColor, TextSize = 10, IsAntialias = true };
            string label = snap.Type switch
            {
                SnapPointType.Endpoint => "END",
                SnapPointType.Midpoint => "MID",
                SnapPointType.Center => "CEN",
                SnapPointType.Quadrant => "QUA",
                SnapPointType.Intersection => "INT",
                SnapPointType.Perpendicular => "PER",
                _ => "SNAP"
            };
            canvas.DrawText(label, p.X + size + 2, p.Y + 4, textPaint);
        }

        /*
           NE: UCS İkonu Çizici
           NEDEN: Sol alt köşede X/Y eksen göstergesi (AutoCAD UCS icon). Yönelim, koordinat sistemini gösterir.
        */
        private void DrawUCSIcon(SKCanvas canvas, float viewW, float viewH)
        {
            float margin = 40f;
            float axisLen = 35f;
            float cx = margin;
            float cy = viewH - margin;

            // X Ekseni (Kırmızı)
            using var xPaint = new SKPaint { Color = new SKColor(220, 50, 50), StrokeWidth = 2.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(cx, cy, cx + axisLen, cy, xPaint);
            // Ok ucu
            canvas.DrawLine(cx + axisLen, cy, cx + axisLen - 6, cy - 4, xPaint);
            canvas.DrawLine(cx + axisLen, cy, cx + axisLen - 6, cy + 4, xPaint);

            // Y Ekseni (Yeşil) — yukarı
            using var yPaint = new SKPaint { Color = new SKColor(50, 220, 50), StrokeWidth = 2.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(cx, cy, cx, cy - axisLen, yPaint);
            canvas.DrawLine(cx, cy - axisLen, cx - 4, cy - axisLen + 6, yPaint);
            canvas.DrawLine(cx, cy - axisLen, cx + 4, cy - axisLen + 6, yPaint);

            // Etiketler
            using var xLabel = new SKPaint { Color = new SKColor(220, 50, 50), TextSize = 12, IsAntialias = true, FakeBoldText = true };
            using var yLabel = new SKPaint { Color = new SKColor(50, 220, 50), TextSize = 12, IsAntialias = true, FakeBoldText = true };
            canvas.DrawText("X", cx + axisLen + 4, cy + 5, xLabel);
            canvas.DrawText("Y", cx - 5, cy - axisLen - 6, yLabel);

            // Merkez noktası
            using var cPaint = new SKPaint { Color = new SKColor(180, 180, 180), StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawCircle(cx, cy, 3, cPaint);
        }

        /*
           NE: Seçim Kutusu Çizici (DrawSelectionBox)
           NEDEN: Farenin hareketine göre sağdan sola (Yeşil - Crossing) veya soldan sağa (Mavi - Window) seçim bölgelerini renklendirerek göstermek için.
        */
        private void DrawSelectionBox(SKCanvas canvas)
        {
            float l = (float)Math.Min(_selectionStartPoint.X, _selectionCurrentPoint.X);
            float t = (float)Math.Min(_selectionStartPoint.Y, _selectionCurrentPoint.Y);
            float r = (float)Math.Max(_selectionStartPoint.X, _selectionCurrentPoint.X);
            float b = (float)Math.Max(_selectionStartPoint.Y, _selectionCurrentPoint.Y);
            bool isCrossing = _selectionCurrentPoint.X < _selectionStartPoint.X;
            
            using var fill = new SKPaint 
            { 
                Color = isCrossing ? new SKColor(46, 204, 113, 80) : new SKColor(52, 152, 219, 80), // Daha modern Yeşil ve Mavi
                Style = SKPaintStyle.Fill 
            };
            
            using var stroke = new SKPaint 
            { 
                Color = isCrossing ? new SKColor(46, 204, 113) : new SKColor(52, 152, 219),
                Style = SKPaintStyle.Stroke, 
                StrokeWidth = 1 
            };
            
            if (isCrossing) stroke.PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0);
            
            var rect = new SKRect(l, t, r, b);
            canvas.DrawRect(rect, fill);
            canvas.DrawRect(rect, stroke);
        }
    }
