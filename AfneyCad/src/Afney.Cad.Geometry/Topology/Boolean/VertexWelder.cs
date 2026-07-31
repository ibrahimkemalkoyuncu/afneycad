using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Vertex Kaynaşması (VertexWelder) — CSG Boolean için 1. yapı taşı
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — genel iki-katı SUBTRACT'in "gerçek CSG kernel'lerinin
          kod hacminin büyük kısmının geldiği" iki ön koşulundan biri (diğeri: coplanar yüz
          birleştirme, ayrı bir oturuma bırakıldı). İki BAĞIMSIZ `Solid` (ör. `BRepBuilder.
          ExtrudeBox` ile ayrı ayrı üretilmiş iki kutu), aynı KONUMDA olsa bile FARKLI `Vertex`
          NESNELERİ taşır (`Vertex` bir class, C# referans eşitliğiyle karşılaştırılıyor —
          `Solid.GetVertices()`'in `HashSet<Vertex>` kullanımı buna dayanıyor). Boolean
          operasyonlarının (kesişim sınıflandırma, ortak sınır tespiti) doğru çalışması için bu
          "aynı konum, farklı nesne" vertex çiftlerinin TEK bir ortak Vertex'e indirgenmesi
          gerekir — winged-edge yapısındaki TÜM `TopologyEdge.StartVertex`/`EndVertex`
          referansları buna göre yönlendirilir (mutasyon — Id eşleştirmesi YETMEZ, fiziksel
          referans değişikliği şart, aksi halde `HashSet<Vertex>` tabanlı Euler sayımı yanlış
          çıkar).

   TOLERANS NOTU: Bu tolerans, `SpaceDetectionEngine`/`WallChainBuilder`'daki kullanıcı-çizim
   toleransı (`MergeTolerance`, 5mm — "kullanıcı elle ne kadar hassas tıkladı" sorusu) ile
   KARIŞTIRILMAMALI. Burası geometrik "aynı nokta mı" kararı — `PlaneCutter.Tolerance` (1e-6)
   mertebesinde, çok daha sıkı bir değer olmalı (çağıran seçer, varsayılan yok — kasıtlı).

   KAPSAM (bilinçli, dar): Basit O(n²) mesafe karşılaştırması (grup başına SADECE grubun ilk
   elemanına karşı, TRANSITIF DEĞİL — ör. A~B yakın, B~C yakın ama A~C uzaksa, A ve C AYNI
   gruba girmez). Büyük Solid sayıları için spatial hash gerekebilir (roadmap'in performans
   notuyla tutarlı) — bu ilk sürümün kapsamı dışında, n küçük (tipik CSG girdi Solid'i) varsayımı.
*/
public static class VertexWelder
{
    /// <summary>
    /// Verilen Solid'ler arasında (veya tek bir Solid içinde) birbirine `tolerance` mesafesinden
    /// yakın Vertex nesnelerini tek bir ortak Vertex'e indirger. Kaynaşan grup içinde İLK
    /// bulunan Vertex korunur (konumu değişmez); diğerlerine referans veren tüm TopologyEdge'ler
    /// korunan Vertex'e yönlendirilir. Solid'lerin geometrisi (pozisyonlar, hacim, alan)
    /// DEĞİŞMEZ — sadece topolojik kimlik birleşir. Kaynaşacak çift yoksa hiçbir şey yapmaz.
    /// </summary>
    public static void Weld(IEnumerable<Solid> solids, double tolerance)
    {
        var edges = new List<TopologyEdge>();
        foreach (var solid in solids)
            edges.AddRange(solid.GetEdges());

        if (edges.Count == 0) return;

        // Edge'lerden referans-bazlı tekil vertex listesi (Solid.GetVertices() ile aynı kaynak).
        var allVertices = new List<Vertex>();
        var seen = new HashSet<Vertex>();
        foreach (var e in edges)
        {
            if (seen.Add(e.StartVertex)) allVertices.Add(e.StartVertex);
            if (seen.Add(e.EndVertex)) allVertices.Add(e.EndVertex);
        }

        var replacement = new Dictionary<Vertex, Vertex>();
        for (int i = 0; i < allVertices.Count; i++)
        {
            var vi = allVertices[i];
            if (replacement.ContainsKey(vi)) continue; // zaten başka bir gruba katılmış

            for (int j = i + 1; j < allVertices.Count; j++)
            {
                var vj = allVertices[j];
                if (replacement.ContainsKey(vj)) continue;
                if (ReferenceEquals(vi, vj)) continue;

                if (vi.Position.DistanceTo(vj.Position) <= tolerance)
                    replacement[vj] = vi;
            }
        }

        if (replacement.Count == 0) return;

        foreach (var e in edges)
        {
            if (replacement.TryGetValue(e.StartVertex, out var newStart))
            {
                e.StartVertex = newStart;
                if (!newStart.Edges.Contains(e)) newStart.Edges.Add(e);
            }
            if (replacement.TryGetValue(e.EndVertex, out var newEnd))
            {
                e.EndVertex = newEnd;
                if (!newEnd.Edges.Contains(e)) newEnd.Edges.Add(e);
            }
        }
    }

    /// <summary>Tek bir Solid'in kendi içindeki yakın vertex çiftlerini kaynaştırır (bkz. <see cref="Weld(IEnumerable{Solid}, double)"/>).</summary>
    public static void Weld(Solid solid, double tolerance) => Weld(new[] { solid }, tolerance);
}
