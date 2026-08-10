using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: ClosedAreaDetector Testleri (ClosedAreaDetectorTests)
   NEDEN: "Otonom Mahal Algılama" (OnAutoDetectSpacesCommand) komutunun temelini oluşturan
          kapalı döngü (oda) tespit algoritması hiç test edilmemişti. Bu testler; uç uca
          birleşmiş çizgilerden oluşan basit bir kapalı poligonun (dörtgen oda) doğru
          tespit edildiğini, açık (kapanmamış) şekillerin oda sayılmadığını ve
          100000 mm² (0.1 m²) alan eşiğinin altındaki döngülerin gürültü olarak
          elendiğini doğruluyor (Shoelace formülü ile çapraz kontrol).
*/
public class ClosedAreaDetectorTests
{
    private static double ShoelaceArea(List<Vector3D> points)
    {
        double area = 0.0;
        int j = points.Count - 1;
        for (int i = 0; i < points.Count; i++)
        {
            area += (points[j].X + points[i].X) * (points[j].Y - points[i].Y);
            j = i;
        }
        return System.Math.Abs(area / 2.0);
    }

    [Fact]
    public void FindClosedAreas_ClosedRectangle_DetectsExactlyOneRoom()
    {
        // 1000mm x 1000mm kare oda (1 m² = 1,000,000 mm², eşik olan 100,000 mm²'nin üzerinde).
        var a = new Vector3D(0, 0, 0);
        var b = new Vector3D(1000, 0, 0);
        var c = new Vector3D(1000, 1000, 0);
        var d = new Vector3D(0, 1000, 0);

        var lines = new List<CadEntity>
        {
            new LineEntity(a, b),
            new LineEntity(b, c),
            new LineEntity(c, d),
            new LineEntity(d, a),
        };

        var detector = new ClosedAreaDetector();
        var cycles = detector.FindClosedAreas(lines);

        Assert.Single(cycles);
        Assert.Equal(4, cycles[0].Count);
        Assert.Equal(1_000_000.0, ShoelaceArea(cycles[0]), precision: 3);
    }

    [Fact]
    public void FindClosedAreas_OpenPolyline_MissingOneSide_DetectsNoRoom()
    {
        // Aynı dörtgenin 4. kenarı eksik -> döngü kapanmıyor, oda algılanmamalı.
        var a = new Vector3D(0, 0, 0);
        var b = new Vector3D(1000, 0, 0);
        var c = new Vector3D(1000, 1000, 0);
        var d = new Vector3D(0, 1000, 0);

        var lines = new List<CadEntity>
        {
            new LineEntity(a, b),
            new LineEntity(b, c),
            new LineEntity(c, d),
            // d -> a kenarı YOK
        };

        var detector = new ClosedAreaDetector();
        var cycles = detector.FindClosedAreas(lines);

        Assert.Empty(cycles);
    }

    [Fact]
    public void FindClosedAreas_TinyClosedLoop_BelowAreaThreshold_IsFilteredOut()
    {
        // 100mm x 100mm kapalı kare = 10,000 mm² < 100,000 mm² eşik -> gürültü olarak elenmeli.
        var a = new Vector3D(0, 0, 0);
        var b = new Vector3D(100, 0, 0);
        var c = new Vector3D(100, 100, 0);
        var d = new Vector3D(0, 100, 0);

        var lines = new List<CadEntity>
        {
            new LineEntity(a, b),
            new LineEntity(b, c),
            new LineEntity(c, d),
            new LineEntity(d, a),
        };

        var detector = new ClosedAreaDetector();
        var cycles = detector.FindClosedAreas(lines);

        Assert.Empty(cycles);
    }

    [Fact]
    public void FindClosedAreas_NoLineEntities_ReturnsEmptyList()
    {
        var detector = new ClosedAreaDetector();
        var cycles = detector.FindClosedAreas(new List<CadEntity>());

        Assert.Empty(cycles);
    }

    [Fact]
    public void FindClosedAreas_TwoSeparateRooms_DetectsBoth()
    {
        // Oda 1: (0,0)-(1000,0)-(1000,1000)-(0,1000)
        var a1 = new Vector3D(0, 0, 0);
        var b1 = new Vector3D(1000, 0, 0);
        var c1 = new Vector3D(1000, 1000, 0);
        var d1 = new Vector3D(0, 1000, 0);

        // Oda 2: tamamen ayrık, uzakta — (5000,5000) tabanlı 1200x800
        var a2 = new Vector3D(5000, 5000, 0);
        var b2 = new Vector3D(6200, 5000, 0);
        var c2 = new Vector3D(6200, 5800, 0);
        var d2 = new Vector3D(5000, 5800, 0);

        var lines = new List<CadEntity>
        {
            new LineEntity(a1, b1), new LineEntity(b1, c1), new LineEntity(c1, d1), new LineEntity(d1, a1),
            new LineEntity(a2, b2), new LineEntity(b2, c2), new LineEntity(c2, d2), new LineEntity(d2, a2),
        };

        var detector = new ClosedAreaDetector();
        var cycles = detector.FindClosedAreas(lines);

        Assert.Equal(2, cycles.Count);
        var areas = cycles.Select(ShoelaceArea).OrderBy(x => x).ToList();
        Assert.Equal(960_000.0, areas[0], precision: 1);   // 1200*800 (Oda 2, küçük)
        Assert.Equal(1_000_000.0, areas[1], precision: 1); // 1000*1000 (Oda 1, büyük)
    }
}
