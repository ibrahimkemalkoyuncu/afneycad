using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Application.Services;

// Polar Tracking + Isometric Grid — AutoCAD benzeri açısal kılavuz
public class PolarTrackingService
{
    public bool IsEnabled { get; set; } = false;
    // AutoCAD varsayılanı 90° — kullanıcı 45/30/15 vb. seçebilir (bkz. UserSettings.PolarAngleIncrement).
    public double IncrementAngle { get; set; } = 90.0;
    // Fareyi açıya "mıknatıslama" toleransı — artık increment'e ORANTILI değil, sabit ±3° (istenen davranış).
    public double AngleTolerance { get; set; } = 3.0;
    public double TrackingDistance { get; set; } = 50000;

    public static readonly double[] StandardIncrements = { 5, 10, 15, 22.5, 30, 45, 90 };

    public Vector3D? Snap(Vector3D basePoint, Vector3D cursor) => SnapDetailed(basePoint, cursor)?.Point;

    /*
       NE: Açı + Nokta Hesabı (SnapDetailed)
       NEDEN: Sadece hizalanmış noktayı değil, hangi standart açıya (0/45/90 vb.) yakalandığını da
              döndürür — CadViewport bu açıyı hizalama çizgisinin yanına etiket olarak basar.
       NASIL: Fare açısı en yakın IncrementAngle katına yuvarlanır; sapma AngleTolerance içindeyse
              (varsayılan ±3°) o açıya kilitlenilir. 359°/0° sınırında da doğru sonuç vermesi için
              fark dairesel (circular) olarak hesaplanır.
    */
    public (Vector3D Point, double Angle)? SnapDetailed(Vector3D basePoint, Vector3D cursor)
    {
        if (!IsEnabled || IncrementAngle <= 0) return null;

        double dx = cursor.X - basePoint.X;
        double dy = cursor.Y - basePoint.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1.0) return null;

        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;

        double snappedAngle = Math.Round(angle / IncrementAngle) * IncrementAngle;
        double diff = Math.Abs(angle - snappedAngle);
        if (diff > 180.0) diff = 360.0 - diff; // 359°↔0° sınırı gibi dairesel sapmaları doğru ölç

        if (diff > AngleTolerance) return null;

        double normalizedAngle = ((snappedAngle % 360.0) + 360.0) % 360.0;
        double rad = normalizedAngle * Math.PI / 180.0;

        var point = new Vector3D(
            basePoint.X + dist * Math.Cos(rad),
            basePoint.Y + dist * Math.Sin(rad),
            cursor.Z);

        return (point, normalizedAngle);
    }

    public List<(Vector3D Start, Vector3D End, double Angle)> GetTrackingLines(Vector3D basePoint)
    {
        var lines = new List<(Vector3D, Vector3D, double)>();
        if (!IsEnabled) return lines;

        for (double a = 0; a < 360; a += IncrementAngle)
        {
            double rad = a * Math.PI / 180.0;
            var end = new Vector3D(basePoint.X + TrackingDistance * Math.Cos(rad), basePoint.Y + TrackingDistance * Math.Sin(rad), 0);
            lines.Add((basePoint, end, a));
        }
        return lines;
    }
}

public class IsometricGridService
{
    public bool IsEnabled { get; set; } = false;
    public IsometricPlane ActivePlane { get; set; } = IsometricPlane.Top;
    public double GridSpacing { get; set; } = 500;

    private static readonly Dictionary<IsometricPlane, (double Angle1, double Angle2)> PlaneAngles = new()
    {
        [IsometricPlane.Top] = (30, 150),
        [IsometricPlane.Left] = (90, 150),
        [IsometricPlane.Right] = (30, 90),
    };

    public (double, double) GetSnapAngles() => PlaneAngles.GetValueOrDefault(ActivePlane, (30, 150));

    public List<(Vector3D Start, Vector3D End)> GenerateGrid(Vector3D center, double viewSize)
    {
        var lines = new List<(Vector3D, Vector3D)>();
        if (!IsEnabled) return lines;

        var (angle1, angle2) = GetSnapAngles();
        double rad1 = angle1 * Math.PI / 180.0;
        double rad2 = angle2 * Math.PI / 180.0;
        int count = (int)(viewSize / GridSpacing);

        for (int i = -count; i <= count; i++)
        {
            double offset = i * GridSpacing;
            var perpDir1 = new Vector3D(Math.Cos(rad1 + Math.PI / 2), Math.Sin(rad1 + Math.PI / 2), 0);
            var basePoint1 = center + perpDir1 * offset;
            var dir1 = new Vector3D(Math.Cos(rad1), Math.Sin(rad1), 0);
            lines.Add((basePoint1 - dir1 * viewSize, basePoint1 + dir1 * viewSize));

            var perpDir2 = new Vector3D(Math.Cos(rad2 + Math.PI / 2), Math.Sin(rad2 + Math.PI / 2), 0);
            var basePoint2 = center + perpDir2 * offset;
            var dir2 = new Vector3D(Math.Cos(rad2), Math.Sin(rad2), 0);
            lines.Add((basePoint2 - dir2 * viewSize, basePoint2 + dir2 * viewSize));
        }
        return lines;
    }

    public void CyclePlane()
    {
        ActivePlane = ActivePlane switch
        {
            IsometricPlane.Top => IsometricPlane.Right,
            IsometricPlane.Right => IsometricPlane.Left,
            IsometricPlane.Left => IsometricPlane.Top,
            _ => IsometricPlane.Top
        };
    }
}

public enum IsometricPlane { Top, Left, Right }
