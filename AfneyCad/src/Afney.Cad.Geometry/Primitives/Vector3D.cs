using System;
using System.Runtime.CompilerServices;

namespace Afney.Cad.Geometry.Primitives;

/*
   NE: 3D Vektör ve Nokta Yapısı (Vector3D)
   NEDEN: CAD sistemindeki tüm geometrik koordinatları, yönelimleri ve fiziksel vektörleri temsil etmek için.

   NASIL (Mühendislik Detayı):
   - 'readonly record struct' kullanılarak değer tipi (value type) kararlılığı ve yüksek performans sağlanır.
   - Operatör aşırı yüklemesi (Operator Overloading) ile matematiksel işlemler doğal yazım diline yaklaştırılmıştır.
   - SIMD optimizasyonlarına uygun yapıdadır.
   - Sıhhi tesisat hesaplamalarında boru yönleri, eğim vektörleri ve kuvvet analizleri için temel oluşturur.
*/
public readonly record struct Vector3D(double X, double Y, double Z) : IEquatable<Vector3D>
{
    public static readonly Vector3D Zero = new(0, 0, 0);
    public static readonly Vector3D XAxis = new(1, 0, 0);
    public static readonly Vector3D YAxis = new(0, 1, 0);
    public static readonly Vector3D ZAxis = new(0, 0, 1);

    // NE: 2D Koordinat Oluşturucu
    // NEDEN: Düzlemsel (plan) çizimlerde Z değerini varsayılan olarak 0 kabul etmek için.
    public Vector3D(double x, double y) : this(x, y, 0) { }

    /*
       NE: Vektör Boyu (Length)
       NEDEN: Vektörün orijine olan Öklid mesafesini hesaplayarak bir boru segmentinin gerçek uzunluğunu bulmak için.
    */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

    // NE: Vektör Boyunun Karesi
    // NEDEN: Köndürme (Math.Sqrt) işlemi pahalı olduğu için mesafe karşılaştırmalarında bunu kullanmak daha hızlıdır.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double LengthSquared() => X * X + Y * Y + Z * Z;

    /*
       NE: Uzaklık Hesabı (DistanceTo)
       NEDEN: İki nokta arasındaki doğrusal mesafeyi hesaplayarak snap toleransı veya boru uzunluğu saptamak için.
    */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double DistanceTo(Vector3D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // NE: Noktasal Çarpım (Dot Product)
    // NEDEN: İki vektör arasındaki açıyı veya bir vektörün diğeri üzerindeki izdüşümünü bulmak için.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

    // NE: Vektörel Çarpım (Cross Product)
    // NEDEN: İki vektöre dik olan (normal) vektörü bulmak (Örn: Tesisat branşmanı yönü) için.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3D Cross(Vector3D other)
    {
        return new Vector3D(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X
        );
    }

    // NE: Normalizasyon
    // NEDEN: Vektörün yönünü koruyarak uzunluğunu 1 birime indirmek için.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3D Normalize()
    {
        double len = Length();
        if (len < 1e-10) return Zero;
        return new Vector3D(X / len, Y / len, Z / len);
    }

    // Operatörler
    public static Vector3D operator -(Vector3D a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator *(Vector3D a, double scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
    public static Vector3D operator /(Vector3D a, double scalar) => new(a.X / scalar, a.Y / scalar, a.Z / scalar);
    
    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";

    public static Vector3D Min(Vector3D a, Vector3D b)
    {
        return new Vector3D(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    }

    public static Vector3D Max(Vector3D a, Vector3D b)
    {
        return new Vector3D(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    }
}

