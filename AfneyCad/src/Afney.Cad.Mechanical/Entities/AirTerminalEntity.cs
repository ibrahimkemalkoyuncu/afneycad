using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Hava Terminal Ünitesi (AirTerminalEntity)
   NEDEN: Kanal ağının son elemanlarını (difüzör, menfez, panjur) MEP grafında temsil etmek için —
          daha önce AfneyCAD'de menfez/difüzör kütüphanesi yoktu, sadece kanal (DuctEntity) vardı.

   NASIL (Mühendislik Detayı):
   - Tek portu vardır (Neck/Boyun) — DuctEntity'nin ucuna bağlanır (SanitaryFixtureEntity'nin
     boruya bağlanma deseniyle aynı: tek port, MEP grafında Terminal düğüm).
   - NeckVelocityMs, AcousticAnalysisService.TerminalDeviceLoss(neckVelocityMs) ile doğrudan
     ses gücü kaybı hesabını besler (ASHRAE Handbook HVAC Applications Ch. 48).
   - NCRating (Noise Criteria) ve ThrowM (atış mesafesi), 4M FineSANI'nin menfez seçim
     tablolarındaki temel iki kriterdir; burada katalog değeri olarak saklanır, seçim ise
     ilgili kütüphane servisinden (gelecekte) veya elle atanır.
*/
public class AirTerminalEntity : MechanicalEntity
{
    public AirTerminalType TerminalType { get; set; } = AirTerminalType.SupplyDiffuser;

    public Vector3D Position { get; set; }
    public double Rotation { get; set; } = 0.0;

    // NE: Boyutlar (mm) — Kare/dikdörtgen menfez gövdesi için.
    public double Width { get; set; } = 300.0;
    public double Height { get; set; } = 300.0;

    // NE: Hava Debisi (m³/h)
    public double AirFlowM3h { get; set; } = 100.0;

    // NE: Boyun Hızı (Neck Velocity, m/s) — AcousticAnalysisService.TerminalDeviceLoss girdisi.
    public double NeckVelocityMs { get; set; } = 3.0;

    // NE: Atış Mesafesi (Throw, m) — ASHRAE terminal hızı 0.25 m/s'ye düştüğü mesafe.
    public double ThrowM { get; set; } = 3.0;

    // NE: Gürültü Kriteri (Noise Criteria, NC) — katalog değeri.
    public int NCRating { get; set; } = 25;

    public AirTerminalEntity(Vector3D position, AirTerminalType type, double airFlowM3h)
    {
        Position = position;
        TerminalType = type;
        AirFlowM3h = airFlowM3h;
        InnerDiameter = 200.0; // Varsayılan boyun çapı
        EntityType = MechanicalEntityType.AirTerminal;
        SystemType = MechanicalSystemType.Ventilation;
        Layer = "MEP_HAVALANDIRMA";
        Color = 0xFF2ECC71;
    }

    /*
       NE: Bağlantı Portunu Getir (GetPorts)
       NEDEN: Terminal ünitesi tek portludur (Neck) — kanal ağının uç (leaf) düğümüdür.
    */
    public override List<MechanicalPort> GetPorts()
    {
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);
        var dir = new Vector3D(cos, sin, 0);

        return new List<MechanicalPort>
        {
            new MechanicalPort(Id, "Neck", Position, new Vector3D(-dir.X, -dir.Y, 0), InnerDiameter)
                { FlowType = FlowDirection.Bidirectional }
        };
    }

    public override void Draw(IRenderContext context)
    {
        double halfW = Width / 2.0;
        double halfH = Height / 2.0;
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        Vector3D Trans(double x, double y)
        {
            double rx = x * cos - y * sin;
            double ry = x * sin + y * cos;
            return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z);
        }

        uint color = IsSelected ? 0xFFFFFFFF : (Color != 0 ? Color : 0xFF2ECC71);
        double thick = IsSelected ? 2.0 : 1.0;

        // Gövde (dikdörtgen çerçeve)
        var p1 = Trans(-halfW, halfH);
        var p2 = Trans(halfW, halfH);
        var p3 = Trans(halfW, -halfH);
        var p4 = Trans(-halfW, -halfH);
        context.DrawLine(p1, p2, color, thick);
        context.DrawLine(p2, p3, color, thick);
        context.DrawLine(p3, p4, color, thick);
        context.DrawLine(p4, p1, color, thick);

        switch (TerminalType)
        {
            case AirTerminalType.SupplyDiffuser:
            case AirTerminalType.JetNozzle:
            case AirTerminalType.FloorDiffuser:
                // Besleme: köşegen X (difüzör lamel sembolü)
                context.DrawLine(p1, p3, color, thick * 0.6);
                context.DrawLine(p2, p4, color, thick * 0.6);
                break;

            case AirTerminalType.ReturnGrille:
            case AirTerminalType.ExhaustGrille:
                // Dönüş/Egzoz: paralel lameller
                for (int i = -1; i <= 1; i++)
                {
                    double y = i * halfH / 2.0;
                    context.DrawLine(Trans(-halfW, y), Trans(halfW, y), color, thick * 0.5);
                }
                break;

            case AirTerminalType.Louver:
                // Panjur: eğik lameller
                for (int i = -2; i <= 2; i++)
                {
                    double y = i * halfH / 2.5;
                    context.DrawLine(Trans(-halfW, y - halfH / 6), Trans(halfW, y + halfH / 6), color, thick * 0.5);
                }
                break;

            case AirTerminalType.LinearSlot:
                // Lineer yarık: tek uzun ince dikdörtgen orta çizgi
                context.DrawLine(Trans(-halfW, 0), Trans(halfW, 0), color, thick * 1.2);
                break;
        }

        if (IsSelected)
        {
            context.DrawText($"{TerminalType} | {AirFlowM3h:F0} m³/h | NC{NCRating}", Trans(0, halfH + 100), 0, 120, color);
        }
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double r = Math.Max(Width, Height);
        return new CadBoundingBox(
            Position - new Vector3D(r, r, r),
            Position + new Vector3D(r, r, r)
        );
    }

    public override void Move(Vector3D delta) => Position += delta;

    public override void Transform(Matrix4x4 matrix) => Position = matrix.Transform(Position);

    public override CadEntity Clone()
    {
        return new AirTerminalEntity(Position, TerminalType, AirFlowM3h)
        {
            Id = Guid.NewGuid(),
            Rotation = this.Rotation,
            Width = this.Width,
            Height = this.Height,
            NeckVelocityMs = this.NeckVelocityMs,
            ThrowM = this.ThrowM,
            NCRating = this.NCRating,
            InnerDiameter = this.InnerDiameter,
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
