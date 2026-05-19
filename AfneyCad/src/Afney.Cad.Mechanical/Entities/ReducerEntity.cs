using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;
using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Eş Merkezli Redüksiyon (ReducerEntity)
   NEDEN: Farklı çaptaki iki boruyu hidrolik olarak birleştirmek için.
   
   NASIL (Geometri):
   - Konik bir geçiş parçası (Hull) olarak çizilir.
   - İki ucu farklı çaplardadır (D1 -> D2).
*/
public class ReducerEntity : MechanicalEntity
{
    public double Diameter1 { get; }
    public double Diameter2 { get; }
    public Vector3D Position { get; set; } // Hata giderildi: Property eklendi
    public Vector3D Direction { get; private set; } = new Vector3D(1, 0, 0);

    public ReducerEntity(Vector3D position, double diameter1, double diameter2)
    {
        Id = Guid.NewGuid();
        Position = position; // Merkez noktası
        Diameter1 = diameter1;
        Diameter2 = diameter2;
    }

    public void SetDirection(Vector3D dir)
    {
        // Yön vektörü
        Direction = new Vector3D(dir.X, dir.Y, dir.Z);
    }

    public override void Draw(IRenderContext context)
    {
        // 1. Daire Çizimi (Basit Temsil)
        // Redüksiyonun iki ucunu farklı çapta dairelerle temsil edelim.
        
        // Büyük Çap
        double maxD = Math.Max(Diameter1, Diameter2);
        context.DrawCircle(Position, maxD / 2.0, Color, 2.0);
        
        // Küçük Çap (İç içe)
        double minD = Math.Min(Diameter1, Diameter2);
        context.DrawCircle(Position, minD / 2.0, Color, 1.0);
    }
    
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
    }

    public override CadEntity Clone()
    {
        var clone = new ReducerEntity(Position, Diameter1, Diameter2);
        clone.SetDirection(Direction);
        clone.Color = Color;
        clone.SystemType = SystemType;
        clone.PipeMaterialType = PipeMaterialType;
        return clone;
    }

    public override void Transform(Matrix4x4 matrix)
    {
        // Matrix transformasyonu eklenecek
    }

    public override void Move(Vector3D offset)
    {
        Position += offset;
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double maxD = Math.Max(Diameter1, Diameter2) + (InsulationThickness * 2);
        return new CadBoundingBox(
            new Vector3D(Position.X - maxD, Position.Y - maxD, Position.Z - maxD),
            new Vector3D(Position.X + maxD, Position.Y + maxD, Position.Z + maxD)
        );
    }

    public override List<MechanicalPort> GetPorts()
    {
        double len = Math.Max(Diameter1, Diameter2); // Temsili uzunluk
        var p1 = Position - (Direction * (len / 2));
        var p2 = Position + (Direction * (len / 2));

        return new List<MechanicalPort>
        {
            new MechanicalPort(Id, "Port1", p1, -Direction),
            new MechanicalPort(Id, "Port2", p2, Direction)
        };
    }
}
