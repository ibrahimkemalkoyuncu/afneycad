using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Yüz Bölme (FaceSplitter) — CSG Boolean Faz 2, Adım B
   NEDEN: EdgeSplitter (Adım A) ile bir Face'in sınırına iki yeni Vertex eklendikten sonra,
          bu iki Vertex'i birleştiren bir "kiriş" (chord) ile Face'i İKİ yeni alt-Face'e
          ayırmak. Bu, kesişim segmentinin Face'in sınırına DEĞMEDİĞİ (uçları zaten sınırda
          olan) durum için CSG boolean'ın "yüz bölme" adımının ikinci ve son parçasıdır.

   KISIT: `v1` ve `v2`, verilen Face'in DIŞ Loop'unun sınırında (GetOrderedVertices() içinde)
          bulunmalı — genelde EdgeSplitter.SplitEdgeAt'in ürettiği yeni Vertex'ler. Delikli
          (inner loop) Face'ler ve v1==v2 gibi dejenere girdiler kapsam dışı (istisna fırlatılır).
*/
public static class FaceSplitter
{
    public static (Face FaceA, Face FaceB, TopologyEdge Chord) SplitAtChord(Solid solid, Face face, Vertex v1, Vertex v2)
    {
        var loop = face.GetOuterLoop() ?? throw new ArgumentException("Face'in dış Loop'u yok.");
        var orderedVertices = loop.GetOrderedVertices();
        var edges = loop.Edges;

        if (orderedVertices.Count != edges.Count)
            throw new InvalidOperationException("Loop tutarsız — vertex/kenar sayısı eşleşmiyor.");

        int i = orderedVertices.FindIndex(v => v.Id == v1.Id);
        int j = orderedVertices.FindIndex(v => v.Id == v2.Id);
        if (i < 0 || j < 0) throw new ArgumentException("v1/v2, Face'in sınırında bulunamadı.");
        if (i == j) throw new ArgumentException("v1 ve v2 aynı köşe olamaz.");

        int n = edges.Count;
        var subEdgesA = new List<TopologyEdge>(); // v1 -> v2 (i..j-1)
        for (int k = i; k != j; k = (k + 1) % n) subEdgesA.Add(edges[k]);

        var subEdgesB = new List<TopologyEdge>(); // v2 -> v1 (j..i-1)
        for (int k = j; k != i; k = (k + 1) % n) subEdgesB.Add(edges[k]);

        var chord = new TopologyEdge(v1, v2);

        var faceA = new Face { Normal = face.Normal }; // subEdgesA + chord(ters, v2->v1)
        var faceB = new Face { Normal = face.Normal }; // subEdgesB + chord(ileri, v1->v2)

        RepointBorrowedEdges(subEdgesA, face, faceA);
        RepointBorrowedEdges(subEdgesB, face, faceB);

        chord.RightFace = faceA; // subLoopA, chord'u v2->v1 (ters) gezer
        chord.LeftFace = faceB;  // subLoopB, chord'u v1->v2 (ileri) gezer

        var loopA = new Loop(isOuter: true);
        loopA.Edges.AddRange(subEdgesA);
        loopA.Edges.Add(chord);
        faceA.Loops.Add(loopA);

        var loopB = new Loop(isOuter: true);
        loopB.Edges.AddRange(subEdgesB);
        loopB.Edges.Add(chord);
        faceB.Loops.Add(loopB);

        solid.Faces.Remove(face);
        solid.Faces.Add(faceA);
        solid.Faces.Add(faceB);

        // BİLİNEN SINIR: Next/Prev işaretçileri güncellenmiyor — bkz. EdgeSplitter notu.
        return (faceA, faceB, chord);
    }

    private static void RepointBorrowedEdges(List<TopologyEdge> borrowedEdges, Face oldFace, Face newFace)
    {
        foreach (var edge in borrowedEdges)
        {
            if (ReferenceEquals(edge.LeftFace, oldFace)) edge.LeftFace = newFace;
            else if (ReferenceEquals(edge.RightFace, oldFace)) edge.RightFace = newFace;
        }
    }

    /*
       NE: Yüz Bölme — POLYLINE Kiriş Genellemesi (CSG Boolean, `docs/Roadmap_CSG_Boolean.md`
           2026-08-14 girdisinin "somut, net gereksinim" bölümünün 1. maddesi).
       NEDEN: `SplitAtChord` yalnızca İKİ ucu da Face'in kendi sınır Loop'unda olan TEK bir düz
              kiriş destekliyor. `FaceIntersection.Intersect` iki Face arasındaki kesişimi
              GERÇEK poligon sınırına kırpılmış segmentler olarak verdiğinde, bu segmentler
              uç-noktalarına göre zincirlendiğinde ortaya çıkan kesişim "kirişi" genelde TEK
              düz segment DEĞİL, bir POLYLINE'dır — polyline'ın İLK ve SON noktası Face'in
              kendi sınırına değer, ama ARA noktaları Face'in İÇİNDEDİR (somut örnek: roadmap'in
              3-düzlemli köşe senaryosu, A'nın X=2000 yüzünde
              (2000,1500,2000)→(2000,1500,1500)→(2000,2000,1500) polyline'ı — orta nokta A'nın
              Face'inin içinde, sınırında değil).

       API NOTU: Görev tanımı `SplitAtPolylineChord(Face, List<Vector3D>)` öneriyordu, ama
       `SplitAtChord` ile AYNI desende (solid.Faces'i güncellemek ZORUNDA — eski Face'i
       kaldırıp iki yeni Face eklemek) `Solid` parametresi olmadan bu metod solid'i tutarsız
       bırakırdı. Bu yüzden imza `SplitAtChord`'unkiyle TUTARLI olacak şekilde `Solid` de
       alıyor — `Vector3D` noktaları (roadmap'in `List<Vertex>` taslağı yerine) tercih edildi
       çünkü ARA noktalar HENÜZ Vertex değil (çağıran taraf için daha basit: ham 3D nokta
       listesi yeterli, Vertex nesnelerini bu metod kendi üretir).

       KISIT: `polylinePoints[0]` ve `polylinePoints[^1]`, verilen Face'in DIŞ Loop'unun
              sınırındaki bir Vertex'in Pozisyonuyla (tolerans içinde) ÇAKIŞMALI — o gerçek
              Vertex nesnesi yeniden kullanılır (SplitAtChord'daki v1/v2 gibi). ARA noktalar
              (varsa) Face'in İÇİNDE YENİ Vertex'ler olarak eklenir. `polylinePoints.Count==2`
              durumunda (tek segment) bu metodun ürettiği sonuç `SplitAtChord`'un ürettiğiyle
              AYNI desendedir (bkz. `FaceSplitterPolylineChordTests.
              SplitAtPolylineChord_TwoPointPolyline_MatchesSplitAtChord`).
    */
    public static (Face FaceA, Face FaceB, List<TopologyEdge> ChordEdges) SplitAtPolylineChord(
        Solid solid, Face face, List<Vector3D> polylinePoints)
    {
        if (polylinePoints == null || polylinePoints.Count < 2)
            throw new ArgumentException("Polyline en az 2 nokta (başlangıç + bitiş) içermeli.");

        for (int k = 0; k < polylinePoints.Count - 1; k++)
        {
            if (polylinePoints[k].DistanceTo(polylinePoints[k + 1]) < 1e-6)
                throw new ArgumentException($"Polyline'da ardışık iki nokta çakışıyor (index {k}/{k + 1}) — dejenere sıfır-uzunluklu segment.");
        }

        var loop = face.GetOuterLoop() ?? throw new ArgumentException("Face'in dış Loop'u yok.");
        var orderedVertices = loop.GetOrderedVertices();
        var edges = loop.Edges;

        if (orderedVertices.Count != edges.Count)
            throw new InvalidOperationException("Loop tutarsız — vertex/kenar sayısı eşleşmiyor.");

        var v1 = FindBoundaryVertexAt(orderedVertices, polylinePoints[0])
            ?? throw new ArgumentException("Polyline'ın ilk noktası Face'in sınırında bulunamadı.");
        var vN = FindBoundaryVertexAt(orderedVertices, polylinePoints[^1])
            ?? throw new ArgumentException("Polyline'ın son noktası Face'in sınırında bulunamadı.");
        if (v1.Id == vN.Id)
            throw new ArgumentException("Polyline'ın ilk ve son noktası aynı köşe olamaz.");

        EnsureNoSelfIntersection(face, polylinePoints);

        int i = orderedVertices.FindIndex(v => v.Id == v1.Id);
        int j = orderedVertices.FindIndex(v => v.Id == vN.Id);

        int n = edges.Count;
        var subEdgesA = new List<TopologyEdge>(); // v1 -> vN (i..j-1), Face sınırı boyunca
        for (int k = i; k != j; k = (k + 1) % n) subEdgesA.Add(edges[k]);

        var subEdgesB = new List<TopologyEdge>(); // vN -> v1 (j..i-1), Face sınırı boyunca
        for (int k = j; k != i; k = (k + 1) % n) subEdgesB.Add(edges[k]);

        // Ara noktalar için YENİ Vertex'ler (Face'in İÇİNDE) + zincirlenmiş TopologyEdge'ler.
        var chainVertices = new List<Vertex> { v1 };
        for (int k = 1; k < polylinePoints.Count - 1; k++)
            chainVertices.Add(new Vertex(polylinePoints[k]));
        chainVertices.Add(vN);

        var chordEdges = new List<TopologyEdge>();
        for (int k = 0; k < chainVertices.Count - 1; k++)
            chordEdges.Add(new TopologyEdge(chainVertices[k], chainVertices[k + 1]));

        var faceA = new Face { Normal = face.Normal }; // subEdgesA + chordEdges(ters, vN->v1)
        var faceB = new Face { Normal = face.Normal }; // subEdgesB + chordEdges(ileri, v1->vN)

        RepointBorrowedEdges(subEdgesA, face, faceA);
        RepointBorrowedEdges(subEdgesB, face, faceB);

        foreach (var chordEdge in chordEdges)
        {
            chordEdge.RightFace = faceA; // subLoopA, zinciri vN->v1 (ters) gezer
            chordEdge.LeftFace = faceB;  // subLoopB, zinciri v1->vN (ileri) gezer
        }

        var loopA = new Loop(isOuter: true);
        loopA.Edges.AddRange(subEdgesA);
        for (int k = chordEdges.Count - 1; k >= 0; k--) loopA.Edges.Add(chordEdges[k]);
        faceA.Loops.Add(loopA);

        var loopB = new Loop(isOuter: true);
        loopB.Edges.AddRange(subEdgesB);
        loopB.Edges.AddRange(chordEdges);
        faceB.Loops.Add(loopB);

        solid.Faces.Remove(face);
        solid.Faces.Add(faceA);
        solid.Faces.Add(faceB);

        // BİLİNEN SINIR: Next/Prev işaretçileri güncellenmiyor — bkz. EdgeSplitter/SplitAtChord notu.
        return (faceA, faceB, chordEdges);
    }

    private static Vertex? FindBoundaryVertexAt(List<Vertex> orderedVertices, Vector3D position)
    {
        return orderedVertices.FirstOrDefault(v => v.Position.DistanceTo(position) < 1e-6);
    }

    /*
       NE: Polyline'ın KENDİ KENDİNİ kesip kesmediğini kontrol eder (dejenere durum — sessizce
           yanlış geometri üretmek yerine açık hata).
       NASIL: Face düzlemsel olduğundan (Normal alanı var), noktalar Face'in düzlemine ait 2D
              yerel bir bazda (u,v) ifade edilip ARDIŞIK OLMAYAN segment çiftleri klasik 2D
              segment-segment kesişim testiyle (yönelim/orientation testi) kontrol edilir.
              Komşu segmentler zaten bir uç noktayı PAYLAŞMASI GEREKTİĞİNDEN (zincirin doğası)
              bu kontrolün dışında tutulur.
    */
    private static void EnsureNoSelfIntersection(Face face, List<Vector3D> polylinePoints)
    {
        var normal = face.Normal.Normalize();
        if (normal.LengthSquared() < 1e-12) return; // Normal tanımsızsa kontrol atlanır.

        // Düzlem içinde iki ortogonal eksen (u,v) seç.
        var arbitrary = Math.Abs(normal.X) < 0.9 ? Vector3D.XAxis : Vector3D.YAxis;
        var u = normal.Cross(arbitrary).Normalize();
        var v = normal.Cross(u).Normalize();

        var pts2D = polylinePoints.Select(p => (X: p.Dot(u), Y: p.Dot(v))).ToList();

        int segCount = pts2D.Count - 1;
        for (int a = 0; a < segCount; a++)
        {
            for (int b = a + 1; b < segCount; b++)
            {
                bool adjacent = b == a + 1;
                if (adjacent) continue; // Komşu segmentler ortak uç noktayı paylaşır — normaldir.

                if (SegmentsIntersect(pts2D[a], pts2D[a + 1], pts2D[b], pts2D[b + 1]))
                    throw new ArgumentException($"Polyline kendi kendini kesiyor (segment {a} ile {b}) — dejenere durum.");
            }
        }
    }

    private static double Cross2D((double X, double Y) o, (double X, double Y) p1, (double X, double Y) p2)
        => (p1.X - o.X) * (p2.Y - o.Y) - (p1.Y - o.Y) * (p2.X - o.X);

    private static bool OnSegment((double X, double Y) p, (double X, double Y) q, (double X, double Y) r)
        => Math.Min(p.X, r.X) - 1e-9 <= q.X && q.X <= Math.Max(p.X, r.X) + 1e-9 &&
           Math.Min(p.Y, r.Y) - 1e-9 <= q.Y && q.Y <= Math.Max(p.Y, r.Y) + 1e-9;

    private static bool SegmentsIntersect((double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, (double X, double Y) p4)
    {
        double d1 = Cross2D(p3, p4, p1);
        double d2 = Cross2D(p3, p4, p2);
        double d3 = Cross2D(p1, p2, p3);
        double d4 = Cross2D(p1, p2, p4);

        const double eps = 1e-9;

        if (((d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps)) &&
            ((d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps)))
            return true;

        if (Math.Abs(d1) <= eps && OnSegment(p3, p1, p4)) return true;
        if (Math.Abs(d2) <= eps && OnSegment(p3, p2, p4)) return true;
        if (Math.Abs(d3) <= eps && OnSegment(p1, p3, p2)) return true;
        if (Math.Abs(d4) <= eps && OnSegment(p1, p4, p2)) return true;

        return false;
    }
}
