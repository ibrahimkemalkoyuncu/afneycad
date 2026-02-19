using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Advanced;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Dirsek Varlığı (ElbowEntity)
   NEDEN: Boru hattının yön değişimlerini (dönüşleri) temsil etmek için.

   MÜHENDİSLİK DETAYI:
   - Gelen (Incoming) ve Giden (Outgoing) boru doğrultularına göre otomatik açı hesaplar.
   - Boru çapıyla (InnerDiameter) doğrudan ilişkilidir.
   - Akış yönündeki basınç kaybını etkileyen bir bileşendir.
   - Görsel olarak dönüş merkezini ve radius bilgisini taşır.
*/
public class ElbowEntity : MechanicalEntity
{
    public Vector3D Center { get; set; }
    public double Radius { get; set; } 
    public Vector3D IncomingVector { get; set; } 
    public Vector3D OutgoingVector { get; set; } 

    public ElbowEntity(Vector3D center, double diameter, Vector3D inVec, Vector3D outVec)
    {
        Center = center;
        InnerDiameter = diameter;
        IncomingVector = inVec.Normalize();
        OutgoingVector = outVec.Normalize();
        
        Radius = 1.5 * diameter; 
    }

    public override void Draw(IRenderContext ctx)
    {
        // Basit görselleştirme: İki çizgi + merkez nokta
        double extensionLength = Radius; 
        
        var p1 = Center - (IncomingVector * extensionLength);
        var p2 = Center + (OutgoingVector * extensionLength);
        
        ctx.DrawLine(p1, Center, Color, InnerDiameter / 10.0);
        ctx.DrawLine(Center, p2, Color, InnerDiameter / 10.0);
        
        ctx.DrawCircle(Center, InnerDiameter / 4, 0xFFFF6600, 2.0); // Turuncu merkez
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        return new CadBoundingBox(
             new Vector3D(Center.X - Radius, Center.Y - Radius, Center.Z - Radius),
             new Vector3D(Center.X + Radius, Center.Y + Radius, Center.Z + Radius)
        );
    }
    
    public override CadEntity Clone() 
    {
        return new ElbowEntity(Center, InnerDiameter, IncomingVector, OutgoingVector) 
        { 
            Color = this.Color,
            Layer = this.Layer,
            SystemType = this.SystemType
        };
    }

    public override void Move(Vector3D delta) => Center += delta;
    
    public override void Transform(Matrix4x4 matrix) => Center = matrix.Transform(Center);

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Center, SnapPointType.Center);
    }

    public override List<MechanicalPort> GetPorts()
    {
        return new List<MechanicalPort>
        {
            new MechanicalPort(Id, "P1", Center - IncomingVector * Radius, IncomingVector * -1),
            new MechanicalPort(Id, "P2", Center + OutgoingVector * Radius, OutgoingVector)
        };
    }
}

