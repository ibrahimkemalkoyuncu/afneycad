using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Domain.Entities.Basic;

public class TextEntity : CadEntity
{
    public Vector3D Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Height { get; set; }
    public double Rotation { get; set; }
    public string Style { get; set; } = "Standard";
    public Vector3D Direction { get; set; } = new Vector3D(1, 0, 0); // Normal Vector
    
    // Standart bir oluşturucu
    /*
       NE: TextEntity YapÄ±cÄ± Metodu
       NEDEN: Metin iÃ§eriÄŸi, konumu ve boyutuyla yeni bir yazÄ± nesnesi oluÅŸturmak iÃ§in.
    */
    public TextEntity(string text, Vector3D position, double height, double rotation = 0)
    {
        Text = text;
        Position = position;
        Height = height;
        Rotation = rotation;
    }

    /*
       NE: Metni Ã‡iz (Draw)
       NEDEN: YazÄ±yÄ± belirtilen konum, aÃ§Ä± ve font boyutuyla render motoruna ileterek ekrana basmak iÃ§in.
    */
    public override void Draw(IRenderContext context)
    {
        // Render Context üzerinden yazı çizdir.
        context.DrawText(Text, Position, Rotation, Height, Color);
    }

    /*
       NE: SÄ±nÄ±rlayÄ±cÄ± Kutu Hesapla (CalculateBoundingBox)
       NEDEN: Metnin uzunluÄŸuna ve boyuna gÃ¶re kapladÄ±ÄŸÄ± yaklaÅŸÄ±k alanÄ± saptamak iÃ§in.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        // Basit BBox hesabı (Gerçek genişlik hesaplanmadığı için tahmini)
        // Karakter genişliğini yaklaşık 0.6 * Yükseklik varsayalım
        double width = Text.Length * (Height * 0.6);
        double halfW = width / 2;
        double halfH = Height / 2;
        
        return new CadBoundingBox(
            new Vector3D(Position.X - halfW, Position.Y - halfH, Position.Z),
            new Vector3D(Position.X + halfW, Position.Y + halfH, Position.Z)
        );
    }

    /*
       NE: Metni TaÅŸÄ± (Move)
       NEDEN: Metnin baÅŸlangÄ±Ã§ (Insertion) noktasÄ±nÄ± verilen fark kadar kaydÄ±rmak iÃ§in.
    */
    public override void Move(Vector3D delta)
    {
        Position = new Vector3D(Position.X + delta.X, Position.Y + delta.Y, Position.Z + delta.Z);
    }

    /*
       NE: Matris DÃ¶nÃ¼ÅŸtÃ¼rme (Transform)
       NEDEN: YazÄ±nÄ±n konumunu ve gerekirse yÃ¶nÃ¼nÃ¼ matris tabanlÄ± iÅŸlemlerle gÃ¼ncellemek iÃ§in.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        // 1. Pozisyonu Dönüştür
        Position = matrix.Transform(Position);

        // 2. Rotasyon ve Ölçek Çıkarımı
        double m00 = matrix[0, 0];
        double m10 = matrix[1, 0];
        // double m01 = matrix[0, 1];
        // double m11 = matrix[1, 1];

        // Scale (X ekseni scaling'i baz alıyoruz)
        double scale = Math.Sqrt(m00 * m00 + m10 * m10);
        
        // Eğer scale çok küçükse (0) işlem yapma
        if (scale < 1e-9) scale = 1.0;

        Height *= scale;

        // Rotation (Z)
        double angle = Math.Atan2(m10, m00);
        Rotation += angle;
    }

    /*
       NE: Nesneyi Klone Et (Clone)
       NEDEN: Metin nesnesinin birebir kopyasÄ±nÄ± oluÅŸturmak iÃ§in.
    */
    public override CadEntity Clone()
    {
        return (TextEntity)this.MemberwiseClone();
    }

    /*
       NE: Kenetlenme NoktalarÄ± (GetSnapPoints)
       NEDEN: Metnin yerleÅŸim (Insertion) noktasÄ±nÄ± yakalanabilir kÄ±lmak iÃ§in.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        // Insertion point yerine Endpoint kullanıyoruz şimdilik
        yield return new SnapPoint(Position, SnapPointType.Endpoint);
    }
}
