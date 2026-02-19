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

public class SkiaRenderContext : IRenderContext
{
    private readonly SKCanvas _canvas;
    private readonly Dictionary<uint, SKPaint> _paintCache = new();
    private readonly Dictionary<string, SKPaint> _textPaintCache = new();

    public double PixelSize { get; }
    public bool IsIsometric { get; set; } = false;

    public SkiaRenderContext(SKCanvas canvas, double pixelSize)
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
        string key = $"{color}_{thickness:F3}_{isDashed}_{linetype}";
        uint hKey = (uint)key.GetHashCode();

        if (!_paintCache.TryGetValue(hKey, out var paint))
        {
            // MÜHENDİSLİK MODU: Hairline (Kıl Çizgi) Teknolojisi
            // StrokeWidth = 0f yaparak çizginin her zoom seviyesinde 1 piksel (Jilet gibi) görünmesini sağlarız.
            // Sadece çok kalın çizgiler (Polyline width > 0) fiziksel kalınlıkla çizilir.
            bool isThick = thickness > 1.5; 

            paint = new SKPaint
            {
                Color = new SKColor(color),
                StrokeWidth = isThick ? (float)thickness : 0f, 
                IsAntialias = true, // Yumuşatma şart
                Style = SKPaintStyle.Stroke,
                FilterQuality = SKFilterQuality.High, // En yüksek kalite filtreleme
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

            _paintCache[hKey] = paint;
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
        // FONT AYARI: AutoCAD standardına en uygun "Arial" veya "ISOCPEUR" (yoksa Arial döner)
        // Consolas yerine teknik çizim fontu kullanıldı.
        string key = $"{color}_{fontSize}_{centerAlign}_Arial";
        if (!_textPaintCache.TryGetValue(key, out var paint))
        {
            paint = new SKPaint
            {
                Color = new SKColor(color),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                TextSize = (float)(fontSize * _zoomFactor), // Font, zoom ile büyümeli
                TextAlign = centerAlign ? SKTextAlign.Center : SKTextAlign.Left,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 
                SubpixelText = true,
                LcdRenderText = true // LCD ekranlarda daha net yazı
            };
            _textPaintCache[key] = paint;
        }
        else
        {
             // TextSize dinamik olduğu için cache'den gelse bile güncellemek gerekebilir (Performans optimizasyonu için burada basitleştirildi)
             // Doğrusu: Font boyutu değiştiyse yeni paint. Şimdilik her frame'de TextSize güncelliyoruz.
             paint.TextSize = (float)(fontSize * _zoomFactor); 
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
    public void DrawSpline(IEnumerable<Vector3D> points, uint color, double thickness, string linetype = "Continuous")
    {
        var pts = points.ToList();
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
}
