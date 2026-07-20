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
}
