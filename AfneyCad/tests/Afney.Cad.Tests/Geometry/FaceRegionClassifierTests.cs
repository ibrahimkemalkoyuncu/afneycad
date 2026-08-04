using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: FaceRegionClassifier Testleri — `GeneralSolidSubtractor`'ın mirror-cap iç/dış ayrımı
       için kullandığı yapı taşının izole doğrulaması.
   NEDEN GERÇEKÇİ SENARYO (komşu iki kutu): `GeneralSolidSubtractor`'daki gerçek kullanım,
       bir Face'in KENDİ sahibi Solid'in (D_i, mirror cap) dışına bakan OUTWARD normaliyle
       KOMŞU bir Solid'e (A∩B) bitişik olup olmadığını test etmek — bu yüzden burada da
       aynı ilişki (bir Solid'in dışa dönük yüzü, KOMŞU başka bir Solid'e bitişik mi)
       kuruluyor, Face'in KENDİ sahibi Solid'ine bitişikliği DEĞİL.
*/
public class FaceRegionClassifierTests
{
    [Fact]
    public void IsFaceAdjacentToRegion_FaceTouchingNeighborSolid_ReturnsTrue()
    {
        // box1=[0,1000]^3, box2=[1000,2000]x[0,1000]x[0,1000] — X=1000 düzleminde bitişik.
        // box1'in X=1000 yüzünün outward normali (+X) box2'nin İÇİNE bakar.
        var box1 = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var box2 = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var face = box1.Faces.First(f => f.Normal.X > 0.9);

        Assert.True(FaceRegionClassifier.IsFaceAdjacentToRegion(face, box2));
    }

    [Fact]
    public void IsFaceAdjacentToRegion_FaceFarFromRegion_ReturnsFalse()
    {
        var box1 = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var farAway = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var face = box1.Faces.First(f => f.Normal.X > 0.9);

        Assert.False(FaceRegionClassifier.IsFaceAdjacentToRegion(face, farAway));
    }

    [Fact]
    public void IsFaceAdjacentToRegion_OppositeFace_DoesNotFalselyMatchNeighbor()
    {
        // box1'in X=0 yüzü (outward normal -X) box2'ye (X=1000..2000) bitişik DEĞİL —
        // yanlış-pozitif riskini (yanlış yüzün seçilmesi) kilitliyor.
        var box1 = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var box2 = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var oppositeFace = box1.Faces.First(f => f.Normal.X < -0.9);

        Assert.False(FaceRegionClassifier.IsFaceAdjacentToRegion(oppositeFace, box2));
    }
}
