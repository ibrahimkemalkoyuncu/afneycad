using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: CoplanarFaceDetector Testleri — CSG Boolean, 2. yapı taşı
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — `VertexWelder`'dan (1. yapı taşı) sonraki adım.
          Bu testler, iki bağımsız kutunun paylaştığı ortak yüzün (ZIT normal yönlü olsa
          bile) coplanar sayıldığını, sadece paralel (farklı ofset) veya tamamen farklı
          yönelimli yüzlerin coplanar SAYILMADIĞINI kilitliyor.
*/
public class CoplanarFaceDetectorTests
{
    private static Face FindFaceWithAllX(Solid solid, double x, double tol = 1e-6) =>
        solid.Faces.First(f => f.GetOuterLoop()!.GetOrderedVertices().All(v => Math.Abs(v.Position.X - x) < tol));

    [Fact]
    public void AreCoplanar_SharedFaceOfTwoAdjacentBoxes_ReturnsTrue_DespiteOppositeNormals()
    {
        // Kutu A: [0,1000]³ — X=1000 yüzü (dışa dönük normal +X).
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        // Kutu B: [1000,2000]x[0,1000]x[0,1000] — X=1000 yüzü (dışa dönük normal -X, A'nınkinin TAM TERSİ).
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        var faceA = FindFaceWithAllX(boxA, 1000);
        var faceB = FindFaceWithAllX(boxB, 1000);

        Assert.True(CoplanarFaceDetector.AreCoplanar(faceA, faceB),
            "İki bitişik kutunun paylaştığı ortak yüz, normaller zıt yönlü olsa bile coplanar sayılmalı.");
    }

    [Fact]
    public void AreCoplanar_ParallelButDifferentOffset_ReturnsFalse()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var faceX0 = FindFaceWithAllX(box, 0);     // sol yüz
        var faceX1000 = FindFaceWithAllX(box, 1000); // sağ yüz — PARALEL ama 1000mm ötede

        Assert.False(CoplanarFaceDetector.AreCoplanar(faceX0, faceX1000),
            "Aynı kutunun karşılıklı iki yüzü paralel ama coplanar DEĞİL (farklı ofset).");
    }

    [Fact]
    public void AreCoplanar_DifferentOrientation_ReturnsFalse()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var faceX = FindFaceWithAllX(box, 1000);
        var faceY = box.Faces.First(f => f.GetOuterLoop()!.GetOrderedVertices().All(v => Math.Abs(v.Position.Y - 1000) < 1e-6));

        Assert.False(CoplanarFaceDetector.AreCoplanar(faceX, faceY),
            "Farklı yönelimli (X-normal vs Y-normal) yüzler paralel bile değil, coplanar olamaz.");
    }
}
