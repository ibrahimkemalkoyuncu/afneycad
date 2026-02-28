using System;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Entities.Basic;

public class CircleEntity : CadEntity
{
    public Vector3D Center { get; set; }
    public double Radius { get; set; }

    /*
       NE: CircleEntity Yapıcı Metodu
       NEDEN: Merkez noktası ve yarıçap bilgisiyle yeni bir çember nesnesi oluşturmak için.
    */
    public CircleEntity(Vector3D center, double radius)
    {
        Center = center;
        Radius = radius;
    }

    /*
       NE: Çember Çiz (Draw)
       NEDEN: Geometri verisini render motoruna göndererek ekranda halka formunda görünmesini sağlamak için.
    */
    public override void Draw(IRenderContext context)
    {
        context.DrawCircle(Center, Radius, Color, 0.0);
    }

    /*
       NE: Sınır Kutusu Hesapla (CalculateBoundingBox)
       NEDEN: Çemberin kapladığı alanı (Merkez +/- Yarıçap) belirlemek için.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        return new CadBoundingBox(
            new Vector3D(Center.X - Radius, Center.Y - Radius, Center.Z),
            new Vector3D(Center.X + Radius, Center.Y + Radius, Center.Z)
        );
    }

    /*
       NE: Çemberi Taşı (Move)
       NEDEN: Merkez noktasını verilen fark vektörü kadar kaydırmak için.
    */
    public override void Move(Vector3D delta)
    {
        Center = new Vector3D(Center.X + delta.X, Center.Y + delta.Y, Center.Z + delta.Z);
    }

    /*
       NE: Kenetlenme Noktaları (SnapPoints)
       NEDEN: Çemberin merkezini ve dört ana (kuadrant) noktasını yakalanabilir kılmak için.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Center, SnapPointType.Center);
        
        // Quadrants (0, 90, 180, 270 degrees)
        yield return new SnapPoint(new Vector3D(Center.X + Radius, Center.Y, Center.Z), SnapPointType.Quadrant);
        yield return new SnapPoint(new Vector3D(Center.X - Radius, Center.Y, Center.Z), SnapPointType.Quadrant);
        yield return new SnapPoint(new Vector3D(Center.X, Center.Y + Radius, Center.Z), SnapPointType.Quadrant);
        yield return new SnapPoint(new Vector3D(Center.X, Center.Y - Radius, Center.Z), SnapPointType.Quadrant);
    }

    /*
       NE: Matris Dönüşümü (Transform)
       NEDEN: Çember merkezine matris (taşıma vb.) uygulamak için.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        Center = matrix.Transform(Center);
    }

    /*
       NE: Nesneyi Klone Et (Clone)
       NEDEN: Çemberin tam bir kopyasını oluşturmak için.
    */
    public override CadEntity Clone()
    {
        var clone = new CircleEntity(Center, Radius);
        CopyBaseProperties(clone);
        return clone;
    }

    /*
       NE: Grip Noktaları
       NEDEN: Merkez ve kuadrant noktalarında mavi kontroller çıkarmak için.
    */
    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return Center;
        yield return new Vector3D(Center.X + Radius, Center.Y, Center.Z);
        yield return new Vector3D(Center.X - Radius, Center.Y, Center.Z);
        yield return new Vector3D(Center.X, Center.Y + Radius, Center.Z);
        yield return new Vector3D(Center.X, Center.Y - Radius, Center.Z);
    }
}
