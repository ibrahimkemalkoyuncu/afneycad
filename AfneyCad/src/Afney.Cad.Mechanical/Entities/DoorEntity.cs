using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Entities;

public enum DoorType { Single, Double, Sliding, Revolving, Fire }

public class DoorEntity : MechanicalEntity
{
    public Vector3D Position     { get; set; }
    public double WidthMm        { get; set; } = 900;
    public double HeightMm       { get; set; } = 2100;
    public double Rotation       { get; set; } = 0;
    public DoorType Type         { get; set; } = DoorType.Single;
    public bool OpensInward      { get; set; } = true;

    public DoorEntity(Vector3D position, double width = 900, double height = 2100)
    {
        Position = position; WidthMm = width; HeightMm = height;
        Layer = "KAPI"; Color = 0xFFDEB887;
    }

    public string GetTypeText() => Type switch
    {
        DoorType.Single => "Tek Kanat", DoorType.Double => "Cift Kanat",
        DoorType.Sliding => "Surme", DoorType.Revolving => "Doner",
        DoorType.Fire => "Yangin", _ => "Kapi"
    };

    public override void Draw(IRenderContext ctx)
    {
        uint c = IsSelected ? 0xFFFFFFFF : Color;
        double t = IsSelected ? 2.0 : 1.0;
        double cos = Math.Cos(Rotation), sin = Math.Sin(Rotation);
        double hw = WidthMm / 2;

        Vector3D Tr(double x, double y) => new(Position.X + x * cos - y * sin, Position.Y + x * sin + y * cos, 0);

        var left = Tr(-hw, 0); var right = Tr(hw, 0);
        ctx.DrawLine(left, right, c, t * 2);

        if (Type == DoorType.Single || Type == DoorType.Fire)
        {
            double swingDir = OpensInward ? 1 : -1;
            var arcEnd = Tr(0, hw * swingDir);
            var hinge = OpensInward ? left : right;
            ctx.DrawArc(hinge, hw, OpensInward ? 0 : Math.PI, OpensInward ? Math.PI / 2 : Math.PI * 1.5, c, t * 0.5);
            ctx.DrawLine(hinge, arcEnd, c, t * 0.5);
        }
        else if (Type == DoorType.Double)
        {
            var mid = Tr(0, 0);
            ctx.DrawArc(left, hw / 2, 0, Math.PI / 2, c, t * 0.5);
            ctx.DrawArc(right, hw / 2, Math.PI / 2, Math.PI, c, t * 0.5);
        }
        else if (Type == DoorType.Sliding)
        {
            ctx.DrawLine(left, right, c, t);
            ctx.DrawLine(Tr(-hw * 0.3, 15), Tr(hw * 0.3, 15), c, t * 0.5);
            ctx.DrawLine(Tr(hw * 0.3, 15), Tr(hw * 0.15, 25), c, t * 0.5);
        }

        if (IsSelected)
            ctx.DrawText($"{GetTypeText()} {WidthMm:F0}x{HeightMm:F0}", new Vector3D(Position.X, Position.Y + hw + 80, 0), 0, 80, 0xFFDEB887);
    }

    public override List<MechanicalPort> GetPorts() => new();
    public override double DistanceTo(Vector3D p) => Position.DistanceTo(p);
    protected override CadBoundingBox CalculateBoundingBox()
    {
        double r = WidthMm;
        return new CadBoundingBox(new Vector3D(Position.X - r, Position.Y - r, 0), new Vector3D(Position.X + r, Position.Y + r, 0));
    }
    public override void Move(Vector3D delta) { Position += delta; InvalidateCache(); }
    public override void Transform(Matrix4x4 matrix) { Position = matrix.Transform(Position); InvalidateCache(); }
    public override CadEntity Clone()
    {
        var d = new DoorEntity(Position, WidthMm, HeightMm) { Type = Type, Rotation = Rotation, OpensInward = OpensInward };
        CopyBaseProperties(d); return d;
    }
    public override IEnumerable<SnapPoint> GetSnapPoints() { yield return new SnapPoint(Position, SnapPointType.Center); }
    public override IEnumerable<Vector3D> GetGripPoints() { yield return Position; }
    public override void MoveGripPointAt(int index, Vector3D pos) { Position = pos; InvalidateCache(); base.MoveGripPointAt(index, pos); }
}

public enum WindowType { Casement, Sliding, Fixed, Awning }

public class WindowEntity : MechanicalEntity
{
    public Vector3D Position     { get; set; }
    public double WidthMm        { get; set; } = 1200;
    public double HeightMm       { get; set; } = 1500;
    public double SillHeightMm   { get; set; } = 900;
    public double Rotation       { get; set; } = 0;
    public WindowType Type       { get; set; } = WindowType.Casement;
    public int PaneCount         { get; set; } = 2;

    public WindowEntity(Vector3D position, double width = 1200, double height = 1500)
    {
        Position = position; WidthMm = width; HeightMm = height;
        Layer = "PENCERE"; Color = 0xFF4FC3F7;
    }

    public string GetTypeText() => Type switch
    {
        WindowType.Casement => "Kanatli", WindowType.Sliding => "Surme",
        WindowType.Fixed => "Sabit", WindowType.Awning => "Vasistas", _ => "Pencere"
    };

    public override void Draw(IRenderContext ctx)
    {
        uint c = IsSelected ? 0xFFFFFFFF : Color;
        double t = IsSelected ? 2.0 : 1.0;
        double cos = Math.Cos(Rotation), sin = Math.Sin(Rotation);
        double hw = WidthMm / 2;

        Vector3D Tr(double x, double y) => new(Position.X + x * cos - y * sin, Position.Y + x * sin + y * cos, 0);

        var left = Tr(-hw, 0); var right = Tr(hw, 0);
        ctx.DrawLine(left, right, c, t * 2);

        double wallThick = 30;
        ctx.DrawLine(Tr(-hw, -wallThick), Tr(-hw, wallThick), c, t);
        ctx.DrawLine(Tr(hw, -wallThick), Tr(hw, wallThick), c, t);

        if (PaneCount >= 2)
        {
            var mid = Tr(0, 0);
            ctx.DrawLine(Tr(0, -wallThick), Tr(0, wallThick), c, t * 0.5);
        }

        ctx.DrawLine(Tr(-hw, -wallThick), Tr(hw, -wallThick), c, t * 0.5);
        ctx.DrawLine(Tr(-hw, wallThick), Tr(hw, wallThick), c, t * 0.5);

        if (IsSelected)
            ctx.DrawText($"{GetTypeText()} {WidthMm:F0}x{HeightMm:F0}", new Vector3D(Position.X, Position.Y + wallThick + 80, 0), 0, 80, 0xFF4FC3F7);
    }

    public override List<MechanicalPort> GetPorts() => new();
    public override double DistanceTo(Vector3D p) => Position.DistanceTo(p);
    protected override CadBoundingBox CalculateBoundingBox()
    {
        double r = WidthMm;
        return new CadBoundingBox(new Vector3D(Position.X - r, Position.Y - r, 0), new Vector3D(Position.X + r, Position.Y + r, 0));
    }
    public override void Move(Vector3D delta) { Position += delta; InvalidateCache(); }
    public override void Transform(Matrix4x4 matrix) { Position = matrix.Transform(Position); InvalidateCache(); }
    public override CadEntity Clone()
    {
        var w = new WindowEntity(Position, WidthMm, HeightMm) { Type = Type, Rotation = Rotation, SillHeightMm = SillHeightMm, PaneCount = PaneCount };
        CopyBaseProperties(w); return w;
    }
    public override IEnumerable<SnapPoint> GetSnapPoints() { yield return new SnapPoint(Position, SnapPointType.Center); }
    public override IEnumerable<Vector3D> GetGripPoints() { yield return Position; }
    public override void MoveGripPointAt(int index, Vector3D pos) { Position = pos; InvalidateCache(); base.MoveGripPointAt(index, pos); }
}
