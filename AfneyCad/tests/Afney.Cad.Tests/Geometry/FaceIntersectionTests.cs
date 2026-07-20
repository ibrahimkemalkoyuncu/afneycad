using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: FaceIntersection Testleri — CSG Boolean Faz 1
   NEDEN: docs/Roadmap_CSG_Boolean.md Faz 1'in doğruluk kanıtı. İki kutunun (BRepBuilder.
          ExtrudeBox) kesişen iki dik yüzünün gerçek kesişim segmentini, elle hesaplanmış
          analitik değerle karşılaştırıyor.

   SENARYO: Box A = [0,2000]³ (küp), Box B = [1000,3000]³ (küp) — 1000mm ötelenmiş, üst üste
   binen bölge [1000,2000]³. Box A'nın x=2000 yüzü (normal=+X) ile Box B'nin y=1000 yüzü
   (normal=-Y) kesişiyor: kesişim doğrusu x=2000, y=1000, z serbest. Box A'da z∈[0,2000],
   Box B'de z∈[1000,3000] — kesişim (∩) z∈[1000,2000]. Beklenen segment:
   (2000,1000,1000) → (2000,1000,2000), uzunluk 1000mm.
*/
public class FaceIntersectionTests
{
    [Fact]
    public void Intersect_TwoOverlappingBoxFaces_ProducesCorrectSegment()
    {
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(1000, 1000, 1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var faceA = boxA.Faces.First(f => f.Normal.X > 0.9);   // Box A'nın x=2000 yüzü
        var faceB = boxB.Faces.First(f => f.Normal.Y < -0.9);  // Box B'nin y=1000 yüzü

        var segments = FaceIntersection.Intersect(faceA, faceB);

        Assert.Single(segments);
        var seg = segments[0];

        double lo = Math.Min(seg.Start.Z, seg.End.Z);
        double hi = Math.Max(seg.Start.Z, seg.End.Z);

        Assert.Equal(2000.0, seg.Start.X, precision: 3);
        Assert.Equal(2000.0, seg.End.X, precision: 3);
        Assert.Equal(1000.0, seg.Start.Y, precision: 3);
        Assert.Equal(1000.0, seg.End.Y, precision: 3);
        Assert.Equal(1000.0, lo, precision: 3);
        Assert.Equal(2000.0, hi, precision: 3);
        Assert.Equal(1000.0, hi - lo, precision: 3);
    }

    [Fact]
    public void Intersect_NonOverlappingFaces_ProducesNoSegment()
    {
        // Box A ve çok uzaktaki bir Box C — düzlemleri kesişse bile (sonsuz düzlemler),
        // gerçek poligon sınırları hiç örtüşmüyor.
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var boxC = BRepBuilder.ExtrudeBox(new Vector3D(1000, 1000, 100000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var faceA = boxA.Faces.First(f => f.Normal.X > 0.9);
        var faceC = boxC.Faces.First(f => f.Normal.Y < -0.9);

        var segments = FaceIntersection.Intersect(faceA, faceC);

        Assert.Empty(segments);
    }

    [Fact]
    public void Intersect_ParallelFaces_ProducesNoSegment()
    {
        // Aynı yöndeki paralel yüzler (iki kutunun karşılıklı x-yüzleri) hiç kesişmemeli.
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(3000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var faceA = boxA.Faces.First(f => f.Normal.X > 0.9);   // x=2000
        var faceB = boxB.Faces.First(f => f.Normal.X < -0.9);  // x=3000 (paralel düzlem)

        var segments = FaceIntersection.Intersect(faceA, faceB);

        Assert.Empty(segments);
    }
}
