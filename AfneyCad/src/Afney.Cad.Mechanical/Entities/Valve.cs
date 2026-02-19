using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Vana Varlığı (Valve)
   NEDEN: Akış kontrolünü sağlayan mekanik elemanları (Vana, Çekvalf vb.) temsil etmek için.

   MÜHENDİSLİK DETAYI:
   - Yerleşim noktası (Position) ve akış doğrultusuna paralel dönüş açısına (RotationAngle) sahiptir.
   - Boru çapıyla (InnerDiameter) uyumlu olmalıdır.
   - Hidrolik devrede akışı kesme veya yönlendirme noktası olarak işlev görür.
   - Görsel olarak emniyet ve kontrol sembolüyle temsil edilir.
*/
public class Valve : MechanicalEntity
{
    public Vector3D Position { get; set; }
    public double RotationAngle { get; set; } = 0; // Radyan cinsinden

    /*
       NE: Bağlantı Portlarını Getir (GetPorts)
       NEDEN: Vananın akış doğrultusundaki giriş-çıkış (P1-P2) noktalarını, vananın kendi rotasyon açısına göre hesaplayıp topoloji grafına bildirmek için.
    */
    public override List<MechanicalPort> GetPorts()
    {
        // 1. Matris ile Yönü Döndür
        var matrix = Matrix4x4.RotationZ(RotationAngle);
        
        var dirIn = new Vector3D(-1, 0, 0); // Varsayılan sol
        var dirOut = new Vector3D(1, 0, 0); // Varsayılan sağ
        
        dirIn = matrix.Transform(dirIn);
        dirOut = matrix.Transform(dirOut);

        return new List<MechanicalPort>
        {
            new MechanicalPort(Id, "P1", Position, dirIn),
            new MechanicalPort(Id, "P2", Position, dirOut) 
        };
    }

    /*
       NE: Vana Sembolü Çiz (Draw)
       NEDEN: Vanayı teknik şemalarda ayırt edilebilir kılan dairesel ve çarpılı (Butterfly Valve sembolü benzeri) formu ekrana basmak için.
    */
    public override void Draw(IRenderContext context)
    {
        double size = InnerDiameter * 2; 
        
        // Vana sembolü: Daire ve içine X
        // Önce Çizim Matrisi Uygula (Dönüş)
        var matrix = Matrix4x4.RotationZ(RotationAngle);
        
        // Göreceli Koordinatlar (Merkeze göre)
        var p1Rel = new Vector3D(-size/2, -size/2, 0);
        var p2Rel = new Vector3D(size/2, size/2, 0);
        var p3Rel = new Vector3D(-size/2, size/2, 0);
        var p4Rel = new Vector3D(size/2, -size/2, 0);
        
        // Dönüşümü ve Pozisyonu Uygula
        var p1 = Position + matrix.Transform(p1Rel);
        var p2 = Position + matrix.Transform(p2Rel);
        var p3 = Position + matrix.Transform(p3Rel);
        var p4 = Position + matrix.Transform(p4Rel);
        
        context.DrawCircle(Position, size, Color, 1.0);
        context.DrawLine(p1, p2, Color, 1.0);
        context.DrawLine(p3, p4, Color, 1.0);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double size = InnerDiameter * 3; // Biraz geniş alalım, rotation bounding box'ı büyütür
        return new CadBoundingBox(
            new Vector3D(Position.X - size, Position.Y - size, 0),
            new Vector3D(Position.X + size, Position.Y + size, 0)
        );
    }

    public override void Move(Vector3D delta)
    {
        Position += delta;
    }

    public override void Transform(Matrix4x4 matrix)
    {
        Position = matrix.Transform(Position);
    }

    public override CadEntity Clone()
    {
         return new Valve
        {
            Position = this.Position,
            RotationAngle = this.RotationAngle,
            SystemType = this.SystemType,
            InnerDiameter = this.InnerDiameter,
            PipeMaterialType = this.PipeMaterialType,
            Color = this.Color,
            Layer = this.Layer,
            Id = Guid.NewGuid()
        };
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
    }
}

