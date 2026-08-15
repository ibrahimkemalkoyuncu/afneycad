using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Segment-Tabanlı Subdivide (SegmentBasedSubdivider) — CSG Boolean, `docs/Roadmap_CSG_Boolean.md`
       2026-08-14 girdisinin 2. yapı taşı. `FaceSplitter.SplitAtPolylineChord`'un (aynı oturumda
       daha önce yazılan 1. yapı taşı) İLK gerçek çağıranı.

   NEDEN: `GeneralSolidSubtractor.SplitFaceAgainstPlanes` (Faz 4/5'in mevcut çok-düzlem subdivide'ı)
          A'nın Face'lerini B'nin yüz DÜZLEMLERİNE göre bölüyor — B'nin kendi Face SINIRLARI hiç
          hesaba katılmıyor, sadece sonsuz düzlem. Bu, roadmap'in UNION denemelerinde defalarca
          çarptığı "köprü yüzü" (bridging face) ihtiyacının köklerinden biriydi (bkz. roadmap
          2026-08-07 girdileri): A ve B'nin BAĞIMSIZ decomposition'ları aynı 3D eğri üzerinde
          oluşmuyordu.

          Bu yapı taşı bunun yerine A'nın Face'lerini doğrudan B'nin GERÇEK Face'leriyle
          (`FaceIntersection.Intersect` — poligon sınırına kırpılmış kesişim segmentleri, plane
          DEĞİL) kesiştirip, ortaya çıkan (muhtemelen çok-segmentli) kesişim polyline'ı ile bölüyor
          (`FaceSplitter.SplitAtPolylineChord`). "Section-first" mimarisinin (OpenCASCADE'in
          "Section" aşamasının bu kod tabanına uyarlanmış, küçültülmüş bir versiyonu — roadmap
          2026-08-07 girdisinin "kısa web araştırması" bölümünde referans verilen yaklaşım) somut
          ilk uygulaması.

   YÖNTEM (dosya başı roadmap taslağıyla birebir):
     1. A'nın HER Face'i için, B'nin HER Face'i ile `FaceIntersection.Intersect` çağrılıp
        kesişim segmentleri toplanır.
     2. Aynı A-Face üzerinde toplanan TÜM segmentler (birden fazla B-Face'ten gelebilir)
        uç-nokta eşleşmesine göre AÇIK polyline'lara zincirlenir (`ChainSegmentsIntoOpenPolylines`
        — `GeneralSolidSubtractor.ChainVertexPairsIntoLoop`'un KAPALI-döngü zincirlemesinden
        FARKLI: kesişim eğrisi Face sınırına İKİ ucundan değebilir, kapanması ZORUNLU değil).
     3. Her zincirlenmiş polyline, "aktif" alt-Face'lerden HANGİSİNİN kendi sınır Loop'unda her
        iki ucunun da bulunduğu kontrol edilip, bulunursa `FaceSplitter.SplitAtPolylineChord` ile
        o alt-Face bölünür (birden fazla polyline aynı orijinal Face'i sırayla bölebilir — her
        bölmeden sonra "aktif" alt-Face kümesi güncellenir).
     4. Bölünen (veya hiç bölünmeyen — coplanar/kesişimsiz durum) her alt-Face fragmanı,
        `SolidClassifier.IsPointInside` ile B'ye göre İÇERİDE/DIŞARIDA sınıflandırılır (temsilci
        nokta: fragmanın köşe ortalaması/centroid'i — dışbükey köşe-kutusu senaryolarında bu her
        zaman fragmanın kendi içinde kalır).
     5. B'nin DIŞINDA kalan fragmanlar döndürülür (UNION'ın A-tarafı yarısı).

   COPLANAR ÜST/ALT YÜZLER (dürüstlük notu — dosya başı görev tanımının vurguladığı durum):
     `FaceIntersection.Intersect` paralel/çakışık (coplanar) Face çiftlerinde boş segment listesi
     döner (kendi dokümantasyonunda açık). Bu yüzden bir A-Face'inin B'nin HİÇBİR Face'iyle GERÇEK
     (transversal) bir kesişimi yoksa (coplanar dahil), o Face hiç bölünmez — TAMAMEN İÇERİDE ya
     da TAMAMEN DIŞARIDA sınıflandırılır (adım 4, "segments.Count==0" dalı). Coplanar İKİ FARKLI
     boyuttaki Face'in ORTAK bölgesini bulmak (gerçek 2D poligon BİRLEŞİMİ/kesişimi) BU yapı
     taşının kapsamı DIŞINDA — roadmap'in "convex-convex 2D union" olarak ayırdığı SONRAKİ adım.

   KAPSAM DIŞI (bilinçli, roadmap ile tutarlı):
     - `GeneralSolidUnion` assembly'sinin TAMAMI (bu metodun ürettiği "A'nın B-dışı fragmanları" +
       simetrik `SubdivideAndClassifyOutside(b, a)` çağrısının ürettiği "B'nin A-dışı fragmanları"
       arasındaki boşluğu `VertexWelder`/`OpenEdgeStitcher` ile dikip TEK bir Solid kurmak) —
       SONRAKİ, ayrı bir adım.
     - Coplanar Face çiftleri için convex-convex 2D poligon birleşimi/kesişimi.
     - Bir kesişim polyline'ının HİÇBİR aktif alt-Face'in sınırına değmediği durum (ör. Face'in
       TAMAMEN İÇİNDE kapalı bir kesişim döngüsü/delik) — açık `NotSupportedException`.
     - `FaceSplitter.SplitAtPolylineChord`'un kendi dokümante ettiği kısıtlar (dışbükey/tek-dış-
       Loop'lu Face varsayımı, kendi kendini kesen polyline reddi) BURADA da AYNEN geçerli.
*/
public static class SegmentBasedSubdivider
{
    /*
       NE: `a`'nın Face'lerini `b`'nin GERÇEK Face sınırlarıyla kesiştirip böler, ortaya çıkan
           HER fragmanı `b`'ye göre içeride/dışarıda sınıflandırır, `b`'nin DIŞINDA kalan
           fragmanların listesini döner (UNION montajının A-tarafı yarısı için gereken çıktı).
       NOT: `a` YERİNDE (in place) İÇSEL ÇALIŞMA KOPYASI olarak değiştirilir — `FaceSplitter.
            SplitAtPolylineChord`'un kendi Solid-mutasyon deseni (`solid.Faces.Remove/Add`) ve
            `GeneralSolidSubtractor.Subtract`'in dokümante ettiği "çağıran taraf `a`'yı sonuç
            olarak KULLANMAMALI" kuralıyla TUTARLI. Orijinal A'yı korumak isteyen çağıran taraf
            önceden bir kopya çıkarmalı.
    */
    public static List<Face> SubdivideAndClassifyOutside(Solid a, Solid b)
    {
        var outsideFragments = new List<Face>();

        // ÖNEMLİ: `a.Faces` bölme sırasında mutasyona uğrar (Remove/Add) — orijinal Face
        // listesinin bir ANLIK GÖRÜNTÜSÜ (snapshot) üzerinde iterasyon yapılmalı.
        var originalFaces = a.Faces.ToList();

        foreach (var originalFace in originalFaces)
        {
            var segments = CollectSegmentsAgainstAllFaces(originalFace, b);

            if (segments.Count == 0)
            {
                // DÜRÜSTLÜK KORUMASI: `FaceIntersection.Intersect`'in coplanar Face çiftlerinde
                // HER ZAMAN boş segment listesi döneceği varsayımı YANLIŞ çıktı (bu yapı taşının
                // canlı testleriyle keşfedildi) — coplanar TAM ÇAKIŞIK çiftlerde gerçekten boş
                // dönüyor, ama coplanar KISMEN ÇAKIŞAN çiftlerde (sınırları birbirini kesen) bazen
                // yine boş dönebiliyor (yön/kenar sırasına bağlı tutarsızlık, kök nedeni bu yapı
                // taşının kapsamı dışında — `FaceIntersection`'ın kendisini incelemek gerekir).
                // "segments.Count==0 → güvenle bölünmeden sınıflandır" varsayımı bu yüzden SADECE
                // gerçekten ETKİLEŞİMSİZ (B ile aynı düzlemde bile değil VEYA aynı düzlemde ama
                // izdüşümleri KESİNLİKLE örtüşmüyor) durumlarda güvenli. Coplanar VE izdüşüm
                // örtüşmesi olan (ama segment üretilmeyen) durumda sessizce yanlış sınıflandırma
                // YERİNE açık hata fırlatılır — bu, convex-convex 2D union primitifi (roadmap'in
                // sonraki, ayrı adımı) tamamlanmadan güvenle çözülemez.
                if (HasAmbiguousCoplanarOverlap(originalFace, b))
                    throw new NotSupportedException(
                        "SegmentBasedSubdivider: A-Face, B'nin bir Face'iyle coplanar VE izdüşümleri " +
                        "örtüşüyor ama FaceIntersection hiç kesişim segmenti üretmedi (tutarsız/eksik " +
                        "kesişim tespiti) — bu durum convex-convex 2D union primitifi olmadan güvenle " +
                        "sınıflandırılamaz, kapsam dışı. Bkz. Roadmap_CSG_Boolean.md.");

                // Coplanar hiç yok VEYA coplanar ama izdüşümler örtüşmüyor (B ile gerçekten
                // etkileşimsiz) durumu —
                // Face BÖLÜNMEDEN, TAM olarak sınıflandırılır.
                ClassifyAndCollect(originalFace, b, outsideFragments);
                continue;
            }

            var chains = ChainSegmentsIntoOpenPolylines(segments);

            var activeFaces = new List<Face> { originalFace };
            foreach (var chain in chains)
            {
                if (chain.Count < 2) continue; // dejenere (tek noktalık) zincir — atla

                var target = activeFaces.FirstOrDefault(f => PointOnFaceBoundary(f, chain[0]) && PointOnFaceBoundary(f, chain[^1]));
                if (target == null)
                    throw new NotSupportedException(
                        "SegmentBasedSubdivider: kesişim polyline'ının uçları hiçbir aktif alt-Face'in " +
                        "kendi sınırında bulunamadı (ör. Face'in TAMAMEN İÇİNDE kapalı bir kesişim " +
                        "döngüsü/delik durumu) — kapsam dışı, bkz. Roadmap_CSG_Boolean.md.");

                // `FaceSplitter.SplitAtPolylineChord`, chain[0]/chain[^1]'in Face'in sınırında
                // GERÇEK bir Vertex nesnesi olmasını ZORUNLU kılıyor — ama `FaceIntersection`'ın
                // ürettiği kesişim uçları genelde bir sınır KENARININ ORTASINDA (henüz Vertex
                // olmayan) bir noktadır. Bu yüzden önce (gerekiyorsa) `EdgeSplitter.SplitEdgeAt`
                // ile o noktada YENİ bir Vertex materyalize edilir (`FaceSplitterPolylineChordTests.
                // BuildCornerScenario`'nun elle yaptığı ön-hazırlığın GENEL/otomatik hâli).
                EnsureBoundaryVertexAt(a, target, chain[0]);
                EnsureBoundaryVertexAt(a, target, chain[^1]);

                var (faceA, faceB, _) = FaceSplitter.SplitAtPolylineChord(a, target, chain);
                activeFaces.Remove(target);
                activeFaces.Add(faceA);
                activeFaces.Add(faceB);
            }

            foreach (var frag in activeFaces)
                ClassifyAndCollect(frag, b, outsideFragments);
        }

        return outsideFragments;
    }

    /*
       NE: `aFace`'in, B'nin coplanar olduğu HERHANGİ bir Face'iyle 3D eksen-hizalı bounding-box
           izdüşümü örtüşüyor mu kontrol eder — `FaceIntersection.Intersect`'in (tutarsız biçimde)
           boş segment döndürdüğü ama gerçekte İKİ Face'in KISMEN çakıştığı belirsiz durumu
           tespit etmek için kullanılır (bkz. yukarıdaki DÜRÜSTLÜK KORUMASI yorumu).
       NASIL: Coplanar bir çift için üç eksenden İKİSİ (düzlemin kendi 2 boyutu) taşıyıcı bilgidir,
              üçüncüsü (düzlem normali yönü) her iki Face için de ZATEN eşit/yakın olacaktır
              (coplanar tanımı gereği) — bu yüzden basit bir TAM 3D AABB kesişim testi (tolerans
              ile) hem doğru hem de en az karmaşık, ayrı bir 2D izdüşüm/projeksiyon gerekmiyor.
    */
    private static bool HasAmbiguousCoplanarOverlap(Face aFace, Solid b)
    {
        const double eps = 1e-6;
        var (aMin, aMax) = GetVertexBounds(aFace);

        foreach (var bFace in b.Faces)
        {
            if (!CoplanarFaceDetector.AreCoplanar(aFace, bFace)) continue;

            var (bMin, bMax) = GetVertexBounds(bFace);
            bool overlapsX = aMin.X <= bMax.X + eps && aMax.X >= bMin.X - eps;
            bool overlapsY = aMin.Y <= bMax.Y + eps && aMax.Y >= bMin.Y - eps;
            bool overlapsZ = aMin.Z <= bMax.Z + eps && aMax.Z >= bMin.Z - eps;
            if (overlapsX && overlapsY && overlapsZ)
                return true;
        }
        return false;
    }

    private static (Vector3D Min, Vector3D Max) GetVertexBounds(Face face)
    {
        var vertices = face.GetOuterLoop()!.GetOrderedVertices();
        var min = new Vector3D(double.MaxValue, double.MaxValue, double.MaxValue);
        var max = new Vector3D(double.MinValue, double.MinValue, double.MinValue);
        foreach (var v in vertices)
        {
            min = new Vector3D(Math.Min(min.X, v.Position.X), Math.Min(min.Y, v.Position.Y), Math.Min(min.Z, v.Position.Z));
            max = new Vector3D(Math.Max(max.X, v.Position.X), Math.Max(max.Y, v.Position.Y), Math.Max(max.Z, v.Position.Z));
        }
        return (min, max);
    }

    /*
       NE: Bir A-Face'inin, B'nin TÜM Face'leriyle olan gerçek (poligon-sınırlı) kesişim
           segmentlerini toplar.
    */
    private static List<(Vector3D Start, Vector3D End)> CollectSegmentsAgainstAllFaces(Face aFace, Solid b)
    {
        var segments = new List<(Vector3D Start, Vector3D End)>();
        foreach (var bFace in b.Faces)
        {
            foreach (var seg in FaceIntersection.Intersect(aFace, bFace))
                segments.Add((seg.Start, seg.End));
        }
        return segments;
    }

    private static void ClassifyAndCollect(Face fragment, Solid b, List<Face> outsideFragments)
    {
        var representative = GetRepresentativePoint(fragment);
        if (!SolidClassifier.IsPointInside(b, representative))
            outsideFragments.Add(fragment);
    }

    /*
       NE: Bir Face fragmanı için "içeride mi/dışarıda mı" testinde kullanılacak temsilci nokta —
           dış Loop'un köşe ortalaması (centroid benzeri). Dışbükey köşe-kutusu senaryolarında
           (bu yapı taşının test kapsamı) her zaman fragmanın kendi içinde kalır; genel içbükey
           fragmanlar için kesin doğruluk GARANTİ EDİLMEZ (roadmap'in dar kapsamlı diğer
           sınıflandırma yardımcılarıyla — ör. `GeneralSolidSubtractor.IsEntirelyOnSide` — AYNI
           bilinçli sınırlama).
    */
    private static Vector3D GetRepresentativePoint(Face face)
    {
        var vertices = face.GetOuterLoop()!.GetOrderedVertices();
        var sum = Vector3D.Zero;
        foreach (var v in vertices) sum += v.Position;
        return sum / vertices.Count;
    }

    /*
       NE: Bir noktanın (kesişim polyline'ının bir ucu) verilen Face'in KENDİ dış Loop sınırında
           (ya var olan bir KÖŞE, ya bir kenarın ÜZERİNDE bir nokta olarak) bulunup bulunmadığını
           kontrol eder — çağırmadan ÖNCE hangi aktif alt-Face'in hedef olduğunu seçmek için
           kullanılır (`EnsureBoundaryVertexAt`'in "materialize edilebilir mi" ön-koşulu).
    */
    private static bool PointOnFaceBoundary(Face face, Vector3D point)
    {
        var loop = face.GetOuterLoop();
        if (loop == null) return false;

        if (loop.GetOrderedVertices().Any(v => v.Position.DistanceTo(point) < 1e-6))
            return true;

        foreach (var edge in loop.Edges)
        {
            if (IsPointOnEdge(edge, point)) return true;
        }
        return false;
    }

    private static bool IsPointOnEdge(TopologyEdge edge, Vector3D point)
    {
        double d1 = point.DistanceTo(edge.StartVertex.Position);
        double d2 = point.DistanceTo(edge.EndVertex.Position);
        double len = edge.StartVertex.Position.DistanceTo(edge.EndVertex.Position);
        return Math.Abs(d1 + d2 - len) <= 1e-3;
    }

    /*
       NE: Bir noktada, verilen Face'in sınırında GERÇEK bir `Vertex` nesnesinin var olmasını
           garanti eder — zaten bir köşe olarak varsa dokunmaz, bir kenarın ORTASINDA (henüz
           Vertex olmayan) bir noktaysa `EdgeSplitter.SplitEdgeAt` ile o kenarı böler (bu, o
           kenarı paylaşan KOMŞU Face'in Loop'unu da otomatik günceller — `EdgeSplitter`'ın
           kendi winged-edge tutarlılık garantisi).
    */
    private static void EnsureBoundaryVertexAt(Solid solid, Face face, Vector3D point)
    {
        var loop = face.GetOuterLoop()!;

        if (loop.GetOrderedVertices().Any(v => v.Position.DistanceTo(point) < 1e-6))
            return; // zaten bir köşe

        var edge = loop.Edges.FirstOrDefault(e => IsPointOnEdge(e, point));
        if (edge == null)
            throw new NotSupportedException(
                "SegmentBasedSubdivider: kesişim noktası Face sınırında bulunamadı (ne var olan bir " +
                "köşe, ne bir kenarın üzerinde) — kapsam dışı, bkz. Roadmap_CSG_Boolean.md.");

        EdgeSplitter.SplitEdgeAt(solid, edge, point);
    }

    /*
       NE: Aynı Face üzerinde toplanan (muhtemelen birden fazla B-Face'ten gelen) ham kesişim
           segmentlerini, uç-nokta eşleşmesine göre AÇIK polyline'lara zincirler.
       NEDEN `GeneralSolidSubtractor.ChainVertexPairsIntoLoop`'tan FARKLI (bilinçli duplicate
           DEĞİL, farklı bir problem): O yöntem TEK bir KAPALI döngü bekler (kesim kirişleri bir
           düzlemin TAM kesitini oluşturur) ve kapanmazsa hata fırlatır. Burada kesişim eğrisi
           Face sınırına İKİ ucundan değebilir (AÇIK zincir, kapanması ZORUNLU değil) VE aynı
           Face üzerinde BİRDEN FAZLA bağımsız/ayrık zincir oluşabilir (B'nin farklı Face'lerinden
           gelen, birbiriyle bağlantısız segment grupları) — bu yüzden TEK bir döngü/hata
           varsayımı yerine, her biri kendi başına bir polyline olan BİRDEN FAZLA zincir üretir.
       YÖNTEM: İlk kullanılmamış segmentten başlanır, uçlarından (önce ileri, sonra geri) eşleşen
           komşu segmentler bulunup zincire eklenir — eşleşen segment kalmayınca zincir kapanır
           (kendi başına, roadmap'in "en az degree-2 nokta zinciri" varsayımıyla tutarlı basit
           durumlar için yeterli; T-birleşim/dallanma gibi dejenere durumlar KAPSAM DIŞI).
    */
    internal static List<List<Vector3D>> ChainSegmentsIntoOpenPolylines(List<(Vector3D Start, Vector3D End)> segments)
    {
        bool SamePoint(Vector3D x, Vector3D y) => x.DistanceTo(y) <= 1e-6;

        var remaining = new List<(Vector3D Start, Vector3D End)>(segments);
        var chains = new List<List<Vector3D>>();

        while (remaining.Count > 0)
        {
            var seg = remaining[0];
            remaining.RemoveAt(0);
            var chain = new List<Vector3D> { seg.Start, seg.End };

            bool extended = true;
            while (extended)
            {
                extended = false;
                var last = chain[^1];
                int idx = remaining.FindIndex(p => SamePoint(p.Start, last) || SamePoint(p.End, last));
                if (idx < 0) continue;

                var next = remaining[idx];
                remaining.RemoveAt(idx);
                chain.Add(SamePoint(next.Start, last) ? next.End : next.Start);
                extended = true;
            }

            extended = true;
            while (extended)
            {
                extended = false;
                var first = chain[0];
                int idx = remaining.FindIndex(p => SamePoint(p.Start, first) || SamePoint(p.End, first));
                if (idx < 0) continue;

                var prev = remaining[idx];
                remaining.RemoveAt(idx);
                chain.Insert(0, SamePoint(prev.Start, first) ? prev.End : prev.Start);
                extended = true;
            }

            chains.Add(chain);
        }

        return chains;
    }
}
