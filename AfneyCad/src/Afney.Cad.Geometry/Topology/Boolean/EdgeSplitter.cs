using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Kenar Bölme (EdgeSplitter) — CSG Boolean Faz 2, Adım A
   NEDEN: Gerçek topolojik B-Rep boolean'ın "yüz bölme" adımının temel taşı: bir kesişim
          noktası, bir Face'in var olan bir kenarının ORTASINA denk geliyorsa, o kenar iki
          parçaya bölünmeli — YENİ vertex, İKİ yeni TopologyEdge, ve bu kenarı paylaşan HER
          İKİ komşu Face'in (LeftFace + RightFace) Loop'ları da güncellenmeli (winged-edge
          tutarlılığı — sadece bir Face'i güncelleyip diğerini unutmak geçersiz/dejenere bir
          Solid üretir).

   DOĞRULUK KANITI: Bir kenarı bölmek SAF TOPOLOJİK bir işlemdir — geometriyi (hacim, yüzey
   alanı) DEĞİŞTİRMEMELİ. Testler bunu Euler formülü (V→V+1, E→E+1, F sabit) ve hacim
   değişmezliğiyle kanıtlıyor.
*/
public static class EdgeSplitter
{
    /*
       NE: Solid içindeki bir TopologyEdge'i, üzerindeki bir noktadan ikiye böler.
       KISIT: `point`, `edge.StartVertex` ile `edge.EndVertex` arasındaki doğru üzerinde
              olmalı (uç noktalardan biriyle çakışıyorsa bölme gereksiz — çağıran taraf bunu
              önceden kontrol etmeli, bu metod dejenere girdide istisna fırlatır).
       ÇIKTI: Yeni Vertex + (StartVertex→newVertex) ve (newVertex→EndVertex) yeni kenarları.
    */
    public static (Vertex NewVertex, TopologyEdge EdgeA, TopologyEdge EdgeB) SplitEdgeAt(Solid solid, TopologyEdge edge, Vector3D point)
    {
        double distToStart = point.DistanceTo(edge.StartVertex.Position);
        double distToEnd = point.DistanceTo(edge.EndVertex.Position);
        double edgeLength = edge.StartVertex.Position.DistanceTo(edge.EndVertex.Position);
        if (distToStart < 1e-6 || distToEnd < 1e-6)
            throw new ArgumentException("Bölme noktası kenarın uç noktalarından biriyle çakışıyor — bölmeye gerek yok.");
        if (Math.Abs(distToStart + distToEnd - edgeLength) > 1e-3)
            throw new ArgumentException("Bölme noktası kenarın üzerinde değil (dejenere durum — Faz 1 kapsam sınırı).");

        var newVertex = new Vertex(point);
        var edgeA = new TopologyEdge(edge.StartVertex, newVertex);
        var edgeB = new TopologyEdge(newVertex, edge.EndVertex);

        ReplaceEdgeInFaceLoop(edge.LeftFace, edge, edgeA, edgeB, forward: true);
        ReplaceEdgeInFaceLoop(edge.RightFace, edge, edgeA, edgeB, forward: false);

        edgeA.LeftFace = edge.LeftFace;
        edgeA.RightFace = edge.RightFace;
        edgeB.LeftFace = edge.LeftFace;
        edgeB.RightFace = edge.RightFace;

        // BİLİNEN SINIR (Faz 2, Adım A): NextLeftEdge/PrevLeftEdge/NextRightEdge/PrevRightEdge
        // işaretçileri burada GÜNCELLENMİYOR. Solid.IsValid()/GetOrderedVertices() bu
        // işaretçilere değil, paylaşılan Vertex zincirlemesine dayandığı için (bkz. Face.cs
        // Loop.GetOrderedVertices) doğruluk testleri (Euler, hacim) etkilenmiyor — ama bu
        // işaretçilere dayanan gelecekteki bir tüketici (ör. doğrudan Next/Prev gezinme)
        // için ayrıca bir geçiş (pass) gerekecek.

        return (newVertex, edgeA, edgeB);
    }

    /*
       NE: Bir Face'in Loop'undaki TEK bir eski kenar referansını, sırayı koruyarak İKİ yeni
           kenarla değiştirir.
       NEDEN forward: `edge.LeftFace` bu kenarı Start→End yönünde gezer (ileri), `RightFace`
              ise End→Start yönünde (ters) — bkz. BRepBuilder.AttachFace deseni. Bu yüzden
              RightFace'in Loop'unda yeni kenarların sırası TERS olmalı (edgeB önce, edgeA sonra).
    */
    private static void ReplaceEdgeInFaceLoop(Face? face, TopologyEdge oldEdge, TopologyEdge edgeA, TopologyEdge edgeB, bool forward)
    {
        if (face == null) return;
        var loop = face.Loops.FirstOrDefault(l => l.Edges.Contains(oldEdge));
        if (loop == null) return;

        int idx = loop.Edges.IndexOf(oldEdge);
        loop.Edges.RemoveAt(idx);
        if (forward)
        {
            loop.Edges.Insert(idx, edgeA);
            loop.Edges.Insert(idx + 1, edgeB);
        }
        else
        {
            loop.Edges.Insert(idx, edgeB);
            loop.Edges.Insert(idx + 1, edgeA);
        }
    }
}
