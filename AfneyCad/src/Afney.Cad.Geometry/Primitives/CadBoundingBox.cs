using System;

namespace Afney.Cad.Geometry.Primitives;

/*
NE:
Eksenlere Hizalanmış Sınırlayıcı Kutu (Axis Aligned Bounding Box - AABB).

NE İÇİN:
Geometrik neskelerin kapladığı alanı en basit haliyle (Min/Max nokta) ifade etmek için.
Çarpışma testleri (Collision Detection), Görünürlük testleri (Culling) için ilk 'kaba' filtre olarak kullanılır.

NEREDE:
Geometry Primitive katmanında.

NE ZAMAN:
Herhangi bir geometrik nesne oluşturulduğunda veya değiştiğinde hesaplanır.

AMAÇ:
Karmaşık geometri (örn: Bezier eğrisi) üzerinde pahalı matematiksel işlemler yapmadan önce,
"Acaba bu kutunun içinde mi?" sorusunu çok hızlı (O(1)) cevaplamak.
*/
public struct CadBoundingBox
{
    public Vector3D Min { get; private set; }
    public Vector3D Max { get; private set; }

    public double Width => Max.X - Min.X;
    public double Height => Max.Y - Min.Y;
    public double Depth => Max.Z - Min.Z;

    public Vector3D Center => new Vector3D(
        (Min.X + Max.X) / 2,
        (Min.Y + Max.Y) / 2,
        (Min.Z + Max.Z) / 2
    );

    public CadBoundingBox(Vector3D min, Vector3D max)
    {
        // Min ve Max'ın gerçekten min ve max olduğundan emin olalım
        double minX = System.Math.Min(min.X, max.X);
        double minY = System.Math.Min(min.Y, max.Y);
        double minZ = System.Math.Min(min.Z, max.Z);

        double maxX = System.Math.Max(min.X, max.X);
        double maxY = System.Math.Max(min.Y, max.Y);
        double maxZ = System.Math.Max(min.Z, max.Z);

        Min = new Vector3D(minX, minY, minZ);
        Max = new Vector3D(maxX, maxY, maxZ);
    }

    /*
       NE: Nokta İçeriyor Mu? (Contains)
       NEDEN: Verilen bir koordinatın (fare imleci gibi) bu kutunun sınırları içinde olup olmadığını O(1) hızında kontrol etmek için.
    */
    public bool Contains(Vector3D point)
    {
        return (point.X >= Min.X && point.X <= Max.X) &&
               (point.Y >= Min.Y && point.Y <= Max.Y) &&
               (point.Z >= Min.Z && point.Z <= Max.Z);
    }

    /*
       NE: Kutuyla Kesişiyor Mu? (Intersects)
       NEDEN: İki nesnenin kaba taslak çarpışıp çarpışmadığını (Bbox-Bbox overlap) en hızlı şekilde saptamak için.
    */
    public bool Intersects(CadBoundingBox other)
    {
        return (Min.X <= other.Max.X && Max.X >= other.Min.X) &&
               (Min.Y <= other.Max.Y && Max.Y >= other.Min.Y) &&
               (Min.Z <= other.Max.Z && Max.Z >= other.Min.Z);
    }

    /*
        NE: Tam Kapsama Kontrolü (Deep Containment)
        NEDEN: Pencere (Window) seçiminde nesnenin tamamının kutu içinde olup olmadığını anlamak için.
    */
    public bool Contains(CadBoundingBox other)
    {
        return (other.Min.X >= Min.X && other.Max.X <= Max.X) &&
               (other.Min.Y >= Min.Y && other.Max.Y <= Max.Y) &&
               (other.Min.Z >= Min.Z && other.Max.Z <= Max.Z);
    }

    /*
    NE: Kutu Genişletme (Expand)
    NEDEN: Yakınlık (Proximity) testlerinde tolerans payı vermek için.
    */
    public CadBoundingBox Expand(double margin)
    {
        return new CadBoundingBox(
            new Vector3D(Min.X - margin, Min.Y - margin, Min.Z - margin),
            new Vector3D(Max.X + margin, Max.Y + margin, Max.Z + margin)
        );
    }

    public Vector3D[] GetCorners()
    {
        return new Vector3D[]
        {
            new Vector3D(Min.X, Min.Y, Min.Z),
            new Vector3D(Max.X, Min.Y, Min.Z),
            new Vector3D(Max.X, Max.Y, Min.Z),
            new Vector3D(Min.X, Max.Y, Min.Z),
            new Vector3D(Min.X, Min.Y, Max.Z),
            new Vector3D(Max.X, Min.Y, Max.Z),
            new Vector3D(Max.X, Max.Y, Max.Z),
            new Vector3D(Min.X, Max.Y, Max.Z)
        };
    }

    public static CadBoundingBox Empty => new CadBoundingBox(new Vector3D(0, 0, 0), new Vector3D(0, 0, 0));
}
