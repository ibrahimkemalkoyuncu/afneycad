using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Akıllı Mahal Sınırı Tespit Servisi (SmartBoundaryService) v2
    NEDEN: FINE SANI standardında, tıklanan iç noktadan odanın duvar köşelerini bularak
           gerçek bir mimari polygon (dikdörtgen, L, U-şekli vb.) üretmek için.

    NASIL (Mühendislik Modu - Segment Endpoint Snapping):
    1. ArchitecturalRecognitionService.FindEnclosedArea → açısal sıralı ışın-çarpma noktaları (ham)
    2. Ham noktaların her biri için en yakın duvar segmenti uç noktasına (endpoint) snap yapılır.
    3. Ardışık tekrar eden snap noktaları tekilleştirilir → bunlar oda köşeleridir.
    4. Köşe sayısı yetersizse (< 3), ham noktalar Douglas-Peucker ile sadeleştirilerek kullanılır.
    5. Polignon geçerliliği (min alan, saat yönü) doğrulanır.

    NEDEN BU YAKLAŞIM:
    - Önceki yaklaşım: 1080 ışın noktasını açısal sırayla döndürüyordu.
      Sorun: Aynı duvar yüzeyine vuran 50+ ardışık ışın, 50 farklı "kenar" oluşturuyordu.
      Polygon "pürüzlü" ve cihaz tespiti için kullanılamaz hale geliyordu.
    - Bu yaklaşım: Her ışın noktasını duvarın gerçek uç noktasına snap'liyor.
      Sonuç: 4 köşeli bir oda için tam 4, L-şekli için tam 6 köşe üretiliyor.
*/
public class SmartBoundaryService
{
    private readonly CadDatabase _database;

    /*
        Snap Toleransı: Bir ışın-çarpma noktası bu mesafe içinde bir duvar uç noktasına
        yakınsa, o uç noktaya "atlanır". Değer birim cinsinden (mm/m scale bağımsız
        çalışır çünkü ortalama çizgi uzunluğuna göre dinamik ayarlanır).
    */
    private const double MaxSnapMm = 200.0;   // mm — snap toleransı üst sınır
    private const double ClosureGapMm = 400.0; // mm — son-ilk nokta bu kadar yakınsa polygon kapat

    public SmartBoundaryService(CadDatabase database)
    {
        _database = database;
    }

    /*
        NE: Mahal Sınırlarını Bul (FindBoundary) v2
        AMACI: Verilen iç nokta etrafındaki duvarları bulup GERÇEK köşe poligonu üretmek.

        DÖNDÜRÜR: Köşe sayısı <= mimari odanın kenar sayısına (4..16) eşit bir polygon.
                  null → kapalı alan yok veya yetersiz geometri.
    */
    public List<Vector3D>? FindBoundary(Vector3D startPoint)
    {
        // 1. Adım: Raw ray cast noktalarını al (açısal sıralı, duvar yüzeyine snap edilmemiş)
        var archService = new ArchitecturalRecognitionService(_database);
        var rawPoints = archService.FindEnclosedArea(startPoint);

        if (rawPoints == null || rawPoints.Count < 3)
        {
            Serilog.Log.Warning("[SmartBoundary] Ham ray cast yeterli nokta üretmedi: {Count}", rawPoints?.Count ?? 0);
            return null;
        }

        Serilog.Log.Information("[SmartBoundary] Ham ray cast noktaları: {Count}", rawPoints.Count);

        // 2. Adım: Tüm duvar segmentlerini topla (snap kaynağı)
        var segments = CollectWallSegments();
        Serilog.Log.Information("[SmartBoundary] Toplam segment: {Count}", segments.Count);

        List<Vector3D> polygon;

        if (segments.Count > 0)
        {
            // ── A YOLU: Segment endpoint snap ──────────────────────────────
            polygon = SnapToCorners(rawPoints, segments);
            Serilog.Log.Information("[SmartBoundary] Endpoint-snap köşe sayısı: {Count}", polygon.Count);

            // Snap başarısız olduysa (tüm noktalar aynı köşeye snap'lendi vb.) B yoluna geç
            if (polygon.Count < 3)
            {
                Serilog.Log.Warning("[SmartBoundary] Snap yetersiz, Douglas-Peucker'a geçiliyor.");
                polygon = SimplifyWithDouglasPeucker(rawPoints, ComputeEpsilon(rawPoints));
            }
        }
        else
        {
            // ── B YOLU: Segment yok → ham noktaları sadeleştir ─────────────
            Serilog.Log.Warning("[SmartBoundary] Segment bulunamadı, Douglas-Peucker uygulanıyor.");
            polygon = SimplifyWithDouglasPeucker(rawPoints, ComputeEpsilon(rawPoints));
        }

        if (polygon.Count < 3)
        {
            Serilog.Log.Error("[SmartBoundary] Polygon üretilemedi. Nokta sayısı: {Count}", polygon.Count);
            return null;
        }

        // 3. Adım: Polygon'u saat yönünün tersine (CCW) sırala
        EnsureCCW(polygon);

        // 4. Adım: Closure kontrolu — son ve ilk nokta yeterince yakınsa kapat
        //    DWG duvaları birkaç mm ayrık olabilir; bu kontrol yine de polygon'u kapatır.
        double closureGap = polygon[polygon.Count - 1].DistanceTo(polygon[0]);
        if (closureGap > 0 && closureGap <= ClosureGapMm)
        {
            // Son noktasını ilk noktaya snap'le (kesin kapanış)
            polygon[polygon.Count - 1] = polygon[0];
            polygon.RemoveAt(polygon.Count - 1); // çakışan son noktayı kaldır
            Serilog.Log.Information("[SmartBoundary] Closure snap uygulandı: {Gap:F1}mm boşluk kapatıldı.", closureGap);
        }
        else if (closureGap > ClosureGapMm)
        {
            Serilog.Log.Warning("[SmartBoundary] Polygon closure gap çok büyük: {Gap:F1}mm (tolerance: {Tol}mm). Polygon açık.", closureGap, ClosureGapMm);
        }

        // 4. Adım: Alan kontrolü (0 alanlı dejenere polygon'ları at)
        double area = ComputeSignedArea(polygon);
        if (Math.Abs(area) < 1.0) // 1 birimkare minimum
        {
            Serilog.Log.Error("[SmartBoundary] Poligon alanı çok küçük: {Area}", area);
            return null;
        }

        Serilog.Log.Information("[SmartBoundary] ✅ Final polygon: {Count} köşe, Alan: {Area:F1}", polygon.Count, Math.Abs(area));
        return polygon;
    }

    /*
        NE: Duvar Segmentlerini Topla (CollectWallSegments)
        NEDEN: Tüm Line ve LwPolyline varlıklarından segment (başlangıç, bitiş noktası çiftleri) listesi üretmek.
               Bu liste, snap işleminde endpoint kaynağı olarak kullanılır.
    */
    private List<(Vector3D P1, Vector3D P2)> CollectWallSegments()
    {
        var segs = new List<(Vector3D, Vector3D)>();
        foreach (var ent in _database.GetAllEntities())
        {
            if (ent is LineEntity line && line.StartPoint.DistanceTo(line.EndPoint) > 1.0)
            {
                segs.Add((line.StartPoint, line.EndPoint));
            }
            else if (ent is LwPolylineEntity poly && poly.Vertices.Count >= 2)
            {
                for (int i = 0; i < poly.Vertices.Count - 1; i++)
                    segs.Add((poly.Vertices[i], poly.Vertices[i + 1]));
                if (poly.IsClosed && poly.Vertices.Count > 2)
                    segs.Add((poly.Vertices[poly.Vertices.Count - 1], poly.Vertices[0]));
            }
        }
        return segs;
    }

    /*
        NE: Segment Endpoint Snap ile Köşe Poligonu Üret (SnapToCorners)
        NEDEN: Ham ışın-çarpma noktalarının çoğu bir duvar segmentinin ortasına düşer.
               Gerçek oda köşeleri ise iki duvarın birleştiği segment uç noktalarıdır.
               Bu metod, her ham noktayı en yakın duvar uç noktasına snap'ler ve
               ardışık tekrarları silerek gerçek köşe kümesini elde eder.

        ALGORİTMA:
        1. Her raw_point için tüm segment endpoint'lerini tara.
        2. En yakın endpoint'i bul. Eğer mesafe < snapTolerance ise snap.
        3. Snap sonucu ardışık listeye ekle. Son eklenen ile aynı ise atla (tekilleştir).
        4. Sonuçtaki listeyi döndür.
    */
    private List<Vector3D> SnapToCorners(List<Vector3D> rawPoints, List<(Vector3D P1, Vector3D P2)> segments)
    {
        // Snap toleransını çizim ölçeğine göre belirle:
        // Ortalama segment uzunluğunun %3'i, ama EN FAZLA MaxSnapMm.
        // NEDEN %15 değil %3: 3000mm'lik duvarda %15 = 450mm snap — yanlış köşeye snap yapar.
        // %3 = 90mm — gerçek bitisçim boşlukları için yeterli, yänliş snap riski çok düşük.
        double avgLen = segments.Average(s => s.P1.DistanceTo(s.P2));
        double snapTol = Math.Min(avgLen * 0.03, MaxSnapMm);
        snapTol = Math.Max(snapTol, 5.0); // En az 5mm (mikro ayrıklıklar için)

        Serilog.Log.Information("[SmartBoundary] Snap toleransı: {Tol:F1}mm (ortalama segment: {Avg:F1}mm)", snapTol, avgLen);

        // Tüm unique endpoint'leri topla
        var endpoints = new List<Vector3D>(capacity: segments.Count * 2);
        foreach (var (p1, p2) in segments)
        {
            endpoints.Add(p1);
            endpoints.Add(p2);
        }

        // Her ham nokta için en yakın endpoint'i bul (snap veya orijinal)
        var snapped = new List<Vector3D>(rawPoints.Count);
        foreach (var raw in rawPoints)
        {
            Vector3D best = raw;
            double bestDist = double.MaxValue;
            foreach (var ep in endpoints)
            {
                double d = raw.DistanceTo(ep);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = ep;
                }
            }
            // Snap sadece tolerans içinde ise uygula
            snapped.Add(bestDist <= snapTol ? best : raw);
        }

        // Ardışık tekrar eden noktaları kaldır (tekilleştirme)
        var corners = new List<Vector3D>();
        for (int i = 0; i < snapped.Count; i++)
        {
            var cur = snapped[i];
            var prev = corners.Count > 0 ? corners[corners.Count - 1] : (Vector3D?)null;

            // Öncekiyle aynı endpoint'e snap'lendiyse atla
            if (prev.HasValue && cur.DistanceTo(prev.Value) < 1.0) continue;

            corners.Add(cur);
        }

        // İlk ve son aynı ise son kaldır (kapalı polygon)
        if (corners.Count > 1 && corners[0].DistanceTo(corners[corners.Count - 1]) < 1.0)
            corners.RemoveAt(corners.Count - 1);

        // İkinci tekilleştirme geçişi: snap sonrası hâlâ çok yakın komşular varsa birleştir
        corners = MergeCloseVertices(corners, snapTol * 0.3);

        return corners;
    }

    /*
        NE: Yakın Köşeleri Birleştir (MergeCloseVertices)
        NEDEN: Snap sonrası iki ardışık köşe hâlâ çok yakınsa (duvar kalınlığı,
               çizim hatası) bunları tek noktaya indirge.
    */
    private List<Vector3D> MergeCloseVertices(List<Vector3D> pts, double mergeRadius)
    {
        if (pts.Count <= 1) return pts;
        var result = new List<Vector3D> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            if (pts[i].DistanceTo(result[result.Count - 1]) > mergeRadius)
                result.Add(pts[i]);
        }
        return result;
    }

    /*
        NE: Douglas-Peucker ile Sadeleştirme (SimplifyWithDouglasPeucker)
        NEDEN: Segment snap başarısız olduğunda, 1080 raw point'i ε-epsilon
               toleransla köşe noktalarına indirgemek için.
        NASIL: Özyinelemeli bölme: iki uç nokta arasındaki en uzak noktayı bul,
               eğer > epsilon ise böl, değilse ara noktaları at.
    */
    private List<Vector3D> SimplifyWithDouglasPeucker(List<Vector3D> pts, double epsilon)
    {
        if (pts.Count <= 2) return new List<Vector3D>(pts);

        // İki uç arasındaki en uzak noktayı bul
        int maxIdx = 0;
        double maxDist = 0;
        var first = pts[0];
        var last = pts[pts.Count - 1];

        for (int i = 1; i < pts.Count - 1; i++)
        {
            double d = PerpendicularDistance(pts[i], first, last);
            if (d > maxDist) { maxDist = d; maxIdx = i; }
        }

        if (maxDist > epsilon)
        {
            // Böl
            var left = SimplifyWithDouglasPeucker(pts.GetRange(0, maxIdx + 1), epsilon);
            var right = SimplifyWithDouglasPeucker(pts.GetRange(maxIdx, pts.Count - maxIdx), epsilon);
            // Birleştir (sınır noktası çift gelir)
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }
        else
        {
            // Ara noktaları at
            return new List<Vector3D> { first, last };
        }
    }

    private static double PerpendicularDistance(Vector3D p, Vector3D a, Vector3D b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return p.DistanceTo(a);
        return Math.Abs(dy * p.X - dx * p.Y + b.X * a.Y - b.Y * a.X) / len;
    }

    /*
        NE: Epsilon Hesapla (ComputeEpsilon)
        NEDEN: Douglas-Peucker için scale-bağımsız epsilon değeri üretmek.
               Points'in bounding-box çapının %1'ini epsilon olarak kullan.
    */
    private static double ComputeEpsilon(List<Vector3D> pts)
    {
        if (pts.Count == 0) return 100.0;
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
        double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        double diag = Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2));
        return Math.Max(diag * 0.01, 50.0);
    }

    /*
        NE: Çokgeni Saatin Tersine Sırala (EnsureCCW)
        NEDEN: WPF ve çoğu rendering engine CCW polygon bekler. Saat yönündeyse ters çevir.
    */
    private static void EnsureCCW(List<Vector3D> poly)
    {
        if (ComputeSignedArea(poly) < 0)
            poly.Reverse();
    }

    /*
        NE: İşaretli Alan (ComputeSignedArea)
        NEDEN: Shoelace (Gauss) formülü ile polygon alanı ve yönünü belirlemek.
               Pozitif = CCW, Negatif = CW.
    */
    private static double ComputeSignedArea(List<Vector3D> poly)
    {
        int n = poly.Count;
        double area = 0;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            area += (a.X * b.Y) - (b.X * a.Y);
        }
        return area / 2.0;
    }

    /*
        NE: Mahal İçindeki Cihazları Tespit Et (GetFixturesInBoundary)
        NEDEN: Odanın içindeki vitrifiyeleri otomatik bulup hesaplamaya dahil etmek için.
    */
    public List<Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity> GetFixturesInBoundary(List<Vector3D> boundary)
    {
        var fixtures = _database.GetAllEntities().OfType<Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity>().ToList();
        return fixtures.Where(f => IsPointInPolygon(f.Position, boundary)).ToList();
    }

    /*
        NE: Poligon İçinde Nokta Testi (IsPointInPolygon)
        NASIL: Jordan Curve Theorem (Ray Casting).
    */
    private bool IsPointInPolygon(Vector3D p, List<Vector3D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                 (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                inside = !inside;
        }
        return inside;
    }
}
