using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Yüz-Yüz Kesişimi (FaceIntersection)
   NEDEN: PlaneIntersection'ın ürettiği SONSUZ kesişim doğrusunu, iki Face'in GERÇEK poligon
          sınırlarına kırpıp (clip) gerçek kesişim segmentlerini bulmak — CSG boolean'ın
          "yüz bölme" (Faz 2, docs/Roadmap_CSG_Boolean.md) adımının girdisi.

   YÖNTEM: Kesişim doğrusu, tanım gereği her iki düzlemin İÇİNDE yatar — yani her Face'in
   kendi düzlemiyle EŞ DÜZLEMSEL (coplanar). Bu yüzden doğruyu her Face'in 2D yerel bazına
   izdüşürüp (PolygonTriangulator'daki ComputePlaneBasis ile aynı teknik — bilgi kaybı YOK,
   çünkü doğru zaten o düzlemde), poligon sınırıyla kesişen t-parametre aralıklarını (doğru
   üzerindeki "içeride" segmentleri) bulup, iki Face'in aralıklarının KESİŞİMİNİ (∩) alıyoruz.
*/
public static class FaceIntersection
{
    public readonly record struct Segment(Vector3D Start, Vector3D End);

    /*
       NE: İki Face'in gerçek kesişim segmentlerini döner.
       KAPSAM DIŞI (dejenere, Faz 1): paralel/çakışık düzlemler, doğrunun bir poligon
       kenarıyla tam çakışması — bu durumlarda boş liste döner (sessiz yanlış sonuç yerine).
    */
    public static List<Segment> Intersect(Face faceA, Face faceB)
    {
        var loopA = faceA.GetOuterLoop();
        var loopB = faceB.GetOuterLoop();
        if (loopA == null || loopB == null) return new List<Segment>();

        var polyA = loopA.GetOrderedVertices().Select(v => v.Position).ToList();
        var polyB = loopB.GetOrderedVertices().Select(v => v.Position).ToList();
        if (polyA.Count < 3 || polyB.Count < 3) return new List<Segment>();

        var normalA = faceA.Normal.Normalize();
        var normalB = faceB.Normal.Normalize();

        var line = PlaneIntersection.Intersect(polyA[0], normalA, polyB[0], normalB);
        if (line == null) return new List<Segment>();

        var (point0, dir) = line.Value;

        var rangeA = ClipLineToPolygon(point0, dir, polyA, normalA);
        var rangeB = ClipLineToPolygon(point0, dir, polyB, normalB);
        if (rangeA.Count == 0 || rangeB.Count == 0) return new List<Segment>();

        var result = new List<Segment>();
        foreach (var (aLo, aHi) in rangeA)
        {
            foreach (var (bLo, bHi) in rangeB)
            {
                double lo = Math.Max(aLo, bLo);
                double hi = Math.Min(aHi, bHi);
                if (hi - lo > 1e-6)
                    result.Add(new Segment(point0 + dir * lo, point0 + dir * hi));
            }
        }
        return result;
    }

    /*
       NE: Sonsuz bir doğrunun, düzlemsel bir poligonun İÇİNDE kaldığı t-parametre
       aralıklarını bulur (t: point0 + t*dir).
       YÖNTEM: Doğrunun poligonun her kenarıyla 2D kesişim t'lerini topla, sırala; ardışık
       t'ler arasındaki orta noktanın poligon içinde olup olmadığını test ederek (nokta-içi-
       poligon), o aralığın "içeride" olup olmadığına karar ver.
    */
    private static List<(double Lo, double Hi)> ClipLineToPolygon(
        Vector3D point0, Vector3D dir, List<Vector3D> polygon, Vector3D normal)
    {
        var (basisU, basisV) = ComputePlaneBasis(normal);

        (double X, double Y) To2D(Vector3D p) => (p.Dot(basisU), p.Dot(basisV));

        var poly2D = polygon.Select(To2D).ToList();
        var p0 = To2D(point0);
        var d = (X: dir.Dot(basisU), Y: dir.Dot(basisV));

        int n = poly2D.Count;
        var crossings = new List<double>();

        for (int i = 0; i < n; i++)
        {
            var a = poly2D[i];
            var b = poly2D[(i + 1) % n];

            // doğru: p0 + t*d  |  kenar: a + s*(b-a), s∈[0,1]
            // Çözüm (Cramer): [d.X -ex; d.Y -ey][t;s] = [a.X-p0.X; a.Y-p0.Y]
            double ex = b.X - a.X, ey = b.Y - a.Y;
            double denom = ex * d.Y - ey * d.X;
            if (Math.Abs(denom) < 1e-12) continue; // doğru kenara paralel

            double t = (ex * (a.Y - p0.Y) - ey * (a.X - p0.X)) / denom;
            double s = (d.X * (a.Y - p0.Y) - d.Y * (a.X - p0.X)) / denom;

            if (s >= -1e-9 && s <= 1 + 1e-9)
                crossings.Add(t);
        }

        crossings.Sort();
        var dedup = new List<double>();
        foreach (var t in crossings)
        {
            if (dedup.Count == 0 || t - dedup[^1] > 1e-9) dedup.Add(t);
        }

        var ranges = new List<(double, double)>();
        for (int i = 0; i < dedup.Count - 1; i++)
        {
            double mid = (dedup[i] + dedup[i + 1]) / 2.0;
            var midPoint2D = (X: p0.X + mid * d.X, Y: p0.Y + mid * d.Y);
            if (IsPointInPolygon2D(midPoint2D, poly2D))
                ranges.Add((dedup[i], dedup[i + 1]));
        }
        return ranges;
    }

    private static bool IsPointInPolygon2D((double X, double Y) p, List<(double X, double Y)> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if (((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    private static (Vector3D U, Vector3D V) ComputePlaneBasis(Vector3D normal)
    {
        var n = normal.Normalize();
        if (n.LengthSquared() < 1e-12) n = Vector3D.ZAxis;

        var reference = Math.Abs(n.Z) < 0.9 ? Vector3D.ZAxis : Vector3D.XAxis;
        var u = reference.Cross(n).Normalize();
        var v = n.Cross(u).Normalize();
        return (u, v);
    }
}
