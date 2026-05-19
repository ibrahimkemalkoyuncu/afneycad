using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Advanced;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: T-Parçası Varlığı (TeeEntity)
   NEDEN: Boru hattından dikey bir branşman (dal) hattı çıkarmak için.

   MÜHENDİSLİK DETAYI:
   - Ana hat (Main Run) ve Sapma Hattı (Branch) bağlantılarını temsil eder.
   - Ana hat doğrusal (180 derece) açıda devam ederken, Sapma hattı 90 derecedir.
   - Farklı çaplardaki boruları birleştirme özelliğine (Reduction Tee) sahiptir.
   - Hidrolik akışın bölünme (Diverging) noktasını belirler.
*/
public class TeeEntity : MechanicalEntity
{
    public Vector3D Center { get; set; }
    public double MainDiameter { get; set; }
    public double BranchDiameter { get; set; }
    public Vector3D MainDirection { get; set; } // Ana hat yönü
    public Vector3D BranchDirection { get; set; } // Dal yönü

    public TeeEntity(Vector3D center, double mainDia, double branchDia, Vector3D mainDir, Vector3D branchDir)
    {
        Center = center;
        MainDiameter = mainDia;
        BranchDiameter = branchDia;
        MainDirection = mainDir.Normalize();
        BranchDirection = branchDir.Normalize();
        
        InnerDiameter = mainDia; // Base property
    }

    public override void Draw(IRenderContext ctx)
    {
        double length = MainDiameter * 2.5; 
        var p1 = Center - (MainDirection * (length / 2));
        var p2 = Center + (MainDirection * (length / 2));
        
        ctx.DrawLine(p1, p2, Color, MainDiameter > 0 ? MainDiameter : 3.0);
        
        double branchLen = length / 2;
        var pBranch = Center + (BranchDirection * branchLen);
        
        ctx.DrawLine(Center, pBranch, Color, BranchDiameter > 0 ? BranchDiameter : 3.0);
        ctx.DrawCircle(Center, MainDiameter / 4, 0xFFFF0000, 1.0);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double size = (MainDiameter * 3) + InsulationThickness;
        return new CadBoundingBox(
             new Vector3D(Center.X - size, Center.Y - size, Center.Z - size),
             new Vector3D(Center.X + size, Center.Y + size, Center.Z + size)
        );
    }
    
    public override CadEntity Clone() 
    {
        return new TeeEntity(Center, MainDiameter, BranchDiameter, MainDirection, BranchDirection) 
        { 
            Color = this.Color,
            Layer = this.Layer,
            SystemType = this.SystemType,
            PipeMaterialType = this.PipeMaterialType
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
            new MechanicalPort(Id, "MainIn", Center - MainDirection * (MainDiameter * 1.25), MainDirection * -1),
            new MechanicalPort(Id, "MainOut", Center + MainDirection * (MainDiameter * 1.25), MainDirection),
            new MechanicalPort(Id, "Branch", Center + BranchDirection * (BranchDiameter * 1.25), BranchDirection)
        };
    }

    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return Center;
    }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (index == 0) Center = newPosition;
        base.MoveGripPointAt(index, newPosition);
    }
}

