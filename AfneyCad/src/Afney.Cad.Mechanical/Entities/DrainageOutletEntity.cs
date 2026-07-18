using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Boşaltma Noktası (DrainageOutletEntity) — Rögar / Tahliye Çıkışı
   NEDEN: OtoNET'teki "Boşaltma Noktası" komutunun AfneyCAD karşılığı.
          Pis su ve yağmur suyu hatlarının bina dışına (rögara) bağlandığı son noktayı temsil eder.
   MÜHENDİSLİK:
   - HydraulicNetwork hesaplamasında "Sink" (alıcı) düğüm.
   - Tesisatı Kabul Et doğrulamasında her WasteWater/RainWater ağında en az bir adet zorunlu.
*/
public class DrainageOutletEntity : MechanicalEntity
{
    public enum OutletType
    {
        SewerManhole,   // Rögar — pis su bağlantısı
        RainDrain,      // Yağmur suyu zemine boşaltma
        Septic          // Fosseptik / arıtma
    }

    public Vector3D Position { get; set; }

    private OutletType _outletType;
    private double _invertLevel;
    private string _label = "";

    public OutletType Type
    {
        get => _outletType;
        set { _outletType = value; OnMetadataChanged(); }
    }

    public double InvertLevel
    {
        get => _invertLevel;
        set { _invertLevel = value; OnMetadataChanged(); }
    }

    public string Label
    {
        get => _label;
        set { _label = value; OnMetadataChanged(); }
    }

    public DrainageOutletEntity(Vector3D position, OutletType type = OutletType.SewerManhole)
    {
        Position = position;
        _outletType = type;
        SystemType = type == OutletType.RainDrain
            ? MechanicalSystemType.RainWater
            : MechanicalSystemType.WasteWater;
    }

    public override List<MechanicalPort> GetPorts() =>
    [
        new MechanicalPort(Id, "DrainInlet", Position, new Vector3D(0, -1, 0), InnerDiameter)
        {
            FlowType = Engine.FlowDirection.Bidirectional
        }
    ];

    public override void Draw(IRenderContext ctx)
    {
        double r = 200; // 200mm = rögar sembol yarıçapı
        uint col = SystemType == MechanicalSystemType.RainWater
            ? 0xFF0096FF   // Mavi — yağmur suyu
            : 0xFF8B5A2B;  // Kahverengi — pis su

        ctx.DrawCircle(Position, r, col, 1.5);
        ctx.DrawLine(
            new Vector3D(Position.X - r, Position.Y, 0),
            new Vector3D(Position.X + r, Position.Y, 0), col, 1.5);
        ctx.DrawLine(
            new Vector3D(Position.X, Position.Y - r, 0),
            new Vector3D(Position.X, Position.Y + r, 0), col, 1.5);

        if (!string.IsNullOrEmpty(_label))
            ctx.DrawText(_label, new Vector3D(Position.X + r + 50, Position.Y, 0), 0, 120, col);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        const double r = 200;
        return new CadBoundingBox(
            new Vector3D(Position.X - r, Position.Y - r, 0),
            new Vector3D(Position.X + r, Position.Y + r, 0));
    }

    public override void Move(Vector3D delta)
    {
        Position = new Vector3D(Position.X + delta.X, Position.Y + delta.Y, Position.Z + delta.Z);
        InvalidateCache();
    }

    public override void Transform(Matrix4x4 matrix)
    {
        Position = matrix.Transform(Position);
        InvalidateCache();
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
    }

    /*
       NE: Grip Noktaları (GetGripPoints / MoveGripPointAt)
       NEDEN: Önceden hiç override yoktu — boşaltma noktası (rögar/tahliye) grip ile
              taşınamıyordu.
    */
    public override IEnumerable<Vector3D> GetGripPoints() { yield return Position; }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        Position = newPosition;
        InvalidateCache();
        base.MoveGripPointAt(index, newPosition);
    }

    public override CadEntity Clone() =>
        new DrainageOutletEntity(Position, _outletType)
        {
            InnerDiameter = InnerDiameter,
            InvertLevel   = _invertLevel,
            Label         = _label,
            Layer         = Layer
        };
}
