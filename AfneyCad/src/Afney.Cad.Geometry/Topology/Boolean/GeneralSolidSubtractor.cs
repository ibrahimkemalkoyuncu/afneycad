using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Genel Çok-Yüzlü SUBTRACT Montajı (GeneralSolidSubtractor) — CSG Boolean, `SolidSubtractor`
       (tek-düzlem özel durumu) ile PARALEL/ADDITIVE bir genişleme; B'nin A'nın sınırını
       BİRDEN FAZLA yüzden kestiği (örn. bir köşeyi kesen çentik) durumu kapsıyor.
       `SolidSubtractor.Subtract` DOKUNULMADI — o hâlâ tek-düzlem durumunda çağrılabilir,
       davranışı birebir aynı.

   YÖNTEM (`docs/Roadmap_CSG_Boolean.md`, iki araştırma turu + bu oturumun ajan araştırmasıyla
   doğrulanmış "somut, hazır algoritma"):
     1. B'nin A'nın MEVCUT sınırını GERÇEKTEN (transversal) kestiği yüz düzlemleri toplanır
        (`SolidSubtractor`'daki AYNI kural — hasPos && hasNeg — burada bilinçli olarak
        DUPLICATE edildi, `SolidSubtractor.cs`'e dokunmamak için).
     2. Bu düzlemlerle A ART ARDA `PlaneCutter.CutWithPlaneKeepDiscarded` ile, HER SEFERİNDE
        B'nin İÇİNE bakan yönde (`-faceNormal` — B'nin dışa-dönük normalinin tersi) kesilir.
        Matematiksel özdeşlik (elle doğrulandı): union_i (A ∩ outside(faceᵢ) ∩ inside(face₁..ᵢ₋₁))
        = A ∩ (∪ outside(faceᵢ)) = A ∖ (∩ inside(faceᵢ)) = A ∖ B (De Morgan + standart ayrık-
        birleşim bölme özdeşliği) — bu yüzden her adımda "atılan" parça (Dᵢ) HER ZAMAN A∖B'nin
        bir alt-kümesidir (B'nin herhangi bir yüzünün dışında olmak, dışbükey B için B'nin
        tamamen dışında olmak demektir), sıra fark etmeksizin doğru.
        Kesim sonunda `a` = A∩B olur (tüm adaylar B'nin içine doğru art arda kesildi).
     3. Her Dᵢ'nin mirror cap'i (kesim düzlemindeki kapak yüzeyi), GERÇEKTEN A∩B'ye mi bitişik
        (o zaman A∖B'nin GERÇEK dış sınırı) yoksa başka bir Dⱼ'ye mi bitişik (o zaman İÇ bir
        ara-yüzey, dahil EDİLMEMELİ) `FaceRegionClassifier.IsFaceAdjacentToRegion` ile test
        edilir — bu, önceki iki araştırma turunun kaçırdığı "iç-yüz çakışması" sorununun
        çözümü.
     4. Her Dᵢ'nin orijinal (kesilmemiş veya kesim sonucu miras kalan) A-yüzleri HER ZAMAN
        sonuca dahil edilir (adım 2'deki özdeşlik gereği zaten A∖B'nin gerçek sınırı) — sadece
        mirror cap'ler adım 3'teki testten geçmek zorunda.
     5. Tüm parçalar tek bir `Solid`'de toplanıp `VertexWelder.Weld` ile köşeler kaynaştırılır,
        `IsValid()` (Euler) ile doğrulanır.

   KAPSAM DIŞI (bilinçli):
     - B'nin A'yı hiç kesmediği (tamamen dışında/gömülü — cavity) durum: adım 1'de aday
       bulunamaz, açık `NotSupportedException`.
     - Bir mirror cap Face'inin KISMEN A∩B'ye, KISMEN başka bir Dⱼ'ye bitişik olduğu (parçalı
       örtüşme) durum: `FaceRegionClassifier` ikili (tam/hiç) karar verir, Face'in kendisini
       BÖLMEZ — bu, `ConvexPolygonClipper2D` ile Face bölünmesini gerektirir, ayrı bir oturum
       konusu. Bu durumda `IsValid()` başarısız olur ve açık bir istisna fırlatılır (sessiz
       yanlış geometri üretilmez).
     - B içbükeyse veya A'nın bir Face'i düzlemi 2'den fazla kenarında kesiyorsa: alttaki
       `PlaneCutter.CutWithPlaneKeepDiscarded`'ın kendi (miras alınan) kısıtlamaları geçerli.
*/
public static class GeneralSolidSubtractor
{
    private const double Tolerance = 1e-6;

    /*
       NE: `a`'dan `b`'yi çıkarır — B'nin A'nın sınırını BİRDEN FAZLA yüzden kestiği genel
           durumu (tek-düzlem özel durumunu da, `SolidSubtractor.Subtract`'e devrederek)
           destekler.
       NOT: `a` YERİNDE (in place) İÇSEL ÇALIŞMA KOPYASI olarak değiştirilir (tek-düzlem
            durumunda A∖B'nin kendisi olur; çok-düzlem durumunda A∩B'ye dönüşür — ÇAĞIRAN
            TARAF `a`'yı sonuç olarak KULLANMAMALI, dönen değer asıl sonuçtur). Orijinal A'yı
            korumak isteyen çağıran taraf önceden bir kopya çıkarmalı (`PlaneCutter`'ın kendi
            deseniyle tutarlı).
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

        var discardedPieces = new List<Solid>();
        int idx = 0;
        foreach (var (point, normal) in candidatePlanes)
        {
            var (_, discarded) = PlaneCutter.CutWithPlaneKeepDiscarded(a, point, -normal, $"D{idx++}");
            discardedPieces.Add(discarded);
        }
        // Bu noktada `a` = A∩B (tüm aday düzlemler B'nin İÇİNE doğru art arda kesildi).

        var result = new Solid(resultName);
        foreach (var piece in discardedPieces)
        {
            // `CutWithPlaneKeepDiscarded` sözleşmesi: piece.Faces = [orijinal atılan A-yüzleri..., mirror cap (SON eleman)].
            var mirrorCap = piece.Faces[^1];
            for (int i = 0; i < piece.Faces.Count - 1; i++)
                result.Faces.Add(piece.Faces[i]);

            if (FaceRegionClassifier.IsFaceAdjacentToRegion(mirrorCap, a))
                result.Faces.Add(mirrorCap);
            // Değilse: bu mirror cap başka bir Dⱼ'ye bitişik İÇ ara-yüzey — sonuca dahil edilmez.
        }

        VertexWelder.Weld(result, Tolerance);

        if (!result.IsValid())
            throw new InvalidOperationException(
                "GeneralSolidSubtractor: montaj sonucu topolojik olarak geçersiz (Euler/manifold testi başarısız) — " +
                "muhtemelen kısmi mirror-cap örtüşmesi (Face'in yalnızca bir kısmı A∩B'ye bitişik, kapsam dışı) " +
                "ile karşılaşıldı.");

        return result;
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
