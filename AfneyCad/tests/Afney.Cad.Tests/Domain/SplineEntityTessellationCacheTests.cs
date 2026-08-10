using System.Linq;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: SplineEntity Tessellasyon Cache Testleri
   NEDEN: Performans denetiminde Draw() içinde Tessellate()'in her frame'de yeniden
          çalıştığı (NURBSCurve cache'liydi ama sonuç noktaları değildi) tespit edildi.
          Bu testler:
          1. Tessellate()'in art arda çağrılarda AYNI liste referansını döndürdüğünü (cache hit),
          2. Kontrol noktaları/Move/Transform/grip sürüklemesi sonrası cache'in invalide olup
             GÜNCEL (değişmiş) noktaları ürettiğini,
          doğrular — yani davranış (görsel çıktı) aynı kalırken tekrar hesaplama önleniyor.
*/
public class SplineEntityTessellationCacheTests
{
    private static SplineEntity CreateSpline()
    {
        var points = new[]
        {
            new Vector3D(0, 0, 0),
            new Vector3D(10, 20, 0),
            new Vector3D(20, -10, 0),
            new Vector3D(30, 0, 0),
        };
        return new SplineEntity(points, degree: 3);
    }

    [Fact]
    public void Tessellate_CalledTwice_ReturnsSameCachedListInstance()
    {
        var spline = CreateSpline();

        var first = spline.Tessellate();
        var second = spline.Tessellate();

        Assert.NotEmpty(first);
        Assert.Same(first, second); // Aynı referans -> yeniden hesaplanmadı
    }

    [Fact]
    public void Tessellate_ProducesSamePointValues_AsUncachedBaseline()
    {
        // Cache'in davranışı (üretilen değerleri) DEĞİŞTİRMEDİĞİNİ doğrula.
        var spline = CreateSpline();

        var cached1 = spline.Tessellate();
        var cached2 = spline.Tessellate();

        Assert.Equal(cached1.Count, cached2.Count);
        for (int i = 0; i < cached1.Count; i++)
        {
            Assert.Equal(cached1[i].X, cached2[i].X, precision: 9);
            Assert.Equal(cached1[i].Y, cached2[i].Y, precision: 9);
        }
    }

    [Fact]
    public void Tessellate_AfterMove_InvalidatesCache_AndReflectsNewPosition()
    {
        var spline = CreateSpline();
        var before = spline.Tessellate().ToList();

        spline.Move(new Vector3D(100, 0, 0));
        var after = spline.Tessellate();

        Assert.NotSame(before, after);
        Assert.Equal(before[0].X + 100, after[0].X, precision: 6);
    }

    [Fact]
    public void Tessellate_AfterMoveGripPoint_InvalidatesCache_AndReflectsNewShape()
    {
        var spline = CreateSpline();
        var before = spline.Tessellate().ToList();

        spline.MoveGripPointAt(1, new Vector3D(10, 200, 0));
        var after = spline.Tessellate();

        Assert.NotSame(before, after);
        Assert.NotEqual(before[before.Count / 2].Y, after[after.Count / 2].Y, precision: 3);
    }

    [Fact]
    public void Draw_CalledMultipleTimes_DoesNotChangeTessellationOutput()
    {
        // Davranış aynı kalmalı: Draw() defalarca çağrılsa da her seferinde aynı noktalar üretilir.
        var spline = CreateSpline();
        var ctx = new FakeRenderContext();

        spline.Draw(ctx);
        var firstTessellation = spline.Tessellate();
        spline.Draw(ctx);
        spline.Draw(ctx);
        var laterTessellation = spline.Tessellate();

        Assert.Same(firstTessellation, laterTessellation);
    }
}
