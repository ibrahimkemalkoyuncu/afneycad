using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: FaceSplitter Testleri — CSG Boolean Faz 2, Adım B
   NEDEN: Bir Face'i iki alt-Face'e bölmek de SAF TOPOLOJİK bir işlemdir — toplam hacim
          DEĞİŞMEMELİ, ama Euler sayıları tutarlı şekilde değişmeli: bir kenar bölme
          (EdgeSplitter) ΔV=+1,ΔE=+1,ΔF=0 üretir; bir yüz bölme (FaceSplitter, yeni bir kiriş
          kenarı ekler) ΔV=0,ΔE=+1,ΔF=+1 üretir. İkisi birlikte (bu testteki senaryo: iki kenar
          bölme + bir yüz bölme) ΔV=+2,ΔE=+3,ΔF=+1 → Δ(V-E+F)=2-3+1=0, yani Solid.IsValid()
          (Euler formülü) HÂLÂ sağlanmalı.
*/
public class FaceSplitterTests
{
    [Fact]
    public void SplitAtChord_BottomFaceOfBox_SplitsIntoTwoRectangles_PreservesEulerAndVolume()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        Assert.True(box.IsValid());

        int vBefore = box.GetVertices().Count();
        int eBefore = box.GetEdges().Count();
        int fBefore = box.Faces.Count;
        double volumeBefore = box.GetVolume();

        var bottomFace = box.Faces.First(f => f.Normal.Z < -0.9);

        var frontEdge = box.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Y) < 1e-6 && Math.Abs(e.EndVertex.Position.Y) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6);
        var backEdge = box.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Y - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.Y - 2000) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6);

        var (frontMid, _, _) = EdgeSplitter.SplitEdgeAt(box, frontEdge, new Vector3D(1000, 0, 0));
        var (backMid, _, _) = EdgeSplitter.SplitEdgeAt(box, backEdge, new Vector3D(1000, 2000, 0));

        var (faceA, faceB, chord) = FaceSplitter.SplitAtChord(box, bottomFace, frontMid, backMid);

        Assert.Equal(vBefore + 2, box.GetVertices().Count());
        Assert.Equal(eBefore + 3, box.GetEdges().Count());
        Assert.Equal(fBefore + 1, box.Faces.Count); // 1 yüz → 2 yüz = net +1
        Assert.True(box.IsValid(), "Yüz bölme sonrası Solid hâlâ Euler-geçerli olmalı.");
        Assert.Equal(volumeBefore, box.GetVolume(), precision: 3); // Geometri DEĞİŞMEMELİ

        // Her alt-yüz 1000x2000mm = 2.000.000 mm² olmalı (4.000.000mm²'lik tabanın yarısı).
        Assert.Equal(2_000_000.0, faceA.GetArea(), precision: 1);
        Assert.Equal(2_000_000.0, faceB.GetArea(), precision: 1);

        Assert.DoesNotContain(bottomFace, box.Faces); // eski yüz artık Solid'de yok
        Assert.Contains(chord, box.GetEdges());
    }

    [Fact]
    public void SplitAtChord_VerticesNotOnFaceBoundary_ThrowsArgumentException()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var bottomFace = box.Faces.First(f => f.Normal.Z < -0.9);
        var topFace = box.Faces.First(f => f.Normal.Z > 0.9);

        var vFromTop = topFace.GetOuterLoop()!.GetOrderedVertices()[0];
        var vFromBottom = bottomFace.GetOuterLoop()!.GetOrderedVertices()[0];

        // vFromTop, bottomFace'in sınırında DEĞİL.
        Assert.Throws<ArgumentException>(() => FaceSplitter.SplitAtChord(box, bottomFace, vFromTop, vFromBottom));
    }
}
