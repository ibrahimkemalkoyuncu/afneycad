using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Yağmur Düşme Alanı (RainfallCatchmentEntity)
   NEDEN: OtoNET'teki "Yağmur Düşme Alanı" komutunun AfneyCAD karşılığı.
          Çatı, teras veya platform üzerinde yağmur suyu toplanan alanı polygon olarak tanımlar.

   MÜHENDİSLİK DETAYI:
   - Kullanıcı çatı üzerinde köşe noktalarını tıklayarak kapalı bir polygon çizer.
   - Program polygon alanını (m²) otomatik hesaplar.
   - Yüzey Türü (düz çatı, yeşil çatı, eğimli çatı) akış katsayısını (C) belirler.
   - WasteWaterDesignService.CalculateRainwaterFlow() bu entity'yi CatchmentArea'ya dönüştürür.
   - Q = r * C * A / 10000 formülü ile debi hesaplanır (TS EN 12056-3).
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

    private List<Vector3D> _vertices = new();
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

    // Akış katsayısı — yüzey tipine göre otomatik
    public double RunoffCoefficient => _surfaceType switch
    {
        SurfaceType.FlatRoof => 1.0,
        SurfaceType.GreenRoof => 0.5,
        SurfaceType.GravelRoof => 0.7,
        SurfaceType.PavedTerrace => 0.9,
        SurfaceType.SlopedRoof => 1.0,
        _ => 1.0
    };

    // Polygon alanı — Shoelace (Gauss) formülü ile hesaplanır (m²)
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

    public void AddVertex(Vector3D point) => _vertices.Add(point);

    public void ClosePolygon()
    {
        // İlk ve son nokta çok yakınsa zaten kapalıdır
        if (_vertices.Count > 2 && (_vertices[0] - _vertices[^1]).Length > 0.01)
            _vertices.Add(_vertices[0]);
    }

    // Merkez nokta — etiket yerleştirme için
    public Vector3D Centroid
    {
        get
        {
            if (_vertices.Count == 0) return Vector3D.Zero;
            double cx = _vertices.Average(v => v.X);
            double cy = _vertices.Average(v => v.Y);
            return new Vector3D(cx, cy, Position.Z);
        }
    }

    public override IEnumerable<MechanicalPort> GetPorts() => Enumerable.Empty<MechanicalPort>();

    public override void Draw(IRenderContext ctx)
    {
        if (_vertices.Count < 3) return;

        // Yarı şeffaf mavi dolgu + mavi çerçeve
        var fillColor = new SkiaSharp.SKColor(0, 120, 255, 50);
        var borderColor = new SkiaSharp.SKColor(0, 120, 255, 200);

        var pts = _vertices.Select(v => (v.X, v.Y)).ToList();

        // Polygon kenarlarını çiz
        for (int i = 0; i < pts.Count - 1; i++)
        {
            ctx.DrawLine(
                (float)pts[i].X, (float)pts[i].Y,
                (float)pts[i + 1].X, (float)pts[i + 1].Y,
                borderColor, 1.5f);
        }

        // Alan ve isim etiketi — centroid'de
        var c = Centroid;
        string label = $"{_areaName}\n{AreaM2:F1} m²\nC={RunoffCoefficient:F1}";
        ctx.DrawText(label, (float)c.X, (float)c.Y, 10f, borderColor);
    }

    public override CadEntity Clone()
    {
        var clone = new RainfallCatchmentEntity
        {
            _areaName = _areaName,
            _surfaceType = _surfaceType,
            LayerId = LayerId
        };
        clone._vertices.AddRange(_vertices);
        return clone;
    }
}
