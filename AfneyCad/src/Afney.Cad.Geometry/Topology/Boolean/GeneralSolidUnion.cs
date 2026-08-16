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
        var bRefForA = CloneSolid(b, "union_b_ref"); // A'nın kesişim/segment-toplama tarafı için "B"

        var bWork = CloneSolid(b, "union_b_work");
        var aRefForB = CloneSolid(a, "union_a_ref"); // B'nin kesişim/segment-toplama tarafı için "A"

        // `SolidClassifier.IsPointInside`'ın ışın-üçgen sayımının GÜVENİLİR çalışması için TAM/
        // eksiksiz (coplanar ön-geçişten HİÇ ETKİLENMEMİŞ) birer klon — bkz. aşağıdaki
        // `MergeCoplanarOverlappingFaces` yorumu ve `SubdivideAndClassifyOutside`'ın YENİ
        // `classificationSolid` parametresinin dokümantasyonu.
        var bClassifyForA = CloneSolid(b, "union_b_classify");
        var aClassifyForB = CloneSolid(a, "union_a_classify");

        // ÖN-GEÇİŞ (pre-pass) — coplanar KISMEN-ÖRTÜŞEN Face çiftlerini `SegmentBasedSubdivider`'a
        // hiç göstermeden ÖNCE, TEK bir yerde (burada, hem A hem B'nin TÜM Face'lerini AYNI ANDA
        // görebilen `GeneralSolidUnion`'ın kendisinde) birleştirir — bkz. dosya başı KAPSAM notu.
        // Bu, "hangi taraf üretir" koordinasyon sorusunu YAPISAL OLARAK ortadan kaldırır: aFace/
        // bFace çifti bulunur bulunmaz İKİSİ de segment-toplama rolündeki TÜM 4 klondan (aWork/
        // aRefForB/bWork/bRefForA) SİLİNİR — `SegmentBasedSubdivider.SubdivideAndClassifyOutside`
        // bu ikisini BİR DAHA HİÇ GÖRMEZ, `HasAmbiguousCoplanarOverlap` bu çift için tetiklenemez
        // (Face zaten yok) VE `FaceIntersection`'ın coplanar (tam çakışık/teğet) Face çiftlerinde
        // ürettiği belgelenmemiş dejenere segmentler (canlı testte YAKALANDI — bkz. aşağıdaki
        // NEDEN notu) diğer, coplanar-OLMAYAN Face'lerin bölünmesine KARIŞMAZ. `bClassifyForA`/
        // `aClassifyForB` (SADECE sınıflandırma için, segment-toplama rolünde HİÇ kullanılmayan
        // klonlar) bu silme işleminden BİLİNÇLİ olarak MUAF tutulur.
        MergeCoplanarOverlappingFacesInto(aWork, bRefForA, bWork, aRefForB, out var mergedCoplanarFaces);

        var aOutside = SegmentBasedSubdivider.SubdivideAndClassifyOutside(aWork, bRefForA, bClassifyForA);
        var bOutside = SegmentBasedSubdivider.SubdivideAndClassifyOutside(bWork, aRefForB, aClassifyForB);

        var result = new Solid(resultName);
        result.Faces.AddRange(aOutside);
        result.Faces.AddRange(bOutside);
        result.Faces.AddRange(mergedCoplanarFaces);

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
       NE: Coplanar VE izdüşümleri (AABB) örtüşen A-Face/B-Face çiftlerini bulup, her çifti
           `ConvexPolygonClipper2D.Union` ile TEK bir birleşik Face'e indirger — bulunan her çift
           SEGMENT-TOPLAMA rolündeki TÜM 4 klondan (`aWork`, `aRefForB`, `bWork`, `bRefForA`) SİLİNİR.
       NEDEN 4 KLONUN HEPSİNDEN SİLİNİYOR (SINIFLANDIRMA klonlarından — `bClassifyForA`/
           `aClassifyForB` — DEĞİL, bkz. `Union`'ın çağrı noktası): `SubdivideAndClassifyOutside`'ın
           YENİ `classificationSolid` parametresi sayesinde artık İKİ AYRI rol var —
           (1) SEGMENT-TOPLAMA (`CollectSegmentsAgainstAllFaces` — `FaceIntersection.Intersect`),
           (2) SINIFLANDIRMA (`SolidClassifier.IsPointInside` — ışın-üçgen sayımı, TAM/eksiksiz
           kabuk gerektirir). Coplanar Face'ler SEGMENT-TOPLAMA rolündeki klonlarda KALIRSA, canlı
           testte YAKALANAN iki farklı BOZULMA türü ortaya çıkıyordu: (a) 4 klonun DÖRDÜNDEN de
           silinmeden SADECE segment-toplama klonlarından silinip sınıflandırma klonları TAM
           bırakılınca, coplanar-OLMAYAN diğer Face çiftleri arasında (ör. A'nın bir yan yüzü İLE
           B'nin, A'nınkiyle AYNI Z aralığında kalan üst/alt yüzü ARASINDA) YENİ, dejenere/teğet
           (tangent) `FaceIntersection` segmentleri üretiliyordu (Face'in kendi SINIRINA teğet ama
           DİĞER Face'in İÇİNDEN geçen bir kesişim çizgisi) — bu, coplanar-örtüşme kontrolünün HİÇ
           yakalamadığı, TAMAMEN FARKLI bir dejenere sınıf (ambiguous-coplanar DEĞİL, "teğet-yüzey"
           kesişimi) ve `SegmentBasedSubdivider`'ın bunu doğru ele aldığı test edilmemiş; (b) segment-
           toplama klonlarından HİÇ silinmeyip SADECE sınıflandırma ayrı tutulsaydı coplanar Face
           çiftleri `HasAmbiguousCoplanarOverlap`'i yeniden tetikleyip `NotSupportedException`
           fırlatırdı (ön-geçişin TÜM amacı bunu önlemekti). Bu yüzden DOĞRU kombinasyon: segment-
           toplama rolündeki TÜM 4 klondan sil (coplanar Face'ler hem "hangi Face bölünecek" hem
           "hangi Face'e karşı kesiştirilecek" listelerinden TAMAMEN kaybolur — ne ambiguous-throw
           ne teğet-kesişim riski kalır), ama SINIFLANDIRMA klonlarını (`bClassifyForA`/
           `aClassifyForB`) TAMAMEN DOKUNULMADAN bırak (ışın-üçgen sayımı hâlâ TAM kabuğa göre
           çalışır, canlı testte YAKALANAN "B'nin içinde kalan bir A-fragmanı üst/alt yüz eksikliği
           YÜZÜNDEN yanlışlıkla dışarıda sayılıyor" hatası ORTADAN KALKAR).
       NASIL (index eşleştirmesi): `aWork`/`aRefForB` (ikisi de `a`'nın BAĞIMSIZ klonu) VE
           `bWork`/`bRefForA` (ikisi de `b`'nin BAĞIMSIZ klonu), `CloneSolid`'in kaynak `Faces`
           listesini SIRAYLA gezip AYNI SIRAYLA yeni Face eklediği için, `aWork.Faces[i]` HER ZAMAN
           `aRefForB.Faces[i]`'nin aynı orijinal A-Face'inin klonu (aynı şekilde `bWork.Faces[j]` ~
           `bRefForA.Faces[j]`) — birleştirilecek Face'in KENDİ (2D) poligonu bu yüzden `aRefForB`/
           `bRefForA`'dan DEĞİL doğrudan `aWork`/`bWork`'ten okunur (aynı Face, farklı klon
           gerekmiyor). Bu fonksiyon ÇAĞRILMADAN ÖNCE (yani hiçbir Face henüz silinmemişken) TÜM 4
           listenin anlık görüntüsü (snapshot) alınır — döngü sırasında `aWork.Faces`/`bWork.Faces`
           mutasyona uğradığı için orijinal index'lere güvenli erişim SADECE bu snapshot'lar
           üzerinden mümkün.
       KAPSAM (bilinçli, dar): SADECE AYNI YÖNLÜ (`na·nb > 0`) coplanar çiftler birleştirilir —
           ZIT yönlü coplanar çakışma (ör. A'nın dışa bakan bir yüzü B'nin İÇİNE gömülü bir
           boşluğun duvarıyla çakışıyor) farklı bir CSG durumu (iç yüzey iptali) ve bu ön-geçişin
           kapsamı DIŞINDA — o durumda normal `SegmentBasedSubdivider` akışı (ve gerekirse kendi
           `HasAmbiguousCoplanarOverlap` koruması) devreye girer. Her aFace/bFace EN FAZLA BİR kez
           eşleştirilir (bir aFace zaten birleştirildiyse sonraki bFace adaylarıyla tekrar
           denenmez) — birden fazla B-Face'in AYNI A-Face ile coplanar-örtüştüğü daha karmaşık
           durumlar (roadmap'in şu ana kadar hiç karşılaşmadığı bir senaryo) kapsam dışı bırakılır,
           o durumda ilgili Face'ler bu ön-geçişten ETKİLENMEDEN normal akışa girer (ve gerekirse
           `HasAmbiguousCoplanarOverlap` kendi korumasını uygular).
    */
    private static void MergeCoplanarOverlappingFacesInto(
        Solid aWork, Solid bRefForA, Solid bWork, Solid aRefForB, out List<Face> merged)
    {
        merged = new List<Face>();

        var aWorkSnapshot = aWork.Faces.ToList();
        var aRefSnapshot = aRefForB.Faces.ToList();
        var bWorkSnapshot = bWork.Faces.ToList();
        var bRefSnapshot = bRefForA.Faces.ToList();

        var aMergedIndices = new HashSet<int>();
        var bMergedIndices = new HashSet<int>();

        for (int i = 0; i < aWorkSnapshot.Count; i++)
        {
            if (aMergedIndices.Contains(i)) continue;
            var aFace = aWorkSnapshot[i];

            for (int j = 0; j < bWorkSnapshot.Count; j++)
            {
                if (bMergedIndices.Contains(j)) continue;
                var bFace = bWorkSnapshot[j];

                if (!IsSameDirectionCoplanarOverlap(aFace, bFace)) continue;

                var polyA = aFace.GetOuterLoop()!.GetOrderedVertices().Select(v => v.Position).ToList();
                var polyB = bFace.GetOuterLoop()!.GetOrderedVertices().Select(v => v.Position).ToList();
                var unionPolygon = ConvexPolygonClipper2D.Union(polyA, polyB, aFace.Normal);

                merged.Add(GeneralSolidSubtractor.BuildFreshOpenCapFace(unionPolygon, aFace.Normal));

                aWork.Faces.Remove(aWorkSnapshot[i]);
                aRefForB.Faces.Remove(aRefSnapshot[i]);
                bWork.Faces.Remove(bWorkSnapshot[j]);
                bRefForA.Faces.Remove(bRefSnapshot[j]);

                aMergedIndices.Add(i);
                bMergedIndices.Add(j);
                break; // bu aFace işlendi, sıradaki i'ye geç
            }
        }
    }

    /*
       NE: İki Face aynı yönlü (`na·nb > 0`) coplanar mı VE 3D AABB izdüşümleri örtüşüyor mu?
       NEDEN AYNI (kopyalanmış) MANTIK `SegmentBasedSubdivider.HasAmbiguousCoplanarOverlap` İLE:
           o metod `private` (bu dosyanın kapsamı dışında, dokunulmadı — görev tanımının "sen karar
           ver" notuna göre burada KÜÇÜK ölçekli, bağımsız bir kopya tercih edildi, aynı test
           mantığı) ama TEK bir A-Face'i B'nin TÜM Face'lerine karşı test ediyordu; burada belirli
           bir (aFace,bFace) ÇİFTİ için gereken, daha dar bir pairwise test.
    */
    private static bool IsSameDirectionCoplanarOverlap(Face aFace, Face bFace)
    {
        const double eps = 1e-6;

        if (!CoplanarFaceDetector.AreCoplanar(aFace, bFace)) return false;
        if (aFace.Normal.Normalize().Dot(bFace.Normal.Normalize()) <= 0) return false;

        var (aMin, aMax) = GetVertexBounds(aFace);
        var (bMin, bMax) = GetVertexBounds(bFace);
        bool overlapsX = aMin.X <= bMax.X + eps && aMax.X >= bMin.X - eps;
        bool overlapsY = aMin.Y <= bMax.Y + eps && aMax.Y >= bMin.Y - eps;
        bool overlapsZ = aMin.Z <= bMax.Z + eps && aMax.Z >= bMin.Z - eps;
        return overlapsX && overlapsY && overlapsZ;
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
