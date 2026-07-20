using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: SolidClassifier Testleri — CSG Boolean Faz 3
   NEDEN: Möller–Trumbore ışın-üçgen kesişim sayımına dayalı nokta-içi-katı testinin gerçekten
          doğru çalıştığını (içeride/dışarıda/tam sınırda yakın noktalar) kanıtlıyor.
*/
public class SolidClassifierTests
{
    private static Solid MakeBox() => BRepBuilder.ExtrudeBox(
        new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

    [Fact]
    public void IsPointInside_CenterOfBox_ReturnsTrue()
    {
        var box = MakeBox();
        Assert.True(SolidClassifier.IsPointInside(box, new Vector3D(1000, 1000, 1000)));
    }

    [Fact]
    public void IsPointInside_FarOutsideBox_ReturnsFalse()
    {
        var box = MakeBox();
        Assert.False(SolidClassifier.IsPointInside(box, new Vector3D(50000, 50000, 50000)));
    }

    [Fact]
    public void IsPointInside_JustInsideFace_ReturnsTrue_JustOutside_ReturnsFalse()
    {
        var box = MakeBox();

        Assert.True(SolidClassifier.IsPointInside(box, new Vector3D(1999, 1000, 1000)));
        Assert.False(SolidClassifier.IsPointInside(box, new Vector3D(2001, 1000, 1000)));
    }

    [Fact]
    public void IsPointInside_OverlappingBoxScenario_MatchesExpectedRegion()
    {
        // Box A=[0,2000]^3, Box B=[1000,3000]^3 — kesişim bölgesi [1000,2000]^3.
        var boxA = MakeBox();
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(1000, 1000, 1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var pointInBoth = new Vector3D(1500, 1500, 1500);   // kesişim bölgesinde
        var pointOnlyInA = new Vector3D(500, 500, 500);     // sadece A'da
        var pointOnlyInB = new Vector3D(2500, 2500, 2500);  // sadece B'de

        Assert.True(SolidClassifier.IsPointInside(boxA, pointInBoth));
        Assert.True(SolidClassifier.IsPointInside(boxB, pointInBoth));

        Assert.True(SolidClassifier.IsPointInside(boxA, pointOnlyInA));
        Assert.False(SolidClassifier.IsPointInside(boxB, pointOnlyInA));

        Assert.False(SolidClassifier.IsPointInside(boxA, pointOnlyInB));
        Assert.True(SolidClassifier.IsPointInside(boxB, pointOnlyInB));
    }
}
