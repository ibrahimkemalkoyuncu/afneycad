using System.Collections.Generic;
using Afney.Cad.Geometry.Algorithms;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: GeomUtils.HasSelfIntersection Testleri
   NEDEN: Bu kontrol önceden WallChainBuilder içinde tekrar tekrar yazılıyordu
          (WallChainBuilder + EdgeCaptureMahalCommand) — tek bir ortak yere
          (GeomUtils) taşındı. Bu testler taşınan mantığın doğrudan (herhangi bir
          duvar-zincirleme veya komut akışına bağlı olmadan) doğru çalıştığını kilitler.
*/
public class GeomUtilsSelfIntersectionTests
{
    [Fact]
    public void SimpleRectangle_IsNotSelfIntersecting()
    {
        var polygon = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 3000, 0), new(0, 3000, 0)
        };

        Assert.False(GeomUtils.HasSelfIntersection(polygon));
    }

    [Fact]
    public void LShapedRoom_IsNotFalselyFlagged()
    {
        var polygon = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 2000, 0),
            new(2000, 2000, 0), new(2000, 4000, 0), new(0, 4000, 0)
        };

        Assert.False(GeomUtils.HasSelfIntersection(polygon));
    }

    [Fact]
    public void BowtiePolygon_IsFlaggedAsSelfIntersecting()
    {
        // Çapraz kenarları olan bir "bowtie" — köşe sırası kasıtlı olarak karıştırıldı.
        var polygon = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 4000, 0), new(4000, 0, 0), new(0, 4000, 0)
        };

        Assert.True(GeomUtils.HasSelfIntersection(polygon));
    }

    [Fact]
    public void Triangle_NeverFlagged()
    {
        var polygon = new List<Vector3D> { new(0, 0, 0), new(1000, 0, 0), new(500, 1000, 0) };

        Assert.False(GeomUtils.HasSelfIntersection(polygon));
    }
}
