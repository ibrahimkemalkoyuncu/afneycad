using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Abstractions;

/*
   NE: Kenetlenme Noktası Tipi (SnapPointType)
   NEDEN: Çizim sırasında farenin hangi geometrik özelliğe (Uç, Orta, Merkez vb.) kilitlendiğini ayırt etmek için.
*/
public enum SnapPointType
{
    None,
    Endpoint,
    Midpoint,
    Center,
    Quadrant,
    Intersection,
    Nearest,
    Perpendicular,
    Tangent,
    Connection, // Mekanik Bağlantı Noktası (Port)
    ControlPoint, // Spline/NURBS Kontrol Noktası
    Insertion // Blok Yerleştirme Noktası
}

public readonly record struct SnapPoint(Vector3D Position, SnapPointType Type)
{
    public double DistanceTo(Vector3D other) => Position.DistanceTo(other);
}