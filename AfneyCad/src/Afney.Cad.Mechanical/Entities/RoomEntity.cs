using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Entities;

/*
    NE: Mahal (Room) Varlığı
    NEDEN: Mimari plandaki kapalı alanları (oda, koridor, banyo vb.) temsil etmek için.
    
    ÖZELLİKLER:
    - Boundary: Odanın geometrik sınırları (Polyline).
    - Area: Alan (m²).
    - Name: Mahal adı (örn: "Banyo").
    - Fixtures: Odadaki sıhhi tesisat armatürleri.
*/
public class RoomEntity : CadEntity
{
    public LwPolylineEntity Boundary { get; private set; }
    public string Name { get; set; } = "Mahal";
    public double Area { get; private set; }
    // Brüt çevre — kapı/pencere açıklıkları duvarın devamı sayılır (mm → m)
    public double Perimeter { get; private set; }
    // Net çevre — kapı/pencere genişlikleri düşülmüş (mm → m)
    public double NetPerimeter { get; private set; }
    // Düşülen toplam açıklık genişliği (m)
    public double TotalOpeningWidth { get; private set; }
    public List<CadEntity> Fixtures { get; private set; } = new List<CadEntity>();
    public double TotalLoadUnits { get; set; } = 0;
    public Afney.Cad.Mechanical.Enums.RoomType Type { get; set; } = Afney.Cad.Mechanical.Enums.RoomType.Unknown;
    
    // Malzeme Bilgileri (Material Information)
    public string FloorMaterial { get; set; } = "";
    public string WallMaterial { get; set; } = "";
    public string CeilingMaterial { get; set; } = "";
    
    // Mühendislik Verileri (Engineering Data)
    public int FloorIndex { get; set; } = 0;
    public double DesignFlow { get; set; } // l/s
    public double CalculatedPipeDiameter { get; set; } // DN
    
    private string _name = "Mahal";

    // Compatibility properties
    public string RoomName { get { return _name; } set { _name = value; } }
    public List<Vector3D> BoundaryPoints { get { return Boundary.Vertices; } }

    public RoomEntity(LwPolylineEntity boundary)
    {
        Boundary = boundary;
        CalculateArea();
    }

    // Constructor used by DetectRoomCommand
    public RoomEntity(List<Vector3D> points, string name)
    {
        Boundary = new LwPolylineEntity(points, true);
        _name = name;
        CalculateArea();
    }

    private void CalculateArea()
    {
        if (Boundary.Vertices.Count < 3) return;

        double area = 0;
        double perim = 0;
        for (int i = 0; i < Boundary.Vertices.Count; i++)
        {
            var p1 = Boundary.Vertices[i];
            var p2 = Boundary.Vertices[(i + 1) % Boundary.Vertices.Count];
            area += (p1.X * p2.Y - p2.X * p1.Y);
            perim += p1.DistanceTo(p2);
        }
        Area = System.Math.Abs(area / 2.0) / 1000000.0; // mm² → m²
        Perimeter = perim / 1000.0;                       // mm → m (brüt)
        NetPerimeter = Perimeter - TotalOpeningWidth;
    }

    // Kapı ve pencere açıklık genişliklerini net çevre hesabı için kaydet.
    // openingWidthsMm: her bir kapı/pencere açıklığının genişliği mm cinsinden.
    public void SetOpenings(IEnumerable<double> openingWidthsMm)
    {
        TotalOpeningWidth = 0;
        foreach (var w in openingWidthsMm)
            TotalOpeningWidth += w / 1000.0; // mm → m
        NetPerimeter = Perimeter - TotalOpeningWidth;
    }

    public override void Draw(IRenderContext context)
    {
        // Sınırları çiz (vurgulu)
        var vertices = Boundary.Vertices;
        if (vertices == null || vertices.Count < 2) return;

        uint color = IsSelected ? 0x8800FF00 : Color;
        double thickness = IsSelected ? 2.0 : 1.0;

        for (int i = 0; i < vertices.Count - 1; i++)
        {
            context.DrawLine(vertices[i], vertices[i + 1], color, thickness);
        }
        
        // Kapat
        if (vertices.Count > 2)
        {
            context.DrawLine(vertices.Last(), vertices.First(), color, thickness);
        }
    }
    
    protected override CadBoundingBox CalculateBoundingBox()
    {
        return Boundary.GetBoundingBox();
    }

    public override void Move(Vector3D delta)
    {
        Boundary.Move(delta);
        foreach (var fixture in Fixtures)
        {
            fixture.Move(delta);
        }
    }

    public override void Transform(Matrix4x4 matrix)
    {
        Boundary.Transform(matrix);
        foreach (var fixture in Fixtures)
        {
            fixture.Transform(matrix);
        }
        CalculateArea(); // Alan değişmiş olabilir
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        return Boundary.GetSnapPoints();
    }

    public override CadEntity Clone()
    {
        return new RoomEntity((LwPolylineEntity)Boundary.Clone())
        {
            RoomName = this.RoomName,
            TotalLoadUnits = this.TotalLoadUnits,
            Type = this.Type,
            FloorMaterial = this.FloorMaterial,
            WallMaterial = this.WallMaterial,
            CeilingMaterial = this.CeilingMaterial,
            FloorIndex = this.FloorIndex,
            DesignFlow = this.DesignFlow,
            CalculatedPipeDiameter = this.CalculatedPipeDiameter,
            Layer = this.Layer,
            Color = this.Color
        };
    }
}
