using Afney.Cad.Application.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Application;

/*
   NE: Polar Tracking + Object Snap Tracking — Saf Matematik Testleri
   NEDEN: Bir denetim raporu, Polar/Object Snap Tracking'in projede sadece birer AYAR BAYRAĞI
          olarak var olduğunu, gerçek hizalama mantığının hiçbir yerde kullanılmadığını
          (CadViewport'a hiç bağlanmadığını) tespit etti. Bu testler artık gerçekten CadViewport
          mouse-move akışında kullanılan `PolarTrackingService.SnapDetailed` ve
          `ObjectSnapTrackingService.FindAlignment/GetTrackingLines` matematiğini kilitliyor.
          WPF fare etkileşimi (CadViewport) doğrudan test edilemez, ama açı-hizalama ve
          hizalama-çizgisi matematiği saf C# olduğu için burada birim testle doğrulanabiliyor.
*/
public class TrackingServicesTests
{
    // ══════════════════════════════ Polar Tracking ══════════════════════════════

    [Fact]
    public void Snap_WhenDisabled_ReturnsNull()
    {
        var svc = new PolarTrackingService { IsEnabled = false, IncrementAngle = 90.0 };
        var result = svc.SnapDetailed(Vector3D.Zero, new Vector3D(1000, 0, 0));
        Assert.Null(result);
    }

    [Theory]
    [InlineData(0.0)]     // Tam 0° üzerinde
    [InlineData(2.9)]     // Tolerans sınırının hemen içinde (±3°)
    [InlineData(-2.9)]
    public void SnapDetailed_AngleWithinTolerance_SnapsToZeroDegrees(double angleOffsetDeg)
    {
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 90.0, AngleTolerance = 3.0 };
        double rad = angleOffsetDeg * System.Math.PI / 180.0;
        double dist = 1000.0;
        var cursor = new Vector3D(dist * System.Math.Cos(rad), dist * System.Math.Sin(rad), 0);

        var result = svc.SnapDetailed(Vector3D.Zero, cursor);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value.Angle, precision: 6);
        // Nokta tam 0° yönünde (X ekseni üzerinde), mesafe korunmalı.
        Assert.Equal(dist, result.Value.Point.X, precision: 3);
        Assert.Equal(0.0, result.Value.Point.Y, precision: 3);
    }

    [Fact]
    public void SnapDetailed_AngleOutsideTolerance_ReturnsNull()
    {
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 90.0, AngleTolerance = 3.0 };
        // 10° sapma → 0° ve 90°'nin her ikisinden de tolerans dışında.
        double rad = 10.0 * System.Math.PI / 180.0;
        var cursor = new Vector3D(1000 * System.Math.Cos(rad), 1000 * System.Math.Sin(rad), 0);

        var result = svc.SnapDetailed(Vector3D.Zero, cursor);

        Assert.Null(result);
    }

    [Fact]
    public void SnapDetailed_NearNinetyDegrees_SnapsToNinety()
    {
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 90.0, AngleTolerance = 3.0 };
        // 88° — 90°'ye 2° uzaklıkta (tolerans içinde), 0°'ye 88° uzaklıkta.
        double rad = 88.0 * System.Math.PI / 180.0;
        var cursor = new Vector3D(1000 * System.Math.Cos(rad), 1000 * System.Math.Sin(rad), 0);

        var result = svc.SnapDetailed(Vector3D.Zero, cursor);

        Assert.NotNull(result);
        Assert.Equal(90.0, result!.Value.Angle, precision: 6);
    }

    [Fact]
    public void SnapDetailed_CircularWraparoundNear360_SnapsToZero()
    {
        // AÇI SINIRI (359°/0°) TESTİ: Round-tabanlı yuvarlama düzeltilmeden önce burada
        // yanlış (büyük) bir fark hesaplanıp snap reddedilebiliyordu — artık dairesel fark kullanılıyor.
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 90.0, AngleTolerance = 3.0 };
        double rad = 358.0 * System.Math.PI / 180.0; // 360'a 2° uzaklıkta
        var cursor = new Vector3D(1000 * System.Math.Cos(rad), 1000 * System.Math.Sin(rad), 0);

        var result = svc.SnapDetailed(Vector3D.Zero, cursor);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value.Angle, precision: 6);
    }

    [Fact]
    public void SnapDetailed_CustomIncrement45_SnapsToClosestMultiple()
    {
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 45.0, AngleTolerance = 3.0 };
        double rad = 44.0 * System.Math.PI / 180.0;
        var cursor = new Vector3D(1000 * System.Math.Cos(rad), 1000 * System.Math.Sin(rad), 0);

        var result = svc.SnapDetailed(Vector3D.Zero, cursor);

        Assert.NotNull(result);
        Assert.Equal(45.0, result!.Value.Angle, precision: 6);
    }

    [Fact]
    public void SnapDetailed_CursorTooCloseToBasePoint_ReturnsNull()
    {
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 90.0 };
        var result = svc.SnapDetailed(Vector3D.Zero, new Vector3D(0.5, 0.5, 0));
        Assert.Null(result);
    }

    [Fact]
    public void Snap_LegacyApi_ReturnsSamePointAsSnapDetailed()
    {
        var svc = new PolarTrackingService { IsEnabled = true, IncrementAngle = 90.0 };
        var basePt = new Vector3D(100, 200, 0);
        var cursor = new Vector3D(100, 1200, 0); // Tam 90° yönünde

        var legacy = svc.Snap(basePt, cursor);
        var detailed = svc.SnapDetailed(basePt, cursor);

        Assert.NotNull(legacy);
        Assert.NotNull(detailed);
        Assert.Equal(detailed!.Value.Point.X, legacy!.Value.X, precision: 6);
        Assert.Equal(detailed.Value.Point.Y, legacy.Value.Y, precision: 6);
    }

    // ═══════════════════════════ Object Snap Tracking ═══════════════════════════

    [Fact]
    public void FindAlignment_WhenDisabled_ReturnsNull()
    {
        var svc = new ObjectSnapTrackingService { Enabled = false };
        svc.AcquirePoint(new Vector3D(500, 500, 0));

        var result = svc.FindAlignment(new Vector3D(500, 900, 0), tolerance: 5.0);

        Assert.Null(result);
    }

    [Fact]
    public void FindAlignment_CursorOnVerticalLineThroughAcquiredPoint_SnapsX()
    {
        var svc = new ObjectSnapTrackingService { Enabled = true };
        svc.AcquirePoint(new Vector3D(500, 500, 0));

        // Fare, yakalanan noktanın X'i ile aynı (500), Y'si farklı (900) → dikey hizalama.
        var result = svc.FindAlignment(new Vector3D(502, 900, 0), tolerance: 5.0);

        Assert.NotNull(result);
        Assert.Equal(500.0, result!.Value.X, precision: 3);
        Assert.Equal(900.0, result.Value.Y, precision: 3);
    }

    [Fact]
    public void FindAlignment_CursorOnHorizontalLineThroughAcquiredPoint_SnapsY()
    {
        var svc = new ObjectSnapTrackingService { Enabled = true };
        svc.AcquirePoint(new Vector3D(500, 500, 0));

        var result = svc.FindAlignment(new Vector3D(900, 498, 0), tolerance: 5.0);

        Assert.NotNull(result);
        Assert.Equal(900.0, result!.Value.X, precision: 3);
        Assert.Equal(500.0, result.Value.Y, precision: 3);
    }

    [Fact]
    public void FindAlignment_CursorOutsideTolerance_ReturnsNull()
    {
        var svc = new ObjectSnapTrackingService { Enabled = true };
        svc.AcquirePoint(new Vector3D(500, 500, 0));

        var result = svc.FindAlignment(new Vector3D(600, 900, 0), tolerance: 5.0);

        Assert.Null(result);
    }

    [Fact]
    public void ClearAcquired_RemovesAllTrackingPoints()
    {
        var svc = new ObjectSnapTrackingService { Enabled = true };
        svc.AcquirePoint(new Vector3D(500, 500, 0));
        svc.ClearAcquired();

        var result = svc.FindAlignment(new Vector3D(500, 900, 0), tolerance: 5.0);

        Assert.Null(result);
    }

    [Fact]
    public void GetTrackingLines_UsesSameToleranceAsFindAlignment()
    {
        var svc = new ObjectSnapTrackingService { Enabled = true };
        svc.AcquirePoint(new Vector3D(500, 500, 0));

        var lines = svc.GetTrackingLines(new Vector3D(502, 900, 0), tolerance: 5.0, extent: 1000);

        Assert.Single(lines);
        // Dikey hizalama çizgisi X=500 üzerinde, +/- extent kadar uzanmalı.
        Assert.Equal(500.0, lines[0].From.X, precision: 3);
        Assert.Equal(500.0, lines[0].To.X, precision: 3);
    }

    [Fact]
    public void GetTrackingLines_WhenDisabled_ReturnsEmpty()
    {
        var svc = new ObjectSnapTrackingService { Enabled = false };
        svc.AcquirePoint(new Vector3D(500, 500, 0));

        var lines = svc.GetTrackingLines(new Vector3D(502, 900, 0), tolerance: 5.0);

        Assert.Empty(lines);
    }
}
