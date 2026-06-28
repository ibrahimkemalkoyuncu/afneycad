using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Application.Services;

// Gelişmiş OSNAP — Intersection, Tangent, Nearest, Quadrant, Node, Apparent Intersection
public class AdvancedSnapService
{
    private readonly CadDatabase _database;
    private double _apertureRadius = 15.0; // pixel
    private double _worldAperture = 500.0; // mm (zoom'a göre ayarlanır)

    // Snap modları
    public bool EnableIntersection { get; set; } = true;
    public bool EnableTangent { get; set; } = true;
    public bool EnableNearest { get; set; } = true;
    public bool EnableQuadrant { get; set; } = true;
    public bool EnableNode { get; set; } = true;
    public bool EnableParallel { get; set; } = true;
    public bool EnableExtension { get; set; } = true;

    public AdvancedSnapService(CadDatabase database) => _database = database;

    public void SetAperture(double worldUnits) => _worldAperture = worldUnits;

    // Intersection snap — iki entity'nin kesişim noktası
    public Vector3D? FindIntersection(Vector3D cursor)
    {
        if (!EnableIntersection) return null;

        var nearbyEntities = GetEntitiesNear(cursor);
        var lines = nearbyEntities.OfType<LineEntity>().ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                var ip = LineLineIntersection(lines[i].StartPoint, lines[i].EndPoint, lines[j].StartPoint, lines[j].EndPoint);
                if (ip.HasValue && (ip.Value - cursor).Length() < _worldAperture)
                    return ip;
            }
        }
        return null;
    }

    // Nearest snap — entity üzerindeki en yakın nokta
    public Vector3D? FindNearest(Vector3D cursor)
    {
        if (!EnableNearest) return null;

        Vector3D? best = null;
        double bestDist = _worldAperture;

        foreach (var ent in GetEntitiesNear(cursor))
        {
            Vector3D? nearest = null;
            if (ent is LineEntity line)
                nearest = NearestPointOnSegment(line.StartPoint, line.EndPoint, cursor);
            else if (ent is CircleEntity circle)
                nearest = NearestPointOnCircle(circle.Center, circle.Radius, cursor);
            else if (ent is ArcEntity arc)
                nearest = NearestPointOnArc(arc, cursor);

            if (nearest.HasValue)
            {
                double dist = (nearest.Value - cursor).Length();
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = nearest;
                }
            }
        }
        return best;
    }

    // Quadrant snap — daire/arc'ın 0°, 90°, 180°, 270° noktaları
    public Vector3D? FindQuadrant(Vector3D cursor)
    {
        if (!EnableQuadrant) return null;

        Vector3D? best = null;
        double bestDist = _worldAperture;

        foreach (var ent in GetEntitiesNear(cursor))
        {
            if (ent is CircleEntity circle)
            {
                var quads = new[]
                {
                    new Vector3D(circle.Center.X + circle.Radius, circle.Center.Y, 0),
                    new Vector3D(circle.Center.X, circle.Center.Y + circle.Radius, 0),
                    new Vector3D(circle.Center.X - circle.Radius, circle.Center.Y, 0),
                    new Vector3D(circle.Center.X, circle.Center.Y - circle.Radius, 0),
                };
                foreach (var q in quads)
                {
                    double dist = (q - cursor).Length();
                    if (dist < bestDist) { bestDist = dist; best = q; }
                }
            }
        }
        return best;
    }

    // Tangent snap — daireden teğet noktası
    public Vector3D? FindTangent(Vector3D cursor, Vector3D? fromPoint = null)
    {
        if (!EnableTangent || !fromPoint.HasValue) return null;

        foreach (var ent in GetEntitiesNear(cursor))
        {
            if (ent is CircleEntity circle)
            {
                var tangent = TangentPoint(fromPoint.Value, circle.Center, circle.Radius);
                if (tangent.HasValue && (tangent.Value - cursor).Length() < _worldAperture)
                    return tangent;
            }
        }
        return null;
    }

    // Extension snap — çizgi uzantısı üzerindeki nokta
    public Vector3D? FindExtension(Vector3D cursor)
    {
        if (!EnableExtension) return null;

        foreach (var ent in GetEntitiesNear(cursor))
        {
            if (ent is LineEntity line)
            {
                var dir = (line.EndPoint - line.StartPoint).Normalize();
                var ext = ProjectOntoLine(line.EndPoint, dir, cursor);
                double distToEnd = (ext - line.EndPoint).Length();
                if (distToEnd < _worldAperture * 3 && distToEnd > 10)
                {
                    double dotForward = (ext - line.EndPoint).X * dir.X + (ext - line.EndPoint).Y * dir.Y;
                    if (dotForward > 0) return ext;
                }
            }
        }
        return null;
    }

    // Tüm snap modlarını dene ve en yakın sonucu döndür
    public SnapResult? FindBestSnap(Vector3D cursor, Vector3D? fromPoint = null)
    {
        var candidates = new List<SnapResult>();

        var intersection = FindIntersection(cursor);
        if (intersection.HasValue) candidates.Add(new SnapResult(intersection.Value, "Intersection", "✕"));

        var quadrant = FindQuadrant(cursor);
        if (quadrant.HasValue) candidates.Add(new SnapResult(quadrant.Value, "Quadrant", "◇"));

        var tangent = FindTangent(cursor, fromPoint);
        if (tangent.HasValue) candidates.Add(new SnapResult(tangent.Value, "Tangent", "○"));

        var extension = FindExtension(cursor);
        if (extension.HasValue) candidates.Add(new SnapResult(extension.Value, "Extension", "→"));

        var nearest = FindNearest(cursor);
        if (nearest.HasValue) candidates.Add(new SnapResult(nearest.Value, "Nearest", "×"));

        if (candidates.Count == 0) return null;
        return candidates.OrderBy(c => (c.Point - cursor).Length()).First();
    }

    // ═══ Yardımcı Geometri ═══

    private IEnumerable<CadEntity> GetEntitiesNear(Vector3D point)
    {
        return _database.GetAllEntities().Where(e =>
        {
            var bbox = e.GetBoundingBox();
            return Math.Abs(bbox.Center.X - point.X) < _worldAperture * 5 &&
                   Math.Abs(bbox.Center.Y - point.Y) < _worldAperture * 5;
        });
    }

    private Vector3D? LineLineIntersection(Vector3D p1, Vector3D p2, Vector3D p3, Vector3D p4)
    {
        double d = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
        if (Math.Abs(d) < 1e-9) return null;
        double ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / d;
        double ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / d;
        if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            return new Vector3D(p1.X + ua * (p2.X - p1.X), p1.Y + ua * (p2.Y - p1.Y), 0);
        return null;
    }

    private Vector3D NearestPointOnSegment(Vector3D a, Vector3D b, Vector3D p)
    {
        var ab = b - a;
        double lenSq = ab.X * ab.X + ab.Y * ab.Y;
        if (lenSq < 1e-10) return a;
        double t = Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / lenSq, 0, 1);
        return new Vector3D(a.X + t * ab.X, a.Y + t * ab.Y, 0);
    }

    private Vector3D NearestPointOnCircle(Vector3D center, double radius, Vector3D point)
    {
        var dir = point - center;
        double dist = dir.Length();
        if (dist < 1e-10) return new Vector3D(center.X + radius, center.Y, 0);
        return center + dir * (radius / dist);
    }

    private Vector3D? NearestPointOnArc(ArcEntity arc, Vector3D point)
    {
        var dir = point - arc.Center;
        double angle = Math.Atan2(dir.Y, dir.X);
        if (angle < 0) angle += 2 * Math.PI;
        double start = arc.StartAngle < 0 ? arc.StartAngle + 2 * Math.PI : arc.StartAngle;
        double end = arc.EndAngle < 0 ? arc.EndAngle + 2 * Math.PI : arc.EndAngle;
        if (end < start) end += 2 * Math.PI;
        if (angle >= start && angle <= end)
            return NearestPointOnCircle(arc.Center, arc.Radius, point);
        return null;
    }

    private Vector3D? TangentPoint(Vector3D from, Vector3D center, double radius)
    {
        double dx = from.X - center.X;
        double dy = from.Y - center.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= radius) return null;
        double angle = Math.Acos(radius / dist);
        double baseAngle = Math.Atan2(dy, dx);
        return new Vector3D(center.X + radius * Math.Cos(baseAngle + angle), center.Y + radius * Math.Sin(baseAngle + angle), 0);
    }

    private Vector3D ProjectOntoLine(Vector3D origin, Vector3D direction, Vector3D point)
    {
        double t = (point.X - origin.X) * direction.X + (point.Y - origin.Y) * direction.Y;
        return new Vector3D(origin.X + t * direction.X, origin.Y + t * direction.Y, 0);
    }
}

public class SnapResult
{
    public Vector3D Point { get; set; }
    public string SnapType { get; set; }
    public string Symbol { get; set; }

    public SnapResult(Vector3D point, string snapType, string symbol)
    {
        Point = point; SnapType = snapType; Symbol = symbol;
    }
}
