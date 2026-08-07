using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Genel Çok-Yüzlü SUBTRACT Montajı (GeneralSolidSubtractor) — CSG Boolean, `SolidSubtractor`
       (tek-düzlem özel durumu) ile PARALEL/ADDITIVE bir genişleme; B'nin A'nın sınırını
       BİRDEN FAZLA yüzden kestiği (örn. bir köşeyi kesen çentik) durumu kapsıyor.
       `SolidSubtractor.Subtract` DOKUNULMADI — o hâlâ tek-düzlem durumunda çağrılabilir,
       davranışı birebir aynı.

   GÜNCELLEME (2026-08-06) — çok-düzlem yolu YENİDEN YAZILDI (klasik "subdivide → classify →
   reconstruct" boundary-evaluation yaklaşımıyla; bkz. Requicha & Voelcker, "Boolean Operations
   in Solid Modeling: Boundary Evaluation and Merging Algorithms", Proc. IEEE 1985 — üç aşama:
   (1) parçalanma/subdivision: her iki katının yüzeyleri, diğer katının sınırını kestikleri
   yerlerde bölünür; (2) sınıflandırma: her alt-parça diğer katıya göre içeride/dışarıda
   sınıflandırılır; (3) yeniden inşa: doğru sınıflandırılan parçalar birleştirilip sonuç B-Rep
   monte edilir). ÖNCEKİ SÜRÜM (bu dosyanın 2026-08-04 hâli, `FaceRegionClassifier` ile) B'nin
   ARDIŞIK düzlemlerle kesilmesine (`PlaneCutter.CutWithPlaneKeepDiscarded` sıralı çağrı) ve her
   adımın "mirror cap"ini ikili (tam/hiç) bir bitişiklik testiyle süzmeye dayanıyordu — roadmap'in
   ayrıntılı belgelediği gibi bu, HEM mirror cap'lerin KISMİ örtüşmesini (Face bölünmesi
   gerektiriyordu) HEM DE ardışık adımların kendi ürettiği ara-kapak yüzlerinin SONRAKİ bir
   adımda yeniden "A yüzü" gibi işlem görüp KENDİ İÇLERİNDE de sahte/iç parçalar üretebilmesini
   (sıra-bağımlı, ikinci bir gizli hata kaynağı — bu güncellemede araştırılırken bulundu)
   çözemiyordu. Bu YENİ sürüm, B'nin HİÇBİR düzlemini `a` üzerinde ARDIŞIK/sıralı kesmiyor —
   bunun yerine A'nın HER ORİJİNAL yüzünü TÜM aday düzlemlere göre TEK SEFERDE (eşzamanlı)
   alt-parçalara ayırıp HER alt-parçayı doğrudan "B'nin herhangi bir düzleminin dışında mı"
   testiyle sınıflandırıyor (De Morgan: A∖B = A ∩ (∪ outsideᵢ), dışbükey B için bir noktanın
   A∖B'de olması TAM OLARAK B'nin herhangi bir yüz-yarı-uzayının dışında olması demektir) — bu
   yüzden sıra bağımlılığı ve "ara-kapağın kendi içinde sahte parça" riski YAPISAL OLARAK ortadan
   kalkıyor (her alt-parça SADECE A'nın orijinal 1 yüzünden türüyor, önceki bir kapaktan değil).

   YÖNTEM (somut adımlar):
     1. B'nin A'nın MEVCUT sınırını GERÇEKTEN (transversal) kestiği yüz düzlemleri toplanır
        (`SolidSubtractor`'daki AYNI kural — hasPos && hasNeg — bilinçli olarak DUPLICATE
        edildi, `SolidSubtractor.cs`'e dokunmamak için).
     2. A'nın HER orijinal Face'i, TÜM aday düzlemlere göre `SplitFaceAgainstPlanes` ile
        alt-parçalara ayrılır: bir alt-parça bir düzlemin TAMAMEN dışında (outsideB) bulunur
        bulunmaz KESİN "kept" (A∖B'nin sınırı) sayılır ve o dal için DAHA FAZLA düzlem
        kontrol edilmez (kısa devre — De Morgan OR); bir düzlemin TAMAMEN içinde (insideB)
        bulunursa bir SONRAKİ düzlemle denenmeye devam eder; karışıksa (düzlem parçayı
        GERÇEKTEN kesiyorsa) `EdgeSplitter`+`FaceSplitter` ile ikiye bölünür (dışbükey sınır
        varsayımı, `PlaneCutter` ile AYNI kısıt). TÜM düzlemlerden "insideB" olarak hayatta
        kalan bir alt-parça KESİN "discarded" (A∩B'nin içi, sonuca dahil edilmez) sayılır.
        Her bölme kirişinin (chord) uç noktaları, HANGİ düzlemde oluştuğu etiketiyle ayrıca
        toplanır (kapak inşası için, adım 4).
     3. Bölme sırasında oluşan ara-parçalar (bir sonraki düzlemle TEKRAR bölünen "insideB"
        dalları) `a.Faces`'ten `FaceSplitter`'ın kendi mekanizmasıyla zaten çıkarılmış olur;
        kalan TÜM kirişlerin (chord) `LeftFace`/`RightFace` alanları, artık `a.Faces` İÇİNDE
        OLMAYAN (kept değil, discarded değil, ARA/geçici) herhangi bir Face'e işaret ediyorsa
        `null`'a çekilir (temizlik geçişi) — bu, o kenarı "açık" (stitch bekleyen) hâle getirir.
     4. Her aday düzlem için, o düzlemde oluşan TÜM kiriş uç-nokta çiftleri TEK bir kapalı
        döngüde zincirlenip (`PlaneCutter.ChainIntoLoop` ile AYNI teknik, bilinçli duplicate)
        A'nın o düzlemdeki TAM (henüz kırpılmamış) kesit poligonunu verir; bu poligon SONRA
        DİĞER TÜM aday düzlemlerin "içeri" (insideB) yarı-uzaylarına göre 3D yarı-düzlem
        kırpılır (`ClipPolygonByHalfSpace` — düzlem-poligon kesişimi, `ConvexPolygonClipper2D`
        ile AYNI Sutherland-Hodgman ilkesi ama coplanar izdüşüme gerek KALMADAN doğrudan 3D'de,
        çünkü kırpılan poligon zaten düzlemsel ve kırpma düzlemiyle kesişimi de o düzlem
        içinde kalır). Kırpılmış poligondan TAMAMEN YENİ (fresh) Vertex/TopologyEdge'lerden
        oluşan bağımsız bir kapak Face'i inşa edilir — kenarların SADECE TEK tarafı
        (`LeftFace`) dolduruluyor, diğer taraf BİLEREK boş bırakılıyor (adım 5'te dikilecek).
     5. Korunan (kept) A-parçaları + tüm kırpılmış kapak Face'leri TEK bir `Solid`'de toplanır,
        `VertexWelder.Weld` ile köşeler kaynaştırılır, YENİ `OpenEdgeStitcher.Stitch` ile HÂLÂ
        açık (tek tarafı dolu) kenarlar — hem A-parçası/kapak sınırında HEM DE komşu iki
        kapağın birbirini kırptığı ortak sınırda — eşleşen ikizleriyle birleştirilir (bkz.
        `OpenEdgeStitcher.cs`, roadmap'in "cross-piece edge stitching" ihtiyacının GENEL/
        parça-bağımsız çözümü — hangi D_i/D_j çiftinin nerede kesiştiğini AYRICA izlemeye
        gerek KALMADAN, sadece kaynaşmış uç-nokta çiftine göre otomatik eşleşiyor).
     6. `IsValid()` (Euler, kabuk-başına) + hacim ile doğrulanır.

   KAPSAM DIŞI (bilinçli, değişmedi):
     - B'nin A'yı hiç kesmediği (tamamen dışında/gömülü — cavity) durum: adım 1'de aday
       bulunamaz, açık `NotSupportedException`.
     - B içbükeyse veya A'nın bir Face'i bir düzlemi 2'den fazla kenarında kesiyorsa (dışbükey
       olmayan kesişim): `NotSupportedException` (adım 2'nin `chordVerts.Count != 2` kontrolü).
     - Bir düzlemle TAM ÇAKIŞIK (coplanar) bir alt-parça: `NotSupportedException` (`PlaneCutter`
       ile AYNI dejenere-durum kuralı).
     - Üç veya daha fazla parçanın TAM AYNI kenarda buluştuğu (T-birleşim) durum:
       `OpenEdgeStitcher` açık `InvalidOperationException` fırlatır.
*/
public static class GeneralSolidSubtractor
{
    private const double Tolerance = 1e-6;

    /*
       NE: `a`'dan `b`'yi çıkarır — B'nin A'nın sınırını BİRDEN FAZLA yüzden kestiği genel
           durumu (tek-düzlem özel durumunu da, `SolidSubtractor.Subtract`'e devrederek)
           destekler.
       NOT: `a` YERİNDE (in place) İÇSEL ÇALIŞMA KOPYASI olarak değiştirilir — ÇAĞIRAN TARAF
            `a`'yı sonuç olarak KULLANMAMALI, dönen değer asıl sonuçtur. Orijinal A'yı korumak
            isteyen çağıran taraf önceden bir kopya çıkarmalı (`PlaneCutter`'ın kendi deseniyle
            tutarlı).
    */
    public static Solid Subtract(Solid a, Solid b, string resultName = "A_minus_B")
    {
        var candidatePlanes = new List<(Vector3D Point, Vector3D Normal)>();

        foreach (var face in b.Faces)
        {
            var loop = face.GetOuterLoop();
            if (loop == null) continue;

            var verts = loop.GetOrderedVertices();
            if (verts.Count == 0) continue;

            var planePoint = verts[0].Position;
            var planeNormal = face.Normal.Normalize();

            if (PlaneIntersectsSolidBoundary(a, planePoint, planeNormal))
                candidatePlanes.Add((planePoint, planeNormal));
        }

        if (candidatePlanes.Count == 0)
            throw new NotSupportedException(
                "GeneralSolidSubtractor: B, A'nın sınırını hiçbir yüz düzleminde GERÇEKTEN (transversal) kesmiyor — " +
                "B tamamen A'nın dışında ya da tamamen A içinde gömülü (cavity/boşluklu katı, çok-kabuklu Solid " +
                "desteği gerekir) olabilir; her iki durum da kapsam dışı, bkz. Roadmap_CSG_Boolean.md.");

        if (candidatePlanes.Count == 1)
        {
            var (p, n) = candidatePlanes[0];
            PlaneCutter.CutWithPlane(a, p, n);
            return a;
        }

        return SubtractMultiPlane(a, candidatePlanes, resultName);
    }

    private static Solid SubtractMultiPlane(Solid a, List<(Vector3D Point, Vector3D Normal)> planes, string resultName)
    {
        // NEDEN kapak kirişleri AYRI/BAĞIMSIZ bir geçişte toplanıyor (sınıflandırma geçişinden
        // ÖNCE, A'nın MUTASYONA UĞRAMAMIŞ orijinal Face'lerinin sınır poligonu üzerinden):
        // `SplitFaceAgainstPlanes`'in sınıflandırma kısa-devresi ("outsideB_k bulununca DİĞER
        // düzlemleri hiç kontrol etme") doğru/verimli bir sınıflandırma kuralı ama bir yan
        // etkisi var — bir A-Face'inin bir aday düzlemle GERÇEKTEN kesişip kesişmediğini SADECE
        // o dal sınıflandırma sırasında o düzleme kadar ULAŞTIYSA keşfediyor. Bir düzlemin TAM
        // (henüz diğer düzlemlerle kırpılmamış) kesit poligonu içinse A'nın TÜM orijinal
        // Face'lerinin O DÜZLEMLE kesişimi gerekiyor — kısa-devre nedeniyle ATLANMIŞ dallardan
        // gelen kesişimler de dahil (ilk yazımda GERÇEK bir hata buradan çıktı: köşe-çentiği
        // senaryosunda P2 düzleminin kapağı sadece 3 kirişle kapanmaya çalışıyordu, 4. kiriş
        // hiç keşfedilmemişti — `ChainVertexPairsIntoLoop` "kapanmıyor" istisnası fırlattı).
        var capChordsByPlane = new List<List<(Vector3D P1, Vector3D P2)>>();
        for (int i = 0; i < planes.Count; i++) capChordsByPlane.Add(new List<(Vector3D, Vector3D)>());

        var originalFacePolygons = a.Faces
            .Select(f => f.GetOuterLoop()!.GetOrderedVertices().Select(v => v.Position).ToList())
            .ToList();

        for (int k = 0; k < planes.Count; k++)
        {
            var (point, normal) = planes[k];
            foreach (var polygon in originalFacePolygons)
            {
                var chord = FindPlaneChordOnPolygon(polygon, point, normal);
                if (chord != null)
                    capChordsByPlane[k].Add(chord.Value);
            }
        }

        var keptFragments = new List<Face>();
        var discardedFragments = new List<Face>();

        foreach (var originalFace in a.Faces.ToList())
        {
            SplitFaceAgainstPlanes(a, originalFace, planes, keptFragments, discardedFragments);
        }

        foreach (var f in discardedFragments)
            a.Faces.Remove(f);

        // Temizlik: `a`'nın (artık SADECE kept parçaları içeren) TÜM kenarlarını tara — bir
        // kenarın Left/RightFace'i hâlâ `a.Faces` İÇİNDE OLMAYAN (ara/geçici VEYA discarded)
        // bir Face'e işaret ediyorsa serbest bırak (açık kenar -> OpenEdgeStitcher'ın dikeceği).
        // NEDEN SADECE `allChords` (FaceSplitter'ın ürettiği YENİ kiriş kenarları) YETERSİZ
        // (ilk yazımda GERÇEK bir hata buradan çıktı): `EdgeSplitter.SplitEdgeAt`, bir kiriş
        // kenarı SONRAKİ bir düzlemle TEKRAR bölündüğünde, bu bölünen kenarın PAYLAŞILAN
        // (hem kept HEM discarded/ara tarafa ait) ESKİ Left/Right referanslarını YENİ iki
        // parçaya da AYNEN kopyalıyor (`edgeA.LeftFace=edge.LeftFace; edgeA.RightFace=
        // edge.RightFace;` — bkz. `EdgeSplitter.cs`) — bu YENİ parça-kenarlar `allChords`
        // listesinde YOK (onlar `FaceSplitter.SplitAtChord`'un DEĞİL, `EdgeSplitter`'ın
        // ürünü) ama YİNE DE artık `a.Faces`'te olmayan bir Face'e işaret edebiliyorlar —
        // bu yüzden temizlik, `a`'nın kendi `GetEdges()`'inden (yani `Faces->Loops->Edges`
        // üzerinden ULAŞILABİLEN HER kenardan) geçmeli, sadece `allChords`'tan değil.
        foreach (var edge in a.GetEdges().ToList())
        {
            if (edge.LeftFace != null && !a.Faces.Contains(edge.LeftFace)) edge.LeftFace = null;
            if (edge.RightFace != null && !a.Faces.Contains(edge.RightFace)) edge.RightFace = null;
        }

        var result = new Solid(resultName);
        result.Faces.AddRange(a.Faces); // = keptFragments (a.Faces artık sadece bunları içeriyor)

        for (int i = 0; i < planes.Count; i++)
        {
            var (_, normal) = planes[i];
            var chords = capChordsByPlane[i];
            if (chords.Count == 0) continue; // bu düzlem hiçbir Face'i gerçekten kesmedi (beklenmez ama savunmacı)

            var clipped = ChainVertexPairsIntoLoop(chords);
            for (int j = 0; j < planes.Count; j++)
            {
                if (j == i) continue;
                clipped = ClipPolygonByHalfSpace(clipped, planes[j].Point, planes[j].Normal);
                if (clipped.Count < 3) break;
            }

            if (clipped.Count < 3) continue; // bu düzlemin katkısı diğer düzlemlerce tamamen elendi

            // NEDEN -normal: `normal` B'nin KENDİ dışa-dönük normali (n_i) — ama bu kapak A∖B'nin
            // sınırıdır, kalan malzeme B'nin İÇİNE değil DIŞINA doğru duruyor, bu yüzden kapağın
            // dışa-dönük normali B'ninkinin TERSİ olmalı (`PlaneCutter.CutWithPlaneKeepDiscarded`'ın
            // da `SolidSubtractor`'dan `-normal` ile çağrılmasıyla AYNI kural — bkz. o dosyadaki
            // `CutWithPlaneKeepDiscarded(a, point, -normal, ...)` çağrısı).
            var capFace = BuildFreshOpenCapFace(clipped, -normal);
            result.Faces.Add(capFace);
        }

        VertexWelder.Weld(result, Tolerance);
        OpenEdgeStitcher.Stitch(result);

        if (!result.IsValid())
            throw new InvalidOperationException(
                "GeneralSolidSubtractor: montaj sonucu topolojik olarak geçersiz (Euler/manifold testi başarısız) — " +
                "beklenmeyen bir dejenere kesişim (bkz. yukarıdaki KAPSAM DIŞI notları).");

        return result;
    }

    /*
       NE: TEK bir A-Face'ini (ve ondan doğan alt-parçaları) TÜM aday düzlemlere göre bölüp
           sınıflandırır — bkz. dosya başı YÖNTEM notu, adım 2.
    */
    private static void SplitFaceAgainstPlanes(
        Solid a, Face originalFace, List<(Vector3D Point, Vector3D Normal)> planes,
        List<Face> keptFragments, List<Face> discardedFragments)
    {
        var active = new List<Face> { originalFace };

        for (int k = 0; k < planes.Count; k++)
        {
            var (point, normal) = planes[k];
            var nextActive = new List<Face>();

            foreach (var frag in active)
            {
                var loop = frag.GetOuterLoop();
                if (loop == null || frag.Loops.Count != 1)
                    throw new NotSupportedException("GeneralSolidSubtractor yalnızca tek dış Loop'lu (deliksiz) Face'leri destekler.");

                var orderedVerts = loop.GetOrderedVertices();
                var dists = orderedVerts.Select(v => (v.Position - point).Dot(normal)).ToList();

                bool hasPos = dists.Any(d => d > Tolerance);
                bool hasNeg = dists.Any(d => d < -Tolerance);

                if (hasPos && !hasNeg)
                {
                    keptFragments.Add(frag); // tamamen outsideB_k -> KESİN kept, kısa devre
                    continue;
                }

                if (hasNeg && !hasPos)
                {
                    nextActive.Add(frag); // tamamen insideB_k -> hâlâ belirsiz, sonraki düzlem denenir
                    continue;
                }

                if (!hasPos && !hasNeg)
                    throw new NotSupportedException("Bir Face aday düzlemle tam çakışık (coplanar) — kapsam dışı.");

                // Karışık: düzlem bu parçayı GERÇEKTEN kesiyor.
                var chordVerts = new List<Vertex>();
                int m = orderedVerts.Count;
                for (int i = 0; i < m; i++)
                    if (Math.Abs(dists[i]) <= Tolerance) chordVerts.Add(orderedVerts[i]);

                var originalEdges = loop.Edges.ToList();
                var pendingSplits = new List<(TopologyEdge Edge, Vector3D Point)>();
                for (int i = 0; i < m; i++)
                {
                    double dA = dists[i];
                    double dB = dists[(i + 1) % m];
                    if (Math.Abs(dA) <= Tolerance || Math.Abs(dB) <= Tolerance) continue;
                    if ((dA > 0) == (dB > 0)) continue;

                    var vA = orderedVerts[i].Position;
                    var vB = orderedVerts[(i + 1) % m].Position;
                    double t = dA / (dA - dB);
                    var pt = vA + (vB - vA) * t;
                    pendingSplits.Add((originalEdges[i], pt));
                }
                foreach (var (edge, pt) in pendingSplits)
                {
                    var (newVertex, _, _) = EdgeSplitter.SplitEdgeAt(a, edge, pt);
                    chordVerts.Add(newVertex);
                }

                if (chordVerts.Count != 2)
                    throw new NotSupportedException(
                        $"Face aday düzlem tarafından {chordVerts.Count} noktada kesiliyor (2 bekleniyordu) — " +
                        "dışbükey olmayan/çoklu-kesim durumu kapsam dışı.");

                var (faceA, faceB, chord) = FaceSplitter.SplitAtChord(a, frag, chordVerts[0], chordVerts[1]);

                bool aOutside = IsEntirelyOnSide(faceA, point, normal, wantPositive: true);
                var outsideFrag = aOutside ? faceA : faceB;
                var insideFrag = aOutside ? faceB : faceA;

                keptFragments.Add(outsideFrag); // outsideB_k -> KESİN kept
                nextActive.Add(insideFrag);     // insideB_k -> belirsiz, devam
                _ = chord; // FaceSplitter.SplitAtChord'un Left/Right ataması yeterli — ayrıca izlenmesine gerek yok (temizlik artık a.GetEdges() üzerinden)
            }

            active = nextActive;
        }

        // Tüm düzlemlerden "insideB" olarak hayatta kalan parçalar -> KESİN discarded (A∩B'nin içi).
        discardedFragments.AddRange(active);
    }

    private static bool IsEntirelyOnSide(Face face, Vector3D planePoint, Vector3D planeNormal, bool wantPositive)
    {
        foreach (var v in face.GetOuterLoop()!.GetOrderedVertices())
        {
            double d = (v.Position - planePoint).Dot(planeNormal);
            if (d > Tolerance) return wantPositive;
            if (d < -Tolerance) return !wantPositive;
        }
        throw new InvalidOperationException("Alt-yüz sınıflandırılamadı — tüm köşeler düzlem üzerinde (dejenere).");
    }

    /*
       NE: `PlaneCutter.ChainIntoLoop` ile AYNI teknik (bilinçli duplicate, `PlaneCutter`'a
           dokunmamak için) — ama TopologyEdge nesneleri yerine ham (nokta,nokta) kiriş
           uç-nokta çiftleri üzerinde çalışır (uç noktalar POZİSYON toleransıyla eşleştirilir,
           kenar/vertex nesne kimliğine bakılmadan — bu kirişler farklı A-Face'lerinden bağımsız
           olarak toplandığı için ortak bir Vertex nesnesi paylaşmıyor olabilirler, ama AYNI
           kesişim noktasını sayısal olarak üretmiş olmalılar).
    */
    private static List<Vector3D> ChainVertexPairsIntoLoop(List<(Vector3D P1, Vector3D P2)> pairs)
    {
        bool SamePoint(Vector3D a, Vector3D b) => a.DistanceTo(b) <= 1e-6;

        var remaining = new List<(Vector3D P1, Vector3D P2)>(pairs);
        var first = remaining[0];
        remaining.RemoveAt(0);

        var ordered = new List<Vector3D> { first.P1, first.P2 };
        while (remaining.Count > 0)
        {
            var last = ordered[^1];
            int idx = remaining.FindIndex(p => SamePoint(p.P1, last) || SamePoint(p.P2, last));
            if (idx < 0)
                throw new NotSupportedException("Kesim kirişleri tek bir kapalı döngü oluşturmuyor — dejenere kesit.");

            var p2 = remaining[idx];
            remaining.RemoveAt(idx);
            ordered.Add(SamePoint(p2.P1, last) ? p2.P2 : p2.P1);
        }

        if (!SamePoint(ordered[^1], ordered[0]))
            throw new NotSupportedException("Kesim kirişleri kapanmıyor — dejenere kesit.");

        ordered.RemoveAt(ordered.Count - 1); // kapanış tekrarını at
        return ordered;
    }

    /*
       NE: Bir A-Face'inin (mutasyona uğramamış, orijinal) sınır poligonunun bir aday düzlemle
           GERÇEKTEN kesişip kesişmediğini, kesişiyorsa kiriş uç noktalarını (2 nokta) döner —
           `PlaneCutter`'ın per-face mixed-crossing mantığıyla AYNI (bilinçli duplicate), ama
           SAF (Solid'i MUTASYONA UĞRATMADAN, sadece pozisyon listesi üzerinde) çalışır.
    */
    private static (Vector3D, Vector3D)? FindPlaneChordOnPolygon(List<Vector3D> polygon, Vector3D planePoint, Vector3D planeNormal)
    {
        var dists = polygon.Select(p => (p - planePoint).Dot(planeNormal)).ToList();
        bool hasPos = dists.Any(d => d > Tolerance);
        bool hasNeg = dists.Any(d => d < -Tolerance);

        if (!hasPos || !hasNeg) return null; // tamamen bir tarafta (veya coplanar) -> bu düzlemle kesişmiyor

        var crossings = new List<Vector3D>();
        int m = polygon.Count;
        for (int i = 0; i < m; i++)
        {
            if (Math.Abs(dists[i]) <= Tolerance)
            {
                crossings.Add(polygon[i]); // mevcut köşe zaten düzlem üzerinde
                continue;
            }
            double dA = dists[i];
            double dB = dists[(i + 1) % m];
            if (Math.Abs(dB) <= Tolerance) continue; // bir sonraki köşe zaten eklenecek
            if ((dA > 0) == (dB > 0)) continue;

            double t = dA / (dA - dB);
            crossings.Add(polygon[i] + (polygon[(i + 1) % m] - polygon[i]) * t);
        }

        if (crossings.Count != 2)
            throw new NotSupportedException(
                $"Face aday düzlem tarafından {crossings.Count} noktada kesiliyor (2 bekleniyordu) — " +
                "dışbükey olmayan/çoklu-kesim durumu kapsam dışı.");

        return (crossings[0], crossings[1]);
    }

    /*
       NE: Düzlemsel bir poligonu (3D nokta listesi) bir yarı-uzayla kırpar — kalan taraf
           `(p - planePoint)·planeNormal <= tolerance` (yani `insideB`, B'nin İÇİNE bakan taraf).
       NASIL: `ConvexPolygonClipper2D`'nin Sutherland-Hodgman ilkesiyle AYNI (bilinçli
           duplicate — burada ayrı, çünkü kırpılan poligon ile kırpma düzlemi FARKLI
           düzlemlerde: `ConvexPolygonClipper2D.Intersect` coplanar İKİ poligon varsayıyor,
           burada tek bir düzlemsel poligonu keyfi bir 3D düzlemle kesiyoruz — 2D izdüşüme
           GEREK YOK, kırpılan poligon zaten kendi düzleminde kalır çünkü hem orijinal
           noktalar hem de doğrusal enterpolasyonla bulunan yeni kesişim noktaları o
           düzlemin İÇİNDE kalır).
    */
    private static List<Vector3D> ClipPolygonByHalfSpace(List<Vector3D> polygon, Vector3D planePoint, Vector3D planeNormal)
    {
        var n = planeNormal.Normalize();
        double SignedDist(Vector3D p) => (p - planePoint).Dot(n);

        var output = new List<Vector3D>();
        int cnt = polygon.Count;
        if (cnt == 0) return output;

        for (int i = 0; i < cnt; i++)
        {
            var current = polygon[i];
            var previous = polygon[(i - 1 + cnt) % cnt];

            double dCurrent = SignedDist(current);
            double dPrevious = SignedDist(previous);
            bool currentInside = dCurrent <= Tolerance;
            bool previousInside = dPrevious <= Tolerance;

            if (currentInside)
            {
                if (!previousInside)
                    output.Add(LineIntersectPlane(previous, current, dPrevious, dCurrent));
                output.Add(current);
            }
            else if (previousInside)
            {
                output.Add(LineIntersectPlane(previous, current, dPrevious, dCurrent));
            }
        }
        return output;
    }

    private static Vector3D LineIntersectPlane(Vector3D p1, Vector3D p2, double d1, double d2)
    {
        double denom = d1 - d2;
        if (Math.Abs(denom) < 1e-12) return p1; // paralel — çağıran mantık bu durumu zaten önler
        double t = d1 / denom;
        return p1 + (p2 - p1) * t;
    }

    /*
       NE: Kırpılmış kapak poligonundan TAMAMEN YENİ (fresh) Vertex/TopologyEdge'lerden oluşan
           bağımsız bir Face inşa eder — her kenarın SADECE `LeftFace` tarafı dolduruluyor,
           `RightFace` BİLEREK boş bırakılıyor (`OpenEdgeStitcher.Stitch` tarafından, komşu
           parçanın/kapağın eşleşen kenarıyla, `VertexWelder.Weld` sonrasında dikilecek).
    */
    private static Face BuildFreshOpenCapFace(List<Vector3D> polygonPoints, Vector3D capNormal)
    {
        var capFace = new Face { Normal = capNormal };
        var loop = new Loop(isOuter: true);

        var vertices = polygonPoints.Select(p => new Vertex(p)).ToList();
        int m = vertices.Count;
        for (int i = 0; i < m; i++)
        {
            var edge = new TopologyEdge(vertices[i], vertices[(i + 1) % m]) { LeftFace = capFace };
            loop.Edges.Add(edge);
        }

        capFace.Loops.Add(loop);
        return capFace;
    }

    /*
       NE: `SolidSubtractor.PlaneIntersectsSolidBoundary` ile BİREBİR aynı kural (bilinçli
           olarak duplicate edildi — `SolidSubtractor.cs`'e dokunmamak, mevcut testlerini
           regresyon riskine sokmamak için).
    */
    private static bool PlaneIntersectsSolidBoundary(Solid solid, Vector3D planePoint, Vector3D planeNormal)
    {
        var n = planeNormal.Normalize();
        double SignedDist(Vector3D p) => (p - planePoint).Dot(n);

        foreach (var face in solid.Faces)
        {
            var loop = face.GetOuterLoop();
            if (loop == null) continue;

            var dists = loop.GetOrderedVertices().Select(v => SignedDist(v.Position)).ToList();
            bool hasPos = dists.Any(d => d > Tolerance);
            bool hasNeg = dists.Any(d => d < -Tolerance);

            if (hasPos && hasNeg)
                return true;
        }

        return false;
    }
}
