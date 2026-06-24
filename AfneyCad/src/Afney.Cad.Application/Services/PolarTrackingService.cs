using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Application.Services;

public class PolarTrackingService
{
    public bool Enabled { get; set; } = false;
    public double IncrementAngle { get; set; } = 90.0;

    private static readonly double[] CommonAngles = { 15, 30, 45, 90 };

    public void SetIncrement(double angle)
    {
        IncrementAngle = angle;
    }

    public Vector3D? SnapToPolar(Vector3D basePoint, Vector3D cursor, double tolerance = 5.0)
    {
        if (!Enabled) return null;

        var dx = cursor.X - basePoint.X;
        var dy = cursor.Y - basePoint.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-9) return null;

        double cursorAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (cursorAngle < 0) cursorAngle += 360;

        for (double a = 0; a < 360; a += IncrementAngle)
        {
            double diff = Math.Abs(cursorAngle - a);
            if (diff > 180) diff = 360 - diff;

            if (diff < tolerance)
            {
                double rad = a * Math.PI / 180.0;
                double snappedX = basePoint.X + Math.Cos(rad) * dist;
                double snappedY = basePoint.Y + Math.Sin(rad) * dist;
                return new Vector3D(snappedX, snappedY, 0);
            }
        }
        return null;
    }

    public List<(Vector3D From, Vector3D To, double Angle)> GetTrackingLines(Vector3D basePoint, double extent = 10000)
    {
        var lines = new List<(Vector3D, Vector3D, double)>();
        if (!Enabled) return lines;

        for (double a = 0; a < 360; a += IncrementAngle)
        {
            double rad = a * Math.PI / 180.0;
            var to = new Vector3D(
                basePoint.X + Math.Cos(rad) * extent,
                basePoint.Y + Math.Sin(rad) * extent, 0);
            lines.Add((basePoint, to, a));
        }
        return lines;
    }
}
