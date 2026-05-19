/*
 * DOSYA: HatchEntity.cs
 * AMAÇ: AutoCAD HATCH entity'sinin solid fill alanlarını temsil eder.
 * 
 * MÜHENDİSLİK NOTU:
 * AutoCAD'de kolonlar, oda alanları ve benzeri bölgeler "Hatch" ile doldurulur.
 * Bu entity sadece solid/gradient fill'ı destekler (çizgili hatch pattern'lar kapsam dışıdır).
 * Render sırasında DrawFilledPolygon() ile yarı şeffaf dolgu + hairline kontur çizilir.
 */
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Entities.Basic;

public class HatchEntity : CadEntity
{
    /// <summary>Dış sınır (boundary) köşe noktaları — kapalı poligon.</summary>
    public List<Vector3D> BoundaryVertices { get; set; } = new();

    /// <summary>Dolgu şeffaflığı (0=tamamen şeffaf, 255=opak). Default 70.</summary>
    public byte FillAlpha { get; set; } = 70;

    // Parameterless constructor (serializasyon için)
    public HatchEntity() { }

    public HatchEntity(IEnumerable<Vector3D> boundary, uint color, byte alpha = 70)
    {
        BoundaryVertices = boundary.ToList();
        Color = color;
        FillAlpha = alpha;
    }

    /*
       NE: Çiz (Draw)
       NEDEN: Kontur poligonunu yarı şeffaf dolgu + hairline kenar çizgisiyle render etmek için.
    */
    public override void Draw(IRenderContext context)
    {
        if (BoundaryVertices == null || BoundaryVertices.Count < 3) return;
        context.DrawFilledPolygon(BoundaryVertices, Color, FillAlpha);
    }

    /*
       NE: Sınırlayıcı Kutu (CalculateBoundingBox)
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        if (BoundaryVertices == null || BoundaryVertices.Count == 0) return CadBoundingBox.Empty;

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var v in BoundaryVertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.Z < minZ) minZ = v.Z;
            if (v.X > maxX) maxX = v.X;
            if (v.Y > maxY) maxY = v.Y;
            if (v.Z > maxZ) maxZ = v.Z;
        }

        return new CadBoundingBox(new Vector3D(minX, minY, minZ), new Vector3D(maxX, maxY, maxZ));
    }

    public override void Move(Vector3D delta)
    {
        for (int i = 0; i < BoundaryVertices.Count; i++)
            BoundaryVertices[i] += delta;
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        if (BoundaryVertices == null) yield break;
        foreach (var v in BoundaryVertices)
            yield return new SnapPoint(v, SnapPointType.Endpoint);
    }

    public override void Transform(Matrix4x4 matrix)
    {
        for (int i = 0; i < BoundaryVertices.Count; i++)
            BoundaryVertices[i] = matrix.Transform(BoundaryVertices[i]);
    }

    public override CadEntity Clone()
    {
        var clone = new HatchEntity(new List<Vector3D>(BoundaryVertices), Color, FillAlpha)
        {
            Layer = this.Layer,
            Linetype = this.Linetype
        };
        return clone;
    }

    public override IEnumerable<Vector3D> GetGripPoints()
    {
        if (BoundaryVertices == null) yield break;
        foreach (var v in BoundaryVertices)
            yield return v;
    }
}
