using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Damper Birimi (DamperEntity)
   NEDEN: Kanal ağındaki debi/güvenlik elemanlarını (Volume/Fire/Smoke Damper) temsil etmek için —
          ValveEntity'nin boru hattı üzerindeki rolünün kanal hattındaki karşılığı.

   NASIL (Mühendislik Detayı):
   - Her damper 2 porta (Inlet/Outlet) sahiptir ve kanal hattına seri bağlanır (ValveEntity ile
     birebir aynı port deseni).
   - Fire/Smoke/FireSmoke tipleri EN 1366-2 uyumlu yangın direnç süresi (FireRatingMin) taşır.
   - Volume tipi, DamperPositionPct (0=kapalı, 100=tam açık) ile debi kısma oranını (EN 1751
     yaklaşık: Δp ~ 1/pozisyon²) temsil eder.
*/
public class DamperEntity : MechanicalEntity
{
    public DamperType DamperType { get; set; } = DamperType.Volume;

    public Vector3D Position { get; set; }
    public double Rotation { get; set; } = 0.0;

    // NE: Boyutlar (Görsel sembol boyutu, mm)
    public double Size { get; set; } = 300.0;

    // NE: Klape Açıklık Oranı (%) — Volume damper için debi kısma kontrolü.
    public double DamperPositionPct { get; set; } = 100.0;

    // NE: Yangın Direnç Süresi (dakika) — Fire/Smoke/FireSmoke damperler için (EN 1366-2: 30/60/90/120).
    public int FireRatingMin { get; set; } = 0;

    public DamperEntity(Vector3D position, DamperType type, double diameter)
    {
        Position = position;
        DamperType = type;
        InnerDiameter = diameter;
        EntityType = MechanicalEntityType.Damper;
        SystemType = MechanicalSystemType.Ventilation;
        Layer = "MEP_HAVALANDIRMA";
        Color = 0xFF2ECC71;
        if (type is Enums.DamperType.Fire or Enums.DamperType.Smoke or Enums.DamperType.FireSmoke)
            FireRatingMin = 90;
    }

    /*
       NE: Bağlantı Portlarını Getir (GetPorts)
       NEDEN: Damperin her iki ucundaki kanal bağlantı noktalarını topolojiye sunmak için.
    */
    public override List<MechanicalPort> GetPorts()
    {
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        Vector3D TransformOffset(double xOffset)
        {
            double rx = xOffset * cos;
            double ry = xOffset * sin;
            return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z);
        }

        var dir = new Vector3D(cos, sin, 0);

        var ports = new List<MechanicalPort>
        {
            new MechanicalPort(Id, "Inlet", TransformOffset(-Size / 2), new Vector3D(-dir.X, -dir.Y, 0), InnerDiameter)
                { FlowType = FlowDirection.Bidirectional },
            new MechanicalPort(Id, "Outlet", TransformOffset(Size / 2), dir, InnerDiameter)
                { FlowType = FlowDirection.Bidirectional }
        };

        return ports;
    }

    public override void Draw(IRenderContext context)
    {
        double halfS = Size / 2.0;
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        Vector3D Trans(double x, double y)
        {
            double rx = x * cos - y * sin;
            double ry = x * sin + y * cos;
            return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z);
        }

        uint color = IsSelected ? 0xFFFFFFFF : (Color != 0 ? Color : 0xFF2ECC71);
        double thick = IsSelected ? 2.0 : 1.2;

        // Damper gövdesi: kanal genişliğinde kutu
        var p1 = Trans(-halfS / 2, halfS / 2);
        var p2 = Trans(halfS / 2, halfS / 2);
        var p3 = Trans(halfS / 2, -halfS / 2);
        var p4 = Trans(-halfS / 2, -halfS / 2);
        context.DrawLine(p1, p2, color, thick);
        context.DrawLine(p2, p3, color, thick);
        context.DrawLine(p3, p4, color, thick);
        context.DrawLine(p4, p1, color, thick);

        switch (DamperType)
        {
            case Enums.DamperType.Volume:
                // Klape yaprağı: pozisyona göre açı (0%=dikey kapalı, 100%=yatay açık)
                double angle = (DamperPositionPct / 100.0) * Math.PI / 2;
                context.DrawLine(Trans(0, 0), Trans(halfS / 2 * Math.Cos(angle), halfS / 2 * Math.Sin(angle)), color, thick * 1.3);
                context.DrawLine(Trans(0, 0), Trans(-halfS / 2 * Math.Cos(angle), -halfS / 2 * Math.Sin(angle)), color, thick * 1.3);
                break;

            case Enums.DamperType.Fire:
                context.DrawText("F", Trans(0, halfS / 2 + 100), 0, 130, color);
                break;

            case Enums.DamperType.Smoke:
                context.DrawText("S", Trans(0, halfS / 2 + 100), 0, 130, color);
                break;

            case Enums.DamperType.FireSmoke:
                context.DrawText("FS", Trans(0, halfS / 2 + 100), 0, 130, color);
                break;

            case Enums.DamperType.BackDraft:
                // Geri tepme klapesi: tek yönlü ok
                context.DrawLine(Trans(-halfS / 4, 0), Trans(halfS / 4, 0), color, thick * 1.5);
                context.DrawLine(Trans(halfS / 4, 0), Trans(0, halfS / 4), color, thick);
                context.DrawLine(Trans(halfS / 4, 0), Trans(0, -halfS / 4), color, thick);
                break;
        }

        if (IsSelected)
        {
            string label = DamperType == Enums.DamperType.Volume
                ? $"VCD {DamperPositionPct:F0}%"
                : $"{DamperType} ({FireRatingMin}dk)";
            context.DrawText(label, Trans(0, -halfS - 150), 0, 120, color);
        }
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        return new CadBoundingBox(
            Position - new Vector3D(Size, Size, Size),
            Position + new Vector3D(Size, Size, Size)
        );
    }

    public override void Move(Vector3D delta) => Position += delta;

    public override void Transform(Matrix4x4 matrix) => Position = matrix.Transform(Position);

    public override CadEntity Clone()
    {
        return new DamperEntity(Position, DamperType, InnerDiameter)
        {
            Id = Guid.NewGuid(),
            Rotation = this.Rotation,
            Size = this.Size,
            DamperPositionPct = this.DamperPositionPct,
            FireRatingMin = this.FireRatingMin,
            Color = this.Color,
            Layer = this.Layer,
            SystemType = this.SystemType
        };
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
        foreach (var port in GetPorts())
            yield return new SnapPoint(port.Position, SnapPointType.Connection);
    }

    public override IEnumerable<Vector3D> GetGripPoints() { yield return Position; }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        Position = newPosition;
        base.MoveGripPointAt(index, newPosition);
    }
}
