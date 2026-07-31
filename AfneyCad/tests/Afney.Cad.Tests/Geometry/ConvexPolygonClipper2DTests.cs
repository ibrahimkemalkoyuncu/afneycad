using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: ConvexPolygonClipper2D Testleri — CSG Boolean, 3. yapı taşı
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — `CoplanarFaceDetector`'dan (2. yapı taşı) sonraki
          adım. Bu testler, dışbükey iki poligonun kesişiminin (tam çakışma, kısmi örtüşme,
          ayrık, biri diğerini kapsıyor) doğru hesaplandığını ve içbükey girdide sessiz yanlış
          sonuç yerine AÇIK HATA fırlatıldığını kilitliyor.
*/
public class ConvexPolygonClipper2DTests
{
    private static readonly Vector3D Normal = Vector3D.ZAxis;

    private static double PolygonArea(List<Vector3D> poly)
    {
        double area = 0;
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) / 2.0;
    }

    private static List<Vector3D> Rect(double x0, double y0, double x1, double y1) => new()
    {
        new Vector3D(x0, y0, 0), new Vector3D(x1, y0, 0), new Vector3D(x1, y1, 0), new Vector3D(x0, y1, 0)
    };

    [Fact]
    public void Intersect_IdenticalSquares_ReturnsOriginalSquareArea()
    {
        var square = Rect(0, 0, 1000, 1000);
        var result = ConvexPolygonClipper2D.Intersect(square, square, Normal);
        Assert.Equal(1_000_000.0, PolygonArea(result), 3);
    }

    [Fact]
    public void Intersect_PartialOverlap_ReturnsClippedQuadArea()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(5, 5, 15, 15);
        var result = ConvexPolygonClipper2D.Intersect(a, b, Normal);
        // Örtüşen bölge: [5,10]x[5,10] -> 5x5 = 25
        Assert.Equal(25.0, PolygonArea(result), 6);
    }

    [Fact]
    public void Intersect_DisjointRectangles_ReturnsEmpty()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(100, 100, 110, 110);
        var result = ConvexPolygonClipper2D.Intersect(a, b, Normal);
        Assert.Empty(result);
    }

    [Fact]
    public void Intersect_OneFullyContainsOther_ReturnsSmallerPolygonArea()
    {
        var big = Rect(0, 0, 100, 100);
        var small = Rect(20, 20, 40, 40);
        var result = ConvexPolygonClipper2D.Intersect(big, small, Normal);
        Assert.Equal(400.0, PolygonArea(result), 6);
    }

    [Fact]
    public void Intersect_ConcaveInputPolygon_ThrowsInsteadOfSilentWrongResult()
    {
        // L-şekli (içbükey)
        var concave = new List<Vector3D>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 5, 0),
            new(5, 5, 0), new(5, 10, 0), new(0, 10, 0)
        };
        var square = Rect(0, 0, 10, 10);
        Assert.Throws<InvalidOperationException>(() => ConvexPolygonClipper2D.Intersect(concave, square, Normal));
    }
}
