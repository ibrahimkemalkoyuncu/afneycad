using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: EdgeSplitter Testleri — CSG Boolean Faz 2, Adım A
   NEDEN: Kenar bölme SAF TOPOLOJİK bir işlemdir — geometri (hacim) değişmemeli, ama Euler
          sayıları (V,E) tam olarak +1 artmalı, F sabit kalmalı. Bu testler bunu kanıtlıyor.
*/
public class EdgeSplitterTests
{
    [Fact]
    public void SplitEdgeAt_MidpointOfBoxEdge_IncreasesVertexAndEdgeCountByOne_PreservesEulerAndVolume()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        Assert.True(box.IsValid());

        int vBefore = box.GetVertices().Count();
        int eBefore = box.GetEdges().Count();
        int fBefore = box.Faces.Count;
        double volumeBefore = box.GetVolume();

        // Alt kapaktaki bir kenarı seç (bottom[0]->bottom[1], x ekseni boyunca, z=0).
        var edgeToSplit = box.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Y) < 1e-6 && Math.Abs(e.EndVertex.Position.Y) < 1e-6);

        var midpoint = new Vector3D(
            (edgeToSplit.StartVertex.Position.X + edgeToSplit.EndVertex.Position.X) / 2,
            (edgeToSplit.StartVertex.Position.Y + edgeToSplit.EndVertex.Position.Y) / 2,
            (edgeToSplit.StartVertex.Position.Z + edgeToSplit.EndVertex.Position.Z) / 2);

        var (newVertex, edgeA, edgeB) = EdgeSplitter.SplitEdgeAt(box, edgeToSplit, midpoint);

        Assert.Equal(vBefore + 1, box.GetVertices().Count());
        Assert.Equal(eBefore + 1, box.GetEdges().Count());
        Assert.Equal(fBefore, box.Faces.Count); // Face sayısı DEĞİŞMEMELİ (sadece kenar bölündü)
        Assert.True(box.IsValid(), "Kenar bölme sonrası Solid hâlâ Euler-geçerli olmalı.");
        Assert.Equal(volumeBefore, box.GetVolume(), precision: 3); // Geometri DEĞİŞMEMELİ

        Assert.Equal(midpoint, newVertex.Position);
        Assert.DoesNotContain(edgeToSplit, box.GetEdges()); // eski kenar artık hiçbir loop'ta yok
        Assert.Contains(edgeA, box.GetEdges());
        Assert.Contains(edgeB, box.GetEdges());
    }

    [Fact]
    public void SplitEdgeAt_PointNotOnEdge_ThrowsArgumentException()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var edge = box.GetEdges().First();

        var offAxisPoint = new Vector3D(99999, 99999, 99999);

        Assert.Throws<ArgumentException>(() => EdgeSplitter.SplitEdgeAt(box, edge, offAxisPoint));
    }

    [Fact]
    public void SplitEdgeAt_PointAtEndpoint_ThrowsArgumentException()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var edge = box.GetEdges().First();

        Assert.Throws<ArgumentException>(() => EdgeSplitter.SplitEdgeAt(box, edge, edge.StartVertex.Position));
    }
}
