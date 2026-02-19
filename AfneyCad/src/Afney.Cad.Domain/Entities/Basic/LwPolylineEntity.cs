using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Entities.Basic;

public class LwPolylineEntity : CadEntity
{
    public List<Vector3D> Vertices { get; set; } = new();
    public bool IsClosed { get; set; }

    // Parameterless constructor for serialization
    public LwPolylineEntity() { }

    /*
       NE: LwPolylineEntity Yapıcı Metodu
       NEDEN: Verilen köşe noktaları dizisini (Vertex) kullanarak birleşik bir çizgi (Polyline) grubu oluşturmak için.
    */
    public LwPolylineEntity(IEnumerable<Vector3D> vertices, bool isClosed = false)
    {
        Vertices = vertices.ToList();
        IsClosed = isClosed;
    }

    /*
       NE: Çokluçizgi Çiz (Draw)
       NEDEN: Birbirine bağlı tüm segmentleri, katman ve renk bilgilerine uygun olarak ekrana basmak için.
    */
    public override void Draw(IRenderContext context)
    {
        if (Vertices == null || Vertices.Count < 2) return;

        // MÜHENDİSLİK DÜZELTMESİ:
        // Kalınlığı "Hairline" (0) olarak zorluyoruz. 
        // Böylece zoom yapınca veya uzaklaşınca çizgiler ekranı kaplamaz (Gri Blok Sorunu Çözümü).
        double drawThickness = 0.0; 

        for (int i = 0; i < Vertices.Count - 1; i++)
        {
            context.DrawLine(Vertices[i], Vertices[i + 1], Color, drawThickness, Linetype);
        }

        if (IsClosed && Vertices.Count > 2)
        {
            context.DrawLine(Vertices.Last(), Vertices.First(), Color, drawThickness, Linetype);
        }
    }

    /*
       NE: SÄ±nÄ±rlayÄ±cÄ± Kutu Hesapla (CalculateBoundingBox)
       NEDEN: TÃ¼m kÃ¶ÅŸe noktalarÄ±nÄ± tarayarak nesneyi tam iÃ§ine alan en kÃ¼Ã§Ã¼k dikdÃ¶rtgen alanÄ± (AABB) saptamak iÃ§in.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        if (Vertices == null || Vertices.Count == 0) return CadBoundingBox.Empty;

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var v in Vertices)
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

    /*
       NE: Çokluçizgiyi Taşı (Move)
       NEDEN: Tüm köşe noktalarını verilen fark vektörü kadar kaydırarak tüm yapıyı birlikte hareket ettirmek için.
    */
    public override void Move(Vector3D delta)
    {
        for (int i = 0; i < Vertices.Count; i++)
        {
            Vertices[i] += delta;
        }
    }

    /*
       NE: Kenetlenme NoktalarÄ± (GetSnapPoints)
       NEDEN: Ã‡okluÃ§izginin her bir kÃ¶ÅŸesini (Vertex) yakalanabilir birer uÃ§ nokta (Endpoint) olarak dÃ¶ndÃ¼rmek iÃ§in.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        if (Vertices == null) yield break;
        
        foreach (var v in Vertices)
        {
             yield return new SnapPoint(v, SnapPointType.Endpoint);
        }
    }

    /*
       NE: Matris DÃ¶nÃ¼ÅŸtÃ¼rme (Transform)
       NEDEN: DÃ¶ndÃ¼rme, Ã¶lÃ§eklendirme veya dÃ¼zlem deÄŸiÅŸtirme gibi matris tabanlÄ± iÅŸlemleri tÃ¼m kÃ¶ÅŸelere aynÄ± anda uygulamak iÃ§in.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        for (int i = 0; i < Vertices.Count; i++)
        {
            Vertices[i] = matrix.Transform(Vertices[i]);
        }
    }

    /*
       NE: Nesneyi Klone Et (Clone)
       NEDEN: Polyline'ın kopyasını oluştururken tüm köşe noktalarını da derin (deep) kopyalayarak bağımsız bir nesne üretmek için.
    */
    public override CadEntity Clone()
    {
        var newVerts = new List<Vector3D>(Vertices);
        var clone = new LwPolylineEntity(newVerts, IsClosed);
        clone.Layer = this.Layer;
        clone.Color = this.Color;
        return clone;
    }
}
