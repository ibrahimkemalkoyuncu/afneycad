using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: GeneralSolidIntersector Testleri — çok-yüzlü genel INTERSECT (A∩B) montajı.
   NEDEN: `docs/Roadmap_CSG_Boolean.md` (2026-08-07 güncellemesi) — INTERSECT,
       `GeneralSolidSubtractor`'ın (2026-08-06) subdivide→classify→reconstruct altyapısının
       "insideB" dalını (SUBTRACT'in attığı) tutup, kapak normalini TERS ÇEVİRMEDEN (B'nin
       KENDİ normaliyle) kullanacak şekilde YENİDEN kullanıyor. Aynı üç senaryo (tek-düzlem,
       köşe-çentiği/2-düzlem, gerçek 3D köşe/3-düzlem, through-slot) `GeneralSolidSubtractorTests`
       ile ÇAPRAZ tutarlı hacimlerle doğrulanıyor (A∩B'nin hacmi, SUBTRACT testlerinin
       "expectedVolume" hesaplarındaki "kesişim_hacmi" terimiyle BİREBİR aynı).
*/
public class GeneralSolidIntersectorTests
{
    [Fact]
    public void Intersect_SinglePlaneCase_ProducesCorrectVolume()
    {
        // A=[0,2000]^3, B=[1000,3000]x[0,2000]x[0,2000] -> A∩B = [1000,2000]x[0,2000]x[0,2000].
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var result = GeneralSolidIntersector.Intersect(a, b);

        Assert.True(result.IsValid());
        Assert.Equal(1000.0 * 2000.0 * 2000.0, result.GetVolume(), precision: 6);
    }

    [Fact]
    public void Intersect_BCompletelyOutsideA_NoPlaneIntersectsBoundary_ThrowsNotSupported()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        Assert.Throws<NotSupportedException>(() => GeneralSolidIntersector.Intersect(a, b));
    }

    [Fact]
    public void Intersect_CornerNotch_ProducesValidResultWithCorrectVolume()
    {
        // A∩B = [1500,2000]x[1500,2000]x[0,2000] = 500x500x2000 (`GeneralSolidSubtractorTests.
        // Subtract_CornerNotch_*`'in "kesişim_hacmi" terimiyle BİREBİR aynı değer).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        var result = GeneralSolidIntersector.Intersect(a, b);

        Assert.True(result.IsValid());
        Assert.Equal(500.0 * 500.0 * 2000.0, result.GetVolume(), precision: 3);
    }

    [Fact]
    public void Intersect_CornerNotch_ResultContainsOnlyTheOverlapCorner()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        var result = GeneralSolidIntersector.Intersect(a, b);

        // Örtüşen köşenin merkezi -> sonuç Solid'in İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1750, 1000)));

        // A'nın örtüşmeyen kalanı (köşe dışı) -> sonuç Solid'in DIŞINDA olmalı.
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(1000, 1000, 1000)));
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1000, 1000)));
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(1000, 1750, 1000)));
    }

    [Fact]
    public void Intersect_TrueCornerNotch_ThreePlanes_ProducesValidResultWithCorrectVolume()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);

        var result = GeneralSolidIntersector.Intersect(a, b);

        Assert.True(result.IsValid());
        Assert.Equal(500.0 * 500.0 * 500.0, result.GetVolume(), precision: 3);
    }

    [Fact]
    public void Intersect_ThroughSlot_TwoParallelPlanesFullySpanOtherAxes_ProducesValidSingleSolid()
    {
        // A=[0,2000]^3. B=[500,1500]x[0,2000]x[0,2000] -> A∩B = B'nin kendisi (tek, bağlantılı
        // parça) = [500,1500]x[0,2000]x[0,2000] = 1000x2000x2000.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(500, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);

        var result = GeneralSolidIntersector.Intersect(a, b);

        Assert.True(result.IsValid());
        Assert.Equal(1000.0 * 2000.0 * 2000.0, result.GetVolume(), precision: 3);
    }

    [Fact]
    public void Intersect_ThroughSlot_ResultContainsOnlySlotRegion()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(500, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);

        var result = GeneralSolidIntersector.Intersect(a, b);

        // Slot'un (A∩B) merkezi -> sonuç Solid'in İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1000, 1000, 1000)));

        // A'nın slot-dışı kalan parçaları -> sonuç Solid'in DIŞINDA olmalı.
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(250, 1000, 1000)));
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1000, 1000)));
    }
}
