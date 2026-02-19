using System;

namespace Afney.Cad.Geometry.Primitives;

/// <summary>
/// A simple geometric line segment defined by two 3D points.
/// Used for pure geometric calculations without domain baggage.
/// </summary>
public struct LineSegment
{
    public Vector3D Start;
    public Vector3D End;

    public LineSegment(Vector3D start, Vector3D end)
    {
        Start = start;
        End = end;
    }

    public double Length => Math.Sqrt(LengthSquared);
    public double LengthSquared => Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2) + Math.Pow(End.Z - Start.Z, 2);

    /// <summary>
    /// Checks if this segment intersects with another segment in 2D (XY plane).
    /// Returns the intersection point if found, null otherwise.
    /// Parallel lines are considered non-intersecting here for simplicity.
    /// </summary>
    public Vector3D? Intersect2D(LineSegment other)
    {
        // 2D Intersection Logic (XY Plane)
        double dev = (other.End.Y - other.Start.Y) * (End.X - Start.X) - (other.End.X - other.Start.X) * (End.Y - Start.Y);
        
        if (Math.Abs(dev) < 1e-9) return null; // Parallel

        double uA = ((other.End.X - other.Start.X) * (Start.Y - other.Start.Y) - (other.End.Y - other.Start.Y) * (Start.X - other.Start.X)) / dev;
        double uB = ((End.X - Start.X) * (Start.Y - other.Start.Y) - (End.Y - Start.Y) * (Start.X - other.Start.X)) / dev;

        if (uA >= 0 && uA <= 1 && uB >= 0 && uB <= 1)
        {
            return new Vector3D(Start.X + uA * (End.X - Start.X), Start.Y + uA * (End.Y - Start.Y), 0); // Z is ignored/averaged
        }
        return null;
    }

    /// <summary>
    /// Calculates the minimum distance from a point to this line segment in 2D.
    /// </summary>
    public double DistanceToPoint(Vector3D p)
    {
        // Vector form
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        if (dx == 0 && dy == 0) return Math.Sqrt(Math.Pow(p.X - Start.X, 2) + Math.Pow(p.Y - Start.Y, 2));

        // Project point onto line (parameter t)
        double t = ((p.X - Start.X) * dx + (p.Y - Start.Y) * dy) / (dx * dx + dy * dy);

        // Clamping to segment
        t = Math.Max(0, Math.Min(1, t));

        double closestX = Start.X + t * dx;
        double closestY = Start.Y + t * dy;

        return Math.Sqrt(Math.Pow(p.X - closestX, 2) + Math.Pow(p.Y - closestY, 2));
    }
}
