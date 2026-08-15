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

    /*
       NE: Union testleri — `docs/Roadmap_CSG_Boolean.md`'nin "convex-convex 2D union
           primitifi" (2026-08-15, Session #67) yapı taşı.
    */

    [Fact]
    public void Union_CornerNotchSquares_RoadmapScenario_ReturnsOctagonWithCorrectAreaAndVertices()
    {
        // Roadmap'in kendi senaryosu: A'nın üst yüzü [0,2000]x[0,2000], B'nin üst yüzü
        // [1500,3000]x[1500,3000] — köşeden örtüşen iki kare. Elle hesap (bkz. görev
        // tanımının kendi analizi):
        //   A_alan = 2000*2000 = 4.000.000 ; B_alan = 1500*1500 = 2.250.000
        //   kesişim = [1500,2000]x[1500,2000] = 500*500 = 250.000
        //   UNION_alan = 4.000.000 + 2.250.000 - 250.000 = 6.000.000
        // Sınır: A'nın 3 köşesi (2000,2000 hariç, B'nin içinde kaldığı için), B'nin 3 köşesi
        // (1500,1500 hariç, A'nın içinde kaldığı için) + 2 GEÇİŞ (kesişim) noktası
        // (2000,1500) ve (1500,2000) = TOPLAM 8 köşe (oktogon).
        var a = Rect(0, 0, 2000, 2000);
        var b = Rect(1500, 1500, 3000, 3000);

        var result = ConvexPolygonClipper2D.Union(a, b, Normal);

        Assert.Equal(8, result.Count);
        Assert.Equal(6_000_000.0, PolygonArea(result), 3);

        var expectedVertices = new List<Vector3D>
        {
            new(0, 0, 0), new(2000, 0, 0), new(2000, 1500, 0), new(3000, 1500, 0),
            new(3000, 3000, 0), new(1500, 3000, 0), new(1500, 2000, 0), new(0, 2000, 0)
        };
        foreach (var expected in expectedVertices)
        {
            Assert.Contains(result, p => p.DistanceTo(expected) < 1e-6);
        }
    }

    [Fact]
    public void Union_IdenticalSquares_ReturnsOriginalSquareArea()
    {
        var square = Rect(0, 0, 1000, 1000);
        var result = ConvexPolygonClipper2D.Union(square, square, Normal);
        Assert.Equal(1_000_000.0, PolygonArea(result), 3);
    }

    [Fact]
    public void Union_OneFullyContainsOther_ReturnsBiggerPolygonArea()
    {
        var big = Rect(0, 0, 100, 100);
        var small = Rect(20, 20, 40, 40);

        var resultBigFirst = ConvexPolygonClipper2D.Union(big, small, Normal);
        Assert.Equal(10_000.0, PolygonArea(resultBigFirst), 6);

        var resultSmallFirst = ConvexPolygonClipper2D.Union(small, big, Normal);
        Assert.Equal(10_000.0, PolygonArea(resultSmallFirst), 6);
    }

    [Fact]
    public void Union_PartialOverlap_AreaMatchesInclusionExclusion()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(5, 5, 15, 15);
        // A_alan=100, B_alan=100, kesişim (Intersect testinden bilinen)=25 -> union=175
        var result = ConvexPolygonClipper2D.Union(a, b, Normal);
        Assert.Equal(175.0, PolygonArea(result), 6);
    }

    [Fact]
    public void Union_DisjointRectangles_ThrowsInsteadOfSilentWrongResult()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(100, 100, 110, 110);
        Assert.Throws<InvalidOperationException>(() => ConvexPolygonClipper2D.Union(a, b, Normal));
    }

    [Fact]
    public void Union_ConcaveInputPolygon_ThrowsInsteadOfSilentWrongResult()
    {
        var concave = new List<Vector3D>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 5, 0),
            new(5, 5, 0), new(5, 10, 0), new(0, 10, 0)
        };
        var square = Rect(0, 0, 10, 10);
        Assert.Throws<InvalidOperationException>(() => ConvexPolygonClipper2D.Union(concave, square, Normal));
    }

    [Fact]
    public void Union_ResultIsSimpleClosedLoop_NoSelfIntersectionOrDuplicateVertices()
    {
        var a = Rect(0, 0, 2000, 2000);
        var b = Rect(1500, 1500, 3000, 3000);
        var result = ConvexPolygonClipper2D.Union(a, b, Normal);

        for (int i = 0; i < result.Count; i++)
        {
            for (int j = i + 1; j < result.Count; j++)
            {
                Assert.True(result[i].DistanceTo(result[j]) > 1e-6, $"Tekrarlanan köşe: index {i} ve {j}");
            }
        }
    }
}
