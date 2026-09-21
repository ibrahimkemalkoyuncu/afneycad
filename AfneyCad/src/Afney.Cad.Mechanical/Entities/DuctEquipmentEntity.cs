using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Entities;

/*
   Kanal hattına seri bağlanan esnek bağlantı / filtre / bobin / CAV / VAV kutusu.
   DamperEntity ile aynı port deseni (Inlet/Outlet); hesap sonuçları ilgili servislerden
   (FlexConnectorService, AirFilterSelectionService, AirCoilSelectionService, VavCavBoxService)
   gelip bu entity'nin alanlarına yazılır.
*/
public class DuctEquipmentEntity : MechanicalEntity
{
    public DuctEquipmentType EquipmentType { get; set; }

    public Vector3D Position { get; set; }
    public double Rotation { get; set; }

    // Gövde boyu (kanal ekseni boyunca, mm). Esnek bağlantıda gerçek bağlantı uzunluğudur.
    public double Size { get; set; }

    public double AirFlowM3h { get; set; }
    public double MinFlowM3h { get; set; }
    public double CapacityKw { get; set; }
    public double PressureDropPa { get; set; }
    public string FilterClass { get; set; } = "";
    public string Notes { get; set; } = "";

    public DuctEquipmentEntity(Vector3D position, DuctEquipmentType type, double diameter)
    {
        Position = position;
        EquipmentType = type;
        InnerDiameter = diameter;
        Size = DefaultBodyLengthMm(type);
        EntityType = MechanicalEntityType.DuctEquipment;
        SystemType = MechanicalSystemType.Ventilation;
        Layer = "MEP_HAVALANDIRMA";
        Color = DefaultColor(type);
    }

    public static double DefaultBodyLengthMm(DuctEquipmentType type) => type switch
    {
        DuctEquipmentType.FlexConnector => 300.0,
        DuctEquipmentType.Filter => 400.0,
        DuctEquipmentType.HeatingCoil => 500.0,
        DuctEquipmentType.CoolingCoil => 500.0,
        _ => 600.0
    };

    public static uint DefaultColor(DuctEquipmentType type) => type switch
    {
        DuctEquipmentType.FlexConnector => 0xFF95A5A6,
        DuctEquipmentType.Filter => 0xFFF1C40F,
        DuctEquipmentType.HeatingCoil => 0xFFE74C3C,
        DuctEquipmentType.CoolingCoil => 0xFF3498DB,
        DuctEquipmentType.CavBox => 0xFF2ECC71,
        _ => 0xFF1ABC9C
    };

    public static string ShortLabel(DuctEquipmentType type) => type switch
    {
        DuctEquipmentType.FlexConnector => "FLEX",
        DuctEquipmentType.Filter => "FLT",
        DuctEquipmentType.HeatingCoil => "ISITMA",
        DuctEquipmentType.CoolingCoil => "SOĞUTMA",
        DuctEquipmentType.CavBox => "CAV",
        _ => "VAV"
    };

    private double BodyWidth => Math.Max(InnerDiameter, 100.0);

    public override List<MechanicalPort> GetPorts()
    {
        double cos = Math.Cos(Rotation), sin = Math.Sin(Rotation);
        var dir = new Vector3D(cos, sin, 0);
        Vector3D At(double x) => new(Position.X + x * cos, Position.Y + x * sin, Position.Z);

        return new List<MechanicalPort>
        {
            new(Id, "Inlet", At(-Size / 2), new Vector3D(-dir.X, -dir.Y, 0), InnerDiameter)
                { FlowType = FlowDirection.Bidirectional },
            new(Id, "Outlet", At(Size / 2), dir, InnerDiameter)
                { FlowType = FlowDirection.Bidirectional }
        };
    }

    public override void Draw(IRenderContext context)
    {
        double cos = Math.Cos(Rotation), sin = Math.Sin(Rotation);
        Vector3D P(double x, double y) =>
            new(Position.X + x * cos - y * sin, Position.Y + x * sin + y * cos, Position.Z);

        double l = Size / 2, w = BodyWidth / 2;
        uint color = IsSelected ? 0xFFFFFFFF : (Color != 0 ? Color : DefaultColor(EquipmentType));
        double thick = IsSelected ? 2.0 : 1.2;

        context.DrawLine(P(-l, -w), P(l, -w), color, thick);
        context.DrawLine(P(l, -w), P(l, w), color, thick);
        context.DrawLine(P(l, w), P(-l, w), color, thick);
        context.DrawLine(P(-l, w), P(-l, -w), color, thick);

        switch (EquipmentType)
        {
            case DuctEquipmentType.FlexConnector:
                for (int i = 1; i < 6; i++)
                {
                    double x = -l + i * Size / 6;
                    context.DrawLine(P(x, -w), P(x, w), color, thick);
                }
                break;

            case DuctEquipmentType.Filter:
                for (int i = 1; i <= 3; i++)
                {
                    double x = -l + i * Size / 4;
                    context.DrawLine(P(x - w / 2, -w), P(x + w / 2, w), color, thick);
                }
                break;

            case DuctEquipmentType.HeatingCoil:
            case DuctEquipmentType.CoolingCoil:
                const int teeth = 6;
                for (int i = 0; i < teeth; i++)
                {
                    double x0 = -l + i * Size / teeth, x1 = -l + (i + 1) * Size / teeth;
                    double y0 = (i % 2 == 0) ? -w * 0.6 : w * 0.6;
                    context.DrawLine(P(x0, y0), P(x1, -y0), color, thick);
                }
                break;

            default:
                context.DrawLine(P(-l * 0.6, -w * 0.6), P(l * 0.6, -w * 0.6), color, thick);
                context.DrawLine(P(l * 0.6, -w * 0.6), P(l * 0.6, w * 0.6), color, thick);
                context.DrawLine(P(l * 0.6, w * 0.6), P(-l * 0.6, w * 0.6), color, thick);
                context.DrawLine(P(-l * 0.6, w * 0.6), P(-l * 0.6, -w * 0.6), color, thick);
                break;
        }

        context.DrawText(ShortLabel(EquipmentType), P(-l / 2, w + 80), 0, 110, color);

        if (IsSelected)
        {
            string detail = EquipmentType switch
            {
                DuctEquipmentType.FlexConnector => $"L={Size:F0} mm",
                DuctEquipmentType.Filter => $"{FilterClass} ΔP={PressureDropPa:F0} Pa",
                DuctEquipmentType.HeatingCoil or DuctEquipmentType.CoolingCoil => $"{CapacityKw:F1} kW",
                _ => $"{MinFlowM3h:F0}–{AirFlowM3h:F0} m³/h"
            };
            context.DrawText(detail, P(-l, -w - 180), 0, 100, color);
        }
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double r = Math.Max(Size, BodyWidth);
        return new CadBoundingBox(Position - new Vector3D(r, r, r), Position + new Vector3D(r, r, r));
    }

    public override void Move(Vector3D delta) => Position += delta;

    public override void Transform(Matrix4x4 matrix) => Position = matrix.Transform(Position);

    public override CadEntity Clone()
    {
        return new DuctEquipmentEntity(Position, EquipmentType, InnerDiameter)
        {
            Id = Guid.NewGuid(),
            Rotation = Rotation,
            Size = Size,
            AirFlowM3h = AirFlowM3h,
            MinFlowM3h = MinFlowM3h,
            CapacityKw = CapacityKw,
            PressureDropPa = PressureDropPa,
            FilterClass = FilterClass,
            Notes = Notes,
            Color = Color,
            Layer = Layer,
            SystemType = SystemType
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
