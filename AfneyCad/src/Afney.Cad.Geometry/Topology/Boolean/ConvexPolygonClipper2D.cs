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
   poligonlar, DIFFERENCE (SUBTRACT'in coplanar-payı kararı sadece INTERSECT'e ihtiyaç duyuyor
   — bkz. roadmap). İleride içbükey/delikli genel poligon boolean gerekirse (ör. içbükey bir
   mahal sınırı), Vatti veya Martinez-Rueda sweep-line algoritmasına bakılmalı.

   GÜNCELLEME (2026-08-15, Session #67) — `Union(polyA, polyB, normal)` eklendi:
   `docs/Roadmap_CSG_Boolean.md`'nin "convex-convex 2D union primitifi" olarak ayırdığı yapı
   taşı. Girdi/ön-koşul `Intersect` ile AYNI (dışbükey, basit, tek döngü — değilse
   `InvalidOperationException`). Matematiksel gerekçe: iki dışbükey kümenin birleşimi (kesişiyor
   veya biri diğerini kapsıyorsa) HER ZAMAN basit-bağlantılı, TEK kapalı döngülü bir bölgedir
   (delikli/çok-parçalı olamaz) — ama bu bölge kendisi dışbükey OLMAYABİLİR (köşe-çentiği
   senaryosu, ör. iki karenin köşeden örtüşmesi 8 köşeli bir oktogon üretir). Bu yüzden
   `Intersect`'in yarı-düzlem kırpma tekniği (sonucun HER ZAMAN dışbükey olduğu varsayımına
   dayanıyor) burada KULLANILAMAZ — yerine kenar-tabanlı bir yaklaşım uygulanıyor: her
   poligonun her kenarı, diğer poligonun TÜM kenarlarıyla kesiştirilip alt-segmentlere bölünür,
   "diğer poligonun KESİNLİKLE DIŞINDA" kalan alt-segmentler tutulur (dahil/sınır ~ dışlanır —
   iç sınır olur), tutulan tüm alt-segmentler uç-nokta eşleşmesiyle TEK kapalı döngüye
   zincirlenir. Ayrık (kesişmeyen) girdi çifti bu yüzden AÇIK `InvalidOperationException`
   fırlatır (birleşimleri tek basit poligon değil, iki ayrı bileşen — kapsam dışı).
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

    /*
       NE: İki dışbükey, coplanar poligonun BİRLEŞİMİNİ döner (kenar-tabanlı, Weiler-Atherton'ın
           "iki dışbükey girdi" özel durumu — dosya başı 2026-08-15 güncellemesinde detaylandırıldı).
       NASIL:
         1. Her iki poligon aynı 2D yerel baza izdüşürülür (Intersect ile AYNI teknik), dışbükeylik
            doğrulanır, CCW yönlendirilir.
         2. Tam kapsama/özdeşlik kısa-yolu: B'nin (veya A'nın) TÜM köşeleri diğerinin içinde/
            sınırındaysa, doğrudan diğerinin kendisi döner (kenar-tabanlı genel algoritma da aynı
            sonucu üretir ama bu kısa-yol daha ucuz VE özdeş-poligon durumunda genel algoritmanın
            "iki taraf da tamamen dışlanır → boş sonuç" tuzağından kaçınır).
         3. Genel durum: her poligonun HER kenarı, diğer poligonun TÜM kenarlarıyla kesiştirilip
            alt-segmentlere bölünür; her alt-segmentin ORTA noktası diğer poligonun KESİNLİKLE
            dışındaysa (sınır dahil değil — iç sınır parçaları union'ın dış hattına dahil OLMAMALI)
            o alt-segment tutulur.
         4. A'dan ve B'den tutulan TÜM alt-segmentler, uç-nokta eşleşmesiyle TEK kapalı döngüye
            zincirlenir (zincirleme başarısız/kapanmazsa `InvalidOperationException` — dejenere/
            kapsam dışı, ör. poligonlar sadece TEĞET değiyor ya da kenar-üst-üste-binme var).
       ÖN KOŞUL: `Intersect` ile AYNI (her iki poligon dışbükey, basit, tek döngü).
    */
    public static List<Vector3D> Union(List<Vector3D> polyA, List<Vector3D> polyB, Vector3D normal)
    {
        if (polyA.Count < 3 || polyB.Count < 3)
            throw new InvalidOperationException("ConvexPolygonClipper2D.Union: her iki poligon da en az 3 köşeye sahip olmalı.");

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

        if (!IsConvex(a2D)) throw new InvalidOperationException("ConvexPolygonClipper2D.Union: polyA dışbükey değil — kapsam dışı.");
        if (!IsConvex(b2D)) throw new InvalidOperationException("ConvexPolygonClipper2D.Union: polyB dışbükey değil — kapsam dışı.");

        a2D = EnsureCounterClockwise(a2D);
        b2D = EnsureCounterClockwise(b2D);

        // Tam kapsama/özdeşlik kısa-yolu.
        if (b2D.All(p => IsInsideOrOnBoundary(a2D, p)))
            return a2D.Select(To3D).ToList();
        if (a2D.All(p => IsInsideOrOnBoundary(b2D, p)))
            return b2D.Select(To3D).ToList();

        var outsideSegments = new List<((double X, double Y) Start, (double X, double Y) End)>();
        outsideSegments.AddRange(CollectOutsideBoundarySegments(a2D, b2D));
        outsideSegments.AddRange(CollectOutsideBoundarySegments(b2D, a2D));

        if (outsideSegments.Count == 0)
            throw new InvalidOperationException(
                "ConvexPolygonClipper2D.Union: poligonlar ayrık (kesişmiyor) — birleşimleri tek basit poligon değil, kapsam dışı.");

        var loop = ChainIntoClosedLoop(outsideSegments);
        return loop.Select(To3D).ToList();
    }

    /*
       NE: `subject` poligonunun, `other` poligonunun KESİNLİKLE DIŞINDA kalan sınır alt-
           segmentlerini döner (Union'ın kenar-tabanlı adımı).
       NASIL: her kenar, `other`'ın TÜM kenarlarıyla kesiştirilip parametrik `t` değerlerine göre
           alt-segmentlere bölünür (kesişim yoksa kenarın tamamı tek alt-segment), her alt-
           segmentin ORTA noktası `other`'ın kesinlikle dışındaysa (sınır DAHİL DEĞİL) tutulur.
    */
    private static List<((double X, double Y) Start, (double X, double Y) End)> CollectOutsideBoundarySegments(
        List<(double X, double Y)> subject, List<(double X, double Y)> other)
    {
        var result = new List<((double X, double Y) Start, (double X, double Y) End)>();
        int n = subject.Count;
        int m = other.Count;

        for (int i = 0; i < n; i++)
        {
            var edgeStart = subject[i];
            var edgeEnd = subject[(i + 1) % n];

            var ts = new List<double> { 0.0, 1.0 };
            for (int j = 0; j < m; j++)
            {
                var otherStart = other[j];
                var otherEnd = other[(j + 1) % m];
                if (TryGetSegmentIntersectionParameter(edgeStart, edgeEnd, otherStart, otherEnd, out double t))
                    ts.Add(t);
            }

            ts.Sort();
            var dedupedTs = new List<double>();
            foreach (var t in ts)
            {
                if (dedupedTs.Count == 0 || t - dedupedTs[^1] > 1e-9)
                    dedupedTs.Add(t);
            }

            (double X, double Y) Lerp(double t) =>
                (edgeStart.X + t * (edgeEnd.X - edgeStart.X), edgeStart.Y + t * (edgeEnd.Y - edgeStart.Y));

            for (int k = 0; k < dedupedTs.Count - 1; k++)
            {
                double t0 = dedupedTs[k];
                double t1 = dedupedTs[k + 1];
                var mid = Lerp((t0 + t1) / 2.0);

                if (!IsInsideOrOnBoundary(other, mid))
                    result.Add((Lerp(t0), Lerp(t1)));
            }
        }

        return result;
    }

    // İki 2D doğru parçasının GERÇEK (proper, sınırlar dahil) kesişim noktasını `subject`
    // parametresi (t, [0,1] aralığında) cinsinden döner — paralel/çakışık kenarlar kapsam dışı
    // (false döner, çağıran taraf bu kesişimsiz sayar; sonucu tutarsız/dejenere yaparsa
    // zincirleme adımı zaten açık hatayla bunu yakalar).
    private static bool TryGetSegmentIntersectionParameter(
        (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, (double X, double Y) p4, out double t)
    {
        t = 0;
        double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
        double denom = d1x * d2y - d1y * d2x;
        if (Math.Abs(denom) < 1e-12) return false; // paralel/çakışık — kapsam dışı

        double tt = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denom;
        double ss = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / denom;
        if (tt < -1e-9 || tt > 1 + 1e-9 || ss < -1e-9 || ss > 1 + 1e-9) return false;

        t = Math.Clamp(tt, 0.0, 1.0);
        return true;
    }

    // Nokta, dışbükey (CCW) bir poligonun içinde VEYA sınırında mı? (Sutherland-Hodgman'ın
    // "currentInside" testiyle AYNI yarı-düzlem kuralı — poly'nin HER kenarının sol/iç tarafında.)
    private static bool IsInsideOrOnBoundary(List<(double X, double Y)> poly, (double X, double Y) point)
    {
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var edgeStart = poly[i];
            var edgeEnd = poly[(i + 1) % n];
            double cross = (edgeEnd.X - edgeStart.X) * (point.Y - edgeStart.Y) - (edgeEnd.Y - edgeStart.Y) * (point.X - edgeStart.X);
            if (cross < -Tolerance) return false;
        }
        return true;
    }

    /*
       NE: Union'ın dış-hat alt-segmentlerini uç-nokta eşleşmesiyle TEK kapalı döngüye zincirler.
       NEDEN `SegmentBasedSubdivider.ChainSegmentsIntoOpenPolylines`'tan FARKLI (bilinçli
           duplicate DEĞİL): o yöntem AÇIK (kapanması zorunlu olmayan, çoklu) zincirler üretir —
           burada matematiksel garanti (iki dışbükey kümenin birleşimi basit-bağlantılı, TEK
           döngü) gereği TEK ve KAPALI bir döngü bekleniyor; kapanmazsa (veya birden fazla ayrık
           parçaya bölünürse) bu garanti ihlal edilmiş demektir — açık hata.
    */
    private static List<(double X, double Y)> ChainIntoClosedLoop(
        List<((double X, double Y) Start, (double X, double Y) End)> segments)
    {
        const double chainTolerance = 1e-6;
        bool SamePoint((double X, double Y) x, (double X, double Y) y) =>
            Math.Abs(x.X - y.X) < chainTolerance && Math.Abs(x.Y - y.Y) < chainTolerance;

        var remaining = new List<((double X, double Y) Start, (double X, double Y) End)>(segments);
        var start = remaining[0].Start;
        var chain = new List<(double X, double Y)> { remaining[0].Start, remaining[0].End };
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            var last = chain[^1];
            int idx = remaining.FindIndex(s => SamePoint(s.Start, last) || SamePoint(s.End, last));
            if (idx < 0)
                throw new InvalidOperationException(
                    "ConvexPolygonClipper2D.Union: dış-hat alt-segmentleri tek kapalı döngüye zincirlenemedi — dejenere/kapsam dışı durum (ör. sadece teğet temas).");

            var seg = remaining[idx];
            remaining.RemoveAt(idx);
            var next = SamePoint(seg.Start, last) ? seg.End : seg.Start;
            chain.Add(next);
        }

        if (!SamePoint(chain[^1], start))
            throw new InvalidOperationException(
                "ConvexPolygonClipper2D.Union: dış-hat döngüsü kapanmadı — dejenere/kapsam dışı durum.");

        chain.RemoveAt(chain.Count - 1); // kapanış noktası = başlangıç noktası, tekrarını at
        return chain;
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
