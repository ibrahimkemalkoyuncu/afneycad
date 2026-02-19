using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Entities.Basic;

public class LineEntity : CadEntity
{
    public Vector3D StartPoint { get; set; }
    public Vector3D EndPoint { get; set; }

    /*
       NE: LineEntity Yapıcı Metodu
       NEDEN: Verilen başlangıç ve bitiş koordinatlarıyla yeni bir çizgi nesnesi oluşturmak için.
    */
    public LineEntity(Vector3D start, Vector3D end)
    {
        StartPoint = start;
        EndPoint = end;
    }

    /*
        NE: Çizgi Uzunluğu
        NEDEN: Ölçeklendirme ve birim tespiti algoritmalarında kullanmak için.
    */
    public double GetLength() => (EndPoint - StartPoint).Length();

    /*
       NE: Çiz (Draw)
       NEDEN: Çizginin dünya koordinatlarını render motoruna ileterek ekranda temsil edilmesini sağlamak için.
    */
    public override void Draw(IRenderContext ctx)
    {
        // Lineweight (mm) -> Ekranda sabit piksel kalınlığı (Zoom'dan bağımsız)
        // LineWeight=25 (0.25mm) -> ~2 piksel
        // PixelSize = 1/Zoom
        
        // MÜHENDİSLİK DÜZELTMESİ (GÖRÜNTÜ KALİTESİ):
        // Uzaklaşınca (Zoom Out) çizgilerin kalınlaşarak ekranı kaplaması sorununu çözmek için
        // Kalınlığı "Hairline" (0) olarak sabitliyoruz.
        double drawThickness = 0.0;

        ctx.DrawLine(StartPoint, EndPoint, Color, drawThickness, Linetype);
    }

    /*
       NE: Sınır Kutusu Hesapla (CalculateBoundingBox)
       NEDEN: Çizginin başlangıç ve bitiş noktalarını kapsayan min/max koordinatlarını belirlemek için.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        return new CadBoundingBox(
            new Vector3D(System.Math.Min(StartPoint.X, EndPoint.X), System.Math.Min(StartPoint.Y, EndPoint.Y), System.Math.Min(StartPoint.Z, EndPoint.Z)),
            new Vector3D(System.Math.Max(StartPoint.X, EndPoint.X), System.Math.Max(StartPoint.Y, EndPoint.Y), System.Math.Max(StartPoint.Z, EndPoint.Z))
        );
    }

    /*
       NE: Çizgiyi Taşı (Move)
       NEDEN: Her iki uç noktayı da verilen fark vektörü kadar kaydırmak için.
    */
    public override void Move(Vector3D delta)
    {
        StartPoint = new Vector3D(StartPoint.X + delta.X, StartPoint.Y + delta.Y, StartPoint.Z + delta.Z);
        EndPoint = new Vector3D(EndPoint.X + delta.X, EndPoint.Y + delta.Y, EndPoint.Z + delta.Z);
    }

    /*
       NE: Kenetlenme Noktaları (SnapPoints)
       NEDEN: Çizginin başlangıç, bitiş ve orta noktalarının CAD motoru tarafından yakalanabilmesini sağlamak için.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(StartPoint, SnapPointType.Endpoint);
        yield return new SnapPoint(EndPoint, SnapPointType.Endpoint);
        
        var mid = new Vector3D((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, (StartPoint.Z + EndPoint.Z) / 2);
        yield return new SnapPoint(mid, SnapPointType.Midpoint);
    }

    /*
       NE: Matris Dönüşümü (Transform)
       NEDEN: Çizgiyi döndürmek, ölçeklemek veya projeksiyon uygulamak için uç noktalarını matrisle çarpmak.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        StartPoint = matrix.Transform(StartPoint);
        EndPoint = matrix.Transform(EndPoint);
    }

    /*
       NE: Nesneyi Klone Et (Clone)
       NEDEN: Bu çizginin özelliklerini yeni bir nesneye birebir aktarmak için.
    */
    public override CadEntity Clone()
    {
        var clone = new LineEntity(StartPoint, EndPoint);
        CopyBaseProperties(clone);
        return clone;
    }
}
