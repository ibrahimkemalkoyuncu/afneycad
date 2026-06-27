using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Entities;

public enum DuctShape { Rectangular, Circular }
public enum DuctType { Supply, Return, Exhaust, FreshAir, Smoke }

public class DuctEntity : MechanicalEntity
{
    public Vector3D StartPoint { get; set; }
    public Vector3D EndPoint   { get; set; }
    public DuctShape Shape     { get; set; } = DuctShape.Rectangular;
    public DuctType  Type      { get; set; } = DuctType.Supply;
    public double WidthMm      { get; set; } = 400;
    public double HeightMm     { get; set; } = 300;
    public double DiameterMm   { get; set; } = 315;
    public double InsulationMm { get; set; } = 25;
    public double AirFlowM3h   { get; set; }
    public double VelocityMs   { get; set; }

    public DuctEntity(Vector3D start, Vector3D end, double width, double height)
    {
        StartPoint = start;
        EndPoint = end;
        WidthMm = width;
        HeightMm = height;
        Shape = DuctShape.Rectangular;
        SystemType = MechanicalSystemType.Ventilation;
        Layer = "MEP_HAVALANDIRMA";
        Color = 0xFF2ECC71;
    }

    public DuctEntity(Vector3D start, Vector3D end, double diameter)
    {
        StartPoint = start;
        EndPoint = end;
        DiameterMm = diameter;
        Shape = DuctShape.Circular;
        SystemType = MechanicalSystemType.Ventilation;
        Layer = "MEP_HAVALANDIRMA";
        Color = 0xFF2ECC71;
    }

    public double GetLength() => (EndPoint - StartPoint).Length();

    public double GetCrossSectionArea() => Shape == DuctShape.Circular
        ? Math.PI * Math.Pow(DiameterMm / 2000.0, 2)
        : (WidthMm / 1000.0) * (HeightMm / 1000.0);

    public double GetPerimeter() => Shape == DuctShape.Circular
        ? Math.PI * DiameterMm
        : 2 * (WidthMm + HeightMm);

    public double GetInsulationArea()
    {
        double perimeterM = GetPerimeter() / 1000.0;
        double lengthM = GetLength() / 1000.0;
        return perimeterM * lengthM;
    }

    public string GetSizeText() => Shape == DuctShape.Circular
        ? $"D{DiameterMm:F0}"
        : $"{WidthMm:F0}x{HeightMm:F0}";

    public string GetTypeText() => Type switch
    {
        DuctType.Supply   => "Besleme",
        DuctType.Return   => "Donüs",
        DuctType.Exhaust  => "Egzoz",
        DuctType.FreshAir => "Taze Hava",
        DuctType.Smoke    => "Duman",
        _                 => "Kanal"
    };

    public override void Draw(IRenderContext ctx)
    {
        uint drawColor = IsSelected ? 0xFFFFFFFF : Color;
        double thick = IsSelected ? 2.0 : 1.0;

        if (Shape == DuctShape.Rectangular)
        {
            var dir = EndPoint - StartPoint;
            double len = dir.Length();
            if (len < 1e-9) return;
            var norm = new Vector3D(-dir.Y / len, dir.X / len, 0);
            double halfW = WidthMm / 2.0;

            var p1 = new Vector3D(StartPoint.X + norm.X * halfW, StartPoint.Y + norm.Y * halfW, 0);
            var p2 = new Vector3D(StartPoint.X - norm.X * halfW, StartPoint.Y - norm.Y * halfW, 0);
            var p3 = new Vector3D(EndPoint.X - norm.X * halfW, EndPoint.Y - norm.Y * halfW, 0);
            var p4 = new Vector3D(EndPoint.X + norm.X * halfW, EndPoint.Y + norm.Y * halfW, 0);

            ctx.DrawLine(p1, p4, drawColor, thick);
            ctx.DrawLine(p2, p3, drawColor, thick);
            ctx.DrawLine(p1, p2, drawColor, thick * 0.5);
            ctx.DrawLine(p3, p4, drawColor, thick * 0.5);

            ctx.DrawLine(StartPoint, EndPoint, drawColor, thick * 0.3, "Dashed", true);
        }
        else
        {
            ctx.DrawLine(StartPoint, EndPoint, drawColor, thick);
            ctx.DrawCircle(StartPoint, DiameterMm / 2.0, drawColor, thick * 0.5);
            ctx.DrawCircle(EndPoint, DiameterMm / 2.0, drawColor, thick * 0.5);
        }

        if (IsSelected)
        {
            var mid = new Vector3D((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, 0);
            ctx.DrawText($"{GetSizeText()} | {GetTypeText()}", mid, 0, 150, 0xFF2ECC71);
        }
    }

    public override double DistanceTo(Vector3D p)
    {
        var v = StartPoint; var w = EndPoint;
        double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
        if (l2 == 0) return p.DistanceTo(v);
        double t = Math.Clamp(((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2, 0, 1);
        var proj = new Vector3D(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y), 0);
        return p.DistanceTo(proj);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double pad = Shape == DuctShape.Circular ? DiameterMm / 2 : WidthMm / 2;
        return new CadBoundingBox(
            new Vector3D(Math.Min(StartPoint.X, EndPoint.X) - pad, Math.Min(StartPoint.Y, EndPoint.Y) - pad, 0),
            new Vector3D(Math.Max(StartPoint.X, EndPoint.X) + pad, Math.Max(StartPoint.Y, EndPoint.Y) + pad, 0));
    }

    public override void Move(Vector3D delta)
    {
        StartPoint = new Vector3D(StartPoint.X + delta.X, StartPoint.Y + delta.Y, StartPoint.Z + delta.Z);
        EndPoint = new Vector3D(EndPoint.X + delta.X, EndPoint.Y + delta.Y, EndPoint.Z + delta.Z);
        InvalidateCache();
    }

    public override void Transform(Matrix4x4 matrix)
    {
        StartPoint = matrix.Transform(StartPoint);
        EndPoint = matrix.Transform(EndPoint);
        InvalidateCache();
    }

    public override CadEntity Clone()
    {
        var clone = Shape == DuctShape.Circular
            ? new DuctEntity(StartPoint, EndPoint, DiameterMm)
            : new DuctEntity(StartPoint, EndPoint, WidthMm, HeightMm);
        clone.Type = Type;
        clone.InsulationMm = InsulationMm;
        clone.AirFlowM3h = AirFlowM3h;
        clone.VelocityMs = VelocityMs;
        CopyBaseProperties(clone);
        return clone;
    }

    public override List<MechanicalPort> GetPorts()
    {
        var ports = new List<MechanicalPort>();
        double dn = Shape == DuctShape.Circular ? DiameterMm : Math.Max(WidthMm, HeightMm);
        var dir = EndPoint - StartPoint;
        double len = dir.Length();
        var norm = len > 1e-9 ? new Vector3D(dir.X / len, dir.Y / len, 0) : Vector3D.XAxis;

        ports.Add(new MechanicalPort(Id, "Inlet", StartPoint, new Vector3D(-norm.X, -norm.Y, 0), dn, Enums.PipeMaterial.Generic));
        ports.Add(new MechanicalPort(Id, "Outlet", EndPoint, norm, dn, Enums.PipeMaterial.Generic));
        return ports;
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(StartPoint, SnapPointType.Endpoint);
        yield return new SnapPoint(EndPoint, SnapPointType.Endpoint);
        var mid = new Vector3D((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, 0);
        yield return new SnapPoint(mid, SnapPointType.Midpoint);
    }

    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return StartPoint;
        yield return EndPoint;
    }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (index == 0) StartPoint = newPosition;
        else if (index == 1) EndPoint = newPosition;
        InvalidateCache();
        base.MoveGripPointAt(index, newPosition);
    }
}
