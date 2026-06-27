using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Entities;

public enum BeamMaterial { ReinforcedConcrete, Steel, Wood, Precast }

public class BeamEntity : MechanicalEntity
{
    public Vector3D StartPoint   { get; set; }
    public Vector3D EndPoint     { get; set; }
    public double WidthMm        { get; set; } = 250;
    public double HeightMm       { get; set; } = 500;
    public BeamMaterial Material { get; set; } = BeamMaterial.ReinforcedConcrete;
    public int FloorIndex        { get; set; } = 0;

    public BeamEntity(Vector3D start, Vector3D end, double width = 250, double height = 500)
    {
        StartPoint = start; EndPoint = end; WidthMm = width; HeightMm = height;
        Layer = "KIRIS"; Color = 0xFF666699;
    }

    public double GetLength() => (EndPoint - StartPoint).Length();
    public double GetLengthM() => GetLength() / 1000.0;
    public double GetVolumeM3() => GetLengthM() * (WidthMm / 1000.0) * (HeightMm / 1000.0);
    public string GetSizeText() => $"{WidthMm:F0}x{HeightMm:F0}";

    public override void Draw(IRenderContext ctx)
    {
        uint c = IsSelected ? 0xFFFFFFFF : Color;
        double t = IsSelected ? 2.5 : 1.5;
        var dir = EndPoint - StartPoint;
        double len = dir.Length();
        if (len < 1e-9) return;
        var norm = new Vector3D(-dir.Y / len, dir.X / len, 0);
        double hw = WidthMm / 2.0;

        var p1 = new Vector3D(StartPoint.X + norm.X * hw, StartPoint.Y + norm.Y * hw, 0);
        var p2 = new Vector3D(StartPoint.X - norm.X * hw, StartPoint.Y - norm.Y * hw, 0);
        var p3 = new Vector3D(EndPoint.X - norm.X * hw, EndPoint.Y - norm.Y * hw, 0);
        var p4 = new Vector3D(EndPoint.X + norm.X * hw, EndPoint.Y + norm.Y * hw, 0);

        ctx.DrawLine(p1, p4, c, t); ctx.DrawLine(p2, p3, c, t);
        ctx.DrawLine(p1, p2, c, t); ctx.DrawLine(p3, p4, c, t);
        ctx.DrawLine(StartPoint, EndPoint, c, t * 0.3, "Dashed", true);

        if (IsSelected)
        {
            var mid = new Vector3D((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, 0);
            ctx.DrawText($"Kiris {GetSizeText()}", mid, 0, 100, 0xFF9999FF);
        }
    }

    public override List<MechanicalPort> GetPorts() => new();
    public override double DistanceTo(Vector3D p)
    {
        var v = StartPoint; var w = EndPoint;
        double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
        if (l2 == 0) return p.DistanceTo(v);
        double t2 = Math.Clamp(((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2, 0, 1);
        return p.DistanceTo(new Vector3D(v.X + t2 * (w.X - v.X), v.Y + t2 * (w.Y - v.Y), 0));
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double pad = WidthMm / 2;
        return new CadBoundingBox(
            new Vector3D(Math.Min(StartPoint.X, EndPoint.X) - pad, Math.Min(StartPoint.Y, EndPoint.Y) - pad, 0),
            new Vector3D(Math.Max(StartPoint.X, EndPoint.X) + pad, Math.Max(StartPoint.Y, EndPoint.Y) + pad, 0));
    }

    public override void Move(Vector3D delta) { StartPoint += delta; EndPoint += delta; InvalidateCache(); }
    public override void Transform(Matrix4x4 matrix) { StartPoint = matrix.Transform(StartPoint); EndPoint = matrix.Transform(EndPoint); InvalidateCache(); }
    public override CadEntity Clone()
    {
        var c = new BeamEntity(StartPoint, EndPoint, WidthMm, HeightMm) { Material = Material, FloorIndex = FloorIndex };
        CopyBaseProperties(c); return c;
    }
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(StartPoint, SnapPointType.Endpoint);
        yield return new SnapPoint(EndPoint, SnapPointType.Endpoint);
    }
    public override IEnumerable<Vector3D> GetGripPoints() { yield return StartPoint; yield return EndPoint; }
    public override void MoveGripPointAt(int index, Vector3D pos)
    { if (index == 0) StartPoint = pos; else EndPoint = pos; InvalidateCache(); base.MoveGripPointAt(index, pos); }
}
