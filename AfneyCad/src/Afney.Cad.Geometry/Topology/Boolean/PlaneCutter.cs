using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Düzlemle Kesme (PlaneCutter) — CSG Boolean Faz 4, "yarı-uzay" (half-space) SUBTRACT
   NEDEN: `docs/Roadmap_CSG_Boolean.md`'nin önerdiği ilk Faz 4 test senaryosu (A=[0,2000]³,
          B=[1000,3000]×[0,2000]×[0,2000] — B'nin Y/Z aralığı A'nınkiyle BİREBİR aynı) analiz
          edilince şu görüldü: B'nin A'yla GERÇEKTEN kesişen tek yüzü B'nin X=1000 yüzüdür —
          B'nin diğer 5 yüzü ya A'nın tamamen dışında (X=3000) ya da A'nın karşılık gelen
          yüzleriyle TAM ÇAKIŞIK (coplanar: alt/üst/Y=0/Y=2000) — ki coplanar yüz çiftleri
          Faz 1-3'ün kendi dokümante ettiği "dejenere, kapsam dışı" durumdur (FaceIntersection
          paralel düzlemler için boş liste döner). Coplanar durumda GENEL iki-katı SUBTRACT,
          köşe/kenar "kaynaşması" (vertex welding — iki BAĞIMSIZ Solid'in aynı konumdaki ama
          FARKLI Vertex nesnelerini tek bir paylaşılan Vertex'e indirgeme) gerektirir; bu,
          Faz 1-3'ün primitiflerinin ötesinde, kendi başına ayrı bir mühendislik çabası
          (gerçek CSG kernel'lerinin çoğu kod hacmi tam olarak buradan gelir).

          BU YÜZDEN Faz 4'ün ilk somut teslimatı, genel iki-katı SUBTRACT DEĞİL, ondan daha
          temel ve daha genel kullanışlı bir birim: **tek bir düzlemle kesme** (yarı-uzay
          SUBTRACT). Roadmap'in önerdiği senaryo TAM OLARAK buna indirgeniyor (B, A'nın X
          eksenindeki her noktasını X=1000'in ötesinde kapsıyor — yani A∖B = A'yı X=1000
          düzlemiyle kesip X<1000 tarafını tutmakla BİREBİR AYNI sonucu verir). Genel (coplanar
          olmayan, karşılıklı vertex-kaynaşması gerektiren) iki-katı SUBTRACT bilinçli olarak
          SONRAKİ bir faza bırakıldı (bkz. roadmap dosyasındaki "Bilinen Riskler" güncellemesi).

   YÖNTEM: Her Face'in sınır köşeleri düzleme göre işaretli mesafeyle (+/-) sınıflandırılır:
   - Tüm köşeler pozitif (veya düzlem üzerinde) → Face AYNEN korunur.
   - Tüm köşeler negatif (veya düzlem üzerinde) → Face TAMAMEN atılır.
   - Karışık (pozitif VE negatif köşe var) → Face düzlemi TAM İKİ kenarında keser (dışbükey
     sınır varsayımı — Faz 1-3'ün "clean/temiz kesişim" kapsamıyla tutarlı): bu iki kesişim
     noktası EdgeSplitter ile (veya zaten bir komşu Face'in kesiminden miras kalan, düzlem
     üzerindeki mevcut bir köşe varsa onu YENİDEN KULLANARAK) bulunur, FaceSplitter ile Face
     ikiye ayrılır, pozitif yarı tutulur.
   - Tüm bu kesim kirişleri (chord) toplanıp TEK bir kapalı döngüde (paylaşılan köşelerle
     zincirlenerek) yeni bir "kesit kapağı" (cap) Face'i oluşturur — kesim düzleminin
     kendisini temsil eden, gerçekten yeni bir yüzey.

   KAPSAM DIŞI (bilinçli, açık hatayla korunuyor): dışbükey OLMAYAN sınırlar (bir Face'i
   düzlem 2'den fazla kenarında kesiyorsa), düzlemle tam çakışık (coplanar) Face'ler, orijinal
   bir köşenin üç veya daha fazla Face'te aynı anda düzlem üzerinde olması gibi yüksek
   dejenerasyon durumları.
*/
public static class PlaneCutter
{
    private const double Tolerance = 1e-6;

    /*
       NE: Solid'i verilen düzlemle keser; `(p - planePoint)·planeNormal >= 0` tarafını
           (pozitif taraf) TUTAR, diğer tarafı ATAR, ve kesim yerine yeni bir "kapak" Face
           ekler (dışa dönük normali -planeNormal — kalan katının dışına, atılan malzemenin
           eski konumuna doğru bakar).
       NOT: `solid` YERİNDE (in place) DEĞİŞTİRİLİR — EdgeSplitter/FaceSplitter'ın kendi
            deseniyle tutarlı (bkz. o dosyaların NEDEN notları). Orijinal Solid'i korumak
            isteyen çağıran taraf önceden bir kopya çıkarmalı.
       ÇIKTI: Yeni oluşturulan kapak Face'i (testlerde alanının analitik kesit alanıyla
              karşılaştırılması için).
    */
    public static Face CutWithPlane(Solid solid, Vector3D planePoint, Vector3D planeNormal)
    {
        var n = planeNormal.Normalize();
        double SignedDist(Vector3D p) => (p - planePoint).Dot(n);

        var chordEdges = new List<TopologyEdge>();
        var facesToRemove = new List<Face>();

        foreach (var face in solid.Faces.ToList())
        {
            var loop = face.GetOuterLoop();
            if (loop == null || face.Loops.Count != 1)
                throw new NotSupportedException("PlaneCutter yalnızca tek dış Loop'lu (deliksiz) Face'leri destekler.");

            var orderedVerts = loop.GetOrderedVertices();
            var dists = orderedVerts.Select(v => SignedDist(v.Position)).ToList();

            bool hasPos = dists.Any(d => d > Tolerance);
            bool hasNeg = dists.Any(d => d < -Tolerance);

            if (hasPos && !hasNeg)
                continue; // tamamen pozitif tarafta -> aynen kalır

            if (hasNeg && !hasPos)
            {
                facesToRemove.Add(face); // tamamen negatif tarafta -> atılır
                continue;
            }

            if (!hasPos && !hasNeg)
                throw new NotSupportedException("Bir Face kesim düzlemiyle tam çakışık (coplanar) — kapsam dışı.");

            // Karışık: Face'i düzlem keser. Kiriş uç noktalarını topla — ya YA mevcut bir
            // köşe zaten düzlem üzerinde (komşu bir Face'in daha önceki kesiminden miras),
            // ya da bir kenarın ortasında YENİ bir kesişim noktası (EdgeSplitter ile).
            var chordVerts = new List<Vertex>();
            int m = orderedVerts.Count;
            for (int i = 0; i < m; i++)
            {
                if (Math.Abs(dists[i]) <= Tolerance)
                    chordVerts.Add(orderedVerts[i]); // mevcut köşe zaten düzlem üzerinde
            }

            // NOT: kenar REFERANSLARI (ve kesişim noktaları) her hangi bir bölme İŞLEMİNDEN
            // ÖNCE, `loop.Edges`'in HENÜZ mutasyona uğramamış hâlinden anlık görüntü (snapshot)
            // olarak alınmalı — EdgeSplitter her çağrıda `loop.Edges`'e 1 kenar çıkarıp 2 kenar
            // ekliyor, bu da listedeki SONRAKİ index'leri kaydırıyor. `loop.Edges[i]`'e MUTASYON
            // SIRASINDA (canlı listeden) erişmek, ikinci kesişim için YANLIŞ bir kenar nesnesi
            // seçilmesine yol açar (kenar üzerinde olmayan bir nokta geçirilir, EdgeSplitter
            // "dejenere" istisnası fırlatır — bu GERÇEK bir hataydı, ilk yazımda yakalandı).
            var originalEdges = loop.Edges.ToList();
            var pendingSplits = new List<(TopologyEdge Edge, Vector3D Point)>();
            for (int i = 0; i < m; i++)
            {
                double dA = dists[i];
                double dB = dists[(i + 1) % m];
                if (Math.Abs(dA) <= Tolerance || Math.Abs(dB) <= Tolerance) continue; // uç zaten köşe-üstü, üstte eklendi
                if ((dA > 0) == (dB > 0)) continue; // aynı taraf, kesişim yok

                var vA = orderedVerts[i].Position;
                var vB = orderedVerts[(i + 1) % m].Position;
                double t = dA / (dA - dB);
                var point = vA + (vB - vA) * t;
                pendingSplits.Add((originalEdges[i], point));
            }
            foreach (var (edge, point) in pendingSplits)
            {
                var (newVertex, _, _) = EdgeSplitter.SplitEdgeAt(solid, edge, point);
                chordVerts.Add(newVertex);
            }

            if (chordVerts.Count != 2)
                throw new NotSupportedException(
                    $"Face düzlem tarafından {chordVerts.Count} noktada kesiliyor (2 bekleniyordu) — " +
                    "dışbükey olmayan/çoklu-kesim durumu kapsam dışı.");

            var (faceA, faceB, chord) = FaceSplitter.SplitAtChord(solid, face, chordVerts[0], chordVerts[1]);

            bool aPositive = IsOnPositiveSide(faceA, SignedDist);
            var kept = aPositive ? faceA : faceB;
            var discarded = aPositive ? faceB : faceA;
            _ = kept; // zaten solid.Faces içinde (FaceSplitter ekledi) — sadece discarded çıkarılacak

            solid.Faces.Remove(discarded);
            chordEdges.Add(chord);
        }

        foreach (var f in facesToRemove)
            solid.Faces.Remove(f);

        var capFace = BuildCapFace(chordEdges, -n);
        solid.Faces.Add(capFace);
        return capFace;
    }

    private static bool IsOnPositiveSide(Face face, Func<Vector3D, double> signedDist)
    {
        foreach (var v in face.GetOuterLoop()!.GetOrderedVertices())
        {
            double d = signedDist(v.Position);
            if (d > Tolerance) return true;
            if (d < -Tolerance) return false;
        }
        throw new InvalidOperationException("Alt-yüz sınıflandırılamadı — tüm köşeler düzlem üzerinde (dejenere).");
    }

    /*
       NE: Kesim kirişlerinden (chord) tek bir kapalı döngü kurup yeni bir kapak Face'i
           oluşturur — BRepBuilder.AttachFace ile AYNI winged-edge desenini (Left/Right +
           Next/Prev zinciri), ama ÖNCEDEN VAR OLAN kenarlar üzerinde (her chord zaten bir
           tarafında kept-Face'e sahip — sadece BOŞ kalan taraf kapağa atanıyor).
    */
    private static Face BuildCapFace(List<TopologyEdge> chordEdges, Vector3D capNormal)
    {
        if (chordEdges.Count < 3)
            throw new NotSupportedException($"Kesim kirişi sayısı {chordEdges.Count} (en az 3 bekleniyordu) — kapak yüz kurulamadı.");

        var orderedVerts = ChainIntoLoop(chordEdges);

        var capFace = new Face { Normal = capNormal };
        var loop = new Loop(isOuter: true);
        int m = orderedVerts.Count;

        var directed = new List<(TopologyEdge Edge, bool Forward)>(m);
        for (int i = 0; i < m; i++)
        {
            var vA = orderedVerts[i];
            var vB = orderedVerts[(i + 1) % m];
            var edge = chordEdges.First(e =>
                (e.StartVertex.Id == vA.Id && e.EndVertex.Id == vB.Id) ||
                (e.StartVertex.Id == vB.Id && e.EndVertex.Id == vA.Id));
            directed.Add((edge, edge.StartVertex.Id == vA.Id));
        }

        for (int i = 0; i < m; i++)
        {
            var (edge, forward) = directed[i];
            loop.Edges.Add(edge);

            if (forward) edge.LeftFace = capFace; else edge.RightFace = capFace;

            var (nextEdge, _) = directed[(i + 1) % m];
            var (prevEdge, _) = directed[(i - 1 + m) % m];
            if (forward) { edge.NextLeftEdge = nextEdge; edge.PrevLeftEdge = prevEdge; }
            else { edge.NextRightEdge = nextEdge; edge.PrevRightEdge = prevEdge; }
        }

        capFace.Loops.Add(loop);
        return capFace;
    }

    private static List<Vertex> ChainIntoLoop(List<TopologyEdge> edges)
    {
        var remaining = new List<TopologyEdge>(edges);
        var first = remaining[0];
        remaining.RemoveAt(0);

        var ordered = new List<Vertex> { first.StartVertex, first.EndVertex };
        while (remaining.Count > 0)
        {
            var last = ordered[^1];
            int idx = remaining.FindIndex(e => e.StartVertex.Id == last.Id || e.EndVertex.Id == last.Id);
            if (idx < 0)
                throw new NotSupportedException("Kesim kirişleri tek bir kapalı döngü oluşturmuyor — dejenere kesit.");

            var e = remaining[idx];
            remaining.RemoveAt(idx);
            ordered.Add(e.StartVertex.Id == last.Id ? e.EndVertex : e.StartVertex);
        }

        if (ordered[^1].Id != ordered[0].Id)
            throw new NotSupportedException("Kesim kirişleri kapanmıyor — dejenere kesit.");

        ordered.RemoveAt(ordered.Count - 1); // kapanış tekrarını at
        return ordered;
    }
}
