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
using Serilog;

namespace Afney.Cad.Presentation.Views;

public partial class CadViewport : UserControl
{
    private CadDatabase? _database;
    private SnapEngine? _snapEngine;
    private SelectionManager? _selectionManager;
    private ICadCommand? _activeCommand;

    private double _zoom = 1.0;
    private Vector3D _offset = new Vector3D(0, 0, 0);
    private bool _isIsometric = false;

    private SnapPoint? _activeSnap;
    private Vector3D? _lastMouseWorldPos;

    private bool _isPanning = false;
    private Point _lastMousePosition;
    private DateTime _lastMiddleClickTime = DateTime.MinValue;

    private bool _isSelecting = false;
    private Point _selectionStartPoint;
    private Point _selectionCurrentPoint;

    private DateTime _lastMouseMoveTime = DateTime.MinValue;
    private CadEntity? _hoveredEntity;

    public event Action<string>? OnFeedback;
    public event Action<System.Collections.Generic.IEnumerable<CadEntity>>? SelectionChanged;

    // --- Katman Yönetimi (Layer Management) ---
    public System.Collections.Generic.HashSet<string> HiddenLayers { get; } = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly SKPaint _gridPaint = new() { Color = new SKColor(60, 60, 60, 50), Style = SKPaintStyle.Stroke, IsAntialias = true }; // Daha yumuşak grid
    private readonly SKPaint _axisPaint = new() { Color = new SKColor(100, 100, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
    private readonly SKPaint _crosshairPaint = new() { Color = SKColors.White.WithAlpha(180), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true }; // Yumuşak imleç

    /*
       NE: CadViewport Yapıcı Metodu
       NEDEN: Bileşenleri belleğe yükler ve XAML tanımlarını başlatır.
    */
    public CadViewport()
    {
        InitializeComponent();
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
        InvalidateViewport();
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
        if (_database == null || !_database.GetAllEntities().Any())
        {
             _zoom = 1.0;
             _offset = new Vector3D(0, 0, 0);
             InvalidateViewport();
             return;
        }

        // AKILLI ZOOM (Smart Zoom Extents)
        // 1. Tüm nesnelerin merkez noktalarını al
        var centers = _database.GetAllEntities().Select(e => e.GetBoundingBox().Center).ToList();
        if (!centers.Any()) return;

        // 2. Ortalama merkezi bul (Centroid)
        double avgX = centers.Any() ? centers.Average(c => c.X) : 0;
        double avgY = centers.Any() ? centers.Average(c => c.Y) : 0;
        
        if (double.IsNaN(avgX) || double.IsNaN(avgY)) { avgX = 0; avgY = 0; }

        // 3. Standart Sapma veya Basit Eşikleme ile Aykırıları At (Outlier Removal)
        // Basitleştirilmiş Yöntem: Merkezden çok uzak (ör: 100km) olanları yoksay
        double threshold = 500000.0; // 500km yarıçap (Mimari için yeterli)
        var validEntities = _database.GetAllEntities()
            .Where(e => Math.Abs(e.GetBoundingBox().Center.X - avgX) < threshold && 
                        Math.Abs(e.GetBoundingBox().Center.Y - avgY) < threshold)
            .ToList();

        if (!validEntities.Any()) validEntities = _database.GetAllEntities().ToList(); // Hepsi aykırıysa mecburen hepsini al

        // 4. Geçerli nesneler için Bounding Box hesapla
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var ent in validEntities)
        {
            var b = ent.GetBoundingBox();
            
            // NaN/Infinity koruması
            if (double.IsNaN(b.Min.X) || double.IsInfinity(b.Min.X)) continue;

            minX = Math.Min(minX, b.Min.X); minY = Math.Min(minY, b.Min.Y);
            maxX = Math.Max(maxX, b.Max.X); maxY = Math.Max(maxY, b.Max.Y);
        }

        // Eğer hala mantıklı bir kutu yoksa (veya tek nokta varsa) varsayılan değerler
        if (minX == double.MaxValue) { minX = 0; maxX = 1000; minY = 0; maxY = 1000; }

        double width = (maxX - minX) * 1.1; // %10 boşluk
        double height = (maxY - minY) * 1.1;
        if (width <= 0) width = 1000; if (height <= 0) height = 1000;

        double screenW = CadCanvas.ActualWidth > 0 ? CadCanvas.ActualWidth : 800;
        double screenH = CadCanvas.ActualHeight > 0 ? CadCanvas.ActualHeight : 600;

        _zoom = Math.Min(screenW / width, screenH / height);
        // Zoom sınırlarını genişlet (Çok küçük detaylar için daha fazla zoom gerekebilir)
        _zoom = Math.Clamp(_zoom, 1e-6, 100.0); 
        if (double.IsNaN(_zoom) || double.IsInfinity(_zoom)) _zoom = 1.0;

        double cx = (minX + maxX) / 2.0;
        double cy = (minY + maxY) / 2.0;

        double offX = screenW / 2.0 - (cx * _zoom);
        double offY = screenH / 2.0 - (cy * _zoom);

        if (double.IsNaN(offX) || double.IsInfinity(offX)) offX = 0;
        if (double.IsNaN(offY) || double.IsInfinity(offY)) offY = 0;

        _offset = new Vector3D(offX, offY, 0);

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
        canvas.Clear(SKColors.Black); // Tam Siyah Arka Plan (AutoCAD Klasik)

        float density = (float)(e.Info.Width / CadCanvas.ActualWidth);
        canvas.Scale(density);

        var pixelSize = _zoom > 0 ? 1.0 / _zoom : 1.0;
        var renderContext = new SkiaRenderContext(canvas, pixelSize);
        renderContext.IsIsometric = _isIsometric;
        renderContext.SetCamera(_offset, _zoom);

        // Grid
        DrawInfiniteGrid(canvas, e.Info);

        // --- B Çözümü: Otomatik Hizalama Kılavuzu (Auto-Align Origin Guide) ---
        // Kullanıcının mimariyi üst üste dizebilmesi için 0,0 noktasına devasa lazerler çiz
        var originProjected = renderContext.GetType().GetMethod("Project", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(renderContext, new object[] { new Vector3D(0, 0, 0) }) as SKPoint? ?? new SKPoint(0,0);
        
        using (var penX = new SKPaint { Color = new SKColor(255, 50, 50, 150), StrokeWidth = 2, IsAntialias = true })
        using (var penY = new SKPaint { Color = new SKColor(50, 255, 50, 150), StrokeWidth = 2, IsAntialias = true })
        using (var textPaint = new SKPaint { Color = SKColors.White, TextSize = 14, IsAntialias = true })
        {
            float w = e.Info.Width;
            float h = e.Info.Height;
            // X Ekseni (Kırmızı)
            canvas.DrawLine(0, originProjected.Y, w, originProjected.Y, penX);
            // Y Ekseni (Yeşil)
            canvas.DrawLine(originProjected.X, 0, originProjected.X, h, penY);
            // Kılavuz Etiketi
            canvas.DrawText("ORIGIN (0,0,0) - AUTO ALIGN GUIDE", originProjected.X + 10, originProjected.Y - 10, textPaint);
            canvas.DrawCircle(originProjected.X, originProjected.Y, 5, penX);
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
        
        // Selection Highlighting (Sarı)
        _selectionManager?.DrawSelection(canvas, vec => {
            var p = WorldToScreen(vec);
            return new SKPoint((float)p.X, (float)p.Y);
        });
        
        if (_lastMouseWorldPos.HasValue) DrawFullScreenCrosshair(canvas, e.Info);
        if (_activeSnap.HasValue) DrawSnapMarker(canvas, _activeSnap.Value);
        _activeCommand?.Draw(renderContext);

        if (_isSelecting) DrawSelectionBox(canvas);
    }

    /*
       NE: Sonsuz Grid Çizme
       NEDEN: Kullanıcının derinlik ve mesafe algısını kolaylaştıran, zoom seviyesine göre dinamik olarak ölçeklenen bir ızgara yapısı çizer.
    */
    private void DrawInfiniteGrid(SKCanvas canvas, SKImageInfo info)
    {
        var tl = ScreenToWorld(new Point(0, 0));
        var br = ScreenToWorld(new Point(info.Width, info.Height));

        double step = 100.0; // 100 birimlik ana ızgara
        if (_zoom < 0.1) step = 1000.0;
        if (_zoom < 0.01) step = 10000.0;
        if (_zoom > 5.0) step = 10.0;

        using var paint = new SKPaint
        {
            Color = SKColors.DarkSlateGray.WithAlpha(50),
            StrokeWidth = 1,
            IsAntialias = true
        };

        for (double x = Math.Floor(tl.X / step) * step; x <= br.X; x += step)
        {
            var p1 = WorldToScreen(new Vector3D(x, tl.Y, 0));
            var p2 = WorldToScreen(new Vector3D(x, br.Y, 0));
            canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, paint);
        }

        for (double y = Math.Floor(br.Y / step) * step; y <= tl.Y; y += step)
        {
            var p1 = WorldToScreen(new Vector3D(tl.X, y, 0));
            var p2 = WorldToScreen(new Vector3D(br.X, y, 0));
            canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, paint);
        }
    }

    /*
       NE: Tam Ekran Artı Göstergesi (Crosshair)
       NEDEN: AutoCAD benzeri bir deneyim için fare imlecinin hizasını ekran boyunca dikey ve yatay çizgilerle göstermek için.
    */
    private void DrawFullScreenCrosshair(SKCanvas canvas, SKImageInfo info)
    {
        if (!_lastMouseWorldPos.HasValue) return;
        var p = WorldToScreen(_lastMouseWorldPos.Value);
        canvas.DrawLine(0, (float)p.Y, info.Width, (float)p.Y, _crosshairPaint);
        canvas.DrawLine((float)p.X, 0, (float)p.X, info.Height, _crosshairPaint);
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
    
    // Eski DrawSnapMarker metodu aşağıdadır.
    /*
       NE: Snap Marker Çizici (DrawSnapMarker)
       NEDEN: Fare bir noktanın üzerine geldiğinde yakalanan yerin tipine göre (Uç nokta: Kare, Orta nokta: Üçgen vb.) sembol çizmek için.
    */
    private void DrawSnapMarker(SKCanvas canvas, SnapPoint snap)
    {
        // ... (Eski kod aynen devam eder)
        var rawP = WorldToScreen(snap.Position);
        var p = new SKPoint((float)rawP.X, (float)rawP.Y);

        using var paint = new SKPaint 
        { 
            Color = new SKColor(255, 165, 0), 
            Style = SKPaintStyle.Stroke, 
            StrokeWidth = 2,
            IsAntialias = true
        };

        float size = 10f; 

        switch (snap.Type)
        {
            case SnapPointType.Endpoint: 
                canvas.DrawRect(p.X - size/2, p.Y - size/2, size, size, paint);
                break;
            case SnapPointType.Midpoint: 
                using (var path = new SKPath())
                {
                    path.MoveTo(p.X, p.Y - size/1.5f);
                    path.LineTo(p.X - size/1.5f, p.Y + size/1.5f);
                    path.LineTo(p.X + size/1.5f, p.Y + size/1.5f);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
            case SnapPointType.Center: 
                canvas.DrawCircle(p.X, p.Y, size/1.5f, paint);
                break;
            case SnapPointType.Quadrant: 
                using (var path = new SKPath())
                {
                    path.MoveTo(p.X, p.Y - size/1.5f);
                    path.LineTo(p.X + size/1.5f, p.Y);
                    path.LineTo(p.X, p.Y + size/1.5f);
                    path.LineTo(p.X - size/1.5f, p.Y);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
            case SnapPointType.Intersection: 
                canvas.DrawLine(p.X - size/1.5f, p.Y - size/1.5f, p.X + size/1.5f, p.Y + size/1.5f, paint);
                canvas.DrawLine(p.X - size/1.5f, p.Y + size/1.5f, p.X + size/1.5f, p.Y - size/1.5f, paint);
                break;
            case SnapPointType.Perpendicular: 
                 using (var path = new SKPath())
                {
                    path.MoveTo(p.X - size/1.5f, p.Y + size/1.5f);
                    path.LineTo(p.X - size/1.5f, p.Y - size/1.5f);
                    path.LineTo(p.X + size/1.5f, p.Y - size/1.5f);
                    path.MoveTo(p.X - size/1.5f, p.Y);
                    path.LineTo(p.X, p.Y);
                    path.LineTo(p.X, p.Y - size/1.5f);
                    canvas.DrawPath(path, paint);
                }
                break;
            default: 
                canvas.DrawRect(p.X - size/2, p.Y - size/2, size, size, paint);
                break;
        }
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
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            if (_activeCommand != null && _lastMouseWorldPos.HasValue)
                _activeCommand.OnPointerPressed(_lastMouseWorldPos.Value);
            else { _isSelecting = true; _selectionStartPoint = _lastMousePosition; _selectionCurrentPoint = _lastMousePosition; CadCanvas.CaptureMouse(); }
        }
        else if (e.ChangedButton == MouseButton.Right && _activeCommand != null)
        {
            _activeCommand.Cancel();
            SetActiveCommand(null);
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
            _offset = new Vector3D(_offset.X + delta.X, _offset.Y + delta.Y, 0);
            _lastMousePosition = currentPos;
        }
        else if (_isSelecting)
        {
            _selectionCurrentPoint = currentPos;
        }

        if (_snapEngine != null)
            _activeSnap = _snapEngine.FindSnapPoint(worldPos, 15.0 / _zoom, _activeCommand?.ActivePoint);

        if (_activeSnap.HasValue) _lastMouseWorldPos = _activeSnap.Value.Position;

        if (_activeCommand != null && _lastMouseWorldPos.HasValue)
            _activeCommand.OnPointerMoved(_lastMouseWorldPos.Value);

        // UI Güncelleme (CoordinateText XAML İsmi ile Uyumlu)
        if (CoordinateText != null)
            CoordinateText.Text = $"X: {worldPos.X:F2}, Y: {worldPos.Y:F2}";

        if ((DateTime.Now - _lastMouseMoveTime).TotalMilliseconds > 16)
        {
            _lastMouseMoveTime = DateTime.Now;
            InvalidateViewport();
        }
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
                Serilog.Log.Information("🖱️ Pan modu kapandı");
                return;
            }
            
            // LEFT BUTTON: Seçim veya Komut
            if (e.ChangedButton == MouseButton.Left && _isSelecting)
            {
                _isSelecting = false;
                
                if (_selectionManager != null)
                {
                    try
                    {
                        var rect = new CadBoundingBox(
                            ScreenToWorld(_selectionStartPoint), 
                            ScreenToWorld(_selectionCurrentPoint));
                        
                        bool isCrossing = _selectionCurrentPoint.X < _selectionStartPoint.X;
                        
                        Serilog.Log.Information("🎯 SEÇİM: {Type}, Rect: ({MinX},{MinY}) → ({MaxX},{MaxY})", 
                            isCrossing ? "Crossing" : "Window",
                            rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y);
                        
                        // SHIFT tuşu basılı değilse öncekiler temizle
                        if (Keyboard.Modifiers != ModifierKeys.Shift) 
                            _selectionManager.ClearSelection();
                        
                        // Crossing veya Window selection
                        if (isCrossing)
                            _selectionManager.SelectByCrossing(rect);
                        else
                            _selectionManager.SelectByWindow(rect);
                        
                        SelectionChanged?.Invoke(_selectionManager.GetSelectedEntities());
                        Serilog.Log.Information("✅ Seçim tamamlandı: {Count} entity", _selectionManager.SelectedCount);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "❌ Seçim sırasında hata!");
                    }
                }
            }
        }
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
       NE: Fare Tekerleği Olayı (MouseWheel)
       NEDEN: Fare tekerleği ile çizime yakınlaşmak (Zoom In) veya uzaklaşmak (Zoom Out) için.
    */
    private void CadCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(CadCanvas);
        var worldPosBefore = ScreenToWorld(mousePos);
        _zoom *= (e.Delta > 0 ? 1.25 : 1.0 / 1.25);
        _zoom = Math.Clamp(_zoom, 1e-6, 1e6);
        _offset = new Vector3D(mousePos.X - (worldPosBefore.X * _zoom), mousePos.Y - (worldPosBefore.Y * _zoom), 0);
        
        if (ZoomText != null) 
            ZoomText.Text = $"Z: {_zoom:F3}";

        InvalidateViewport();
    }

    /*
       NE: Klavye Olay Yöneticisi (KeyDown)
       NEDEN: ESC (İptal), DELETE (Sil) ve ENTER (Onay) gibi AutoCAD standart klavye etkileşimlerini handle etmek için.
    */
    private void CadCanvas_KeyDown(object sender, KeyEventArgs e)
    {
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
                if (CadCanvas.IsMouseCaptured)
                    CadCanvas.ReleaseMouseCapture();
                OnFeedback?.Invoke("Seçim iptal edildi (ESC)");
                Serilog.Log.Information("❌ Seçim modu iptal edildi");
            }
            
            // 3. Pan modu aktifse kapat
            if (_isPanning)
            {
                _isPanning = false;
                if (CadCanvas.IsMouseCaptured)
                    CadCanvas.ReleaseMouseCapture();
                OnFeedback?.Invoke("Pan modu kapatıldı (ESC)");
                Serilog.Log.Information("❌ Pan modu kapatıldı");
            }
            
            // 4. Seçimi temizle
            if (_selectionManager != null && _selectionManager.SelectedCount > 0)
            {
                _selectionManager.ClearSelection();
                OnFeedback?.Invoke("Seçim temizlendi (ESC)");
                Serilog.Log.Information("🧹 Seçim temizlendi (ESC)");
            }
            
            InvalidateViewport();
            e.Handled = true;
        }
        
        // DELETE TUŞU: SEÇİLİ ENTİTYLERİ SİL
        else if (e.Key == Key.Delete)
        {
            if (_selectionManager != null && _selectionManager.SelectedCount > 0)
            {
                var selectedCount = _selectionManager.SelectedCount;
                // TODO: Transaction ile silme işlemi
                OnFeedback?.Invoke($"DELETE: {selectedCount} entity silinecek (henüz implementasyon yok)");
                Serilog.Log.Information("🗑️ DELETE tuşuna basıldı: {Count} entity", selectedCount);
            }
            e.Handled = true;
        }
        
        // ENTER TUŞU: Aktif komutu tamamla (bazı komutlar için)
        else if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (_activeCommand != null)
            {
                // Komut kendi içinde ENTER'ı handle edebilir
                OnFeedback?.Invoke("ENTER tuşuna basıldı");
                Serilog.Log.Information("✅ ENTER tuşuna basıldı (aktif komut: {Name})", _activeCommand.GetType().Name);
            }
            e.Handled = true;
        }
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
    private void OnContextMenu_Pan(object sender, RoutedEventArgs e)
    {
        // Pan modunu aktif et (Middle mouse button gibi)
        _isPanning = true;
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
            // TODO: Properties dialog aç
        }
        else
        {
            OnFeedback?.Invoke("Properties: Hiçbir entity seçili değil");
        }
    }
}
