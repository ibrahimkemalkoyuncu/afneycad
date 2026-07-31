using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Dışbükey Poligon Kesişimi (ConvexPolygonClipper2D) — CSG Boolean için 3. yapı taşı
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — `CoplanarFaceDetector`'dan (2. yapı taşı, coplanar
          YÜZ ÇİFTİ tespiti) sonraki adım. İki Face coplanar bulunduğunda, B'nin yüz izdüşümü
          A'nınkiyle TAM ÇAKIŞIK olmayabilir (ör. B'nin yüzü A'nınkinden küçük/kaymış) — bu
          durumda genel SUBTRACT'in "B'nin A içinde kalan payı" kararını verebilmesi için
          gerçek bir 2D poligon KESİŞİMİ (intersection) gerekir.

   KAPSAM (bilinçli, dar — araştırma ajanının bulgusu): Gerçek endüstri standardı (Vatti/
   Martinez-Rueda sweep-line, Clipper/GPC'nin temeli) rastgele (içbükey, kendiyle kesişen,
   delikli, çok parçalı) poligonları destekler ama tek dosyada, tek oturumda SAĞLAM yazılması
   gerçekçi değil (üretim kütüphaneleri bunun için binlerce satır kullanır). Bu projenin
   GERÇEK mevcut kullanımı (`*BRepService.cs` — duvar/kanal/kapı/pencere, hepsi
   `BRepBuilder.ExtrudeBox`/`ExtrudePolygon` çıktısı) SADECE dışbükey (çoğunlukla dikdörtgen)
   yüzler üretiyor. Bu yüzden burada SADECE dışbükey∩dışbükey kesişimi (genelleştirilmiş
   Sutherland-Hodgman / yarı-düzlem kırpma) uygulanıyor — matematiksel olarak dışbükey iki
   poligonun kesişimi HER ZAMAN tek, dışbükey bir bölge veya boştur (çok-parçalı sonuç
   İMKANSIZ), bu yüzden `List<Vector3D>` (tek loop) dönüş tipi dürüst bir sözleşmedir.

   KAPSAM DIŞI (bilinçli, açık hatayla korunuyor — sessiz yanlış sonuç yerine): içbükey girdi
   poligonları (dışbükeylik testi başarısız olursa `InvalidOperationException`), delikli
   poligonlar, UNION/DIFFERENCE (SUBTRACT'in coplanar-payı kararı sadece INTERSECT'e ihtiyaç
   duyuyor — bkz. roadmap). İleride içbükey/delikli genel poligon boolean gerekirse (ör.
   içbükey bir mahal sınırı), Vatti veya Martinez-Rueda sweep-line algoritmasına bakılmalı.
*/
public static class ConvexPolygonClipper2D
{
    private const double Tolerance = 1e-9;

    /*
       NE: İki dışbükey, coplanar poligonun kesişimini döner (yarı-düzlem kırpma).
       NASIL: polyB, poligon B'nin HER kenarının tanımladığı yarı-düzlemle sırayla polyA'yı
       kırpar (Sutherland-Hodgman) — polyA/polyB önce Face'in 2D yerel bazına (normal'e göre,
       `FaceIntersection.ComputePlaneBasis` ile aynı teknik) izdüşürülür, kırpma 2D'de yapılır,
       sonuç 3D'ye geri izdüşürülür.
       ÖN KOŞUL: her iki poligon da dışbükey, basit (kendiyle kesişmeyen), tek döngü olmalı —
       değilse `InvalidOperationException` (dejenere/kapsam dışı girdide sessiz yanlış sonuç
       üretmemek için).
    */
    public static List<Vector3D> Intersect(List<Vector3D> polyA, List<Vector3D> polyB, Vector3D normal)
    {
        if (polyA.Count < 3 || polyB.Count < 3)
            throw new InvalidOperationException("ConvexPolygonClipper2D.Intersect: her iki poligon da en az 3 köşeye sahip olmalı.");

        var (basisU, basisV) = ComputePlaneBasis(normal);
        var origin = polyA[0];

        (double X, double Y) To2D(Vector3D p)
        {
            var d = p - origin;
            return (d.Dot(basisU), d.Dot(basisV));
        }
        Vector3D To3D((double X, double Y) p) => origin + basisU * p.X + basisV * p.Y;

        var a2D = polyA.Select(To2D).ToList();
        var b2D = polyB.Select(To2D).ToList();

        if (!IsConvex(a2D)) throw new InvalidOperationException("ConvexPolygonClipper2D.Intersect: polyA dışbükey değil — kapsam dışı.");
        if (!IsConvex(b2D)) throw new InvalidOperationException("ConvexPolygonClipper2D.Intersect: polyB dışbükey değil — kapsam dışı.");

        a2D = EnsureCounterClockwise(a2D);
        b2D = EnsureCounterClockwise(b2D);

        var clipped = a2D;
        int n = b2D.Count;
        for (int i = 0; i < n && clipped.Count > 0; i++)
        {
            var edgeStart = b2D[i];
            var edgeEnd = b2D[(i + 1) % n];
            clipped = ClipAgainstHalfPlane(clipped, edgeStart, edgeEnd);
        }

        return clipped.Select(To3D).ToList();
    }

    // Sutherland-Hodgman: subject poligonu (edgeStart→edgeEnd) kenarının SOL (iç) tarafında kalacak şekilde kırp.
    private static List<(double X, double Y)> ClipAgainstHalfPlane(
        List<(double X, double Y)> subject, (double X, double Y) edgeStart, (double X, double Y) edgeEnd)
    {
        var output = new List<(double X, double Y)>();
        int n = subject.Count;
        if (n == 0) return output;

        double Cross((double X, double Y) o, (double X, double Y) p, (double X, double Y) q) =>
            (p.X - o.X) * (q.Y - o.Y) - (p.Y - o.Y) * (q.X - o.X);

        for (int i = 0; i < n; i++)
        {
            var current = subject[i];
            var previous = subject[(i - 1 + n) % n];

            bool currentInside = Cross(edgeStart, edgeEnd, current) >= -Tolerance;
            bool previousInside = Cross(edgeStart, edgeEnd, previous) >= -Tolerance;

            if (currentInside)
            {
                if (!previousInside)
                    output.Add(LineIntersection(previous, current, edgeStart, edgeEnd));
                output.Add(current);
            }
            else if (previousInside)
            {
                output.Add(LineIntersection(previous, current, edgeStart, edgeEnd));
            }
        }
        return output;
    }

    private static (double X, double Y) LineIntersection(
        (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, (double X, double Y) p4)
    {
        double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
        double denom = d1x * d2y - d1y * d2x;
        if (Math.Abs(denom) < 1e-12) return p1; // paralel — çağıran kırpma mantığı bu durumu zaten önler

        double t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denom;
        return (p1.X + t * d1x, p1.Y + t * d1y);
    }

    private static bool IsConvex(List<(double X, double Y)> poly)
    {
        int n = poly.Count;
        if (n < 3) return false;

        int sign = 0;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            var c = poly[(i + 2) % n];
            double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(cross) < Tolerance) continue; // ardışık köşeler — dejenere değil, sadece bilgi vermiyor

            int currentSign = Math.Sign(cross);
            if (sign == 0) sign = currentSign;
            else if (currentSign != sign) return false;
        }
        return true;
    }

    private static List<(double X, double Y)> EnsureCounterClockwise(List<(double X, double Y)> poly)
    {
        double signedArea = 0;
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            signedArea += a.X * b.Y - b.X * a.Y;
        }
        return signedArea < 0 ? poly.AsEnumerable().Reverse().ToList() : poly;
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
