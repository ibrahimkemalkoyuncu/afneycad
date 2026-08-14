using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Tests.Domain;

/*
   NE: Sahte Render Bağlamı (FakeRenderContext)
   NEDEN: DimensionEntity gibi Draw() içinde ok başı stiline göre farklı ilkel çağrılar
          (DrawFilledPolygon/DrawLine) yapan entity'leri, gerçek bir Skia yüzeyi kurmadan
          test edebilmek için — hangi ilkellerin kaç kez ve ne parametreyle çağrıldığını kaydeder.
*/
public class FakeRenderContext : IRenderContext
{
    public double PixelSize => 1.0;
    public bool IsHighlightMode { get; set; }

    public List<(Vector3D p1, Vector3D p2)> Lines { get; } = new();
    public List<(IReadOnlyList<Vector3D> vertices, uint color)> FilledPolygons { get; } = new();
    public List<(string text, Vector3D pos)> Texts { get; } = new();

    public void DrawLine(Vector3D p1, Vector3D p2, uint color, double thickness = 1.0, string linetype = "Continuous", bool isDashed = false)
        => Lines.Add((p1, p2));

    public void DrawLines(IEnumerable<(Vector3D start, Vector3D end)> segments, uint color, double thickness = 1.0, string linetype = "Continuous", bool isDashed = false)
    {
        foreach (var s in segments) Lines.Add((s.start, s.end));
    }

    public void DrawCircle(Vector3D center, double radius, uint color, double thickness, bool isDashed = false) { }
    public void DrawArc(Vector3D center, double radius, double startAngle, double endAngle, uint color, double thickness, bool isDashed = false) { }
    public void DrawRectangle(Vector3D min, Vector3D max, uint color, double thickness, bool isDashed = false) { }
    public void DrawSolidLine(Vector3D p1, Vector3D p2, uint color, double innerDiameter, double outerDiameter) { }
    public void DrawSpline(IReadOnlyList<Vector3D> points, uint color, double thickness, string linetype = "Continuous") { }

    public void DrawText(string text, Vector3D position, double angleDegrees, double fontSize, uint color, bool centerAlign = true)
        => Texts.Add((text, position));

    public void DrawFilledPolygon(IEnumerable<Vector3D> vertices, uint color, byte alpha = 80)
        => FilledPolygons.Add((new List<Vector3D>(vertices), color));
}
