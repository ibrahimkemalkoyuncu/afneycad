/*
 * DOSYA: SkiaRenderContext.cs
 * AMAÇ: CAD nesnelerini ekran kartı (GPU) destekli çizim motoru (SkiaSharp) ile ekrana basmak.
 * SORUMLULUKLARI:
 * 1. Koordinat Dönüşümü: Dünya (3D) -> Ekran (2D) dönüşümünü (Zoom/Pan dahil) yapmak.
 * 2. Kaynak Yönetimi: Paint (Boya) ve Font nesnelerini önbellekte tutarak performansı artırmak.
 * 3. Çizgi Tipi (Linetype): Kesik, Noktalı, Eksen çizgilerini AutoCAD standartlarına göre simüle etmek.
 * 
 * MÜHENDİSLİK DETAYI (Mete & Kemal):
 * Bu motor, "Mükemmel Görüntü" için Anti-Aliasing (Yumuşatma) ve Subpixel Rendering teknolojilerini kullanır.
 * Çizgi kalınlıkları "Hairline" (Kıl Çizgi) modunda çalışarak zoomdan bağımsız netlik sağlar.
 */

using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Render.Engines;

public class SkiaRenderContext : IRenderContext, IDisposable
{
    /// <summary>
    /// NE: Paint Önbellek Anahtarı (PaintKey)
    /// NEDEN: Önceden string interpolasyonu + GetHashCode() ile üretilen uint anahtar, hem her
    /// çağrıda allocation yaratıyordu hem de teorik hash-çakışmasında yanlış paint dönebiliyordu.
    /// Struct tabanlı, value-equality'li anahtar hem allocation-free hem çakışmasız.
    /// </summary>
    private readonly record struct PaintKey(uint Color, float Thickness, bool IsDashed, string Linetype);

    /// <summary>
    /// NE: Dolgu Paint Önbellek Anahtarı (FillPaintKey)
    /// NEDEN: DrawFilledPolygon (Hatch) her çağrıda fillPaint/strokePaint'i "using new SKPaint{...}"
    /// ile yaratıyordu. Diğer Draw* metodlarıyla AYNI cache mekanizması (Dictionary + struct key)
    /// kullanılarak fill/stroke paint'leri de artık renk+alpha+style bazında önbelleklenir.
    /// </summary>
    private readonly record struct FillPaintKey(uint Color, byte Alpha, SKPaintStyle Style);

    private readonly Dictionary<FillPaintKey, SKPaint> _fillPaintCache = new();

    // NOT: _canvas ve PixelSize artık viewport ömrü boyunca SetCanvas() ile güncellenir;
    // sınıf her frame'de yeniden yaratılmaz (bkz. CadViewport.OnPaintSurface).
    private SKCanvas _canvas;
    private readonly Dictionary<PaintKey, SKPaint> _paintCache = new();
    private readonly Dictionary<string, SKPaint> _textPaintCache = new();
    private SKPaint? _highlightPaint;
    private bool _disposed;

    public double PixelSize { get; private set; }
    public bool IsIsometric { get; set; } = false;
    public bool IsHighlightMode { get; set; } = false;

    public SkiaRenderContext(SKCanvas canvas, double pixelSize)
    {
        _canvas = canvas;
        PixelSize = pixelSize;
    }

    /// <summary>
    /// NE: Canvas Güncelle (SetCanvas)
    /// NEDEN: SkiaSharp her frame'de yeni bir SKCanvas/SKSurface üretir (WPF OnPaintSurface).
    /// Önceden bu yüzden her frame yeni bir SkiaRenderContext yaratılıyordu ve paint cache'i
    /// hiç isabet almıyordu. Artık tek instance korunuyor, sadece canvas referansı ve
    /// pixelSize her frame'de burada güncelleniyor — paint cache'leri kalıcı kalıyor.
    /// </summary>
    public void SetCanvas(SKCanvas canvas, double pixelSize)
    {
        _canvas = canvas;
        PixelSize = pixelSize;
    }

    /// <summary>
    /// NE: Boya Nesnesi Al (GetPaint)
    /// NEDEN: Her çizim için yeni SKPaint oluşturmak maliyetlidir. Renk, kalınlık ve çizgi tipine göre önbellekten döndürür.
    /// MÜHENDİSLİK: AutoCAD renklerini (ACI) RGB'ye en parlak haliyle eşler.
    /// </summary>
    private SKPaint GetPaint(uint color, double thickness, bool isDashed = false, string linetype = "Continuous")
    {
        if (IsHighlightMode)
        {
            // Vurgu (Selection Glow) Efekti
            // AutoCAD standardı: Seçili nesneler parlak, kalın ve yarı-şeffaf mavi/sarı çizilir.
            // Sabit bir görünüm olduğu için tek bir paint yeterli — artık cache'leniyor,
            // her segment için yeni (sahiplenilmeyen, hiç Dispose edilmeyen) SKPaint yaratılmıyor.
            return _highlightPaint ??= new SKPaint
            {
                Color = new SKColor(255, 255, 0).WithAlpha(200), // Parlak Sarı Glow
                StrokeWidth = 3f, // Kalın sınır
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                FilterQuality = SKFilterQuality.High,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
        }

        var key = new PaintKey(color, (float)thickness, isDashed, linetype ?? "Continuous");

        if (!_paintCache.TryGetValue(key, out var paint))
        {
            // MÜHENDİSLİK MODU: Hairline (Kıl Çizgi) Teknolojisi
            // StrokeWidth = 0f yaparak çizginin her zoom seviyesinde 1 piksel (Jilet gibi) görünmesini sağlarız.
            // Sadece çok kalın çizgiler (Polyline width > 0) fiziksel kalınlıkla çizilir.
            bool isThick = thickness > 1.5;

            paint = new SKPaint
            {
                Color = new SKColor(color),
                StrokeWidth = isThick ? (float)thickness : 0f,
                IsAntialias = isThick, // Kıl çizgiler (hairline) için antialias KAPALI (AutoCAD netliği/crisp)
                Style = SKPaintStyle.Stroke,
                FilterQuality = SKFilterQuality.None, // En net (keskin) piksel görünümü için
                SubpixelText = true // Metin kenarlarını süper netleştirir
            };

            // Çizgi Tipi (Linetype) Simülasyonu (AutoCAD LTSCALE / PSLTSCALE=1)
            if (isDashed || (!string.IsNullOrEmpty(linetype) && !linetype.Equals("Continuous", StringComparison.OrdinalIgnoreCase) && !linetype.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)))
            {
                // MÜHENDİSLİK DÜZELTMESİ (Mete):
                // Biz çizimi "Screen Space" (Ekran Koordinatları) üzerinde yapıyoruz (Project metodu ile).
                // Bu yüzden Dash Array değerleri doğrudan "Piksel" cinsindendir.
                // Zoom faktörüyle çarpmamalıyız. Sabit değer verirsek, zoom yapınca desen ekranda sabit kalır.
                // Bu da AutoCAD'in Paper Space (Layout) görünümüne eşdeğerdir ve en okunaklısıdır.

                float s = 10.0f; // 10 Piksel baz uzunluk (Sabit)

                if (isDashed || linetype.Contains("Dash", StringComparison.OrdinalIgnoreCase) || linetype.Contains("Hidden", StringComparison.OrdinalIgnoreCase))
                    paint.PathEffect = SKPathEffect.CreateDash(new float[] { 2 * s, 1 * s }, 0); // 20px çizgi, 10px boşluk
                else if (linetype.Contains("Center", StringComparison.OrdinalIgnoreCase)) // Eksen Çizgisi
                    paint.PathEffect = SKPathEffect.CreateDash(new float[] { 4 * s, 1 * s, 1 * s, 1 * s }, 0);
                else if (linetype.Contains("Dot", StringComparison.OrdinalIgnoreCase))
                    paint.PathEffect = SKPathEffect.CreateDash(new float[] { 0.2f * s, 1 * s }, 0);
            }

            _paintCache[key] = paint;
        }

        return paint;
    }

    /// <summary>
    /// NE: Dolgu/Kontur Boyası Al (GetFillPaint)
    /// NEDEN: DrawFilledPolygon için renk+alpha+stil kombinasyonuna göre önbellekten SKPaint döndürür
    /// (diğer Draw* metodlarındaki GetPaint() ile aynı desen — her çağrıda "new SKPaint" yaratılmaz).
    /// </summary>
    private SKPaint GetFillPaint(uint color, byte alpha, SKPaintStyle style)
    {
        var key = new FillPaintKey(color, alpha, style);
        if (!_fillPaintCache.TryGetValue(key, out var paint))
        {
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            paint = style == SKPaintStyle.Fill
                ? new SKPaint
                {
                    Color = new SKColor(r, g, b, alpha),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                }
                : new SKPaint
                {
                    Color = new SKColor(r, g, b, alpha),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 0f, // Hairline
                    IsAntialias = false,
                };

            _fillPaintCache[key] = paint;
        }

        return paint;
    }

    private Vector3D _cameraOffset;
    private double _zoomFactor = 1.0;

    /// <summary>
    /// NE: Kamera Ayarla (SetCamera)
    /// NEDEN: Viewport'taki Pan ve Zoom değerlerini Render motoruna iletmek için.
    /// </summary>
    public void SetCamera(Vector3D offset, double zoom)
    {
        _cameraOffset = offset;
        _zoomFactor = zoom;
    }

    /// <summary>
    /// NE: Kaynakları Serbest Bırak (Dispose)
    /// NEDEN: SKPaint unmanaged (native Skia) kaynak tutar; instance artık viewport ömrü boyunca
    /// yaşadığı için (bkz. CadViewport tek alan olarak tutuyor) kontrol kapanırken/unload olurken
    /// tüm cache'lenmiş paint'ler burada tek seferde temizlenir.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var paint in _paintCache.Values) paint.Dispose();
        _paintCache.Clear();

        foreach (var paint in _textPaintCache.Values) paint.Dispose();
        _textPaintCache.Clear();

        foreach (var paint in _fillPaintCache.Values) paint.Dispose();
        _fillPaintCache.Clear();

        _highlightPaint?.Dispose();
        _highlightPaint = null;

        GC.SuppressFinalize(this);
    }

    /*
       NE: Koordinat İzdüşümü (Project)
       NEDEN: 3D dünya koordinatlarını (Meter/mm), mevcut Zoom ve Pan değerlerini kullanarak ekran üzerindeki piksel koordinatlarına dönüştürmek için.
    */
    private SKPoint Project(Vector3D v)
    {
        double x, y;
        if (IsIsometric)
        {
            double cos30 = 0.86602540378;
            double sin30 = 0.5;
            double isoX = (v.X - v.Y) * cos30;
            double isoY = (v.X + v.Y) * sin30 - v.Z;
            x = isoX * _zoomFactor + _cameraOffset.X;
            y = isoY * _zoomFactor + _cameraOffset.Y;
        }
        else
        {
            x = v.X * _zoomFactor + _cameraOffset.X;
            y = v.Y * _zoomFactor + _cameraOffset.Y;
        }
        return new SKPoint((float)x, (float)y);
    }

    /*
       NE: Çizgi Çiz (DrawLine)
       NEDEN: İki dünya koordinatı arasında, belirtilen katman rengi ve çizgi tipinde (Dashed, Hidden vb.) tekil bir doğru parçası çizmek için.
    */
    public void DrawLine(Vector3D start, Vector3D end, uint color, double thickness, string linetype = "Continuous", bool isDashed = false)
    {
        var paint = GetPaint(color, thickness, isDashed, linetype);
        // Çok küçük çizgileri (1 pikselden az) çizme (Performans)
        // Ancak AutoCAD standardı "Her şeyi çiz" der. O yüzden kaldırıyoruz.
        var p1 = Project(start);
        var p2 = Project(end);
        _canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y, paint);
    }

    /*
       NE: Çoklu Çizgi Çiz (DrawLines)
       NEDEN: Birbirini takip eden çizgi segmentlerini, bir dizi halinde optimize edilmiş şekilde (Path kullanarak) render etmek için.
    */
    public void DrawLines(IEnumerable<(Vector3D start, Vector3D end)> segments, uint color, double thickness, string linetype = "Continuous", bool isDashed = false)
    {
        var paint = GetPaint(color, thickness, isDashed, linetype);
        using var path = new SKPath();
        bool first = true;
        foreach (var seg in segments)
        {
            var p1 = Project(seg.start);
            var p2 = Project(seg.end);
            if (first) { path.MoveTo(p1.X, p1.Y); first = false; }
            else if (Math.Abs(path.LastPoint.X - p1.X) > 1 || Math.Abs(path.LastPoint.Y - p1.Y) > 1) // Süreksizse MoveTo
                 path.MoveTo(p1.X, p1.Y);
            
            path.LineTo(p2.X, p2.Y);
        }
        _canvas.DrawPath(path, paint);
    }

    /*
       NE: Çember Çiz (DrawCircle)
       NEDEN: Merkez noktası ve yarıçapı verilen daireyi, dünya biriminden piksele dönüştürerek ekranda görselleştirmek için.
    */
    public void DrawCircle(Vector3D center, double radius, uint color, double thickness, bool isDashed = false)
    {
        var paint = GetPaint(color, thickness, isDashed);
        var p = Project(center);
        float r = (float)(radius * _zoomFactor); 
        // Eğer yarıçap çok küçükse (0.5 pikselden az) nokta olarak çiz
        if (r < 0.5f) _canvas.DrawPoint(p, paint);
        else _canvas.DrawCircle(p.X, p.Y, r, paint); 
    }

    /*
       NE: Yay Çiz (DrawArc)
       NEDEN: Bir daire parçasını (yay), başlangıç ve bitiş açılarını dikkate alarak ekran koordinatlarında çizmek için.
    */
    public void DrawArc(Vector3D center, double radius, double startAngle, double endAngle, uint color, double thickness, bool isDashed = false)
    {
        var paint = GetPaint(color, thickness, isDashed);
        float r = (float)(radius * _zoomFactor);
        var rect = new SKRect((float)(center.X - radius) * (float)_zoomFactor, (float)(center.Y - radius) * (float)_zoomFactor, (float)(center.X + radius) * (float)_zoomFactor, (float)(center.Y + radius) * (float)_zoomFactor);
        
        // Arc için Rect'i ekran koordinatlarına manuel hesapla (Project fonksiyonunu rect için kullanmak zor)
        var pC = Project(center);
        var rectScreen = new SKRect(pC.X - r, pC.Y - r, pC.X + r, pC.Y + r);

        _canvas.DrawArc(rectScreen, (float)startAngle, (float)(endAngle - startAngle), false, paint);
    }

    /*
       NE: Dikdörtgen Çiz (DrawRectangle)
       NEDEN: İki köşe noktası verilen dikdörtgeni, mevcut kamera görünümüne göre ekrana basmak için.
    */
    public void DrawRectangle(Vector3D min, Vector3D max, uint color, double thickness, bool isDashed = false)
    {
        var paint = GetPaint(color, thickness, isDashed);
        var p1 = Project(min);
        var p2 = Project(max);
        _canvas.DrawRect(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y, paint);
    }

    /*
       NE: Metin Yaz (DrawText)
       NEDEN: Teknik etiketleri, boru çaplarını ve oda isimlerini belirtilen açıda ve okunabilir font kalitesinde ekrana basmak için.
    */
    public void DrawText(string text, Vector3D position, double angleDegrees, double fontSize, uint color, bool centerAlign = true)
    {
        // FONT AYARI: Türkçe karakter desteği öncelikli.
        // ISOCPEUR Türkçe karakterleri (ş,ö,ü,ğ,ı,İ,Ş,Ö,Ü,Ğ) desteklemiyor, kaldırıldı.
        // Segoe UI (Windows standart, tam Unicode Türkçe), ardından Arial Unicode MS denenir.
        // Hiçbiri yoksa SKFontManager.MatchCharacter ile Türkçe 'Ş' destekleyen system fontu seçilir.
        string key = $"{color}_{fontSize}_{centerAlign}_CADFont";
        if (!_textPaintCache.TryGetValue(key, out var paint))
        {
            var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
                        ?? SKTypeface.FromFamilyName("Arial Unicode MS", SKFontStyle.Normal)
                        ?? SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
                        ?? SKFontManager.Default.MatchCharacter('\u015e') // 'Ş' destekli font
                        ?? SKTypeface.Default;

            paint = new SKPaint
            {
                Color = new SKColor(color),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                /*
                   MÜHENDİSLİK: Önceden burada "doğru görünüm" için bir piksel-sınırı vardı
                   (sırasıyla 300, 60, 36px denendi, kullanıcı her seferinde "hâlâ büyük" dedi).
                   Araştırma ajanı bulgusu: gerçek AutoCAD/DIMSCALE standardında metin yüksekliği
                   EKRAN PİKSELİNDE değil DÜNYA BİRİMİNDE sabittir — diğer geometri gibi zoom'la
                   doğal olarak büyür/küçülür. Bu yüzden "görünümü düzeltmek için" bir piksel
                   sınırı kavramsal olarak YANLIŞTI — asıl düzeltme veri katmanında yapıldı
                   (DimensionEntity.TextHeight ve DimensionStyleService stilleri, kapı/pencere
                   etiketleriyle [fontSize=80mm, kullanıcının doğru kabul ettiği referans] görsel
                   olarak tutarlı olacak şekilde küçültüldü).
                   ANCAK: bu uygulamada fare tekerleği zoom'u 1e6'ya kadar çıkabiliyor (bkz.
                   CadViewport.xaml.cs CadCanvas_MouseWheel — Math.Clamp(_zoom*factor, 1e-6, 1e6)),
                   gerçek AutoCAD'in aksine. fontSize*zoomFactor'ü SINIRSIZ bırakmak, kullanıcı
                   aşırı yakınlaştırdığında (100mm × 1e6 = 100.000.000px gibi) SkiaSharp'ın
                   dev bir font rasterize etmeye çalışıp ÇÖKMESİNE/donmasına yol açabilir. Bu
                   yüzden "görünümü ayarlayan" bir sınır DEĞİL, sadece ÇÖKME ÖNLEYİCİ, hiçbir
                   normal/hatta çok yakın kullanımda asla dokunulmayacak kadar yüksek bir güvenlik
                   tavanı (4000px) bırakıldı.
                */
                TextSize = (float)Math.Min(fontSize * _zoomFactor, 4000.0),
                TextAlign = centerAlign ? SKTextAlign.Center : SKTextAlign.Left,
                Typeface = typeface, 
                SubpixelText = true,
                LcdRenderText = true // LCD ekranlarda daha net yazı
            };
            _textPaintCache[key] = paint;
        }
        else
        {
             // TextSize dinamik olduğu için cache'den gelse bile güncellemek gerekebilir (Performans optimizasyonu için burada basitleştirildi)
             // Doğrusu: Font boyutu değiştiyse yeni paint. Şimdilik her frame'de TextSize güncelliyoruz.
             paint.TextSize = (float)Math.Min(fontSize * _zoomFactor, 4000.0);
        }

        var p = Project(position);
        _canvas.Save();
        _canvas.Translate(p.X, p.Y);
        // AutoCAD'de yazı açısı ters olabilir, kontrol edilmeli. Şimdilik standart dönme.
        _canvas.RotateDegrees(-(float)angleDegrees); 
        _canvas.DrawText(text, 0, 0, paint);
        _canvas.Restore();
    }

    /*
       NE: Spline Eğrisi Çiz (DrawSpline)
       NEDEN: NURBS veya kontrol noktaları verilen eğrileri, çoklu doğru segmentlerine (Polyline) dönüştürüp akıcı bir şekilde render etmek için.
    */
    public void DrawSpline(IReadOnlyList<Vector3D> points, uint color, double thickness, string linetype = "Continuous")
    {
        var pts = points;
        if (pts.Count < 2) return;
        var paint = GetPaint(color, thickness, false, linetype);
        using var path = new SKPath();
        var pStart = Project(pts[0]);
        path.MoveTo(pStart.X, pStart.Y);
        for (int i = 1; i < pts.Count; i++) { var p = Project(pts[i]); path.LineTo(p.X, p.Y); }
        _canvas.DrawPath(path, paint);
    }

    /*
       NE: Tesisat Borusu Çiz (DrawSolidLine)
       NEDEN: Borunun dış cidarını ve merkez (eksen) çizgisini birlikte çizerek, tesisat görünümünü 2D plan düzleminde gerçekçi şekilde simüle etmek için.
    */
    public void DrawSolidLine(Vector3D p1, Vector3D p2, uint color, double innerDiameter, double outerDiameter)
    {
        // Tesisat Borusu Çizimi (Geliştirilmiş)
        var dir = (p2 - p1).Normalize();
        var normal = new Vector3D(-dir.Y, dir.X, 0);
        double r = outerDiameter / 2.0;

        var v1 = Project(p1 + normal * r);
        var v2 = Project(p2 + normal * r);
        var v3 = Project(p2 - normal * r);
        var v4 = Project(p1 - normal * r);

        var paintBody = GetPaint(color, 2.0); // Boru dış hattı kalın
        _canvas.DrawLine(v1.X, v1.Y, v2.X, v2.Y, paintBody);
        _canvas.DrawLine(v3.X, v3.Y, v4.X, v4.Y, paintBody);

        var paintCenter = GetPaint(color, 1.0, false, "Center"); // Eksen çizgisi
        var c1 = Project(p1);
        var c2 = Project(p2);
        _canvas.DrawLine(c1.X, c1.Y, c2.X, c2.Y, paintCenter);
    }

    /*
       NE: Dolu Çokgen Çiz (DrawFilledPolygon)
       NEDEN: AutoCAD Hatch entity'lerinin solid fill alanlarını yarı şeffaf dolgu olarak render etmek için.
       MÜHENDİSLİK: Alpha değeri ile hem görünürlük hem de altındaki çizgileri kapatmama dengesi kurulur.
    */
    public void DrawFilledPolygon(IEnumerable<Vector3D> vertices, uint color, byte alpha = 80)
    {
        var verts = vertices.ToList();
        if (verts.Count < 3) return;

        // Screen koordinatlarına proje et
        var pts = verts.Select(v => Project(v)).ToArray();

        // SKPath ile kapalı poligon oluştur
        using var path = new SKPath();
        path.MoveTo(pts[0].X, pts[0].Y);
        for (int i = 1; i < pts.Length; i++)
            path.LineTo(pts[i].X, pts[i].Y);
        path.Close();

        // Fill paint — yarı şeffaf dolgu (cache'lenmiş, diğer Draw* metodlarıyla aynı desen)
        var fillPaint = GetFillPaint(color, alpha, SKPaintStyle.Fill);
        _canvas.DrawPath(path, fillPaint);

        // Kontur çizgisi (hairline, opak) — kenarları belirginleştirir
        var strokePaint = GetFillPaint(color, 180, SKPaintStyle.Stroke);
        _canvas.DrawPath(path, strokePaint);
    }
}
