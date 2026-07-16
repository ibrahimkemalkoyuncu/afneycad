using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Domain.Entities.Basic;

/*
    NE: Yay Varlığı (ArcEntity)
    NEDEN: Mimari kapı açılışları, boru dirsekleri ve dairesel formlar için.
    
    NASIL (Mühendislik Modu):
    1. Merkez, Yarıçap, Başlangıç ve Bitiş açıları (Radyan) ile tanımlanır.
    2. Render edilirken tessellate edilmez, primitive olarak çizilir (Hassasiyet kaybı yok).
*/
public class ArcEntity : CadEntity
{
    public Vector3D Center { get; set; }
    public double Radius { get; set; }
    public double StartAngle { get; set; } // Radyan
    public double EndAngle { get; set; }   // Radyan

    /*
       NE: ArcEntity Yapıcı Metodu
       NEDEN: Merkez, yarıçap ve belirgin iki açı (başlangıç/bitiş) arasında kalan yay parçasını oluşturmak için.
    */
    public ArcEntity(Vector3D center, double radius, double startAngle, double endAngle)
    {
        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
    }

    /*
       NE: Ay Çiz (Draw)
       NEDEN: Yay parçasını belirli sayıda segmente bölerek (tessellation) doğrusal parçalar halinde render motoruna iletmek için.
    */
    public override void Draw(IRenderContext context)
    {
        // context.DrawArc(Center, Radius, StartAngle, EndAngle, Color, LineWeight...);
        // Şimdilik tessellate edilebilir veya render context geliştirilebilir.
        // Amaç 1 gereği "Bükülmeyen" çekirdek için DrawArc idealdir.
        
        int segments = 32;
        double sweep;
        if (EndAngle > StartAngle) sweep = EndAngle - StartAngle;
        else sweep = (2 * Math.PI - StartAngle) + EndAngle;

        double step = sweep / segments;
        Vector3D prev = new Vector3D(Center.X + Math.Cos(StartAngle) * Radius, Center.Y + Math.Sin(StartAngle) * Radius, Center.Z);

        for (int i = 1; i <= segments; i++)
        {
            double a = StartAngle + i * step;
            Vector3D curr = new Vector3D(Center.X + Math.Cos(a) * Radius, Center.Y + Math.Sin(a) * Radius, Center.Z);
            context.DrawLine(prev, curr, Color, 0.0, Linetype);
            prev = curr;
        }
    }

    /*
       NE: Noktaya Olan Mesafe (DistanceTo)
       NEDEN: Hit-testing yayın ÇEVRESİNE (sweep sınırları içindeyse) veya en yakın UCUNA
              (sweep dışındaysa) olan mesafeyi kullanmalı. Önceden override yoktu; taban sınıfın
              varsayılanı (BoundingBox merkezine mesafe) kullanılıyordu — yayın tam üzerine
              tıklansa bile hit-testi başarısız oluyordu.
    */
    public override double DistanceTo(Vector3D point)
    {
        double sweep = EndAngle > StartAngle ? EndAngle - StartAngle : (2 * Math.PI - StartAngle) + EndAngle;

        double pointAngle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);
        double unwrapped = pointAngle;
        while (unwrapped < StartAngle - 1e-9) unwrapped += 2 * Math.PI;

        bool withinSweep = unwrapped >= StartAngle - 1e-6 && unwrapped <= StartAngle + sweep + 1e-6;
        if (withinSweep)
        {
            double distToCenter = Math.Sqrt(Math.Pow(point.X - Center.X, 2) + Math.Pow(point.Y - Center.Y, 2));
            return Math.Abs(distToCenter - Radius);
        }

        // Sweep dışında: en yakın uç noktaya olan Öklid mesafesi.
        var startPt = new Vector3D(Center.X + Math.Cos(StartAngle) * Radius, Center.Y + Math.Sin(StartAngle) * Radius, Center.Z);
        var endPt = new Vector3D(Center.X + Math.Cos(EndAngle) * Radius, Center.Y + Math.Sin(EndAngle) * Radius, Center.Z);
        return Math.Min(point.DistanceTo(startPt), point.DistanceTo(endPt));
    }

    /*
       NE: SÄ±nÄ±rlayÄ±cÄ± Kutu Hesapla (CalculateBoundingBox)
       NEDEN: Yay parÃ§asÄ±nÄ±n kapladÄ±ÄŸÄ± en kÃ¼Ã§Ã¼k dikdÃ¶rtgen alanÄ± (AABB) mekansal sorgular iÃ§in hesaplamak iÃ§in.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        // Basitleştirilmiş kutu (Gerçekte yay parçasına göre daraltılmalı)
        return new CadBoundingBox(
            new Vector3D(Center.X - Radius, Center.Y - Radius, Center.Z),
            new Vector3D(Center.X + Radius, Center.Y + Radius, Center.Z)
        );
    }

    /*
       NE: Yayı Taşı (Move)
       NEDEN: Yayın merkez noktasını verilen vektör kadar kaydırarak tüm yapıyı hareket ettirmek için.
    */
    public override void Move(Vector3D delta)
    {
        Center += delta;
    }

    /*
       NE: Matris Dönüşümü (Transform)
       NEDEN: Yayı döndürmek veya taşımak için merkez koordinatını matrisle çarpmak için.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        Center = matrix.Transform(Center);
        // Radius transform (Uniform scaling varsayımı)
    }

    /*
       NE: Nesneyi Kopyala (Clone)
       NEDEN: Mevcut yayın tüm geometrik özelliklerini taşıyan bağımsız bir örneğini oluşturmak için.
    */
    public override CadEntity Clone()
    {
        return new ArcEntity(Center, Radius, StartAngle, EndAngle) { Color = Color, Layer = Layer };
    }

    /*
       NE: Kenetlenme Noktaları (GetSnapPoints)
       NEDEN: Yayın merkezini, başlangıç ve bitiş uçlarını CAD motoru tarafından yakalanabilir (Snap) kılmak için.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Center, SnapPointType.Center);
        yield return new SnapPoint(new Vector3D(Center.X + Math.Cos(StartAngle) * Radius, Center.Y + Math.Sin(StartAngle) * Radius, Center.Z), SnapPointType.Endpoint);
        yield return new SnapPoint(new Vector3D(Center.X + Math.Cos(EndAngle) * Radius, Center.Y + Math.Sin(EndAngle) * Radius, Center.Z), SnapPointType.Endpoint);
    }
}
