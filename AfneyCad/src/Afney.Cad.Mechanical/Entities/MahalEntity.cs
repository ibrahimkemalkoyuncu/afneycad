using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
    NE: Gelişmiş Sıhhi Tesisat Mahali (Sanitary Room / MahalEntity)
    NEDEN: Mimari projedeki bir odayı, içindeki tüm su tüketen birimlerle (Vitrifiye) ve hidrolik yüklerle birlikte yönetmek için.
*/
public class MahalEntity : MechanicalEntity
{
    public string MahalName { get; set; } = "Yeni Mahal";
    public string MahalType { get; set; } = "Genel"; 

    // Geriye dönük uyumluluk (Alias)
    public string Name { get => MahalName; set => MahalName = value; }
    public string RoomName { get => MahalName; set => MahalName = value; }
    public string RoomType { get => MahalType; set => MahalType = value; }

    public void FinalizeMahal() { /* Silinen metodun yerini tutarak UI çökmesini önler */ }
    
    public List<Vector3D> BoundaryPoints { get; set; } = new();
    public double Area { get; set; }
    public double Perimeter { get; set; }
    
    public List<SanitaryFixtureEntity> Fixtures { get; set; } = new();
    public List<Guid> FixtureIds => Fixtures.Select(f => f.Id).ToList();
    
    public double TotalLoadUnits => Fixtures.Sum(f => f.LoadUnits);
    public double DesignFlow { get; set; }
    public double CalculatedPipeDiameter { get; set; }
    public int FloorIndex { get; set; } = 0;

    public MahalEntity(IEnumerable<Vector3D> boundary, string name = "Oda", string type = "Genel")
    {
        MahalName = name;
        MahalType = type;
        BoundaryPoints = boundary.ToList();
        EntityType = MechanicalEntityType.Room;
        CalculateGeometry();
    }

    public override List<MechanicalPort> GetPorts()
    {
        return new List<MechanicalPort>();
    }

    private void CalculateGeometry()
    {
        if (BoundaryPoints.Count < 3) return;
        
        double area = 0;
        double perimeter = 0;
        for (int i = 0; i < BoundaryPoints.Count; i++)
        {
            var p1 = BoundaryPoints[i];
            var p2 = BoundaryPoints[(i + 1) % BoundaryPoints.Count];
            area += (p1.X * p2.Y) - (p2.X * p1.Y);
            perimeter += p1.DistanceTo(p2);
        }
        Area = System.Math.Abs(area) / 2.0;
        Perimeter = perimeter;
    }

    public override void Draw(IRenderContext context)
    {
        if (BoundaryPoints.Count < 2) return;
        
        uint drawColor = IsSelected ? 0xFF00FFFF : 0xFF808080;
        double thickness = IsSelected ? 2.0 : 0.8;

        for (int i = 0; i < BoundaryPoints.Count; i++)
        {
            var p1 = BoundaryPoints[i];
            var p2 = BoundaryPoints[(i + 1) % BoundaryPoints.Count];
            context.DrawLine(p1, p2, drawColor, thickness);
        }

        var center = GetBoundingBox().Center;
        
        // --- 1. Şeffaf UI Arka Plan Çerçevesi (Render Overlay) ---
        // Yazıların okunabilirliğini artırmak için odanın ortasına çerçeve (kutu) çizer
        double boxHWidth = 60.0;
        double boxHHeight = (Fixtures.Count > 0) ? 40.0 : 25.0;
        
        var min = new Vector3D(center.X - boxHWidth, center.Y - boxHHeight, 0);
        var max = new Vector3D(center.X + boxHWidth, center.Y + boxHHeight, 0);
        
        // Çerçeve (Opaklık ayarlı arka plan rengi: 0x88202020 -> Yarı saydam koyu gri)
        context.DrawRectangle(min, max, 0x88202020, 1.5);
        context.DrawRectangle(new Vector3D(min.X-2, min.Y-2, 0), new Vector3D(max.X+2, max.Y+2, 0), 0xFFFFAA00, 0.5); // İnce Turuncu Vurgu Sınırı

        // --- 2. Metin İçerikleri ---
        context.DrawText($"{MahalName.ToUpper()} [{MahalType}]", center + new Vector3D(0, 15, 0), 0, 15, 0xFFFFFFFF, true);
        context.DrawText($"{Area:F2} m²", center + new Vector3D(0, -5, 0), 0, 12, 0xFFAAAAAA, true);

        if (Fixtures.Count > 0)
        {
            string hydInfo = $"LU: {TotalLoadUnits:F1} | Ø: DN{CalculatedPipeDiameter:F0}";
            context.DrawText(hydInfo, center + new Vector3D(0, -25, 0), 0, 12, 0xFF00FF00, true);
        }
    }

    public override void Move(Vector3D delta)
    {
        for (int i = 0; i < BoundaryPoints.Count; i++) BoundaryPoints[i] += delta;
        foreach (var fix in Fixtures) fix.Move(delta);
    }

    public override void Transform(Matrix4x4 matrix)
    {
        for (int i = 0; i < BoundaryPoints.Count; i++)
            BoundaryPoints[i] = matrix.Transform(BoundaryPoints[i]);
        CalculateGeometry();
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        if (BoundaryPoints.Count == 0) return CadBoundingBox.Empty;
        return new CadBoundingBox(
            new Vector3D(BoundaryPoints.Min(p => p.X), BoundaryPoints.Min(p => p.Y), 0),
            new Vector3D(BoundaryPoints.Max(p => p.X), BoundaryPoints.Max(p => p.Y), 0)
        );
    }

    public override CadEntity Clone()
    {
        var clone = new MahalEntity(BoundaryPoints, MahalName, MahalType)
        {
            FloorIndex = this.FloorIndex,
            CalculatedPipeDiameter = this.CalculatedPipeDiameter,
            DesignFlow = this.DesignFlow,
            Color = this.Color,
            Layer = this.Layer
        };
        foreach(var f in Fixtures) clone.Fixtures.Add((SanitaryFixtureEntity)f.Clone());
        return clone;
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        foreach (var p in BoundaryPoints) yield return new SnapPoint(p, SnapPointType.Endpoint);
        yield return new SnapPoint(GetBoundingBox().Center, SnapPointType.Center);
    }
}
