using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Açık Kenar Dikişi (OpenEdgeStitcher) — CSG Boolean, genel/tekrar-kullanılabilir yapı taşı.
   NEDEN: `docs/Roadmap_CSG_Boolean.md` (2026-08-04 "Yol B" girişi) — çok-yüzlü SUBTRACT'in
          mirror-cap'leri BAŞKA aday düzlemlerin "içeri" yarı-uzaylarına göre kırpılınca
          (`GeneralSolidSubtractor`'ın yeni çok-düzlem yolu), kırpma sınırında YENİ bir kenar
          oluşuyor ve bu kenarın "diğer tarafı" (winged-edge modelinde `LeftFace`/`RightFace`,
          İKİSİ de dolu olmalı) BAŞKA bir parçanın (komşu bir kapak veya A'nın kendi sınırı)
          KENDİ topolojisinde eşleşen bir "ikiz kenar" olarak duruyor — iki bağımsız inşa
          sürecinden (farklı Face'ler, farklı Vertex/TopologyEdge nesneleri) geldikleri için
          nesne referansıyla eşleşmiyorlar.
   YÖNTEM: `VertexWelder.Weld`'den SONRA çağrılır (uç noktalar zaten aynı fiziksel Vertex
          nesnesine indirgenmiş olmalı). Solid'in TÜM "açık" kenarlarını (`LeftFace`/`RightFace`
          alanlarından TAM OLARAK biri null) toplar, (StartVertex,EndVertex) çiftine göre
          (yönden bağımsız) gruplar. Her çift-grup için TAM 2 açık kenar bulunmalı — bu ikisi
          TEK bir kenara birleştirilir (biri `keep` olarak kalır, `remove`'un dolu Face'i
          `keep`'in boş slotuna atanır, `remove`'a referans veren Face Loop'ları `keep`'e
          yönlendirilir).
   NEDEN LEFT/RIGHT "YÖN" AYRIMI GEREKMİYOR (basitleştirme, kaynak kodla doğrulandı): Bu
          codebase'te `TopologyEdge.LeftFace`/`RightFace` alanları SADECE (a) `Solid.IsValid()`
          manifold null-kontrolünde ve (b) Face-bağlantı BFS'inde (hangi Face'lerin hangi
          kenarı PAYLAŞTIĞI) kullanılıyor — HİÇBİR yerde "Left = ileri yön, Right = ters yön"
          bilgisine göre GERÇEK geometrik gezinme (traversal) yapılmıyor (her Face kendi
          `Loop.GetOrderedVertices()`'i ile paylaşılan Vertex zincirlemesinden bağımsız olarak
          kendi sırasını kurar — `EdgeSplitter`'ın kendi NOT'unda da belgelendiği gibi Next/Prev
          işaretçileri zaten bu codebase'te tam bakımlı değil). Bu yüzden birleştirirken
          `remove`'un dolu Face'ini `keep`'in BOŞ olan slotuna (Left ya da Right, hangisi boşsa)
          atamak YETERLİ ve DOĞRU — yön uyumu ayrıca kontrol edilmesi gerekmiyor.
   KAPSAM DIŞI (bilinçli): bir çiftte 2'den FAZLA açık kenar bulunursa (üç veya daha fazla
          parçanın TAM AYNI kenarda buluştuğu, dejenere/T-birleşim durumu) `InvalidOperationException`
          (sessiz yanlış sonuç yerine). Tek kalan açık kenarlar (grup büyüklüğü 1) burada
          dokunulmadan bırakılır — `Solid.IsValid()`'in manifold kontrolü bunu zaten yakalar.
*/
public static class OpenEdgeStitcher
{
    public static void Stitch(Solid solid)
    {
        var openEdges = solid.GetEdges()
            .Where(e => (e.LeftFace == null) != (e.RightFace == null))
            .ToList();

        var groups = new Dictionary<(Guid, Guid), List<TopologyEdge>>();
        foreach (var e in openEdges)
        {
            var a = e.StartVertex.Id;
            var b = e.EndVertex.Id;
            var key = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<TopologyEdge>();
                groups[key] = list;
            }
            list.Add(e);
        }

        foreach (var list in groups.Values)
        {
            if (list.Count == 1) continue; // hâlâ açık -> IsValid() yakalayacak, dürüst bırakılıyor
            if (list.Count != 2)
                throw new InvalidOperationException(
                    $"OpenEdgeStitcher: aynı köşe çiftini paylaşan {list.Count} açık kenar bulundu " +
                    "(2 bekleniyordu) — dejenere/çoklu-birleşim kesişimi kapsam dışı.");

            MergeEdges(list[0], list[1]);
        }
    }

    private static void MergeEdges(TopologyEdge keep, TopologyEdge remove)
    {
        var removeFace = remove.LeftFace ?? remove.RightFace
            ?? throw new InvalidOperationException("OpenEdgeStitcher: 'açık' kenarın dolu bir Face'i yok (beklenmeyen durum).");

        if (keep.LeftFace == null) keep.LeftFace = removeFace;
        else if (keep.RightFace == null) keep.RightFace = removeFace;
        else throw new InvalidOperationException("OpenEdgeStitcher: birleştirilecek kenarın boş slotu yok (beklenmeyen durum).");

        ReplaceEdgeInFace(removeFace, remove, keep);
    }

    private static void ReplaceEdgeInFace(Face face, TopologyEdge oldEdge, TopologyEdge newEdge)
    {
        foreach (var loop in face.Loops)
        {
            for (int i = 0; i < loop.Edges.Count; i++)
            {
                if (ReferenceEquals(loop.Edges[i], oldEdge))
                    loop.Edges[i] = newEdge;
            }
        }
    }
}
