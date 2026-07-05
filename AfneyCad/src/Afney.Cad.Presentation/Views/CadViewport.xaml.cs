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
        private CadDatabase? _database;
        private SnapEngine? _snapEngine;
        private SelectionManager? _selectionManager;
        private ICadCommand? _activeCommand;

        private double _zoom = 1.0;
        private double _targetZoom = 1.0;
        private Vector3D _offset = new Vector3D(0, 0, 0);
        private Vector3D _targetOffset = new Vector3D(0, 0, 0);
        private bool _isIsometric = false;

        private SnapPoint? _activeSnap;
        private Vector3D? _lastMouseWorldPos;
        public Vector3D? LastMouseWorldPos => _lastMouseWorldPos;

        public void ZoomToSelection()
        {
            if (_selectionManager == null || _selectionManager.SelectedCount == 0) return;
            var selected = _selectionManager.GetSelectedEntities().ToList();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var ent in selected)
            {
                var bb = ent.GetBoundingBox();
                if (bb.Min.X < minX) minX = bb.Min.X;
                if (bb.Min.Y < minY) minY = bb.Min.Y;
                if (bb.Max.X > maxX) maxX = bb.Max.X;
                if (bb.Max.Y > maxY) maxY = bb.Max.Y;
            }
            double width = maxX - minX;
            double height = maxY - minY;
            if (width < 1 || height < 1) return;
            double padding = 1.2;
            double cx = (minX + maxX) / 2;
            double cy = (minY + maxY) / 2;
            double screenW = CadCanvas.ActualWidth;
            double screenH = CadCanvas.ActualHeight;
            _zoom = Math.Min(screenW / (width * padding), screenH / (height * padding));
            _zoom = Math.Clamp(_zoom, 1e-6, 100.0);
            _offset = new Vector3D(screenW / 2.0 - cx * _zoom, screenH / 2.0 - cy * _zoom, 0);
            _targetZoom = _zoom;
            _targetOffset = _offset;
            InvalidateViewport();
        }

        private bool _isPanning = false;
        private Point _lastMousePosition;
        private DateTime _lastMiddleClickTime = DateTime.MinValue;

        private bool _isSelecting = false;
        private bool _isStretching = false;
        private CadEntity? _activeGripEntity = null;
        private int? _activeGripIndex = null;
        private Point _selectionStartPoint;
        private Point _selectionCurrentPoint;
        private bool _rightClickCanceledCommand = false;

        private DateTime _lastMouseMoveTime = DateTime.MinValue;
        private CadEntity? _hoveredEntity;



        public event Action<string>? OnFeedback;
        public event Action<System.Collections.Generic.IEnumerable<CadEntity>>? SelectionChanged;
        public event Action<bool>? OrthoToggled;
        public event Action<CadEntity>? EntityDoubleClicked;

        public bool IsOrthoEnabled { get; private set; } = false;

        // --- Katman Yönetimi (Layer Management) ---
        public System.Collections.Generic.HashSet<string> HiddenLayers { get; } = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly SKPaint _axisPaint = new() { Color = new SKColor(100, 100, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        private readonly SKPaint _crosshairPaint = new() { Color = SKColors.White.WithAlpha(180), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };

        // ── Cached SKPaint nesneleri (her frame new() yaratmaktan kaçınmak için) ──────────
        private readonly SKPaint _gridMinorPaint = new() { Color = new SKColor(255, 255, 255, 14), StrokeWidth = 0, IsAntialias = false };
        private readonly SKPaint _gridMajorPaint = new() { Color = new SKColor(255, 255, 255, 35), StrokeWidth = 0, IsAntialias = false };
        private readonly SKPaint _gridDotPaint = new() { Color = new SKColor(255, 255, 255, 50), StrokeWidth = 2, StrokeCap = SKStrokeCap.Round, IsAntialias = false };
        private bool _gridDotMode = false;
        public bool GridDotMode { get => _gridDotMode; set { _gridDotMode = value; InvalidateViewport(); } }
        private readonly SKPaint _originXPaint   = new() { Color = new SKColor(255, 50, 50, 150), StrokeWidth = 2, IsAntialias = true };
        private readonly SKPaint _originYPaint   = new() { Color = new SKColor(50, 255, 50, 150), StrokeWidth = 2, IsAntialias = true };
        private readonly SKPaint _originTxtPaint = new() { Color = SKColors.White, TextSize = 13, IsAntialias = true };
        private readonly SKPaint _hoverBoxPaint  = new() { Color = new SKColor(173, 216, 230, 190), Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };

        // ── Smooth Zoom (Animasyonlu, AutoCAD hissiyatı) ─────────────────────────────────
        private System.Windows.Threading.DispatcherTimer? _zoomTimer;
        private const double ZoomLerp = 0.22;   // Her frame %22 yaklaşma → ~15 frame'de yerine oturur
        private const double ZoomSnapThr = 1e-5; // Bu kadar yakında snap yap

        /*
           NE: CadViewport Yapıcı Metodu
           NEDEN: Bileşenleri belleğe yükler ve XAML tanımlarını başlatır.
        */
        public CadViewport()
        {
            InitializeComponent();
        }

        /*
           NE: Kaynakları Serbest Bırak (Dispose)
           NEDEN: SkiaSharp nesneleri (SKPaint, SKPath vb.) Unmanaged (C++) tabanlı olduğu için .NET Garbage Collector tarafından otomatik silinmez, Memory Leak yaratmamak için manuel temizlenmeli.
        */
        public void Dispose()
        {
            _axisPaint?.Dispose();
            _crosshairPaint?.Dispose();
            _gridMinorPaint?.Dispose();
            _gridMajorPaint?.Dispose();
            _originXPaint?.Dispose();
            _originYPaint?.Dispose();
            _originTxtPaint?.Dispose();
            _hoverBoxPaint?.Dispose();
            _zoomTimer?.Stop();
            Serilog.Log.Information("🧹 CadViewport Skia kaynakları temizlendi.");
        }



        /*
           NE: Viewport Başlatma (Initialize)
           NEDEN: Viewport'un ihtiyaç duyduğu veritabanı, yakalama (Snap) ve seçim (Selection) servislerini bağlamak için.
        */
        public void Initialize(CadDatabase database, SnapEngine snapEngine, SelectionManager selectionManager)
        {
            _database = database;
            _snapEngine = snapEngine;
            _selectionManager = selectionManager;
            _database.EntityAdded += (e) => InvalidateViewport();
            _database.EntityRemoved += (e) => InvalidateViewport();
        }

        public ICadCommand? ActiveCommand => _activeCommand;

        /*
           NE: Aktif Komutu Ayarla (SetActiveCommand)
           NEDEN: Çizgi çizme, boru döşeme gibi işlemleri yürüten sınıfları viewport'a enjekte etmek için.
        */
        public void SetActiveCommand(ICadCommand? command)
        {
            _activeCommand = command;
            InvalidateViewport();
        }

        /*
           NE: SetCommand (Alias)
           NEDEN: MainWindow uyumluluğu için SetActiveCommand takma adı.
        */
        public void SetCommand(ICadCommand? command) => SetActiveCommand(command);

        /*
           NE: Mevcut Komutu İptal Et (CancelCurrentCommand)
           NEDEN: MainWindow üzerinden aktif komutu güvenli bir şekilde sonlandırmak için.
        */
        public void CancelCurrentCommand()
        {
            if (_activeCommand != null)
            {
                _activeCommand.Cancel();
                SetActiveCommand(null);
                OnFeedback?.Invoke("Komut iptal edildi.");
            }
        }

        /*
           NE: Aktif Komutu Getir
           NEDEN: Şu an hangi komutun (Line, Pipe vb.) çalıştığını öğrenmek için.
        */
        public ICadCommand? GetActiveCommand() => _activeCommand;

        /*
           NE: Seçili Nesneleri Al (GetSelectedEntities)
           NEDEN: Şu an viewport üzerinde mavi/yeşil kutu veya tıklama ile seçilmiş olan tüm nesne referanslarını döndürmek için.
        */
        public System.Collections.Generic.IEnumerable<CadEntity> GetSelectedEntities()
        {
            return _database?.GetSelectedEntities() ?? System.Linq.Enumerable.Empty<CadEntity>();
        }

        /*
           NE: Kamera Merkezini Hesapla
           NEDEN: Zoom ve Offset değerlerine göre şu an ekranın tam ortasında hangi dünya koordinatının olduğunu bulmak için.
        */
        public Vector3D GetCameraCenter()
        {
            double midX = (CadCanvas.ActualWidth / 2.0 - _offset.X) / _zoom;
            double midY = (CadCanvas.ActualHeight / 2.0 - _offset.Y) / _zoom;
            return new Vector3D(midX, midY, 0);
        }

        /*
           NE: Görünüm Modunu Değiştir (2D/3D)
           NEDEN: Çizimi plan görünümünden izometrik görünüme geçirmek için render motorunu bilgilendirir.
        */
        public void SetViewMode(bool isIsometric)
        {
            _isIsometric = isIsometric;
            CadCanvas.InvalidateVisual();
        }

        /*
           NE: Ekranı Yenile (Redraw)
           NEDEN: Değişikliklerin ekrana yansıması için Canvas'ı yeniden çizilmeye zorlar.
        */
        public void InvalidateViewport() => CadCanvas.InvalidateVisual();

        /*
           NE: Ekrana Sığdır (Zoom Extents)
           NEDEN: Tüm çizim elemanlarını kapsayan bir sınır hesaplayarak, kamerayı bu sınırı tam görecek şekilde ayarlar.
        */
        public void ZoomExtents()
        {
            if (_database == null) { _zoom = 1.0; _targetZoom = 1.0; _offset = new Vector3D(0, 0, 0); _targetOffset = _offset; InvalidateViewport(); return; }

            // Tek seferde tüm entity'leri cache'le (GetAllEntities'i sadece 1 kez çağır!)
            var allEntities = _database.GetAllEntities().ToList();
            if (allEntities.Count == 0)
            {
                 _zoom = 1.0;
                 _targetZoom = 1.0;
                 _offset = new Vector3D(0, 0, 0);
                 _targetOffset = _offset;
                 InvalidateViewport();
                 return;
            }

            // 1. Centroid (Merkez) hesapla
            double sumX = 0, sumY = 0;
            int count = 0;
            foreach (var e in allEntities)
            {
                var c = e.GetBoundingBox().Center;
                if (double.IsNaN(c.X) || double.IsInfinity(c.X)) continue;
                sumX += c.X; sumY += c.Y; count++;
            }
            if (count == 0) return;
            double avgX = sumX / count;
            double avgY = sumY / count;

            // 2. Outlier Removal + BoundingBox tek döngüde
            double threshold = 500000.0;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            int validCount = 0;

            foreach (var ent in allEntities)
            {
                var b = ent.GetBoundingBox();
                if (double.IsNaN(b.Min.X) || double.IsInfinity(b.Min.X)) continue;
                var cx2 = b.Center;
                if (Math.Abs(cx2.X - avgX) > threshold || Math.Abs(cx2.Y - avgY) > threshold) continue;

                minX = Math.Min(minX, b.Min.X); minY = Math.Min(minY, b.Min.Y);
                maxX = Math.Max(maxX, b.Max.X); maxY = Math.Max(maxY, b.Max.Y);
                validCount++;
            }

            // Hiç geçerli nesne yoksa tüm listeyi kullan
            if (validCount == 0)
            {
                foreach (var ent in allEntities)
                {
                    var b = ent.GetBoundingBox();
                    if (double.IsNaN(b.Min.X) || double.IsInfinity(b.Min.X)) continue;
                    minX = Math.Min(minX, b.Min.X); minY = Math.Min(minY, b.Min.Y);
                    maxX = Math.Max(maxX, b.Max.X); maxY = Math.Max(maxY, b.Max.Y);
                }
            }

            if (minX == double.MaxValue) { minX = 0; maxX = 1000; minY = 0; maxY = 1000; }

            double width = (maxX - minX) * 1.1;
            double height = (maxY - minY) * 1.1;
            if (width <= 0) width = 1000; if (height <= 0) height = 1000;

            double screenW = CadCanvas.ActualWidth > 0 ? CadCanvas.ActualWidth : 800;
            double screenH = CadCanvas.ActualHeight > 0 ? CadCanvas.ActualHeight : 600;

            _zoom = Math.Min(screenW / width, screenH / height);
            _zoom = Math.Clamp(_zoom, 1e-6, 100.0); 
            if (double.IsNaN(_zoom) || double.IsInfinity(_zoom)) _zoom = 1.0;

            double cx = (minX + maxX) / 2.0;
            double cy = (minY + maxY) / 2.0;

            double offX = screenW / 2.0 - (cx * _zoom);
            double offY = screenH / 2.0 - (cy * _zoom);

            if (double.IsNaN(offX) || double.IsInfinity(offX)) offX = 0;
            if (double.IsNaN(offY) || double.IsInfinity(offY)) offY = 0;

            _offset = new Vector3D(offX, offY, 0);
            _targetZoom = _zoom;
            _targetOffset = _offset;

            InvalidateViewport();
        }

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
            var renderContext = new SkiaRenderContext(canvas, pixelSize);
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

        /*
           NE: Fare Basılma Olayı (MouseDown)
           NEDEN: Farenin hangi tuşuna basıldığına göre (Sol: Seçim/Çizim, Orta: Pan, Sağ: İptal) ilgili işlemi başlatmak için.
        */
        private void CadCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition(CadCanvas);
            CadCanvas.Focus();

            if (e.ChangedButton == MouseButton.Middle)
            {
                if (e.ClickCount == 2) { ZoomExtents(); return; }
                _isPanning = true;
                CadCanvas.CaptureMouse();
                CadCanvas.Cursor = System.Windows.Input.Cursors.Hand;
                return;
            }

            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 && _activeCommand == null
                && _database != null && _lastMouseWorldPos.HasValue)
            {
                // Çift tıkla → Entity Properties
                double ht = 12.0 / Math.Max(0.001, _zoom);
                var wp = _lastMouseWorldPos.Value;
                var all = _database.GetAllEntities().ToList();
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    var ent = all[i];
                    if (HiddenLayers.Contains(ent.Layer)) continue;
                    var bb = ent.GetBoundingBox();
                    if (wp.X >= bb.Min.X - ht && wp.X <= bb.Max.X + ht &&
                        wp.Y >= bb.Min.Y - ht && wp.Y <= bb.Max.Y + ht &&
                        ent.DistanceTo(wp) < ht)
                    {
                        EntityDoubleClicked?.Invoke(ent);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // MÜHENDİSLİK: Eğer BlockCommand obje seçimi bekliyorsa (step = 2) OnPointerPressed göndermek yerine
                // doğrudan Viewport'un normal seçim kutusu (selection box) mantığına geçiş yap.
                bool isBmakeSelecting = _activeCommand is Afney.Cad.Commands.BasicCommands.BlockCommand bc && bc.IsSelectingObjects;

                if (_activeCommand != null && _lastMouseWorldPos.HasValue && !isBmakeSelecting)
                    _activeCommand.OnPointerPressed(_lastMouseWorldPos.Value);
                else 
                { 
                    // MÜHENDİSLİK: Grip point üzerine tıklandı mı diye kontrol et (Stretch işlemi başlangıcı)
                    if (_selectionManager != null && _selectionManager.SelectedCount > 0 && _lastMouseWorldPos.HasValue)
                    {
                        var worldPos = _lastMouseWorldPos.Value;
                        double selectionThreshold = 10.0 / Math.Max(0.001, _zoom); // Ekranda yaklaşık 10px mesafe töleransı
                        
                        foreach (var entity in _selectionManager.GetSelectedEntities())
                        {
                            var grips = entity.GetGripPoints().ToList();
                            for (int i = 0; i < grips.Count; i++)
                            {
                                if (grips[i].DistanceTo(worldPos) < selectionThreshold)
                                {
                                    _activeGripEntity = entity;
                                    _activeGripIndex = i;
                                    _isStretching = true;
                                    CadCanvas.CaptureMouse();
                                    return; // Seçim kutusu oluşturma
                                }
                            }
                        }
                    }
                    
                    _isSelecting = true; 
                    _selectionStartPoint = _lastMousePosition; 
                    _selectionCurrentPoint = _lastMousePosition; 
                    CadCanvas.CaptureMouse(); 
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (_activeCommand != null)
                {
                    /*
                       MÜHENDİSLİK: Komut aktifken sağ tık = "Enter gibi onayla"
                       NEDEN: Manuel Mahal gibi komutlarda sağ tık ile seçimi bitirmek
                              AutoCAD standardıdır. Cancel() çağırmak seçimi silerdi.
                       SONUÇ: Komut kendi OnKeyDown(Enter) mantığıyla bitirir;
                              OnCompleted event'i tetiklendiğinde viewport komutu temizler.
                    */
                    Serilog.Log.Information("[Viewport] Sağ tık → aktif komuta OnKeyDown(Enter) gönderiliyor.");
                    _activeCommand.OnKeyDown(InputKey.Enter);
                    _rightClickCanceledCommand = false;
                    InvalidateViewport();
                }
                else
                {
                    _rightClickCanceledCommand = false;
                }
            }
            InvalidateViewport();
        }

        /*
           NE: Fare Hareket Olayı (MouseMove)
           NEDEN: Farenin pozisyonuna göre koordinatları güncellemek, Snap noktalarını yakalamak ve Pan/Seçim kutusunu güncellemek için.
        */
        private void CadCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var currentPos = e.GetPosition(CadCanvas);
            var worldPos = ScreenToWorld(currentPos);
            _lastMouseWorldPos = worldPos;

            if (_isPanning)
            {
                var delta = currentPos - _lastMousePosition;
                // Pan hızı: 1:1 pixel takip — AutoCAD standardı (zoom bağımsız).
                _offset = new Vector3D(_offset.X + delta.X, _offset.Y + delta.Y, 0);
                _targetOffset = _offset;
                _lastMousePosition = currentPos;
                InvalidateViewport();
            }
            else if (_isSelecting)
            {
                _selectionCurrentPoint = currentPos;
            }
            
            // Grip Stretching Aktifse nesneyi hareket ettir
            else if (_isStretching && _activeGripEntity != null && _activeGripIndex.HasValue && _lastMouseWorldPos.HasValue)
            {
                var oldPos = _activeGripEntity.GetGripPoints().ElementAt(_activeGripIndex.Value);
                var newPos = _lastMouseWorldPos.Value;
                var delta = newPos - oldPos;

                _activeGripEntity.MoveGripPointAt(_activeGripIndex.Value, newPos);

                // MÜHENDİSLİK: Faz 23 - Stretch (Bağlı Elemanların Otomatik Sündürülmesi)
                // Eğer borunun bir ucu çekiliyorsa, ucundaki Dirsek/T-Parçası da hareket etmeli,
                // Ve o Dirsek/T-Parçasına bağlı diğer boruların DA o ucu sündürülmeli!
                if (_activeGripEntity is PipeEntity pipe && (_activeGripIndex == 0 || _activeGripIndex == 1))
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    var fieldInfo = mainWindow?.GetType().GetField("_mechanicalKernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                   ?? mainWindow?.GetType().GetField("MechanicalKernel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                   
                    var kernel = fieldInfo?.GetValue(mainWindow);
                    var graph = kernel?.GetType().GetProperty("TopologyGraph")?.GetValue(kernel) as Afney.Cad.Mechanical.Engine.MechanicalTopologyGraph;

                    if (graph == null)
                    {
                        // Fallback to searching Database manually if graph is null
                    }
                    else
                    {
                        var node = graph.GetNode(pipe.Id);
                        if (node != null)
                        {
                            var portName = _activeGripIndex == 0 ? "Start" : "End";
                            var port = node.Ports.FirstOrDefault(p => p.Name == portName);
                            if (port != null && port.IsConnected && port.ConnectedEntityId.HasValue)
                            {
                                var connectedEntityId = port.ConnectedEntityId.Value;
                                var connectedEntity = _database?.GetEntity(connectedEntityId);

                                if (connectedEntity != null)
                                {
                                    // Dirseği veya T-Parçasını taşı (Tüm nesne kayar)
                                    connectedEntity.Move(delta);

                                    // Dirsek/T-Parçasının DİĞER portlarına bağlı boruları bul ve onların ilgili uçlarını esnet
                                    var connectedNode = graph.GetNode(connectedEntityId);
                                    if (connectedNode != null)
                                    {
                                        foreach (var cPort in connectedNode.Ports)
                                        {
                                            if (cPort.IsConnected && cPort.ConnectedEntityId.HasValue && cPort.ConnectedEntityId.Value != pipe.Id)
                                            {
                                                var otherPipe = _database?.GetEntity(cPort.ConnectedEntityId.Value) as PipeEntity;
                                                if (otherPipe != null)
                                                {
                                                    // Hangi ucu bu port'a bağlı?
                                                    var otherNode = graph.GetNode(otherPipe.Id);
                                                    if (otherNode != null)
                                                    {
                                                        var otherPort = otherNode.Ports.FirstOrDefault(p => p.ConnectedEntityId == connectedEntityId);
                                                        if (otherPort != null)
                                                        {
                                                            if (otherPort.Name == "Start")
                                                                otherPipe.MoveGripPointAt(0, newPos);
                                                            else if (otherPort.Name == "End")
                                                                otherPipe.MoveGripPointAt(1, newPos);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (_snapEngine != null)
                _activeSnap = _snapEngine.FindSnapPoint(worldPos, 15.0 / _zoom, _activeCommand?.ActivePoint);

            if (_activeSnap.HasValue)
            {
                _lastMouseWorldPos = _activeSnap.Value.Position;
            }
            else if (IsOrthoEnabled && _activeCommand != null && _activeCommand.ActivePoint.HasValue)
            {
                // MÜHENDİSLİK: AutoCAD standartlarına göre OSNAP yoksa ve ORTHO açıksa, koordinatları kısıtla
                var basePoint = _activeCommand.ActivePoint.Value;
                var current = _lastMouseWorldPos.Value;
                
                double dx = Math.Abs(current.X - basePoint.X);
                double dy = Math.Abs(current.Y - basePoint.Y);

                if (dx > dy)
                {
                    // Yatay kilit
                    _lastMouseWorldPos = new Vector3D(current.X, basePoint.Y, current.Z);
                }
                else
                {
                    // Dikey kilit
                    _lastMouseWorldPos = new Vector3D(basePoint.X, current.Y, current.Z);
                }
            }

            if (_activeCommand != null && _lastMouseWorldPos.HasValue)
                _activeCommand.OnPointerMoved(_lastMouseWorldPos.Value);

            // UI Güncelleme (CoordinateText XAML İsmi ile Uyumlu)
            if (CoordinateText != null)
                CoordinateText.Text = $"X: {worldPos.X:F2}, Y: {worldPos.Y:F2}";

            if ((DateTime.Now - _lastMouseMoveTime).TotalMilliseconds > 16)
            {
                _lastMouseMoveTime = DateTime.Now;
                
                // MÜHENDİSLİK: Faz 26 - Hover detection (Hit-Testing)
                if (!_isPanning && !_isSelecting && _database != null && _activeCommand == null)
                {
                    double hitTolerance = 10.0 / Math.Max(0.001, _zoom); // Ekranda 10px tolerans
                    CadEntity? foundHit = null;

                    // Ters döngü: Üstte çizilmiş (son eklenmiş) objeleri önce bul
                    var allEntities = _database.GetAllEntities().ToList();
                    for (int i = allEntities.Count - 1; i >= 0; i--)
                    {
                        var ent = allEntities[i];
                        if (!HiddenLayers.Contains(ent.Layer))
                        {
                            var bbox = ent.GetBoundingBox();
                            // Hızlı BoundingBox ön-filtresi (Genişletilmiş tolerance ile)
                            if (worldPos.X >= bbox.Min.X - hitTolerance && worldPos.X <= bbox.Max.X + hitTolerance &&
                                worldPos.Y >= bbox.Min.Y - hitTolerance && worldPos.Y <= bbox.Max.Y + hitTolerance)
                            {
                                // Detaylı mesafe ölçümü
                                if (ent.DistanceTo(worldPos) < hitTolerance)
                                {
                                    foundHit = ent;
                                    break;
                                }
                            }
                        }
                    }

                    if (foundHit != _hoveredEntity)
                    {
                        _hoveredEntity = foundHit;
                        UpdateHoverTooltip(_hoveredEntity);
                    }
                }
                
                InvalidateViewport();
            }
        }

        /*
           NE: ToolTip Güncelleme ve Gösterme
           NEDEN: Fare bir nesnenin üzerine geldiğinde o nesnenin tipine (Pipe, Line vb.) göre detaylı mühendislik bilgisini oluşturup ekranda göstermek için.
        */
        private void UpdateHoverTooltip(CadEntity? entity)
        {
            if (entity == null)
            {
                if (this.FindName("EntityToolTip") is ToolTip toolTipHiding)
                {
                    toolTipHiding.IsOpen = false;
                    toolTipHiding.Visibility = Visibility.Collapsed;
                }
                return;
            }

            var toolTip = this.FindName("EntityToolTip") as ToolTip;
            var contentPanel = this.FindName("EntityToolTipContent") as StackPanel;

            if (toolTip == null || contentPanel == null) return;

            contentPanel.Children.Clear();

            // Başlık (Entity Tipi)
            var titleText = new TextBlock 
            { 
                Text = entity.GetType().Name.Replace("Entity", ""), 
                FontWeight = FontWeights.Bold, 
                Foreground = System.Windows.Media.Brushes.Gold,
                Margin = new Thickness(0,0,0,5)
            };
            contentPanel.Children.Add(titleText);

            // Temel Özellikler
            contentPanel.Children.Add(new TextBlock { Text = $"Katman: {entity.Layer}", Foreground = System.Windows.Media.Brushes.LightGray });
            
            // Mekanik Nesnelere (Domain/Mechanical) özel bilgiler
            if (entity is MechanicalEntity mech)
            {
                contentPanel.Children.Add(new TextBlock { Text = $"Sistem: {mech.SystemType}", Foreground = System.Windows.Media.Brushes.Cyan });
                
                if (mech is PipeEntity pipe)
                {
                    contentPanel.Children.Add(new TextBlock { Text = $"Malzeme: {pipe.PipeMaterialType}", Foreground = System.Windows.Media.Brushes.White });
                    contentPanel.Children.Add(new TextBlock { Text = $"Çap: DN {pipe.InnerDiameter:F1}", FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White });
                    contentPanel.Children.Add(new TextBlock { Text = $"Debi: {pipe.FlowRate:F2} l/s", Foreground = System.Windows.Media.Brushes.LightGreen });
                    contentPanel.Children.Add(new TextBlock { Text = $"Hız: {pipe.Velocity:F2} m/s", Foreground = System.Windows.Media.Brushes.LightGreen });
                    contentPanel.Children.Add(new TextBlock { Text = $"Kayıp: {pipe.PressureDrop:F2} Pa/m", Foreground = System.Windows.Media.Brushes.Salmon });
                }
                // (Gelecekte buraya FittingEntity veya FixtureEntity gibi eklemeler yapılabilir)
            }
            
            toolTip.Visibility = Visibility.Visible;
            toolTip.IsOpen = true;
        }

        /*
           NE: Fare Bırakılma Olayı (MouseUp)
           NEDEN: Pan işlemini bitirmek veya fareyle çekilen seçim kutusuna (Window/Crossing) giren nesneleri seçmek için.
        */
        private void CadCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // MIDDLE BUTTON: Pan sonu
                if (e.ChangedButton == MouseButton.Middle)
                {
                    _isPanning = false;
                    CadCanvas.ReleaseMouseCapture();
                    CadCanvas.Cursor = System.Windows.Input.Cursors.Cross;
                    Serilog.Log.Information("🖱️ Pan modu kapandı");
                    return;
                }
                
                // RIGHT BUTTON: WPF ContextMenuOpening otomatik çalışacak
                if (e.ChangedButton == MouseButton.Right)
                {
                    // e.Handled işlemlerini ContextMenuOpening eventi içerisinde yapacağız (komut iptal edildiyse açılmasını engelledik)
                    return;
                }
                
                // LEFT BUTTON: Seçim veya Komut veya Grip Release
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (_isStretching)
                    {
                        _isStretching = false;
                        _activeGripEntity = null;
                        _activeGripIndex = null;
                        CadCanvas.ReleaseMouseCapture();
                        Serilog.Log.Information("✍️ Grip Stretch işlemi bitirildi.");
                        return; // Seçim kutusu hesaplamasına girme
                    }

                    if (_isSelecting)
                    {
                        _isSelecting = false;

                    if (_selectionManager != null)
                    {
                        try
                        {
                            double dragDist = Math.Sqrt(
                                Math.Pow(_selectionCurrentPoint.X - _selectionStartPoint.X, 2) +
                                Math.Pow(_selectionCurrentPoint.Y - _selectionStartPoint.Y, 2));

                            if (dragDist < 5.0)
                            {
                                // TEK TIK — en yakın entity'yi seç (AutoCAD pick)
                                var wp = ScreenToWorld(_selectionStartPoint);
                                double pickTol = 12.0 / Math.Max(0.001, _zoom);

                                if (Keyboard.Modifiers != ModifierKeys.Shift)
                                    _selectionManager.ClearSelection();

                                CadEntity? best = null;
                                double bestDist = pickTol;
                                foreach (var ent in _database!.GetAllEntities())
                                {
                                    if (HiddenLayers.Contains(ent.Layer)) continue;
                                    double d = ent.DistanceTo(wp);
                                    if (d < bestDist) { bestDist = d; best = ent; }
                                }

                                if (best != null)
                                {
                                    _selectionManager.ToggleEntity(best.Id);
                                    OnFeedback?.Invoke($"Seçildi: {best.GetType().Name} — Katman: {best.Layer}");
                                }

                                SelectionChanged?.Invoke(_selectionManager.GetSelectedEntities());
                            }
                            else
                            {
                                // PENCERE SEÇİMİ — drag ile seçim kutusu
                                var p1 = ScreenToWorld(_selectionStartPoint);
                                var p2 = ScreenToWorld(_selectionCurrentPoint);
                                p1 = new Vector3D(p1.X, p1.Y, -1000000);
                                p2 = new Vector3D(p2.X, p2.Y, 1000000);

                                var rect = new CadBoundingBox(p1, p2);
                                bool isCrossing = _selectionCurrentPoint.X < _selectionStartPoint.X;

                                if (Keyboard.Modifiers != ModifierKeys.Shift)
                                    _selectionManager.ClearSelection();

                                if (isCrossing)
                                    _selectionManager.SelectByCrossing(rect);
                                else
                                    _selectionManager.SelectByWindow(rect);

                                SelectionChanged?.Invoke(_selectionManager.GetSelectedEntities());
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "Seçim sırasında hata!");
                        }
                    }
                }
                } // Added to close the e.ChangedButton == MouseButton.Left block
            } // Closes outer try block
            finally
            {
                // GARANTİ: Her zaman mouse capture'ı release et!
                if (CadCanvas.IsMouseCaptured)
                {
                    CadCanvas.ReleaseMouseCapture();
                    Serilog.Log.Information("🔓 Mouse capture release edildi (finally)");
                }
                
                InvalidateViewport();
            }
        }

        /*
           NE: Fare Tekerleği Olayı (MouseWheel) — AutoCAD 2026 Standardı
           NEDEN: Fare tekerleği ile çizime yakınlaşmak veya uzaklaşmak için.
           STANDART: AutoCAD instant zoom kullanır — fare imlecine doğru/dosyandan anında zoom.
           Faktör: 1.15x (çevrim başına %15)
        */
        /*
           NE: Fare Tekerleği — AutoCAD Standardı
           NASIL:
           1. Delta-aware: e.Delta / 120 → kaç notch → Math.Pow(1.12, notches)
              → hızlı scroll = büyük adım, yavaş = küçük adım (kümülatif)
           2. Anında (instant) zoom — AutoCAD animasyon kullanmaz, lerp YOK
           3. Pivot = fare imleci konumu (dünya koordinatı sabit kalır)
        */
        private void CadCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mousePos = e.GetPosition(CadCanvas);

            // Kaç notch? (1 notch = 120 delta birimi; bazı fareler kesirli verir)
            double notches = e.Delta / 120.0;
            // Her ±1 notch ~%25 zoom — AutoCAD 2026 standardı (eski: 1.12 = %12, çok yavaş).
            double factor = Math.Pow(1.25, notches);

            // Yeni zoom değeri (klamp: aşırı in/out'u engelle)
            double newZoom = Math.Clamp(_zoom * factor, 1e-6, 1e6);

            // Pivot hesabı: fare imlecinin dünya koordinatı (worldPivot) sabit kalmalı.
            // worldPivot = (mousePos - offset) / zoom
            // Yeni offset = mousePos - worldPivot * newZoom
            double worldPivotX = (mousePos.X - _offset.X) / _zoom;
            double worldPivotY = (mousePos.Y - _offset.Y) / _zoom;

            _zoom   = newZoom;
            _offset = new Vector3D(
                mousePos.X - worldPivotX * newZoom,
                mousePos.Y - worldPivotY * newZoom, 0);

            // Target'ları da güncelle (ZoomExtents vb. ile tutarlılık)
            _targetZoom   = _zoom;
            _targetOffset = _offset;

            if (ZoomText != null)
                ZoomText.Text = $"Z: {_zoom:F4}x";

            InvalidateViewport();
        }


        /*
           NE: Klavye Olay Yöneticisi (KeyDown)
           NEDEN: ESC (İptal), DELETE (Sil) ve ENTER (Onay) gibi AutoCAD standart klavye etkileşimlerini handle etmek için.
        */
        private void CadCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            /*
               NE: Enter / Space → aktif komuta "onayla" sinyali gönder
               NEDEN: Manuel Mahal gibi komutlar Enter ile polygon oluşturur.
                      Viewport'un bu tuşları yutmaması için e.Handled = true yapılır.
            */
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Space)
            {
                if (_activeCommand != null)
                {
                    Serilog.Log.Information("[Viewport] Enter/Space → _activeCommand.OnKeyDown(Enter)");
                    _activeCommand.OnKeyDown(InputKey.Enter);
                    e.Handled = true;
                    InvalidateViewport();
                    return;
                }
            }

            // ESC TUŞU: HER ŞEYİ İPTAL ET!
            if (e.Key == Key.Escape)
            {
                Serilog.Log.Information("⛔ ESC tuşuna basıldı - İşlemler iptal ediliyor");

                // 1. Aktif komut varsa iptal et
                if (_activeCommand != null)
                {
                    _activeCommand.Cancel();
                    SetActiveCommand(null);
                    OnFeedback?.Invoke("Komut iptal edildi (ESC)");
                    Serilog.Log.Information("❌ Aktif komut iptal edildi");
                }

                // 2. Seçim modu aktifse kapat
                if (_isSelecting)
                {
                    _isSelecting = false;
                    _selectionStartPoint = new Point();
                    _selectionCurrentPoint = new Point();
                    if (CadCanvas.IsMouseCaptured) CadCanvas.ReleaseMouseCapture();
                    InvalidateViewport();
                }

                // 3. Seçimleri iptal et
                if (_selectionManager != null && _selectionManager.SelectedCount > 0)
                {
                    _selectionManager.ClearSelection();
                    SelectionChanged?.Invoke(System.Linq.Enumerable.Empty<CadEntity>());
                    InvalidateViewport();
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (_selectionManager != null && _database != null && _selectionManager.SelectedCount > 0)
                {
                    var toDelete = _selectionManager.GetSelectedEntities().ToList();
                    _selectionManager.ClearSelection();

                    foreach (var ent in toDelete)
                    {
                        _database.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.RemoveEntityOperation(_database, ent));
                    }

                    SelectionChanged?.Invoke(System.Linq.Enumerable.Empty<CadEntity>());
                    InvalidateViewport();
                    OnFeedback?.Invoke($"{toDelete.Count} obje silindi.");
                }
            }
            else if (e.Key == Key.F8)
            {
                ToggleOrtho();
                e.Handled = true;
            }
        }

        public void ToggleOrtho()
        {
            ToggleOrthoMode(!IsOrthoEnabled);
        }

        public void ToggleOrthoMode(bool isEnabled)
        {
            if (IsOrthoEnabled == isEnabled) return;
            IsOrthoEnabled = isEnabled;
            Serilog.Log.Information("📐 Ortho Mode Set to: {Status}", IsOrthoEnabled);
            OrthoToggled?.Invoke(IsOrthoEnabled);
            OnFeedback?.Invoke(IsOrthoEnabled ? "Ortho Mode: AÇIK" : "Ortho Mode: KAPALI");
            InvalidateViewport();
        }


    /*
       NE: Ekrandan Dünyaya Dönüşüm (ScreenToWorld)
       NEDEN: Ekrandaki piksel koordinatlarını (ör: farenin tıkladığı yer) çizimdeki gerçek dünya koordinatlarına çevirmek için.
    */
    public Vector3D ScreenToWorld(Point screen)
    {
        return new Vector3D((screen.X - _offset.X) / _zoom, (screen.Y - _offset.Y) / _zoom, 0);
    }

    /*
       NE: Dünyadan Ekrana Dönüşüm (WorldToScreen)
       NEDEN: Çizimdeki gerçek dünya koordinatlarını ekranın piksellerine çevirerek nesneleri doğru yere çizmek için.
    */
    public Point WorldToScreen(Vector3D world)
    {
        return new Point(world.X * _zoom + _offset.X, world.Y * _zoom + _offset.Y);
    }

    // ===== CONTEXT MENU EVENT HANDLERS =====
    
    /*
        NE: Sağ Tıklama Menüsü - Pan Modu
        NEDEN: AutoCAD'de sağ tık → Pan yaygın kullanımdır
    */
    private void OnContextMenu_Move(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.MoveCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Mirror(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.MirrorCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Rotate(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.RotateCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Scale(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.ScaleCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Stretch(object sender, RoutedEventArgs e)
    {
        OnFeedback?.Invoke("STRETCH: Grip noktalarını sürükleyerek esnetin.");
    }

    private void OnContextMenu_GripPoint(object sender, RoutedEventArgs e)
    {
        OnFeedback?.Invoke("Tutma noktası: Nesne seçin, mavi grip kutularını sürükleyin.");
    }

    private void OnContextMenu_Copy(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.CopyCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Pan(object sender, RoutedEventArgs e)
    {
        _isPanning = true;
        CadCanvas.Cursor = System.Windows.Input.Cursors.Hand;
        OnFeedback?.Invoke("PAN modu aktif - Fareyi hareket ettirin");
    }

    /*
       NE: Sağ Tıklama Menüsü - Ekrana Sığdır
       NEDEN: Kullanıcının menü üzerinden Zoom Extents yapabilmesi için.
    */
    private void OnContextMenu_ZoomExtents(object sender, RoutedEventArgs e)
    {
        ZoomExtents();
    }

    /*
       NE: Sağ Tıklama Menüsü - Tümünü Seç
       NEDEN: Çizimdeki tüm nesneleri tek seferde seçebilmek için.
    */
    private void OnContextMenu_SelectAll(object sender, RoutedEventArgs e)
    {
        if (_selectionManager != null && _database != null)
        {
            var allEntities = _database.GetAllEntities();
            foreach (var entity in allEntities)
            {
                _selectionManager.ToggleEntity(entity.Id);
            }
            OnFeedback?.Invoke($"Tüm entityler seçildi: {_selectionManager.SelectedCount} adet");
            InvalidateViewport();
        }
    }

    /*
       NE: Sağ Tıklama Menüsü - Seçimi Temizle
       NEDEN: Seçili olan tüm nesnelerin seçim durumunu iptal etmek için.
    */
    private void OnContextMenu_ClearSelection(object sender, RoutedEventArgs e)
    {
        _selectionManager?.ClearSelection();
        OnFeedback?.Invoke("Seçim temizlendi");
        InvalidateViewport();
    }

    /*
       NE: Sağ Tıklama Menüsü - Geri Al (Undo)
       NEDEN: Son yapılan işlemi geri almak için (Placeholder).
    */
    private void OnContextMenu_Undo(object sender, RoutedEventArgs e)
    {
        // MainWindow'dan transaction manager'a erişmek gerekli
        // Şimdilik feedback ver
        OnFeedback?.Invoke("Undo - Henüz implementasyon yok");
    }

    /*
       NE: Sağ Tıklama Menüsü - İleri Al (Redo)
       NEDEN: Geri alınan işlemi tekrar uygulamak için (Placeholder).
    */
    private void OnContextMenu_Redo(object sender, RoutedEventArgs e)
    {
        OnFeedback?.Invoke("Redo - Henüz implementasyon yok");
    }

    /*
       NE: Sağ Tıklama Menüsü - Özellikler
       NEDEN: Seçili nesnelerin teknik detaylarını ve özelliklerini görmek için ilgili paneli tetikler.
    */
    private void OnContextMenu_Properties(object sender, RoutedEventArgs e)
    {
        if (_selectionManager != null && _selectionManager.SelectedCount > 0)
        {
            var selected = _selectionManager.GetSelectedEntities();
            OnFeedback?.Invoke($"Properties: {selected.Count} entity seçili");
        }
    }

    /*
       NE: Sağ Tıklama Menüsü - Sil
       NEDEN: Seçili nesneleri veritabanından kalıcı olarak silmek için.
    */
    private void OnContextMenu_Delete(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;

        var toDelete = _selectionManager.GetSelectedEntities().ToList();
        _selectionManager.ClearSelection();

        foreach (var ent in toDelete)
            _database.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.RemoveEntityOperation(_database, ent));

        SelectionChanged?.Invoke(System.Linq.Enumerable.Empty<CadEntity>());
        InvalidateViewport();
        OnFeedback?.Invoke($"{toDelete.Count} obje silindi. (Ctrl+Z ile geri alınabilir)");
    }

    /*
       NE: Dinamik Context Menu Açılışı (ContextMenuOpening)
       NEDEN: Menü açılmadan hemen önce çalışarak gereksiz/aktif olmayan işlemleri (Sil, Özellikler vb.) devre dışı bırakmak veya yeni dinamik bilgiler eklemek için.
    */
    private void CadCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // 1. Sağ tık aktif bir komutu iptal ettiyse menüyü açma (Örn: Çizgi komutundan çıkarken menü çıkmasın)
        if (_rightClickCanceledCommand)
        {
            e.Handled = true;
            _rightClickCanceledCommand = false;
            return;
        }

        bool hasSelection = _selectionManager != null && _selectionManager.SelectedCount > 0;
        
        // 2. Statik menü elemanlarını etkinleştir/devre dışı bırak
        if (this.FindName("CtxMenu_ClearSelection") is MenuItem clearMenu) clearMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Properties") is MenuItem propsMenu) propsMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Delete") is MenuItem deleteMenu) deleteMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Move") is MenuItem moveMenu) moveMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Mirror") is MenuItem mirrorMenu) mirrorMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Rotate") is MenuItem rotateMenu) rotateMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Scale") is MenuItem scaleMenu) scaleMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Stretch") is MenuItem stretchMenu) stretchMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_GripPoint") is MenuItem gripMenu) gripMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Copy") is MenuItem copyMenu) copyMenu.IsEnabled = hasSelection;

        var ctx = CadCanvas.ContextMenu;
        if (ctx != null)
        {
            // 3. Eski dinamik eklemeleri (Örn: Boru Çapı textleri) temizle
            var toRemove = new System.Collections.Generic.List<object>();
            foreach (var item in ctx.Items)
            {
                if (item is FrameworkElement fe && fe.Tag is string tg && tg == "Dynamic")
                {
                    toRemove.Add(item);
                }
            }
            foreach (var item in toRemove) ctx.Items.Remove(item);

            // 4. Sadece tek bir boru seçiliyse debi/çap bilgisini direkt menüye ekle (Bonus Bilgi)
            if (hasSelection && _selectionManager != null && _selectionManager.SelectedCount == 1)
            {
                var ent = _selectionManager.GetSelectedEntities().First();
                if (ent is Mechanical.Entities.PipeEntity pipe)
                {
                    ctx.Items.Add(new Separator { Tag = "Dynamic" });
                    ctx.Items.Add(new MenuItem 
                    { 
                        Header = $"Boru Çapı: DN {pipe.InnerDiameter:F1}", 
                        IsEnabled = false, 
                        Tag = "Dynamic", 
                        Foreground = System.Windows.Media.Brushes.Cyan 
                    });
                }
            }
        }
    }
}
