using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Algorithms;

public static class GeomUtils
{
    // Akıllı Ray Casting Algoritması
    // Bir noktadan verilen yönde (Direction) en yakın çizgiyi bulur.
    public static (LineSegment? HitSegment, Vector3D? HitPoint, double Distance) 
        RayCast(Vector3D origin, Vector3D direction, IEnumerable<LineSegment> segments, double maxRange = 100000)
    {
        LineSegment? closestSegment = null;
        Vector3D? closestPoint = null;
        double minDistance = maxRange;

        foreach (var seg in segments)
        {
            // Intersection Logic (Ray vs Line Segment)
            // Ray: Origin + t * Direction (t > 0)
            // Segment: Start + u * (End - Start) (0 <= u <= 1)
            
            // 2D Kesişim (XY Plane)
            double x1 = origin.X; double y1 = origin.Y;
            double dx1 = direction.X; double dy1 = direction.Y;
            
            double x3 = seg.Start.X; double y3 = seg.Start.Y;
            double dx3 = seg.End.X - seg.Start.X; double dy3 = seg.End.Y - seg.Start.Y;
            
            double det = dx1 * dy3 - dy1 * dx3;
            if (Math.Abs(det) < 1e-9) continue; // Parallel

            double t = ((x3 - x1) * dy3 - (y3 - y1) * dx3) / det;
            double u = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / det;

            if (t > 1e-9 && t < minDistance && u >= 0 && u <= 1)
            {
                minDistance = t;
                closestSegment = seg;
                closestPoint = new Vector3D(x1 + t * dx1, y1 + t * dy1, 0);
            }
        }
        
        return (closestSegment, closestPoint, minDistance);
    }
    
    // Gap Tolerance ile Bağlantı Kontrolü
    public static bool ArePointsConnected(Vector3D p1, Vector3D p2, double tolerance)
    {
        double distSq = Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.Z - p2.Z, 2);
        return distSq <= (tolerance * tolerance);
    }

    /*
       NE: Yakındaki Çizgileri Bul (FindNearbySegments)
       NEDEN: Duvar takibi yaparken, mevcut çizginin ucuna yakın (tolerance dahilinde) olan diğer aday çizgileri bulmak için.
       DETAY: Ray Casting algoritmasının "Sonraki Adım" (Next Hop) fonksiyonudur.
    */
    public static List<(LineSegment Segment, Vector3D StartPoint, Vector3D EndPoint)> FindNearbySegments(
        Vector3D searchPoint, 
        IEnumerable<LineSegment> segments, 
        LineSegment currentSegment, // Kendi kendine dönmemesi için
        double tolerance)
    {
        var result = new List<(LineSegment, Vector3D, Vector3D)>();
        double tolSq = tolerance * tolerance;

        foreach (var seg in segments)
        {
            // Kendisiyle karşılaştırma (Struct olduğu için değer kontrolü veya ref kontrolü zor, basitçe koordinat bak)
            // Ama buradaki 'currentSegment' referans değil değer.
            // En iyisi: Koordinatları birebir aynıysa atla.
            if (seg.Start == currentSegment.Start && seg.End == currentSegment.End) continue;

            // Start ucu yakın mı?
            double d1 = Math.Pow(seg.Start.X - searchPoint.X, 2) + Math.Pow(seg.Start.Y - searchPoint.Y, 2);
            if (d1 <= tolSq)
            {
                result.Add((seg, seg.Start, seg.End)); // Start'tan girdik, End'e gideceğiz
                continue;
            }

            // End ucu yakın mı?
            double d2 = Math.Pow(seg.End.X - searchPoint.X, 2) + Math.Pow(seg.End.Y - searchPoint.Y, 2);
            if (d2 <= tolSq)
            {
                result.Add((seg, seg.End, seg.Start)); // End'den girdik, Start'a gideceğiz
            }
        }
        return result;
    }

    /*
       NE: Saat Yönünde Açı Hesapla (CalculateClockwiseAngle)
       NEDEN: Odanın içini taramak için "En Sağdaki" (Rightmost) dönüşü seçmek gerekir.
       GİRDİ: Geliş Vektörü (currentDir) ve Aday Vektör (nextDir).
       ÇIKTI: 0-360 derece arası açı.
    */
    public static double CalculateClockwiseAngle(Vector3D currentDir, Vector3D nextDir)
    {
        // Vector açısı: atan2(y, x)
        double ang1 = Math.Atan2(currentDir.Y, currentDir.X);
        double ang2 = Math.Atan2(nextDir.Y, nextDir.X);
        
        // Radyan -> Derece
        double deg1 = ang1 * 180.0 / Math.PI;
        double deg2 = ang2 * 180.0 / Math.PI;
        
        // Fark (Saat Yönünde Dönüş: current -> next)
        // Matematiksel açı CCW artar (Doğu=0, Kuzey=90).
        // Biz "Sağa Dönüş" arıyoruz. Vektörün baktığı yöne göre sağ taraf.
        
        // Basit açı farkı hesapla
        double diff = deg1 - deg2; // CCW sisteminde, deg1'den deg2'ye gitmek için ne kadar "Sağa" dönülür?
        
        // Normalize (0-360)
        while (diff < 0) diff += 360.0;
        while (diff >= 360) diff -= 360.0;
        
        return diff;
    }
    /*
       NE: Çizgi Parçası Kesişimi (DoSegmentsIntersect)
       NEDEN: Boru çakışmalarını veya borunun duvardan geçip geçmediğini anlamak için. (Boolean Logic 2D)
    */
    public static bool DoSegmentsIntersect(Vector3D a, Vector3D b, Vector3D c, Vector3D d, out Vector3D intersection)
    {
        intersection = new Vector3D(0, 0, 0);
        
        double den = (d.Y - c.Y) * (b.X - a.X) - (d.X - c.X) * (b.Y - a.Y);
        if (Math.Abs(den) < 1e-9) return false;

        double ua = ((d.X - c.X) * (a.Y - c.Y) - (d.Y - c.Y) * (a.X - c.X)) / den;
        double ub = ((b.X - a.X) * (a.Y - c.Y) - (b.Y - a.Y) * (a.X - c.X)) / den;

        if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
        {
            intersection = new Vector3D(a.X + ua * (b.X - a.X), a.Y + ua * (b.Y - a.Y), 0);
            return true;
        }
        return false;
    }

    /*
       NE: Noktanın Çizgiye Uzaklığı (PointToSegmentDistance)
       NEDEN: Snapping (Yakalama) mesafesi ölçmek veya "bu nokta bu borunun yakınında mı?" kontrolü için.
    */
    public static double PointToSegmentDistance(Vector3D p, Vector3D a, Vector3D b)
    {
        double l2 = Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2);
        if (l2 == 0.0) return p.DistanceTo(a);

        double t = ((p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y)) / l2;
        t = Math.Max(0, Math.Min(1, t));

        var projection = new Vector3D(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), 0);
        return p.DistanceTo(projection);
    }

    /*
       NE: Nokta Çokgenin İçinde mi? (IsPointInPolygon)
       NEDEN: Bulunan mahal sınırları içindeki tefrişleri (blokları) tespit etmek için.
    */
    public static bool IsPointInPolygon(Vector3D point, List<Vector3D> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if (polygon[i].Y < point.Y && polygon[j].Y >= point.Y || polygon[j].Y < point.Y && polygon[i].Y >= point.Y)
            {
                if (polygon[i].X + (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < point.X)
                {
                    inside = !inside;
                }
            }
            j = i;
        }
        return inside;
    }
}
