using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Yağmur Düşme Alanı (RainfallCatchmentEntity)
   NEDEN: OtoNET'teki "Yağmur Düşme Alanı" komutunun AfneyCAD karşılığı.
          Çatı/teras üzerinde yağmur suyu toplanan alanı polygon olarak tanımlar.
   FORMÜL: Q = r * C * A / 10000  (TS EN 12056-3)
*/
public class RainfallCatchmentEntity : MechanicalEntity
{
    public enum SurfaceType
    {
        FlatRoof,       // Düz çatı / teras — C = 1.0
        GreenRoof,      // Yeşil çatı — C = 0.5
        GravelRoof,     // Çakıl çatı — C = 0.7
        PavedTerrace,   // Döşemeli teras — C = 0.9
        SlopedRoof      // Eğimli çatı — C = 1.0
    }

    // Polygon merkezi — konumlandırma için
    public Vector3D Position { get; set; } = Vector3D.Zero;

    private readonly List<Vector3D> _vertices = [];
    private SurfaceType _surfaceType = SurfaceType.FlatRoof;
    private string _areaName = "Çatı Alanı";

    public IReadOnlyList<Vector3D> Vertices => _vertices.AsReadOnly();

    public SurfaceType Surface
    {
        get => _surfaceType;
        set { _surfaceType = value; OnMetadataChanged(); }
    }

    public string AreaName
    {
        get => _areaName;
        set { _areaName = value; OnMetadataChanged(); }
    }

    public double RunoffCoefficient => _surfaceType switch
    {
        SurfaceType.GreenRoof    => 0.5,
        SurfaceType.GravelRoof   => 0.7,
        SurfaceType.PavedTerrace => 0.9,
        _                        => 1.0
    };

    // Shoelace (Gauss) formülü ile m² cinsinden alan
    public double AreaM2
    {
        get
        {
            if (_vertices.Count < 3) return 0;
            double area = 0;
            int n = _vertices.Count;
            for (int i = 0; i < n; i++)
            {
                var a = _vertices[i];
                var b = _vertices[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            return System.Math.Abs(area) / 2.0;
        }
    }

    public RainfallCatchmentEntity()
    {
        SystemType = MechanicalSystemType.RainWater;
    }

    public void AddVertex(Vector3D point)
    {
        _vertices.Add(point);
        InvalidateCache();
    }

    public void ClosePolygon()
    {
        if (_vertices.Count > 2 && (_vertices[0] - _vertices[^1]).Length() > 0.01)
            _vertices.Add(_vertices[0]);
        InvalidateCache();
    }

    public Vector3D Centroid
    {
        get
        {
            if (_vertices.Count == 0) return Position;
            double cx = _vertices.Average(v => v.X);
            double cy = _vertices.Average(v => v.Y);
            return new Vector3D(cx, cy, Position.Z);
        }
    }

    public override List<MechanicalPort> GetPorts() => [];

    public override void Draw(IRenderContext ctx)
    {
        if (_vertices.Count < 3) return;

        const uint border = 0xFF0078FF; // ARGB: opak, mavi — yağmur suyu rengi

        for (int i = 0; i < _vertices.Count - 1; i++)
        {
            ctx.DrawLine(_vertices[i], _vertices[i + 1], border, 1.5);
        }

        var c = Centroid;
        string label = $"{_areaName}  {AreaM2:F1} m²  C={RunoffCoefficient:F1}";
        ctx.DrawText(label, c, 0, 120, border);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        if (_vertices.Count == 0)
            return new CadBoundingBox(Position, Position);

        double minX = _vertices.Min(v => v.X);
        double minY = _vertices.Min(v => v.Y);
        double maxX = _vertices.Max(v => v.X);
        double maxY = _vertices.Max(v => v.Y);
        return new CadBoundingBox(new Vector3D(minX, minY, 0), new Vector3D(maxX, maxY, 0));
    }

    public override void Move(Vector3D delta)
    {
        for (int i = 0; i < _vertices.Count; i++)
            _vertices[i] = new Vector3D(_vertices[i].X + delta.X, _vertices[i].Y + delta.Y, _vertices[i].Z + delta.Z);
        Position = new Vector3D(Position.X + delta.X, Position.Y + delta.Y, Position.Z + delta.Z);
        InvalidateCache();
    }

    public override void Transform(Matrix4x4 matrix)
    {
        for (int i = 0; i < _vertices.Count; i++)
            _vertices[i] = matrix.Transform(_vertices[i]);
        Position = matrix.Transform(Position);
        InvalidateCache();
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        foreach (var v in _vertices)
            yield return new SnapPoint(v, SnapPointType.Endpoint);
        if (_vertices.Count > 0)
            yield return new SnapPoint(Centroid, SnapPointType.Center);
    }

    /*
       NE: Grip Noktaları (GetGripPoints / MoveGripPointAt)
       NEDEN: Önceden hiç override yoktu — yağmur düşme alanı poligonu köşe köşe
              düzenlenemiyordu.
    */
    public override IEnumerable<Vector3D> GetGripPoints() => _vertices;

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (index >= 0 && index < _vertices.Count)
        {
            _vertices[index] = newPosition;
            InvalidateCache();
        }
        base.MoveGripPointAt(index, newPosition);
    }

    public override CadEntity Clone()
    {
        var clone = new RainfallCatchmentEntity
        {
            _areaName    = _areaName,
            _surfaceType = _surfaceType,
            Layer        = Layer,
            Position     = Position
        };
        clone._vertices.AddRange(_vertices);
        return clone;
    }
}
