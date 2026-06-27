using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Entities;

public enum WallMaterial { Concrete, Brick, AeratedConcrete, Steel, Wood, Glass, Composite }

public class WallEntity : MechanicalEntity
{
    public Vector3D StartPoint   { get; set; }
    public Vector3D EndPoint     { get; set; }
    public double ThicknessMm    { get; set; } = 200;
    public double HeightMm       { get; set; } = 3000;
    public WallMaterial Material { get; set; } = WallMaterial.Brick;
    public double UValue         { get; set; } = 0.35;
    public bool IsLoadBearing    { get; set; } = true;
    public bool HasInsulation    { get; set; } = false;
    public double InsulationMm   { get; set; } = 0;

    public WallEntity(Vector3D start, Vector3D end, double thickness = 200)
    {
        StartPoint = start;
        EndPoint = end;
        ThicknessMm = thickness;
        Layer = "DUVAR";
        Color = 0xFFAAAAAA;
    }

    public double GetLength() => (EndPoint - StartPoint).Length();
    public double GetLengthM() => GetLength() / 1000.0;
    public double GetAreaM2() => GetLengthM() * (HeightMm / 1000.0);
    public double GetVolumeM3() => GetAreaM2() * (ThicknessMm / 1000.0);

    public string GetMaterialText() => Material switch
    {
        WallMaterial.Concrete => "Betonarme",
        WallMaterial.Brick => "Tugla",
        WallMaterial.AeratedConcrete => "Gazbeton",
        WallMaterial.Steel => "Celik",
        WallMaterial.Wood => "Ahsap",
        WallMaterial.Glass => "Cam",
        WallMaterial.Composite => "Kompozit",
        _ => "Diger"
    };

    public override void Draw(IRenderContext ctx)
    {
        uint drawColor = IsSelected ? 0xFFFFFFFF : Color;
        double thick = IsSelected ? 2.5 : 1.5;

        var dir = EndPoint - StartPoint;
        double len = dir.Length();
        if (len < 1e-9) return;
        var norm = new Vector3D(-dir.Y / len, dir.X / len, 0);
        double halfT = ThicknessMm / 2.0;

        var p1 = new Vector3D(StartPoint.X + norm.X * halfT, StartPoint.Y + norm.Y * halfT, 0);
        var p2 = new Vector3D(StartPoint.X - norm.X * halfT, StartPoint.Y - norm.Y * halfT, 0);
        var p3 = new Vector3D(EndPoint.X - norm.X * halfT, EndPoint.Y - norm.Y * halfT, 0);
        var p4 = new Vector3D(EndPoint.X + norm.X * halfT, EndPoint.Y + norm.Y * halfT, 0);

        ctx.DrawLine(p1, p4, drawColor, thick);
        ctx.DrawLine(p2, p3, drawColor, thick);
        ctx.DrawLine(p1, p2, drawColor, thick);
        ctx.DrawLine(p3, p4, drawColor, thick);

        if (IsLoadBearing)
        {
            ctx.DrawLine(p1, p3, drawColor, thick * 0.3);
            ctx.DrawLine(p2, p4, drawColor, thick * 0.3);
        }

        if (IsSelected)
        {
            var mid = new Vector3D((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, 0);
            ctx.DrawText($"{GetMaterialText()} {ThicknessMm:F0}mm", mid, 0, 120, 0xFFFFAA00);
        }
    }

    public override List<MechanicalPort> GetPorts() => new();

    public override double DistanceTo(Vector3D p)
    {
        var v = StartPoint; var w = EndPoint;
        double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
        if (l2 == 0) return p.DistanceTo(v);
        double t = Math.Clamp(((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2, 0, 1);
        return p.DistanceTo(new Vector3D(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y), 0));
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double pad = ThicknessMm / 2;
        return new CadBoundingBox(
            new Vector3D(Math.Min(StartPoint.X, EndPoint.X) - pad, Math.Min(StartPoint.Y, EndPoint.Y) - pad, 0),
            new Vector3D(Math.Max(StartPoint.X, EndPoint.X) + pad, Math.Max(StartPoint.Y, EndPoint.Y) + pad, 0));
    }

    public override void Move(Vector3D delta)
    {
        StartPoint += delta; EndPoint += delta; InvalidateCache();
    }

    public override void Transform(Matrix4x4 matrix)
    {
        StartPoint = matrix.Transform(StartPoint); EndPoint = matrix.Transform(EndPoint); InvalidateCache();
    }

    public override CadEntity Clone()
    {
        var c = new WallEntity(StartPoint, EndPoint, ThicknessMm)
        { HeightMm = HeightMm, Material = Material, UValue = UValue, IsLoadBearing = IsLoadBearing };
        CopyBaseProperties(c); return c;
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(StartPoint, SnapPointType.Endpoint);
        yield return new SnapPoint(EndPoint, SnapPointType.Endpoint);
        yield return new SnapPoint(new Vector3D((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, 0), SnapPointType.Midpoint);
    }

    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return StartPoint; yield return EndPoint;
    }

    public override void MoveGripPointAt(int index, Vector3D pos)
    {
        if (index == 0) StartPoint = pos; else if (index == 1) EndPoint = pos;
        InvalidateCache(); base.MoveGripPointAt(index, pos);
    }
}
