using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Vana Birimi (ValveEntity)
   NEDEN: Tesisat ağındaki kesme ve kontrol elemanlarını (Küresel Vana, Çek Valf vb.) temsil etmek için.
   
   NASIL (Mühendislik Detayı):
   - Her vana 2 porta (Inlet/Outlet) sahiptir.
   - Akış yönüne duyarlıdır (Örn: Çek Valf ters takılırsa topoloji hata verir).
   - Çap (InnerDiameter), vananın bağlı olduğu boruyla uyumlu olmalıdır.
   - Semboller TSE ve DIN standartlarındaki vana piktogramlarını yansıtır.
*/
public class ValveEntity : MechanicalEntity
{
    public ValveType ValveType { get; set; } = ValveType.BallValve;

    // NE: Yerleşim Noktası ve Yönelim
    public Vector3D Position { get; set; }
    public double Rotation { get; set; } = 0.0;

    // NE: Boyutlar (Görsel sembol boyutu, mm)
    public double Size { get; set; } = 250.0; // DN25-DN50 arası vana sembol boyutu

    public ValveEntity(Vector3D position, ValveType type, double diameter)
    {
        Position = position;
        ValveType = type;
        InnerDiameter = diameter;
        EntityType = MechanicalEntityType.Valve;
    }

    /*
       NE: Bağlantı Portlarını Getir (GetPorts)
       NEDEN: Vananın her iki ucundaki boru bağlantı noktalarını topolojiye sunmak için.
    */
    public override List<MechanicalPort> GetPorts()
    {
        var ports = new List<MechanicalPort>();
        
        // Rotasyon Matrisi (Z ekseni etrafında)
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        Vector3D TransformOffset(double xOffset)
        {
             double rx = xOffset * cos;
             double ry = xOffset * sin;
             return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z);
        }

        Vector3D dir = new Vector3D(cos, sin, 0);

        // Port 1 (Giriş) - Yön geriye (negatif x)
        ports.Add(new MechanicalPort(Id, "Inlet", TransformOffset(-Size / 2), -dir, InnerDiameter)
            { FlowType = FlowDirection.Bidirectional });
            
        // Port 2 (Çıkış) - Yön ileri (pozitif x)
        ports.Add(new MechanicalPort(Id, "Outlet", TransformOffset(Size / 2), dir, InnerDiameter)
            { FlowType = FlowDirection.Bidirectional });

        return ports;
    }

    /*
       NE: Vana Sembolü Çizimi (Draw)
       NEDEN: Vana tipine göre global sembolleri (TSE/DIN) render etmek için.
    */
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

        uint color = IsSelected ? 0xFFFFFFFF : (Color != 0 ? Color : 0xFF00FF00); 
        double thick = IsSelected ? 2.0 : 1.2;

        // Vana Gövdesi (Üçgenler - Kum saati formu)
        var p1 = Trans(-halfS, halfS / 2);
        var p2 = Trans(-halfS, -halfS / 2);
        var p3 = Trans(halfS, halfS / 2);
        var p4 = Trans(halfS, -halfS / 2);
        var center = Position;

        // Sol kanat
        context.DrawLine(p1, p2, color, thick);
        context.DrawLine(p2, center, color, thick);
        context.DrawLine(center, p1, color, thick);

        // Sağ kanat
        context.DrawLine(p3, p4, color, thick);
        context.DrawLine(p4, center, color, thick);
        context.DrawLine(center, p3, color, thick);

        // Tipe Özel İşaretler
        switch (ValveType)
        {
            case ValveType.BallValve:
                // Küresel Vana: Merkezde daire
                context.DrawCircle(center, halfS / 4, color, thick);
                break;

            case ValveType.CheckValve:
                // Çek Valf: Akış yönünü gösteren ok (merkezden sağa)
                context.DrawLine(Trans(-halfS/4, 0), Trans(halfS/4, 0), color, thick * 1.5);
                context.DrawLine(Trans(halfS/4, 0), Trans(0, halfS/4), color, thick);
                context.DrawLine(Trans(halfS/4, 0), Trans(0, -halfS/4), color, thick);
                break;

            case ValveType.PRV:
                // Basınç Düşürücü: Üzerinde elips veya P harfi
                context.DrawText("P", center + Trans(0, halfS), 0, 12, color);
                break;

            case ValveType.Filter:
                // Pislik Tutucu: Altında bir süzgeç haznesi
                context.DrawLine(center, Trans(0, -halfS), color, thick);
                context.DrawLine(Trans(-halfS/2, -halfS), Trans(halfS/2, -halfS), color, thick);
                break;
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
        return new ValveEntity(Position, ValveType, InnerDiameter)
        {
            Id = Guid.NewGuid(),
            Rotation = this.Rotation,
            Size = this.Size,
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

    /*
       NE: Grip Noktaları (GetGripPoints / MoveGripPointAt)
       NEDEN: Önceden hiç override yoktu — vana grip ile taşınamıyordu (sadece Move komutuyla).
    */
    public override IEnumerable<Vector3D> GetGripPoints() { yield return Position; }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        Position = newPosition;
        base.MoveGripPointAt(index, newPosition);
    }
}
