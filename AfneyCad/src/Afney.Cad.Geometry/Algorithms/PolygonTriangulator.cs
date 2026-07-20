using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Algorithms;

/*
   NE: Düzlemsel Poligon Üçgenleme (PolygonTriangulator)
   NEDEN: B-Rep Face'leri (herhangi bir 3D düzlemde, dış bükey olması zorunlu olmayan basit
          kapalı poligonlar) render için üçgen mesh'e dönüştürülmeli. GeomUtils'te nokta-içinde
          testi vardı ama genel bir üçgenleme yoktu.

   ALGORİTMA: Ear Clipping (kulak kesme), O(n²).
   1. Poligonun normaline göre en dominant eksen düşürülerek 2D'ye izdüşürülür (Vertex'leri
      3D'de tutmaya gerek yok — üçgenleme sadece topoloji/indeks üretir, 3D koordinatlar
      çağıran tarafından orijinal listeden okunur).
   2. Sarım yönü (winding) CCW'ye normalize edilir.
   3. Her adımda "kulak" (bir köşe + iki komşusu, üçgeni başka hiçbir vertex içermeyen ve
      dış bükey olan) bulunup kesilir, ta ki 1 üçgen kalana kadar.
*/
public static class PolygonTriangulator
{
    /*
       NE: 3D düzlemsel poligonu üçgenlere ayırır.
       DÖNÜŞ: Girdi listesindeki indekslere (0-tabanlı) göre üçgen üçlüleri.
    */
    public static List<(int A, int B, int C)> Triangulate(IReadOnlyList<Vector3D> polygon, Vector3D normal)
    {
        var result = new List<(int, int, int)>();
        int n = polygon.Count;
        if (n < 3) return result;
        if (n == 3) { result.Add((0, 1, 2)); return result; }

        var (basisU, basisV) = ComputePlaneBasis(normal);

        var pts2D = new (double X, double Y)[n];
        for (int i = 0; i < n; i++)
        {
            pts2D[i] = (polygon[i].Dot(basisU), polygon[i].Dot(basisV));
        }

        double signedArea = SignedArea(pts2D);
        var indices = new List<int>(n);
        for (int i = 0; i < n; i++) indices.Add(i);
        bool clockwise = signedArea < 0;
        if (clockwise) indices.Reverse();

        int guard = 0;
        int maxIterations = n * n + 8;

        while (indices.Count > 3 && guard++ < maxIterations)
        {
            bool earFound = false;
            int m = indices.Count;

            for (int i = 0; i < m; i++)
            {
                int iPrev = indices[(i - 1 + m) % m];
                int iCurr = indices[i];
                int iNext = indices[(i + 1) % m];

                var a = pts2D[iPrev];
                var b = pts2D[iCurr];
                var c = pts2D[iNext];

                if (!IsConvex(a, b, c)) continue;

                bool anyInside = false;
                for (int j = 0; j < m; j++)
                {
                    int idx = indices[j];
                    if (idx == iPrev || idx == iCurr || idx == iNext) continue;
                    if (PointInTriangle(pts2D[idx], a, b, c)) { anyInside = true; break; }
                }
                if (anyInside) continue;

                result.Add((iPrev, iCurr, iNext));
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound) break; // dejenere/self-intersecting poligon — kalanı fan ile kapat
        }

        if (indices.Count == 3)
        {
            result.Add((indices[0], indices[1], indices[2]));
        }
        else if (indices.Count > 3)
        {
            for (int i = 1; i < indices.Count - 1; i++)
                result.Add((indices[0], indices[i], indices[i + 1]));
        }

        return result;
    }

    private static (Vector3D U, Vector3D V) ComputePlaneBasis(Vector3D normal)
    {
        var n = normal.Normalize();
        if (n.LengthSquared() < 1e-12) n = Vector3D.ZAxis;

        var reference = System.Math.Abs(n.Z) < 0.9 ? Vector3D.ZAxis : Vector3D.XAxis;
        var u = reference.Cross(n).Normalize();
        var v = n.Cross(u).Normalize();
        return (u, v);
    }

    private static double SignedArea((double X, double Y)[] pts)
    {
        double area = 0;
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            var p1 = pts[i];
            var p2 = pts[(i + 1) % n];
            area += p1.X * p2.Y - p2.X * p1.Y;
        }
        return area / 2.0;
    }

    private static bool IsConvex((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        return cross > 1e-12;
    }

    private static bool PointInTriangle((double X, double Y) p, (double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        double d1 = Cross(p, a, b);
        double d2 = Cross(p, b, c);
        double d3 = Cross(p, c, a);

        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;

        return !(hasNeg && hasPos);
    }

    private static double Cross((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        => (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
}
