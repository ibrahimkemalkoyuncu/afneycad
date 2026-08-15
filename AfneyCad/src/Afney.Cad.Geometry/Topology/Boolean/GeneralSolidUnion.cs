using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Genel Çok-Yüzlü UNION Montajı (GeneralSolidUnion) — CSG Boolean, roadmap'in Faz 5 hedefinin
       4. (ve şimdilik SON) yapı taşı. `docs/Roadmap_CSG_Boolean.md`, "Güncelleme — 2026-08-14/15
       (Session #64-67)" girdilerinin bıraktığı yerden devam ediyor: `FaceSplitter.
       SplitAtPolylineChord` (1. yapı taşı) + `SegmentBasedSubdivider.SubdivideAndClassifyOutside`
       (2. yapı taşı — A'nın Face'lerini B'nin GERÇEK Face sınırlarıyla, "Section-first" mimarisiyle
       böler, `SolidClassifier` ile sınıflandırır, `a`'nın B-DIŞI fragmanlarını döner) + `ConvexPolygonClipper2D.
       Union` (3. yapı taşı — coplanar kısmen-örtüşen Face çiftleri için, BURADA henüz entegre
       EDİLMEDİ, aşağıdaki KAPSAM bölümüne bkz.) hazırdı; eksik olan SADECE bu üçünü tek bir
       `Solid`'de birleştiren montaj katmanıydı.

   KAPSAM (BİLİNÇLİ, DAR — görev tanımıyla tutarlı): SADECE "temiz" (coplanar-OLMAYAN, transversal
   kesişimli) durumlar için. `SegmentBasedSubdivider.SubdivideAndClassifyOutside`, coplanar VE
   izdüşüm-örtüşmesi olan bir A-Face/B-Face çifti bulduğunda KENDİSİ zaten açık
   `NotSupportedException` fırlatıyor (`HasAmbiguousCoplanarOverlap` koruması, Session #67) — bu
   sınıf o korumaya HİÇ dokunmuyor, İKİ çağrısından (A→B ve B→A) biri bu istisnayı fırlatırsa
   YAKALAMADAN/YUTMADAN olduğu gibi çağırana yükseltiyor (görev tanımının açık isteği — sessizce
   farklı bir şey dönmek YERİNE dürüst hata). Coplanar durumun `ConvexPolygonClipper2D.Union` ile
   gerçek çözümü SONRAKİ bir oturuma bırakıldı (roadmap'in Session #67 girdisinin son paragrafının
   belgelediği KOORDİNASYON sorunu — hangi tarafın [A mı B mi] union-face'i üreteceğine dair YEREL
   bilgiyle verilemeyen bir karar — henüz çözülmedi).

   NEDEN "SECTION-FIRST" MİMARİSİ BURADA GÜVENLE ÇALIŞIYOR (roadmap'in 2026-08-14 girdisinin
   HİPOTEZİ, bu dosyada İLK KEZ test edilip doğrulandı): `SegmentBasedSubdivider`, A'nın Face'lerini
   B'nin PLANE'İNE değil GERÇEK Face SINIRINA (`FaceIntersection.Intersect`, poligon-kırpılmış
   segment) göre böldüğü için, A tarafının kesim kirişi İLE B tarafının (simetrik çağrıdaki) kesim
   kirişi TANIM GEREĞİ AYNI 3D konumdadır (iki düzlemin kesişimi — hangi taraftan hesaplanırsa
   hesaplansın aynı doğru/segment) — roadmap'in 2026-08-07 girdisinin çürüttüğü "eski" mimarideki
   (`GeneralSolidSubtractor`'ın PLANE-tabanlı `SplitFaceAgainstPlanes`'i B'nin düzlemlerine göre A'yı,
   ayrıca A'nın düzlemlerine göre B'yi bağımsız bölmesi) "500 birim arada iki paralel kapak" sorunu
   YAPISAL OLARAK oluşmuyor. Bu yüzden köprü/mirror-cap yüzü İNŞA ETMEYE gerek YOK — A'nın B-dışı
   fragmanları + B'nin A-dışı fragmanları, kesim sınırlarında zaten AYNI konumda uç noktalara sahip
   olduğundan `VertexWelder` + `OpenEdgeStitcher` ile DOĞRUDAN dikilebiliyor.

   YÖNTEM (görev tanımının taslağıyla birebir, `SubdivideAndClassifyOutside`'ın "`a` YERİNDE
   değiştirilir" kısıtına göre orkestre edildi):
     1. A'nın ve B'nin BAĞIMSIZ derin kopyaları çıkarılır (`CloneSolid`, bu dosyada YENİ —
        kod tabanında daha önce genel amaçlı bir Solid deep-copy yardımcı metodu YOKTU, grep ile
        doğrulandı). Her çağrı (A→B, B→A) kendi ÇALIŞMA kopyasını (mutasyona uğrayacak taraf) VE
        kendi REFERANS kopyasını (sadece okunacak, ama savunmacı olarak yine de bağımsız bir klon —
        `SubdivideAndClassifyOutside`'ın ikinci parametreyi mutasyona uğratmadığı dokümante edilmiş
        olsa da, iki çağrı arasında YANLIŞLIKLA paylaşılan nesne/hal riskini SIFIRA indirmek için)
        kullanır — TOPLAM 4 klon.
     2. `aOutside = SubdivideAndClassifyOutside(aWork, bRefForA)` — A'nın B-dışı fragmanları.
     3. `bOutside = SubdivideAndClassifyOutside(bWork, aRefForB)` — B'nin A-dışı fragmanları
        (BAĞIMSIZ bir çağrı, adım 2'nin kopyalarıyla hiçbir paylaşılan durumu yok).
     4. `aOutside ∪ bOutside` yeni bir `Solid`'e eklenir.
     5. **KRİTİK temizlik adımı (görev tanımında AÇIKÇA yazılmamıştı, kaynak kod incelemesiyle
        BULUNDU — `GeneralSolidSubtractor`/`GeneralSolidIntersector`'ın AYNI deseni):**
        `FaceSplitter.SplitAtPolylineChord`, bölünen bir Face'in İKİ yarısını da (`faceA`+`faceB`)
        `solid.Faces`'e EKLER — ama `SubdivideAndClassifyOutside` bunlardan sadece B-DIŞINDA olanı
        `outsideFragments`'e dahil eder, B-İÇİNDE kalanı (aWork/bWork'ün KENDİ `Faces` listesinde
        kalsa da) sonuca dahil ETMEZ. Bu yüzden kept fragmanın PAYLAŞILAN kesim kirişi kenarı,
        artık sonuç Solid'de OLMAYAN bir "hayalet" Face'e (discarded yarı) işaret eden dolu bir
        `LeftFace`/`RightFace` alanı taşımaya devam eder — `OpenEdgeStitcher`'ın "açık kenar"
        filtresi (`(LeftFace==null) != (RightFace==null)`) bunu YAKALAYAMAZ (İKİSİ de dolu
        görünür), ve bu kenar B'nin karşılık gelen fragmanına DİKİLEMEDEN kalır. Çözüm:
        `result.Faces`'e eklenen fragmanların TÜM kenarları taranır, `result.Faces` İÇİNDE
        OLMAYAN bir Face'e işaret eden `LeftFace`/`RightFace` `null`'a çekilir (`ClearDanglingFaceReferences`)
        — bu, kenarı gerçekten "açık" hâle getirip `OpenEdgeStitcher`'ın A-tarafı/B-tarafı
        eşleşen ikizini bulup dikmesini SAĞLAR.
     6. `VertexWelder.Weld(result, tolerance)` — A-tarafı ve B-tarafı fragmanlarının kesişim
        sınırındaki AYNI-konumlu ama FARKLI nesne olan Vertex'leri tek nesneye indirger.
     7. `OpenEdgeStitcher.Stitch(result)` — adım 5'in açığa çıkardığı açık kenarları, karşı
        taraftan gelen eşleşen ikizleriyle birleştirir.
     8. `result.IsValid()` (Euler/manifold, kabuk-başına) doğrulanır — başarısızsa açık
        `InvalidOperationException` (sessiz yanlış geometri YERİNE).

   KAPSAM DIŞI (bilinçli, DEĞİŞMEDİ):
     - Coplanar kısmen-örtüşen Face çiftleri: `SegmentBasedSubdivider`'ın kendi `NotSupportedException`'ı
       (`HasAmbiguousCoplanarOverlap`) BURADA yakalanmadan/yutulmadan yukarı fırlar.
     - `SegmentBasedSubdivider`/`FaceSplitter.SplitAtPolylineChord`'un kendi dokümante ettiği TÜM
       kısıtlar (dışbükey/tek-dış-Loop'lu Face varsayımı, kendi kendini kesen polyline reddi,
       kesişim polyline'ının Face sınırına hiç değmediği "delik" durumu) BURADA da AYNEN geçerli.
     - 3+ parçanın TAM AYNI kenarda buluştuğu (T-birleşim) dejenere durum: `OpenEdgeStitcher`'ın
       kendi `InvalidOperationException`'ı.
*/
public static class GeneralSolidUnion
{
    private const double Tolerance = 1e-6;

    /*
       NE: `a` ile `b`'nin birleşimini (A∪B) hesaplar — SADECE coplanar-olmayan (temiz, transversal
           kesişimli) durumlar için (bkz. dosya başı KAPSAM notu).
       NOT: Ne `a` NE `b` mutasyona uğrar (`SubdivideAndClassifyOutside`'ın "`a` yerinde değişir"
            kısıtına rağmen) — bu metod HER İKİSİNİN de bağımsız çalışma kopyalarını kullanır,
            orijinal girdiler çağıran tarafta DEĞİŞMEDEN kalır (`GeneralSolidSubtractor.Subtract`/
            `GeneralSolidIntersector.Intersect`'in "çağıran `a`'yı sonuç olarak kullanmamalı"
            uyarısından BİLİNÇLİ bir sapma — UNION'ın kendi doğası, İKİ girdiyi de SİMETRİK olarak
            iki kez [bir kez çalışma kopyası, bir kez salt-okunur referans olarak] kullanmayı
            gerektirdiğinden, hem `a` hem `b` için TEMİZ/orijinal kopyalar üzerinden çalışmak daha
            güvenli ve daha az sürpriz).
    */
    public static Solid Union(Solid a, Solid b, string resultName = "A_union_B")
    {
        var aWork = CloneSolid(a, "union_a_work");
        var bRefForA = CloneSolid(b, "union_b_ref");
        var aOutside = SegmentBasedSubdivider.SubdivideAndClassifyOutside(aWork, bRefForA);

        var bWork = CloneSolid(b, "union_b_work");
        var aRefForB = CloneSolid(a, "union_a_ref");
        var bOutside = SegmentBasedSubdivider.SubdivideAndClassifyOutside(bWork, aRefForB);

        var result = new Solid(resultName);
        result.Faces.AddRange(aOutside);
        result.Faces.AddRange(bOutside);

        ClearDanglingFaceReferences(result);

        VertexWelder.Weld(result, Tolerance);
        OpenEdgeStitcher.Stitch(result);

        if (!result.IsValid())
            throw new InvalidOperationException(
                "GeneralSolidUnion: montaj sonucu topolojik olarak geçersiz (Euler/manifold testi " +
                "başarısız) — beklenmeyen bir dejenere birleşim (bkz. Roadmap_CSG_Boolean.md).");

        return result;
    }

    /*
       NE: `result.Faces`'e (aOutside+bOutside) eklenen fragmanların kenarlarını tarar, artık
           `result.Faces` İÇİNDE OLMAYAN bir Face'e işaret eden `LeftFace`/`RightFace` alanlarını
           `null`'a çeker — bkz. dosya başı YÖNTEM adım 5.
       NEDEN `GeneralSolidSubtractor.SubtractMultiPlane`/`GeneralSolidIntersector.IntersectMultiPlane`
           İLE AYNI DESEN (bilinçli, küçük ölçekli duplicate): O ikisi `a.GetEdges()` üzerinden
           TÜM Solid'i tarıyordu (tek bir mutasyona uğrayan Solid vardı); burada İKİ BAĞIMSIZ
           kaynaktan (aWork, bWork) gelen fragmanlar TEK bir yeni `result` Solid'inde birleştiği
           için tarama doğrudan `result.GetEdges()` üzerinden yapılıyor — kavramsal olarak AYNI
           "artık listede olmayan Face'e işaret eden referansı temizle" adımı.
    */
    private static void ClearDanglingFaceReferences(Solid result)
    {
        var kept = new HashSet<Face>(result.Faces);
        foreach (var edge in result.GetEdges().ToList())
        {
            if (edge.LeftFace != null && !kept.Contains(edge.LeftFace)) edge.LeftFace = null;
            if (edge.RightFace != null && !kept.Contains(edge.RightFace)) edge.RightFace = null;
        }
    }

    /*
       NE: Bir `Solid`'in TAM bağımsız derin kopyasını çıkarır — Vertex/TopologyEdge/Loop/Face
           nesnelerinin HİÇBİRİ orijinal Solid ile PAYLAŞILMAZ (referans eşitliği açısından TAMAMEN
           yeni nesneler), ama winged-edge topolojisi (paylaşılan kenarlar İKİ komşu Face'in
           Loop'unda da AYNI klon nesnesine işaret eder, `LeftFace`/`RightFace` doğru klon
           Face'lere yönlendirilir) BİREBİR korunur.
       NEDEN GEREKLİ (görev tanımının açıkça istediği ön koşul): `SegmentBasedSubdivider.
           SubdivideAndClassifyOutside`'ın kendi dokümantasyonu "`a` YERİNDE değiştirilir, çağıran
           taraf önceden kopya çıkarmalı" diyor — bu metod olmadan A/B'nin orijinal (çağıran
           tarafın hâlâ elinde tuttuğu) Solid nesneleri bu montaj sırasında sessizce bozulurdu.
           Kod tabanında daha önce genel amaçlı bir Solid-kopyalama yardımcı metodu YOKTU (grep ile
           doğrulandı) — bu yüzden burada YENİ yazıldı (SADECE bu dosyanın kapsamı için, additive).
       NEDEN NextLeftEdge/PrevLeftEdge/NextRightEdge/PrevRightEdge KOPYALANMIYOR (bilinçli):
           `EdgeSplitter`/`OpenEdgeStitcher`'ın kendi dokümante ettiği gibi bu codebase'te bu
           alanlar hiçbir yerde GERÇEK geometrik gezinme için kullanılmıyor (her Face kendi
           `Loop.GetOrderedVertices()`'i ile bağımsız sırasını kurar) — SADECE `LeftFace`/
           `RightFace` (manifold/komşuluk) kopyalanır, `GeneralSolidSubtractor`/`GeneralSolidIntersector`'ın
           `BuildFreshOpenCapFace`'inin de bu alanları HİÇ doldurmadığı desenle TUTARLI.
    */
    private static Solid CloneSolid(Solid source, string name)
    {
        var vertexMap = new Dictionary<Vertex, Vertex>();
        var edgeMap = new Dictionary<TopologyEdge, TopologyEdge>();
        var faceMap = new Dictionary<Face, Face>();

        Vertex CloneVertex(Vertex v)
        {
            if (!vertexMap.TryGetValue(v, out var nv))
            {
                nv = new Vertex(v.Position);
                vertexMap[v] = nv;
            }
            return nv;
        }

        TopologyEdge CloneEdge(TopologyEdge e)
        {
            if (!edgeMap.TryGetValue(e, out var ne))
            {
                ne = new TopologyEdge(CloneVertex(e.StartVertex), CloneVertex(e.EndVertex));
                edgeMap[e] = ne;
            }
            return ne;
        }

        var result = new Solid(name);

        foreach (var face in source.Faces)
        {
            var newFace = new Face { Normal = face.Normal };
            foreach (var loop in face.Loops)
            {
                var newLoop = new Loop(loop.IsOuter);
                foreach (var edge in loop.Edges)
                    newLoop.Edges.Add(CloneEdge(edge));
                newFace.Loops.Add(newLoop);
            }
            faceMap[face] = newFace;
            result.Faces.Add(newFace);
        }

        // İkinci geçiş: TÜM Face'ler klonlandıktan (faceMap tamamlandıktan) SONRA LeftFace/RightFace
        // bağlanır — bir kenar, kendisini oluşturan Face'ten ÖNCE komşu Face'e referans veriyor
        // olabilir (winged-edge'de iki taraf da eşit önemde, sıralama garantisi yok).
        foreach (var (originalEdge, clonedEdge) in edgeMap)
        {
            clonedEdge.LeftFace = originalEdge.LeftFace != null && faceMap.TryGetValue(originalEdge.LeftFace, out var lf) ? lf : null;
            clonedEdge.RightFace = originalEdge.RightFace != null && faceMap.TryGetValue(originalEdge.RightFace, out var rf) ? rf : null;
        }

        return result;
    }
}
