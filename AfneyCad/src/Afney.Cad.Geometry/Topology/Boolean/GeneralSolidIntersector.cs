using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Genel Çok-Yüzlü INTERSECT Montajı (GeneralSolidIntersector) — CSG Boolean,
       `GeneralSolidSubtractor` (2026-08-06, subdivide→classify→reconstruct yeniden yazımı)
       ÜZERİNE additive bir genişleme. `GeneralSolidSubtractor.cs`'e DOKUNULMADI — SADECE
       oradaki 250 satırlık, testlerle doğrulanmış subdivide/kırpma yardımcı metodları
       `private`den `internal`e çevrildi (davranış AYNI) ki burada TEKRAR KULLANILABİLSİN
       (bilinçli tercih: 250 satırlık bir algoritmayı kopyalamak yerine TEK doğrulanmış
       implementasyonu paylaşmak — bkz. Roadmap_CSG_Boolean.md, 2026-08-07 güncellemesi).

   NEDEN INTERSECT, UNION'DAN FARKLI OLARAK GÜVENLE BU ALTYAPI ÜZERİNE KURULABİLİYOR
   (araştırma + elle doğrulama, 2026-08-07): `SubtractMultiPlane`'in subdivide adımı
   (`SplitFaceAgainstPlanes`), B DIŞBÜKEY olduğu için, A'nın HER orijinal Face'ini B'nin
   yarı-uzaylarına göre klasik bir "convex clipping" (Sutherland-Hodgman'ın 3D genellemesi)
   ile ikiye ayırıyor: "outsideB" dalı (B'nin EN AZ bir yüz-yarı-uzayının dışında — De Morgan
   OR) ve TÜM düzlemlerden hayatta kalan "insideB" dalı (B'nin TÜM yarı-uzaylarının içinde,
   yani gerçekten A∩B'nin İÇİNDE). SUBTRACT bu ikinci grubu (`discardedFragments`) ATIYOR ve
   ilk grubu (`keptFragments`) + kapak yüzlerini (kesişim düzlemlerinin A içinde kalan,
   diğer düzlemlerle kırpılmış kesitleri, normali B'nin normalinin TERSİ) TUTUYOR. INTERSECT
   ise TAM TERSİNİ istiyor: A∩B'nin sınırı = A'nın B-İÇİNDE kalan parçaları (`insideB`
   fragmanları, DEĞİŞTİRİLMEMİŞ normalle — bunlar zaten A'nın kendi orijinal yüzeyinin bir alt
   kümesi, A∩B'nin o noktadaki dışa-dönük yönü A'nınkiyle AYNI) + AYNI kapak yüzleri (AYNI
   düzlemler, AYNI kırpma) ama normali TERS ÇEVRİLMEMİŞ (B'nin KENDİ normali doğrudan
   kullanılıyor — çünkü artık kapağın "dışarı" tarafı B'nin dışı, yani B'nin kendi outward
   yönüyle AYNI). Elle doğrulama (köşe-çentiği ve through-slot senaryoları, Roadmap'te
   belgelendi): bu formül HER İKİ senaryoda da eksiksiz, kapaksız/boşluksuz bir katı üretiyor
   — çünkü "A'yı B'nin yarı-uzaylarıyla kırp" işlemi (INTERSECT), dışbükey bir kırpma bölgesine
   karşı standart bir "clip" operasyonu ve zaten TEK bir Solid'in (A'nın) kendi içinde
   tutarlı bir decomposition'ı — B'nin KENDİ Face'lerini AYRICA A'nın düzlemleriyle kesip
   ikinci BAĞIMSIZ bir decomposition üretmeye VE bu iki decomposition'ın açık kenarlarını
   birbirine dikmeye HİÇ gerek YOK.

   NEDEN UNION AYNI YOLLA YAPILAMIYOR (araştırıldı, KAPSAM DIŞI bırakıldı — ayrı dosyaya bkz.
   Roadmap_CSG_Boolean.md 2026-08-07 güncellemesi): UNION(A,B)'nin sınırı = (A'nın B-DIŞI
   parçaları) ∪ (B'nin A-DIŞI parçaları) — bu, İKİ BAĞIMSIZ decomposition (A, B'nin
   düzlemleriyle VE B, A'nın düzlemleriyle AYRI AYRI) gerektiriyor, ve bu iki decomposition'ın
   açık kenar döngüleri GENEL OLARAK AYNI 3D eğri ÜZERİNDE DEĞİL (elle doğrulanmış köşe-çentiği
   karşı-örneği: A'nın açık döngüsü B'nin düzlemlerinde (X=1500/Y=1500), B'nin açık döngüsü
   A'nın düzlemlerinde (X=2000/Y=2000) — birbirinden 500 birim UZAKTA, iki AYRI 6-köşeli
   döngü, sadece 2 köşede kesişiyor) — aradaki boşluğu kapatmak için YENİ, henüz bu kod
   tabanında karşılığı olmayan bir "köprü yüzü" (bridging face) inşası gerekiyor. Bu,
   `ConvexPolygonClipper2D`/`OpenEdgeStitcher`'ın çözdüğü "TEK Solid'in kendi içindeki
   kırpma sınırı" probleminden YAPISAL OLARAK farklı bir problem sınıfı.

   KAPSAM DIŞI (bilinçli, `GeneralSolidSubtractor` ile AYNI sınırlamalar — `SplitFaceAgainstPlanes`
   PAYLAŞILDIĞI için otomatik olarak miras alınıyor): B içbükeyse veya A'nın bir Face'i bir
   aday düzlemi 2'den fazla kenarında kesiyorsa (dışbükey olmayan kesişim) `NotSupportedException`;
   A ve B kesişmiyorsa (adayı yok) `NotSupportedException`; 3+ parçanın TAM AYNI kenarda
   buluştuğu (T-birleşim) dejenere durumda `OpenEdgeStitcher` açık `InvalidOperationException`.
*/
public static class GeneralSolidIntersector
{
    private const double Tolerance = 1e-6;

    /*
       NE: `a` ile `b`'nin kesişimini (A∩B) hesaplar — B'nin A'nın sınırını BİRDEN FAZLA
           yüzden kestiği genel durumu (tek-düzlem özel durumunu `PlaneCutter.CutWithPlane`'e
           devrederek) destekler.
       NOT: `a` YERİNDE (in place) İÇSEL ÇALIŞMA KOPYASI olarak değiştirilir — ÇAĞIRAN TARAF
            `a`'yı sonuç olarak KULLANMAMALI, dönen değer asıl sonuçtur (`GeneralSolidSubtractor.
            Subtract`'in AYNI deseniyle tutarlı).
    */
    public static Solid Intersect(Solid a, Solid b, string resultName = "A_intersect_B")
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

            if (GeneralSolidSubtractor.PlaneIntersectsSolidBoundary(a, planePoint, planeNormal))
                candidatePlanes.Add((planePoint, planeNormal));
        }

        if (candidatePlanes.Count == 0)
            throw new NotSupportedException(
                "GeneralSolidIntersector: B, A'nın sınırını hiçbir yüz düzleminde GERÇEKTEN (transversal) kesmiyor — " +
                "A ve B kesişmiyor (boş kesişim) ya da biri diğerinin içinde tamamen gömülü (bu durumda kesişim " +
                "gömülü olanın KENDİSİDİR ama bu otomatik tespit kapsam dışı); bkz. Roadmap_CSG_Boolean.md.");

        if (candidatePlanes.Count == 1)
        {
            var (p, n) = candidatePlanes[0];
            // `PlaneCutter.CutWithPlane` `planeNormal` yönündeki (pozitif) tarafı TUTAR. B'nin
            // İÇİ, B'nin outward normali `n`'nin TERSİ yönündedir — bu yüzden A∩B'yi (B'nin
            // içinde kalan A parçasını) tutmak için `-n` geçilir (kapak normali otomatik
            // `-(-n)=+n` çıkar — INTERSECT kapak kuralıyla, bkz. dosya başı NEDEN notu, TUTARLI).
            PlaneCutter.CutWithPlane(a, p, -n);
            return a;
        }

        return IntersectMultiPlane(a, candidatePlanes, resultName);
    }

    private static Solid IntersectMultiPlane(Solid a, List<(Vector3D Point, Vector3D Normal)> planes, string resultName)
    {
        // Kapak kirişlerinin AYRI/BAĞIMSIZ toplanma NEDENİ `GeneralSolidSubtractor.
        // SubtractMultiPlane` ile BİREBİR aynı (bkz. o dosyadaki NEDEN notu) — bilinçli
        // duplicate (bu blok küçük, paylaşılan `FindPlaneChordOnPolygon`'u çağırıyor).
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
                var chord = GeneralSolidSubtractor.FindPlaneChordOnPolygon(polygon, point, normal);
                if (chord != null)
                    capChordsByPlane[k].Add(chord.Value);
            }
        }

        // NE FARK EDER (SUBTRACT'e göre): `outsideFragments` (B'nin herhangi bir yarı-uzayının
        // DIŞINDA olduğu KESİN olarak bulunan dallar) burada ATILACAK — INTERSECT'in istediği
        // A∩B'nin sınırı DEĞİL. `insideFragments` (TÜM düzlemlerden B-İÇİNDE hayatta kalan
        // dallar — SUBTRACT'te "discarded") burada TUTULACAK — bunlar A'nın B∩A içinde kalan
        // gerçek yüzey parçaları.
        var outsideFragments = new List<Face>();
        var insideFragments = new List<Face>();

        foreach (var originalFace in a.Faces.ToList())
        {
            // `SplitFaceAgainstPlanes`'in kendisi "kept"/"discarded" isimlendirmesiyle
            // (SUBTRACT'in bakış açısıyla) yazıldı ama SAF bir subdivide/classify adımı —
            // hangi listenin NE anlama geldiği tamamen ÇAĞIRANA bağlı (parametre isimleri
            // `keptFragments`/`discardedFragments`, ama INTERSECT için bunlar sırasıyla
            // "outsideFragments" (atılacak) ve "insideFragments" (tutulacak) olarak okunmalı).
            GeneralSolidSubtractor.SplitFaceAgainstPlanes(a, originalFace, planes, outsideFragments, insideFragments);
        }

        // INTERSECT: `outsideFragments`'i `a.Faces`'ten ÇIKAR (SUBTRACT'in tam tersi — SUBTRACT
        // `discardedFragments`'i çıkarırdı).
        foreach (var f in outsideFragments)
            a.Faces.Remove(f);

        // Temizlik: `GeneralSolidSubtractor.SubtractMultiPlane` ile AYNI gerekçe (bkz. o
        // dosyadaki NEDEN notu) — `a.GetEdges()` üzerinden HER kenar taranır, artık
        // `a.Faces`'te olmayan bir Face'e işaret eden Left/RightFace null'a çekilir.
        foreach (var edge in a.GetEdges().ToList())
        {
            if (edge.LeftFace != null && !a.Faces.Contains(edge.LeftFace)) edge.LeftFace = null;
            if (edge.RightFace != null && !a.Faces.Contains(edge.RightFace)) edge.RightFace = null;
        }

        var result = new Solid(resultName);
        result.Faces.AddRange(a.Faces); // = insideFragments (a.Faces artık sadece bunları içeriyor)

        for (int i = 0; i < planes.Count; i++)
        {
            var (_, normal) = planes[i];
            var chords = capChordsByPlane[i];
            if (chords.Count == 0) continue;

            var clipped = GeneralSolidSubtractor.ChainVertexPairsIntoLoop(chords);
            for (int j = 0; j < planes.Count; j++)
            {
                if (j == i) continue;
                clipped = GeneralSolidSubtractor.ClipPolygonByHalfSpace(clipped, planes[j].Point, planes[j].Normal);
                if (clipped.Count < 3) break;
            }

            if (clipped.Count < 3) continue;

            // NEDEN normal TERS ÇEVRİLMİYOR (SUBTRACT'in `-normal`'inin TERSİ): Bu kapak
            // A∩B'nin sınırıdır — kalan malzeme (A∩B) B'nin İÇİNDE duruyor, kapağın dışa-dönük
            // yönü (malzemenin OLMADIĞI tarafa, yani B'nin DIŞINA) B'nin KENDİ outward
            // normaliyle AYNI yönde olmalı (bkz. dosya başı NEDEN notu, elle doğrulanmış
            // köşe-çentiği ve through-slot senaryolarıyla tutarlı).
            var capFace = GeneralSolidSubtractor.BuildFreshOpenCapFace(clipped, normal);
            result.Faces.Add(capFace);
        }

        VertexWelder.Weld(result, Tolerance);
        OpenEdgeStitcher.Stitch(result);

        if (!result.IsValid())
            throw new InvalidOperationException(
                "GeneralSolidIntersector: montaj sonucu topolojik olarak geçersiz (Euler/manifold testi başarısız) — " +
                "beklenmeyen bir dejenere kesişim (bkz. yukarıdaki KAPSAM DIŞI notları).");

        return result;
    }
}
