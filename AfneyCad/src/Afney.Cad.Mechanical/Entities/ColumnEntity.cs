using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Entities;

public enum ColumnShape { Rectangular, Circular }
public enum ColumnMaterial { ReinforcedConcrete, Steel, Wood, Composite }

public class ColumnEntity : MechanicalEntity
{
    public Vector3D Position     { get; set; }
    public ColumnShape Shape     { get; set; } = ColumnShape.Rectangular;
    public ColumnMaterial Material { get; set; } = ColumnMaterial.ReinforcedConcrete;
    public double WidthMm        { get; set; } = 300;
    public double DepthMm        { get; set; } = 300;
    public double DiameterMm     { get; set; } = 400;
    public double HeightMm       { get; set; } = 3000;
    public double Rotation       { get; set; } = 0;
    public int FloorIndex        { get; set; } = 0;

    public ColumnEntity(Vector3D position, double width = 300, double depth = 300)
    {
        Position = position;
        WidthMm = width;
        DepthMm = depth;
        Shape = ColumnShape.Rectangular;
        Layer = "KOLON";
        Color = 0xFF888888;
    }

    public double GetCrossSectionAreaM2() => Shape == ColumnShape.Circular
        ? Math.PI * Math.Pow(DiameterMm / 2000.0, 2)
        : (WidthMm / 1000.0) * (DepthMm / 1000.0);

    public double GetVolumeM3() => GetCrossSectionAreaM2() * (HeightMm / 1000.0);
    public string GetSizeText() => Shape == ColumnShape.Circular ? $"D{DiameterMm:F0}" : $"{WidthMm:F0}x{DepthMm:F0}";

    public override void Draw(IRenderContext ctx)
    {
        uint c = IsSelected ? 0xFFFFFFFF : Color;
        double t = IsSelected ? 2.5 : 1.5;

        if (Shape == ColumnShape.Circular)
        {
            ctx.DrawCircle(Position, DiameterMm / 2.0, c, t);
            ctx.DrawLine(new Vector3D(Position.X - DiameterMm / 2, Position.Y, 0),
                         new Vector3D(Position.X + DiameterMm / 2, Position.Y, 0), c, t * 0.3);
            ctx.DrawLine(new Vector3D(Position.X, Position.Y - DiameterMm / 2, 0),
                         new Vector3D(Position.X, Position.Y + DiameterMm / 2, 0), c, t * 0.3);
        }
        else
        {
            double cos = Math.Cos(Rotation), sin = Math.Sin(Rotation);
            double hw = WidthMm / 2, hd = DepthMm / 2;
            Vector3D Tr(double x, double y) => new(Position.X + x * cos - y * sin, Position.Y + x * sin + y * cos, 0);

            var p1 = Tr(-hw, -hd); var p2 = Tr(hw, -hd);
            var p3 = Tr(hw, hd);   var p4 = Tr(-hw, hd);
            ctx.DrawLine(p1, p2, c, t); ctx.DrawLine(p2, p3, c, t);
            ctx.DrawLine(p3, p4, c, t); ctx.DrawLine(p4, p1, c, t);
            ctx.DrawLine(p1, p3, c, t * 0.3); ctx.DrawLine(p2, p4, c, t * 0.3);
        }

        if (IsSelected)
            ctx.DrawText($"Kolon {GetSizeText()}", new Vector3D(Position.X, Position.Y + (Shape == ColumnShape.Circular ? DiameterMm / 2 + 100 : DepthMm / 2 + 100), 0), 0, 100, 0xFFFFAA00);
    }

    public override List<MechanicalPort> GetPorts() => new();
    public override double DistanceTo(Vector3D p) => Position.DistanceTo(p);

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double r = Shape == ColumnShape.Circular ? DiameterMm / 2 : Math.Max(WidthMm, DepthMm) / 2;
        return new CadBoundingBox(new Vector3D(Position.X - r, Position.Y - r, 0), new Vector3D(Position.X + r, Position.Y + r, 0));
    }

    public override void Move(Vector3D delta) { Position += delta; InvalidateCache(); }
    public override void Transform(Matrix4x4 matrix) { Position = matrix.Transform(Position); InvalidateCache(); }

    public override CadEntity Clone()
    {
        var c = new ColumnEntity(Position, WidthMm, DepthMm) { Shape = Shape, DiameterMm = DiameterMm, HeightMm = HeightMm, Material = Material, Rotation = Rotation, FloorIndex = FloorIndex };
        CopyBaseProperties(c); return c;
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
    }

    public override IEnumerable<Vector3D> GetGripPoints() { yield return Position; }

    public override void MoveGripPointAt(int index, Vector3D pos) { Position = pos; InvalidateCache(); base.MoveGripPointAt(index, pos); }
}
