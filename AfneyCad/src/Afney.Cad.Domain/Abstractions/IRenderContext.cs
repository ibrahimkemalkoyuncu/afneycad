using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Abstractions; // Namespace değişti

/*
NE:
Render Context Arayüzü (DIP gereği Domain içinde).

NE İÇİN:
Domain entitylerinin kendilerini çizebilmesi için.

NEREDE:
Domain Layer.
*/
public interface IRenderContext
{
    // View Context Info
    double PixelSize { get; }
    bool IsHighlightMode { get; set; }

    void DrawLine(Vector3D p1, Vector3D p2, uint color, double thickness = 1.0, string linetype = "Continuous", bool isDashed = false);
    
    // NE: Toplu Çizgi Çizimi (Batching)
    // NEDEN: Binlerce çizgiyi tek bir emirle ekran kartına göndererek performansı (FPS) katlamak için.
    void DrawLines(IEnumerable<(Vector3D start, Vector3D end)> segments, uint color, double thickness = 1.0, string linetype = "Continuous", bool isDashed = false);

    void DrawCircle(Vector3D center, double radius, uint color, double thickness, bool isDashed = false);
    void DrawArc(Vector3D center, double radius, double startAngle, double endAngle, uint color, double thickness, bool isDashed = false);
    void DrawRectangle(Vector3D min, Vector3D max, uint color, double thickness, bool isDashed = false);
    
    // NE: 3D Katı Görünüm Desteği
    // AMACI: Boruları sadece çizgi değil, et kalınlığı ve gölgelendirmesi olan silindirler olarak çizmek için.
    void DrawSolidLine(Vector3D p1, Vector3D p2, uint color, double innerDiameter, double outerDiameter);
    
    void DrawSpline(IReadOnlyList<Vector3D> points, uint color, double thickness, string linetype = "Continuous");
    void DrawText(string text, Vector3D position, double angleDegrees, double fontSize, uint color, bool centerAlign = true);

    // NE: Dolu Çokgen Çiz (Hatch Fill)
    // NEDEN: AutoCAD Hatch entity'lerinin solid/gradient dolgu alanlarını kapalı poligon olarak render etmek için.
    // alpha: 0 (tamamen şeffaf) → 255 (opak). Tipik DXF Hatch için 80 (yarı şeffaf) önerilir.
    void DrawFilledPolygon(IEnumerable<Vector3D> vertices, uint color, byte alpha = 80);
}
