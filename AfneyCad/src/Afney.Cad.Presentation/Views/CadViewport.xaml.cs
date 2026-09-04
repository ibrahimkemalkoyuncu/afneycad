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
using Afney.Cad.Mechanical;

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

        // ── Polar Tracking + Object Snap Tracking (bkz. CadViewport.Input.cs / CadViewport.Rendering.cs) ──
        private readonly PolarTrackingService _polarTracking = new();
        private readonly ObjectSnapTrackingService _objectSnapTracking = new();

        // Render için: aktif hizalama çizgisi/etiketi (MouseMove'da doldurulur, OnPaintSurface'de çizilir)
        private (Vector3D From, Vector3D To, double Angle)? _activePolarTrack;
        private System.Collections.Generic.List<(Vector3D From, Vector3D To)>? _activeOTrackLines;

        public bool IsPolarTrackingEnabled
        {
            get => _polarTracking.IsEnabled;
            set { _polarTracking.IsEnabled = value; InvalidateViewport(); }
        }

        public double PolarAngleIncrement
        {
            get => _polarTracking.IncrementAngle;
            set { _polarTracking.IncrementAngle = value > 0 ? value : 90.0; }
        }

        public bool IsObjectSnapTrackingEnabled
        {
            get => _objectSnapTracking.Enabled;
            set
            {
                _objectSnapTracking.Enabled = value;
                if (!value) _objectSnapTracking.ClearAcquired();
                InvalidateViewport();
            }
        }

        public event Action<bool>? PolarTrackingToggled;
        public event Action<bool>? ObjectSnapTrackingToggled;

        /*
           NE: Mekanik Kernel Referansı (MechanicalKernel)
           NEDEN: Boru grip sürükleme (Stretch) sırasında bağlı Dirsek/T-Parçası topolojisine
                  erişmek için önceden her MouseMove'da reflection (GetType().GetField/GetProperty)
                  kullanılıyordu — saniyede onlarca kez çalışan bir hot-path için ağır ve kırılgan.
                  MainWindow artık CreateNewDocument() içinde bu referansı doğrudan set ediyor.
        */
        public MechanicalKernel? MechanicalKernel { get; set; }

        // Tab ile üst üste binen nesneler arası geçiş (bkz. CycleOverlappingEntity)
        private List<CadEntity>? _tabCycleCandidates;
        private int _tabCycleIndex = -1;
        private Vector3D? _tabCycleOrigin;

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
        public event Action? OnUndoRequested;
        public event Action? OnRedoRequested;

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

        // ── UCS İkonu paint'leri (DrawUCSIcon her frame koşulsuz çağrılır) ─────────────────
        private readonly SKPaint _ucsXPaint      = new() { Color = new SKColor(220, 50, 50), StrokeWidth = 2.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        private readonly SKPaint _ucsYPaint      = new() { Color = new SKColor(50, 220, 50), StrokeWidth = 2.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        private readonly SKPaint _ucsXLabelPaint = new() { Color = new SKColor(220, 50, 50), TextSize = 12, IsAntialias = true, FakeBoldText = true };
        private readonly SKPaint _ucsYLabelPaint = new() { Color = new SKColor(50, 220, 50), TextSize = 12, IsAntialias = true, FakeBoldText = true };
        private readonly SKPaint _ucsCenterPaint = new() { Color = new SKColor(180, 180, 180), StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };

        // ── Snap marker paint'leri (tip başına stroke/glow/text — DrawSnapMarker sık çağrılır) ─
        private readonly System.Collections.Generic.Dictionary<SnapPointType, SKPaint> _snapStrokePaints = new();
        private readonly System.Collections.Generic.Dictionary<SnapPointType, SKPaint> _snapGlowPaints = new();
        private readonly System.Collections.Generic.Dictionary<SnapPointType, SKPaint> _snapTextPaints = new();

        // ── Polar Tracking / Object Snap Tracking çizim paint'leri ───────────────────────
        private readonly SKPaint _polarTrackLinePaint = new() { Color = new SKColor(255, 190, 0, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = true, PathEffect = SKPathEffect.CreateDash(new float[] { 6, 4 }, 0) };
        private readonly SKPaint _polarTrackTextPaint = new() { Color = new SKColor(255, 190, 0), TextSize = 12, IsAntialias = true, FakeBoldText = true };
        private readonly SKPaint _otrackLinePaint = new() { Color = new SKColor(0, 255, 140, 190), Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = true, PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0) };
        private readonly SKPaint _otrackMarkerPaint = new() { Color = new SKColor(0, 255, 140, 220), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };

        // ── Seçim kutusu paint'leri (window/crossing × fill/stroke — sürükleme sırasında sık çağrılır) ─
        private readonly SKPaint _selBoxWindowFill = new() { Color = new SKColor(52, 152, 219, 80), Style = SKPaintStyle.Fill };
        private readonly SKPaint _selBoxWindowStroke = new() { Color = new SKColor(52, 152, 219), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        private readonly SKPaint _selBoxCrossingFill = new() { Color = new SKColor(46, 204, 113, 80), Style = SKPaintStyle.Fill };
        private readonly SKPaint _selBoxCrossingStroke = new() { Color = new SKColor(46, 204, 113), Style = SKPaintStyle.Stroke, StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0) };

        // NE: Kalıcı Render Context (SkiaRenderContext)
        // NEDEN: Önceden OnPaintSurface HER FRAME'de "new SkiaRenderContext(...)" ile bu sınıfı
        // sıfırdan yaratıyordu. Sınıfın paint cache'i (_paintCache/_textPaintCache) instance alanı
        // olduğu için bu, cache'in hiçbir zaman isabet almaması ve binlerce SKPaint'in hiç
        // Dispose edilmeden birikmesi (native memory leak) anlamına geliyordu. Artık viewport
        // ömrü boyunca TEK instance tutuluyor; her frame'de sadece SetCanvas ile canvas/pixelSize
        // güncelleniyor, cache'ler kalıcı kalıyor.
        private SkiaRenderContext? _renderContext;

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
            _ucsXPaint?.Dispose();
            _ucsYPaint?.Dispose();
            _ucsXLabelPaint?.Dispose();
            _ucsYLabelPaint?.Dispose();
            _ucsCenterPaint?.Dispose();
            foreach (var p in _snapStrokePaints.Values) p?.Dispose();
            foreach (var p in _snapGlowPaints.Values) p?.Dispose();
            foreach (var p in _snapTextPaints.Values) p?.Dispose();
            _polarTrackLinePaint?.Dispose();
            _polarTrackTextPaint?.Dispose();
            _otrackLinePaint?.Dispose();
            _otrackMarkerPaint?.Dispose();
            _selBoxWindowFill?.Dispose();
            _selBoxWindowStroke?.Dispose();
            _selBoxCrossingFill?.Dispose();
            _selBoxCrossingStroke?.Dispose();
            _renderContext?.Dispose();
            _renderContext = null;
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
            // Yeni komut başladığında/bittiğinde önceki tracking zincirini sıfırla (AutoCAD davranışı).
            _objectSnapTracking.ClearAcquired();
            _activePolarTrack = null;
            _activeOTrackLines = null;
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
        public bool HasActiveCommand => _activeCommand != null;

        /*
           NE: Koordinat/Mesafe Girişini Kabul Et (Direct Distance Entry)
           NEDEN: AutoCAD gibi komut aktifken klavyeyle mesafe veya koordinat girilmesine izin vermek için.
           FORMAT:
             "10000"        → fare yönünde 10000 birim (ORTHO aktifse 90° snap)
             "1000,2000"    → mutlak X,Y koordinatı
             "@1000,2000"   → son noktaya göre rölatif X,Y
             "@1000<45"     → son noktadan 45 derecede 1000 birim (polar)
        */
        public bool AcceptCoordinateInput(string raw)
        {
            if (_activeCommand == null) return false;
            raw = raw.Trim();
            if (string.IsNullOrEmpty(raw)) return false;

            Vector3D? targetPoint = null;
            var lastPt = _activeCommand.ActivePoint;

            try
            {
                // FORMAT: @dist<angle  (polar)
                if (raw.StartsWith("@") && raw.Contains('<'))
                {
                    var parts = raw.Substring(1).Split('<');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dist) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double angleDeg))
                    {
                        var rad = angleDeg * Math.PI / 180.0;
                        var from = lastPt ?? _lastMouseWorldPos ?? new Vector3D(0, 0, 0);
                        targetPoint = new Vector3D(from.X + dist * Math.Cos(rad), from.Y + dist * Math.Sin(rad), 0);
                    }
                }
                // FORMAT: @dx,dy  (rölatif)
                else if (raw.StartsWith("@") && raw.Contains(','))
                {
                    var parts = raw.Substring(1).Split(',');
                    if (parts.Length >= 2 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dx) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dy))
                    {
                        var from = lastPt ?? _lastMouseWorldPos ?? new Vector3D(0, 0, 0);
                        targetPoint = new Vector3D(from.X + dx, from.Y + dy, 0);
                    }
                }
                // FORMAT: x,y  (mutlak koordinat)
                else if (raw.Contains(','))
                {
                    var parts = raw.Split(',');
                    if (parts.Length >= 2 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ax) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ay))
                    {
                        targetPoint = new Vector3D(ax, ay, 0);
                    }
                }
                // FORMAT: mesafe  (Direct Distance Entry — fare yönünde)
                else if (double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double distance))
                {
                    var from = lastPt ?? _lastMouseWorldPos;
                    var mouse = _lastMouseWorldPos;
                    if (from.HasValue && mouse.HasValue)
                    {
                        double dx = mouse.Value.X - from.Value.X;
                        double dy = mouse.Value.Y - from.Value.Y;

                        // ORTHO aktifse 90° açı snap
                        if (IsOrthoEnabled)
                        {
                            if (Math.Abs(dx) >= Math.Abs(dy))
                                targetPoint = new Vector3D(from.Value.X + (dx >= 0 ? distance : -distance), from.Value.Y, 0);
                            else
                                targetPoint = new Vector3D(from.Value.X, from.Value.Y + (dy >= 0 ? distance : -distance), 0);
                        }
                        else
                        {
                            double len = Math.Sqrt(dx * dx + dy * dy);
                            if (len > 0)
                                targetPoint = new Vector3D(from.Value.X + (dx / len) * distance, from.Value.Y + (dy / len) * distance, 0);
                        }
                    }
                    else if (from.HasValue)
                    {
                        // Mouse pozisyonu yok — sağa doğru varsay
                        targetPoint = new Vector3D(from.Value.X + distance, from.Value.Y, 0);
                    }
                }
            }
            catch { return false; }

            if (targetPoint == null) return false;

            _activeCommand.OnPointerPressed(targetPoint.Value);
            InvalidateViewport();
            return true;
        }

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
                  Session #55: gerçek Direct3D11 B-Rep render'a bağlandı — 3D moda geçişte
                  `Viewport3D` (Direct3DViewportControl) görünür yapılır ve güncel veritabanı
                  yüklenir; 2D moda dönüşte tekrar gizlenir (render döngüsü `IsVisible` ile
                  otomatik duraklıyor, bkz. Direct3DViewportControl.OnRendering).
        */
        public void SetViewMode(bool isIsometric)
        {
            _isIsometric = isIsometric;

            if (isIsometric)
            {
                if (_database != null) Viewport3D.LoadFromDatabase(_database);
                Viewport3D.Visibility = Visibility.Visible;
            }
            else
            {
                Viewport3D.Visibility = Visibility.Collapsed;
            }

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
    }
