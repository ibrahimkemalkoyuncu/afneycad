using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

public enum HatchPatternType
{
    Solid,
    Concrete,
    Earth,
    Water,
    Brick,
    Insulation,
    Steel,
    Sand,
    CrossHatch,
    Diagonal
}

public class HatchPatternService
{
    private const string Layer = "HATCH";

    public static readonly Dictionary<HatchPatternType, (string Name, double Angle, double Spacing)> Patterns = new()
    {
        [HatchPatternType.Solid]       = ("SOLID",       0,   0),
        [HatchPatternType.Concrete]    = ("BETON",      45,   2.0),
        [HatchPatternType.Earth]       = ("TOPRAK",      0,   3.0),
        [HatchPatternType.Water]       = ("SU",          0,   4.0),
        [HatchPatternType.Brick]       = ("TUGLA",       0,   2.5),
        [HatchPatternType.Insulation]  = ("YALITIM",    45,   3.0),
        [HatchPatternType.Steel]       = ("CELIK",      45,   1.5),
        [HatchPatternType.Sand]        = ("KUM",         0,   5.0),
        [HatchPatternType.CrossHatch]  = ("CAPRAZ",      0,   3.0),
        [HatchPatternType.Diagonal]    = ("DIYAGONAL",  45,   3.0),
    };

    public List<CadEntity> GeneratePattern(List<Vector3D> boundary, HatchPatternType type, double scale = 1.0)
    {
        var entities = new List<CadEntity>();
        if (boundary.Count < 3) return entities;

        var (_, angle, spacing) = Patterns[type];
        spacing *= scale;

        if (type == HatchPatternType.Solid)
        {
            entities.Add(new HatchEntity(boundary, 0x808080, 80) { Layer = Layer });
            return entities;
        }

        var bb = GetBounds(boundary);
        double rad = angle * Math.PI / 180.0;
        uint color = GetPatternColor(type);

        entities.Add(new HatchEntity(boundary, color & 0x00FFFFFF, (byte)((color >> 24) & 0xFF)) { Layer = Layer });

        var lines = GenerateHatchLines(bb, rad, spacing, boundary);
        foreach (var (p1, p2) in lines)
            entities.Add(new LineEntity(p1, p2) { Color = color, Layer = Layer, Linetype = "Continuous" });

        if (type == HatchPatternType.CrossHatch)
        {
            var crossLines = GenerateHatchLines(bb, rad + Math.PI / 2, spacing, boundary);
            foreach (var (p1, p2) in crossLines)
                entities.Add(new LineEntity(p1, p2) { Color = color, Layer = Layer });
        }

        return entities;
    }

    private static List<(Vector3D, Vector3D)> GenerateHatchLines(
        (double minX, double minY, double maxX, double maxY) bb,
        double angle, double spacing, List<Vector3D> boundary)
    {
        var lines = new List<(Vector3D, Vector3D)>();
        if (spacing < 0.01) return lines;

        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double perpX = -sin;
        double perpY = cos;

        double diagLen = Math.Sqrt(Math.Pow(bb.maxX - bb.minX, 2) + Math.Pow(bb.maxY - bb.minY, 2));
        double cx = (bb.minX + bb.maxX) / 2;
        double cy = (bb.minY + bb.maxY) / 2;

        int count = (int)(diagLen / spacing) + 2;
        for (int i = -count; i <= count; i++)
        {
            double offset = i * spacing;
            double baseX = cx + perpX * offset;
            double baseY = cy + perpY * offset;

            var p1 = new Vector3D(baseX - cos * diagLen, baseY - sin * diagLen, 0);
            var p2 = new Vector3D(baseX + cos * diagLen, baseY + sin * diagLen, 0);

            var clipped = ClipLineToBoundary(p1, p2, boundary);
            if (clipped != null)
                lines.Add(clipped.Value);
        }
        return lines;
    }

    private static (Vector3D, Vector3D)? ClipLineToBoundary(Vector3D p1, Vector3D p2, List<Vector3D> boundary)
    {
        var intersections = new List<double>();
        for (int i = 0; i < boundary.Count; i++)
        {
            int j = (i + 1) % boundary.Count;
            var t = LineIntersect(p1, p2, boundary[i], boundary[j]);
            if (t.HasValue) intersections.Add(t.Value);
        }
        if (intersections.Count < 2) return null;
        intersections.Sort();
        double t1 = intersections[0], t2 = intersections[^1];
        var dx = p2.X - p1.X; var dy = p2.Y - p1.Y;
        return (new Vector3D(p1.X + dx * t1, p1.Y + dy * t1, 0),
                new Vector3D(p1.X + dx * t2, p1.Y + dy * t2, 0));
    }

    private static double? LineIntersect(Vector3D a1, Vector3D a2, Vector3D b1, Vector3D b2)
    {
        double d = (a2.X - a1.X) * (b2.Y - b1.Y) - (a2.Y - a1.Y) * (b2.X - b1.X);
        if (Math.Abs(d) < 1e-12) return null;
        double t = ((b1.X - a1.X) * (b2.Y - b1.Y) - (b1.Y - a1.Y) * (b2.X - b1.X)) / d;
        double u = ((b1.X - a1.X) * (a2.Y - a1.Y) - (b1.Y - a1.Y) * (a2.X - a1.X)) / d;
        if (u < 0 || u > 1) return null;
        return t;
    }

    private static (double minX, double minY, double maxX, double maxY) GetBounds(List<Vector3D> pts)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in pts)
        {
            if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
        }
        return (minX, minY, maxX, maxY);
    }

    private static uint GetPatternColor(HatchPatternType type) => type switch
    {
        HatchPatternType.Concrete   => 0x40888888,
        HatchPatternType.Earth      => 0x408B6914,
        HatchPatternType.Water      => 0x400088FF,
        HatchPatternType.Brick      => 0x40CC4400,
        HatchPatternType.Insulation => 0x40FF69B4,
        HatchPatternType.Steel      => 0x40C0C0C0,
        HatchPatternType.Sand       => 0x40DEB887,
        HatchPatternType.CrossHatch => 0x40666666,
        HatchPatternType.Diagonal   => 0x40999999,
        _                           => 0x40808080,
    };
}
