using Afney.Cad.Geometry.Algorithms;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: FILLET/CHAMFER Matematik Testleri (FilletChamferMathTests)
   NEDEN: FilletChamferMath.TryComputeFillet/TryComputeChamfer, kod tabanında önceden hiç
          bulunmayan FILLET/CHAMFER komutlarının çekirdek geometrisidir. Elle hesaplanmış basit
          bir dik-açı örneğiyle (iki dik doğru, orijinde kesişen) beklenen teğet/pah noktaları ve
          yay merkezi/açıları kilitlenir — "derleniyor" ile "doğru çalışıyor" aynı şey değil.
*/
public class FilletChamferMathTests
{
    private const double Tol = 1e-6;

    // Ortak kurgu: A = (0,0)->(10,0) [yatay, +X], B = (0,0)->(0,10) [dikey, +Y].
    // Kesişim P=(0,0). pickA=(8,0) -> korunan uç (10,0). pickB=(0,8) -> korunan uç (0,10).
    private static readonly Vector3D AStart = new(0, 0, 0);
    private static readonly Vector3D AEnd = new(10, 0, 0);
    private static readonly Vector3D BStart = new(0, 0, 0);
    private static readonly Vector3D BEnd = new(0, 10, 0);
    private static readonly Vector3D PickA = new(8, 0, 0);
    private static readonly Vector3D PickB = new(0, 8, 0);

    [Fact]
    public void TryComputeFillet_PerpendicularLinesAtOrigin_ProducesExpectedTangentPointsAndArc()
    {
        bool ok = FilletChamferMath.TryComputeFillet(
            AStart, AEnd, BStart, BEnd, radius: 2.0, PickA, PickB,
            out var result, out var error);

        Assert.True(ok, error);

        // Elle hesap: T = R / tan(45°) = 2. Teğet noktaları (2,0) ve (0,2).
        AssertVectorEqual(new Vector3D(2, 0, 0), result.TrimmedAStart);
        AssertVectorEqual(AEnd, result.TrimmedAEnd); // Korunan uç değişmedi
        AssertVectorEqual(new Vector3D(0, 2, 0), result.TrimmedBStart);
        AssertVectorEqual(BEnd, result.TrimmedBEnd);

        // Elle hesap: merkez = P + normalize(1,1)*(R/sin(45°)) = (2,2).
        AssertVectorEqual(new Vector3D(2, 2, 0), result.ArcCenter);
        Assert.Equal(2.0, result.ArcRadius, precision: 6);

        // Yayın iki ucu da merkezden gerçekten R uzaklıkta olmalı.
        var startPt = new Vector3D(
            result.ArcCenter.X + System.Math.Cos(result.ArcStartAngle) * result.ArcRadius,
            result.ArcCenter.Y + System.Math.Sin(result.ArcStartAngle) * result.ArcRadius, 0);
        var endPt = new Vector3D(
            result.ArcCenter.X + System.Math.Cos(result.ArcEndAngle) * result.ArcRadius,
            result.ArcCenter.Y + System.Math.Sin(result.ArcEndAngle) * result.ArcRadius, 0);

        // Yayın uçları, {(2,0),(0,2)} teğet noktalarının biri olmalı (sıralama Start/End keyfi olabilir).
        bool matchesForward = NearlyEqual(startPt, new Vector3D(0, 2, 0)) && NearlyEqual(endPt, new Vector3D(2, 0, 0));
        bool matchesReverse = NearlyEqual(startPt, new Vector3D(2, 0, 0)) && NearlyEqual(endPt, new Vector3D(0, 2, 0));
        Assert.True(matchesForward || matchesReverse);

        // Sweep, köşeyi yuvarlayan KÜÇÜK yay olmalı (90°'lik dik açı için sweep = 90°).
        double sweep = result.ArcEndAngle > result.ArcStartAngle
            ? result.ArcEndAngle - result.ArcStartAngle
            : (2 * System.Math.PI - result.ArcStartAngle) + result.ArcEndAngle;
        Assert.Equal(System.Math.PI / 2, sweep, precision: 5);
    }

    [Fact]
    public void TryComputeChamfer_PerpendicularLinesAtOrigin_ProducesExpectedChamferPoints()
    {
        bool ok = FilletChamferMath.TryComputeChamfer(
            AStart, AEnd, BStart, BEnd, dist1: 3.0, dist2: 4.0, PickA, PickB,
            out var result, out var error);

        Assert.True(ok, error);

        AssertVectorEqual(new Vector3D(3, 0, 0), result.TrimmedAStart);
        AssertVectorEqual(AEnd, result.TrimmedAEnd);
        AssertVectorEqual(new Vector3D(0, 4, 0), result.TrimmedBStart);
        AssertVectorEqual(BEnd, result.TrimmedBEnd);

        AssertVectorEqual(new Vector3D(3, 0, 0), result.ChamferStart);
        AssertVectorEqual(new Vector3D(0, 4, 0), result.ChamferEnd);
    }

    [Fact]
    public void TryComputeFillet_ParallelLines_FailsWithError()
    {
        var a1 = new Vector3D(0, 0, 0);
        var a2 = new Vector3D(10, 0, 0);
        var b1 = new Vector3D(0, 5, 0);
        var b2 = new Vector3D(10, 5, 0);

        bool ok = FilletChamferMath.TryComputeFillet(a1, a2, b1, b2, 2.0, new Vector3D(5, 0, 0), new Vector3D(5, 5, 0), out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryComputeChamfer_ParallelLines_FailsWithError()
    {
        var a1 = new Vector3D(0, 0, 0);
        var a2 = new Vector3D(10, 0, 0);
        var b1 = new Vector3D(0, 5, 0);
        var b2 = new Vector3D(10, 5, 0);

        bool ok = FilletChamferMath.TryComputeChamfer(a1, a2, b1, b2, 2.0, 2.0, new Vector3D(5, 0, 0), new Vector3D(5, 5, 0), out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryComputeFillet_NonPositiveRadius_FailsWithError()
    {
        bool ok = FilletChamferMath.TryComputeFillet(AStart, AEnd, BStart, BEnd, 0.0, PickA, PickB, out _, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryComputeChamfer_NonPositiveDistance_FailsWithError()
    {
        bool ok = FilletChamferMath.TryComputeChamfer(AStart, AEnd, BStart, BEnd, 0.0, 5.0, PickA, PickB, out _, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    /*
       NE/NEDEN — Session #75 mimari denetiminde bulunan test kapsamı boşluğu: FILLET/CHAMFER'ın
       "teğet/pah noktası çizgi dışına taşıyor" reddi (distKeepA/distKeepB doğrulaması) daha önce
       hiç test edilmiyordu — tam da bu sınıftaki bir regresyonun (Session #75'te bulunan gerçek
       hatanın) fark edilmeden geri gelebileceği kör nokta.
       Ortak kurgu (AStart/AEnd/BStart/BEnd): 90° köşe, distKeepA = distKeepB = 10 (P'den
       (10,0)/(0,10) korunan uçlara mesafe). 90° için tangentLength = R / tan(45°) = R,
       yani R > 10 olduğunda teğet noktası kesinlikle segmentin dışına taşar.
    */
    [Fact]
    public void TryComputeFillet_RadiusLargerThanAvailableSegment_FailsWithError()
    {
        // R=15 > distKeepA=distKeepB=10 -> tangentLength=15 > 10, her iki koldan da taşar.
        bool ok = FilletChamferMath.TryComputeFillet(AStart, AEnd, BStart, BEnd, radius: 15.0, PickA, PickB, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("çok büyük", error);
    }

    [Fact]
    public void TryComputeFillet_RadiusJustUnderAvailableSegment_Succeeds()
    {
        // R=9.9 < distKeepA=distKeepB=10 -> tangentLength=9.9, guard'ın off-by-one reddetmediğini kanıtlar.
        bool ok = FilletChamferMath.TryComputeFillet(AStart, AEnd, BStart, BEnd, radius: 9.9, PickA, PickB, out var result, out var error);

        Assert.True(ok, error);
        AssertVectorEqual(new Vector3D(9.9, 0, 0), result.TrimmedAStart);
        AssertVectorEqual(new Vector3D(0, 9.9, 0), result.TrimmedBStart);
    }

    [Fact]
    public void TryComputeChamfer_Dist1LargerThanAvailableSegment_FailsWithError()
    {
        // dist1=15 > distKeepA=10 (A kolunda taşma) -> reddedilmeli.
        bool ok = FilletChamferMath.TryComputeChamfer(AStart, AEnd, BStart, BEnd, dist1: 15.0, dist2: 3.0, PickA, PickB, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("çok büyük", error);
    }

    [Fact]
    public void TryComputeChamfer_Dist2LargerThanAvailableSegment_FailsWithError()
    {
        // dist2=15 > distKeepB=10 (B kolunda taşma) -> reddedilmeli.
        bool ok = FilletChamferMath.TryComputeChamfer(AStart, AEnd, BStart, BEnd, dist1: 3.0, dist2: 15.0, PickA, PickB, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("çok büyük", error);
    }

    [Fact]
    public void TryComputeChamfer_DistancesJustUnderAvailableSegment_Succeeds()
    {
        bool ok = FilletChamferMath.TryComputeChamfer(AStart, AEnd, BStart, BEnd, dist1: 9.9, dist2: 9.9, PickA, PickB, out var result, out var error);

        Assert.True(ok, error);
        AssertVectorEqual(new Vector3D(9.9, 0, 0), result.ChamferStart);
        AssertVectorEqual(new Vector3D(0, 9.9, 0), result.ChamferEnd);
    }

    private static void AssertVectorEqual(Vector3D expected, Vector3D actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 6);
        Assert.Equal(expected.Y, actual.Y, precision: 6);
        Assert.Equal(expected.Z, actual.Z, precision: 6);
    }

    private static bool NearlyEqual(Vector3D a, Vector3D b) => a.DistanceTo(b) < Tol;
}
