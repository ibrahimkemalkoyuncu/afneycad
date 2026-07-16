using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Entities.Annotation;

public enum DimensionType { Linear, Aligned, Radius, Angular }

public class DimensionEntity : CadEntity
{
    public Vector3D FirstPoint   { get; set; }
    public Vector3D SecondPoint  { get; set; }
    public Vector3D DimLinePoint { get; set; }
    public DimensionType DimType { get; set; } = DimensionType.Linear;
    public double TextHeight     { get; set; } = 250.0;

    // NE: Ölçü stili parametreleri (DimensionStyleService ile eşleşir)
    // NEDEN: Önceden ok boyu/uzatma boşluğu/aşımı hep TextHeight'ın sabit oranıydı (0.8/0.2/0.3),
    //        stil tanımlarından bağımsız çalışıyorlardı. Artık her biri ayrı ayarlanabilir.
    public double ArrowSize   { get; set; } = 200.0;
    public double ExtLineGap  { get; set; } = 50.0;
    public double ExtLineOver { get; set; } = 75.0;
    public int    Precision   { get; set; } = 0;
    public bool   ShowUnits   { get; set; } = true;
    public string UnitFormat  { get; set; } = "mm";

    public DimensionEntity(Vector3D p1, Vector3D p2, Vector3D dimLinePoint, DimensionType type)
    {
        FirstPoint   = p1;
        SecondPoint  = p2;
        DimLinePoint = dimLinePoint;
        DimType      = type;
        Color        = 0xFF00CCFF;
    }

    private bool IsHorizontal =>
        Math.Abs(SecondPoint.X - FirstPoint.X) >= Math.Abs(SecondPoint.Y - FirstPoint.Y);

    public Vector3D AngularVertex { get; set; }

    private double GetMeasurement() => DimType switch
    {
        DimensionType.Linear  => IsHorizontal
                                    ? Math.Abs(SecondPoint.X - FirstPoint.X)
                                    : Math.Abs(SecondPoint.Y - FirstPoint.Y),
        DimensionType.Aligned => (SecondPoint - FirstPoint).Length(),
        DimensionType.Radius  => (SecondPoint - FirstPoint).Length(),
        DimensionType.Angular => GetAngleDegrees(),
        _                     => 0
    };

    private double GetAngleDegrees()
    {
        var v1 = FirstPoint - AngularVertex;
        var v2 = SecondPoint - AngularVertex;
        double dot   = v1.X * v2.X + v1.Y * v2.Y;
        double cross = v1.X * v2.Y - v1.Y * v2.X;
        double rad   = Math.Atan2(Math.Abs(cross), dot);
        return rad * 180.0 / Math.PI;
    }

    public string GetDxfText() => GetText();

    private string GetText()
    {
        double m = GetMeasurement();
        if (DimType == DimensionType.Angular)
            return $"{m.ToString("F" + Precision)}°";

        string prefix = DimType == DimensionType.Radius ? "R " : "";
        if (!ShowUnits)
            return $"{prefix}{m.ToString("F" + Precision)}";

        return UnitFormat switch
        {
            "m"  => $"{prefix}{(m / 1000.0).ToString("F" + Precision)} m",
            "cm" => $"{prefix}{(m / 10.0).ToString("F" + Precision)} cm",
            _    => m >= 1000
                        ? $"{prefix}{(m / 1000.0).ToString("F" + Math.Max(Precision, 2))} m"
                        : $"{prefix}{m.ToString("F" + Precision)} mm"
        };
    }

    public override void Draw(IRenderContext ctx)
    {
        switch (DimType)
        {
            case DimensionType.Linear:  DrawLinear(ctx);  break;
            case DimensionType.Aligned: DrawAligned(ctx); break;
            case DimensionType.Radius:  DrawRadius(ctx);  break;
            case DimensionType.Angular: DrawAngular(ctx); break;
        }
    }

    private void DrawLinear(IRenderContext ctx)
    {
        double arrow = ArrowSize;
        double gap   = ExtLineGap;
        double over  = ExtLineOver;

        if (IsHorizontal)
        {
            double dimY = DimLinePoint.Y;
            double x1 = FirstPoint.X,  y1 = FirstPoint.Y;
            double x2 = SecondPoint.X, y2 = SecondPoint.Y;

            ctx.DrawLine(new Vector3D(x1, dimY, 0), new Vector3D(x2, dimY, 0), Color, 0);

            double s1 = dimY >= y1 ? 1 : -1;
            double s2 = dimY >= y2 ? 1 : -1;
            ctx.DrawLine(new Vector3D(x1, y1 + s1 * gap, 0), new Vector3D(x1, dimY + s1 * over, 0), Color, 0);
            ctx.DrawLine(new Vector3D(x2, y2 + s2 * gap, 0), new Vector3D(x2, dimY + s2 * over, 0), Color, 0);

            DrawArrow(ctx, new Vector3D(x1, dimY, 0), new Vector3D(x2, dimY, 0), arrow);
            DrawArrow(ctx, new Vector3D(x2, dimY, 0), new Vector3D(x1, dimY, 0), arrow);

            ctx.DrawText(GetText(), new Vector3D((x1 + x2) / 2, dimY + TextHeight * 0.6 * s1, 0), 0, TextHeight, Color);
        }
        else
        {
            double dimX = DimLinePoint.X;
            double x1 = FirstPoint.X,  y1 = FirstPoint.Y;
            double x2 = SecondPoint.X, y2 = SecondPoint.Y;

            ctx.DrawLine(new Vector3D(dimX, y1, 0), new Vector3D(dimX, y2, 0), Color, 0);

            double s1 = dimX >= x1 ? 1 : -1;
            double s2 = dimX >= x2 ? 1 : -1;
            ctx.DrawLine(new Vector3D(x1 + s1 * gap, y1, 0), new Vector3D(dimX + s1 * over, y1, 0), Color, 0);
            ctx.DrawLine(new Vector3D(x2 + s2 * gap, y2, 0), new Vector3D(dimX + s2 * over, y2, 0), Color, 0);

            DrawArrow(ctx, new Vector3D(dimX, y1, 0), new Vector3D(dimX, y2, 0), arrow);
            DrawArrow(ctx, new Vector3D(dimX, y2, 0), new Vector3D(dimX, y1, 0), arrow);

            ctx.DrawText(GetText(), new Vector3D(dimX + TextHeight * 0.6 * s1, (y1 + y2) / 2, 0), 90, TextHeight, Color);
        }
    }

    private void DrawAligned(IRenderContext ctx)
    {
        double arrow = ArrowSize;
        double gap   = ExtLineGap;
        double over  = ExtLineOver;

        var seg = SecondPoint - FirstPoint;
        double len = seg.Length();
        if (len < 1e-9) return;

        var dir  = new Vector3D(seg.X / len, seg.Y / len, 0);
        var perp = new Vector3D(-dir.Y, dir.X, 0);

        var dp     = DimLinePoint - FirstPoint;
        double off = dp.X * perp.X + dp.Y * perp.Y;
        double s   = off >= 0 ? 1 : -1;

        var dimP1 = new Vector3D(FirstPoint.X  + perp.X * off, FirstPoint.Y  + perp.Y * off, 0);
        var dimP2 = new Vector3D(SecondPoint.X + perp.X * off, SecondPoint.Y + perp.Y * off, 0);

        ctx.DrawLine(dimP1, dimP2, Color, 0);

        ctx.DrawLine(
            new Vector3D(FirstPoint.X  + perp.X * gap * s, FirstPoint.Y  + perp.Y * gap * s, 0),
            new Vector3D(dimP1.X + perp.X * over * s, dimP1.Y + perp.Y * over * s, 0), Color, 0);
        ctx.DrawLine(
            new Vector3D(SecondPoint.X + perp.X * gap * s, SecondPoint.Y + perp.Y * gap * s, 0),
            new Vector3D(dimP2.X + perp.X * over * s, dimP2.Y + perp.Y * over * s, 0), Color, 0);

        DrawArrow(ctx, dimP1, dimP2, arrow);
        DrawArrow(ctx, dimP2, dimP1, arrow);

        double angle   = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;
        var    mid     = new Vector3D((dimP1.X + dimP2.X) / 2, (dimP1.Y + dimP2.Y) / 2, 0);
        var    textPos = new Vector3D(mid.X + perp.X * TextHeight * 0.6 * s, mid.Y + perp.Y * TextHeight * 0.6 * s, 0);
        ctx.DrawText(GetText(), textPos, angle, TextHeight, Color);
    }

    private void DrawRadius(IRenderContext ctx)
    {
        double arrow = ArrowSize;

        var dir = SecondPoint - FirstPoint;
        double len = dir.Length();
        if (len < 1e-9) return;
        var norm = new Vector3D(dir.X / len, dir.Y / len, 0);

        ctx.DrawLine(FirstPoint, SecondPoint, Color, 0);
        DrawArrow(ctx, SecondPoint, FirstPoint, arrow);
        ctx.DrawCircle(FirstPoint, TextHeight * 0.2, Color, 0);

        var textPos = new Vector3D(SecondPoint.X + norm.X * TextHeight * 1.5, SecondPoint.Y + norm.Y * TextHeight * 1.5, 0);
        ctx.DrawText(GetText(), textPos, 0, TextHeight, Color);
    }

    private void DrawAngular(IRenderContext ctx)
    {
        var v1 = FirstPoint - AngularVertex;
        var v2 = SecondPoint - AngularVertex;
        double len1 = v1.Length();
        double len2 = v2.Length();
        if (len1 < 1e-9 || len2 < 1e-9) return;

        double arcRadius = Math.Min(len1, len2) * 0.6;
        double startAngle = Math.Atan2(v1.Y, v1.X) * 180.0 / Math.PI;
        double endAngle   = Math.Atan2(v2.Y, v2.X) * 180.0 / Math.PI;

        if (startAngle < 0) startAngle += 360;
        if (endAngle   < 0) endAngle   += 360;

        double sweep = endAngle - startAngle;
        if (sweep < 0) sweep += 360;
        if (sweep > 180) { sweep = 360 - sweep; (startAngle, endAngle) = (endAngle, startAngle); }

        ctx.DrawLine(AngularVertex, FirstPoint, Color, 0);
        ctx.DrawLine(AngularVertex, SecondPoint, Color, 0);
        ctx.DrawArc(AngularVertex, arcRadius, startAngle, startAngle + sweep, Color, 0);

        double midAngleRad = (startAngle + sweep / 2) * Math.PI / 180.0;
        var textPos = new Vector3D(
            AngularVertex.X + Math.Cos(midAngleRad) * arcRadius * 1.3,
            AngularVertex.Y + Math.Sin(midAngleRad) * arcRadius * 1.3, 0);
        ctx.DrawText(GetText(), textPos, 0, TextHeight, Color);
    }

    private void DrawArrow(IRenderContext ctx, Vector3D tip, Vector3D from, double size)
    {
        var seg = from - tip;
        double len = seg.Length();
        if (len < 1e-9) return;
        var dir  = new Vector3D(seg.X / len, seg.Y / len, 0);
        var perp = new Vector3D(-dir.Y, dir.X, 0);

        var b1 = new Vector3D(tip.X + dir.X * size + perp.X * size * 0.3,
                               tip.Y + dir.Y * size + perp.Y * size * 0.3, 0);
        var b2 = new Vector3D(tip.X + dir.X * size - perp.X * size * 0.3,
                               tip.Y + dir.Y * size - perp.Y * size * 0.3, 0);
        ctx.DrawFilledPolygon(new[] { tip, b1, b2 }, Color);
    }

    public override void Move(Vector3D delta)
    {
        FirstPoint   = new Vector3D(FirstPoint.X   + delta.X, FirstPoint.Y   + delta.Y, FirstPoint.Z   + delta.Z);
        SecondPoint  = new Vector3D(SecondPoint.X  + delta.X, SecondPoint.Y  + delta.Y, SecondPoint.Z  + delta.Z);
        DimLinePoint = new Vector3D(DimLinePoint.X + delta.X, DimLinePoint.Y + delta.Y, DimLinePoint.Z + delta.Z);
        InvalidateCache();
    }

    public override void Transform(Matrix4x4 matrix)
    {
        FirstPoint   = matrix.Transform(FirstPoint);
        SecondPoint  = matrix.Transform(SecondPoint);
        DimLinePoint = matrix.Transform(DimLinePoint);
        InvalidateCache();
    }

    public override CadEntity Clone()
    {
        var c = new DimensionEntity(FirstPoint, SecondPoint, DimLinePoint, DimType)
        {
            TextHeight    = TextHeight,
            ArrowSize     = ArrowSize,
            ExtLineGap    = ExtLineGap,
            ExtLineOver   = ExtLineOver,
            Precision     = Precision,
            ShowUnits     = ShowUnits,
            UnitFormat    = UnitFormat,
            AngularVertex = AngularVertex
        };
        CopyBaseProperties(c);
        return c;
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double minX = Math.Min(FirstPoint.X, Math.Min(SecondPoint.X, DimLinePoint.X));
        double minY = Math.Min(FirstPoint.Y, Math.Min(SecondPoint.Y, DimLinePoint.Y));
        double maxX = Math.Max(FirstPoint.X, Math.Max(SecondPoint.X, DimLinePoint.X));
        double maxY = Math.Max(FirstPoint.Y, Math.Max(SecondPoint.Y, DimLinePoint.Y));
        return new CadBoundingBox(new Vector3D(minX, minY, 0), new Vector3D(maxX, maxY, 0));
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(FirstPoint,   SnapPointType.Endpoint);
        yield return new SnapPoint(SecondPoint,  SnapPointType.Endpoint);
        yield return new SnapPoint(DimLinePoint, SnapPointType.Midpoint);
    }

    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return FirstPoint;
        yield return SecondPoint;
        yield return DimLinePoint;
    }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if      (index == 0) FirstPoint   = newPosition;
        else if (index == 1) SecondPoint  = newPosition;
        else if (index == 2) DimLinePoint = newPosition;
        InvalidateCache();
        base.MoveGripPointAt(index, newPosition);
    }
}
