using System;

namespace Afney.Cad.Geometry.Primitives;

public struct Matrix4x4
{
    /*
       NE: 16 ayrı alan (satır-öncelikli: M{satır}{sütun}) ile matris depolama
       NEDEN: Önceden burada `private double[,] _m` (heap array) vardı — struct kopyalandığında
              (atama, metod parametresi, dönüş değeri) sadece array REFERANSI kopyalanıyordu,
              yani "bağımsız" iki Matrix4x4 kopyası aslında AYNI arka plan array'ini paylaşıyordu.
              Biri indexer ile (this[i,j] = ...) mutasyona uğrarsa diğer kopya da sessizce
              değişiyordu — struct'ın value-type garantisini kıran bir doğruluk hatası.
              Kod tabanında şu an bunu tetikleyen bir "kopyala sonra mutasyona uğrat" kullanımı
              yok (tüm static factory'ler taze bir instance üretip dolduruyor) ama indexer public
              olduğu için gelecekte doğal bir kullanım bu hataya düşebilirdi. 16 ayrı double alanı
              gerçek value-type kopyalama sağlıyor, hiçbir heap allocation gerektirmiyor.
    */
    private double M00, M01, M02, M03;
    private double M10, M11, M12, M13;
    private double M20, M21, M22, M23;
    private double M30, M31, M32, M33;

    /*
       NE: Matrix4x4 Yapıcı Metodu
       NEDEN: Birim matris (Identity Matrix) oluşturarak geometrik dönüşümlere nötr bir başlangıç yapmak için.
    */
    public Matrix4x4()
    {
        M00 = 1.0; M01 = 0.0; M02 = 0.0; M03 = 0.0;
        M10 = 0.0; M11 = 1.0; M12 = 0.0; M13 = 0.0;
        M20 = 0.0; M21 = 0.0; M22 = 1.0; M23 = 0.0;
        M30 = 0.0; M31 = 0.0; M32 = 0.0; M33 = 1.0;
    }

    // Erişim İndeksleyicisi
    public double this[int row, int col]
    {
        get => (row, col) switch
        {
            (0, 0) => M00, (0, 1) => M01, (0, 2) => M02, (0, 3) => M03,
            (1, 0) => M10, (1, 1) => M11, (1, 2) => M12, (1, 3) => M13,
            (2, 0) => M20, (2, 1) => M21, (2, 2) => M22, (2, 3) => M23,
            (3, 0) => M30, (3, 1) => M31, (3, 2) => M32, (3, 3) => M33,
            _ => throw new IndexOutOfRangeException($"Matrix4x4 indeksi 0-3 aralığında olmalı: [{row},{col}]")
        };
        set
        {
            switch (row, col)
            {
                case (0, 0): M00 = value; break; case (0, 1): M01 = value; break; case (0, 2): M02 = value; break; case (0, 3): M03 = value; break;
                case (1, 0): M10 = value; break; case (1, 1): M11 = value; break; case (1, 2): M12 = value; break; case (1, 3): M13 = value; break;
                case (2, 0): M20 = value; break; case (2, 1): M21 = value; break; case (2, 2): M22 = value; break; case (2, 3): M23 = value; break;
                case (3, 0): M30 = value; break; case (3, 1): M31 = value; break; case (3, 2): M32 = value; break; case (3, 3): M33 = value; break;
                default: throw new IndexOutOfRangeException($"Matrix4x4 indeksi 0-3 aralığında olmalı: [{row},{col}]");
            }
        }
    }

    public static Matrix4x4 Identity => new Matrix4x4();

    // Dönüşüm Matrisindeki Öteleme Vektörü (Position)
    public Vector3D Translation => new Vector3D(M03, M13, M23);

    /*
       NE: Öteleme Matrisi Oluştur (TranslationMatrix)
       NEDEN: Bir nesneyi dünya koordinatlarında X, Y, Z yönlerinde kaydırmak için kullanılan matrisi üretmek için.
    */
    public static Matrix4x4 TranslationMatrix(double x, double y, double z)
    {
        var mat = new Matrix4x4();
        mat.M03 = x;
        mat.M13 = y;
        mat.M23 = z;
        return mat;
    }

    /*
       NE: Ölçekleme Matrisi (Scaling)
       NEDEN: Bir nesneyi tüm eksenlerde (X, Y, Z) üniform (eşit) oranda büyütmek veya küçültmek için kullanılan matrisi üretmek için.
    */
    public static Matrix4x4 Scaling(double s)
    {
        var mat = new Matrix4x4();
        mat.M00 = s;
        mat.M11 = s;
        mat.M22 = s;
        return mat;
    }

    public static Matrix4x4 Scaling(double x, double y, double z)
    {
        var mat = new Matrix4x4(); // Identity
        mat.M00 = x;
        mat.M11 = y;
        mat.M22 = z;
        return mat;
    }

    // Basit bir çarpma operatörü (vektör dönüşümü için)
    /*
       NE: Vektörü Dönüştür (Transform)
       NEDEN: Bir 3D noktayı (Vector3D) mevcut matrisle çarparak; taşıma, döndürme ve ölçekleme işlemlerini koordinatlara uygulamak için.
    */
    public Vector3D Transform(Vector3D v)
    {
        double x = M00 * v.X + M01 * v.Y + M02 * v.Z + M03;
        double y = M10 * v.X + M11 * v.Y + M12 * v.Z + M13;
        double z = M20 * v.X + M21 * v.Y + M22 * v.Z + M23;
        // w bileşenini ihmal ediyoruz (homojen koordinatlar için gerekli olabilir ama şu an basit tutalım)
        return new Vector3D(x, y, z);
    }

    public static Matrix4x4 RotationX(double radians)
    {
        var mat = new Matrix4x4();
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);
        mat.M11 = c;
        mat.M12 = -s;
        mat.M21 = s;
        mat.M22 = c;
        return mat;
    }

    public static Matrix4x4 RotationY(double radians)
    {
        var mat = new Matrix4x4();
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);
        mat.M00 = c;
        mat.M02 = s;
        mat.M20 = -s;
        mat.M22 = c;
        return mat;
    }

    /*
       NE: Z Ekseni Etrafında Döndür (RotationZ)
       NEDEN: CAD çizimlerindeki ROTATE komutu gibi nesneleri XY düzleminde belirtilen radyan açısı kadar döndürmek için kullanılan matrisi üretmek için.
    */
    public static Matrix4x4 RotationZ(double radians)
    {
        var mat = new Matrix4x4();
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);
        mat.M00 = c;
        mat.M01 = -s;
        mat.M10 = s;
        mat.M11 = c;
        return mat;
    }

    // --- UYUMLULUK İÇİN EK METODLAR (AutoCAD/Numerics Tarzı) ---
    public static Matrix4x4 CreateTranslation(Vector3D v) => TranslationMatrix(v.X, v.Y, v.Z);
    public static Matrix4x4 CreateTranslation(double x, double y, double z) => TranslationMatrix(x, y, z);
    public static Matrix4x4 CreateScale(double s) => Scaling(s);
    public static Matrix4x4 CreateScale(double x, double y, double z) => Scaling(x, y, z);
    public static Matrix4x4 CreateRotationZ(double radians) => RotationZ(radians);
    // ----------------------------------------------------------

    /*
       NE: İki Noktaya Göre Yansıma (Reflection) Matrisi
       NEDEN: Orijinal AutoCAD'deki gibi iki noktanın oluşturduğu eksene göre nesnelerin simetriğini almak için.
    */
    public static Matrix4x4 Reflection(Vector3D p1, Vector3D p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;

        // Eksenin açısı
        double angle = Math.Atan2(dy, dx);

        // Algoritma:
        // 1. P1 noktasından Orijine (0,0) taşı
        // 2. Ekseni X ekseni ile hizala (Döndür)
        // 3. Y ekseninde yansıt (Scale: Y = -1)
        // 4. Tekrar geri döndür
        // 5. P1 noktasına geri taşı

        var t1 = CreateTranslation(-p1.X, -p1.Y, 0);
        var r1 = CreateRotationZ(-angle);
        var s1 = CreateScale(1.0, -1.0, 1.0); // Y eksenini ters çevir
        var r2 = CreateRotationZ(angle);
        var t2 = CreateTranslation(p1.X, p1.Y, 0);

        // Matris Çarpımı (Sırasıyla sağdan sola uygulanır ama biz a*b yapıyoruz)
        // C#'ta (T2 * R2 * S1 * R1 * T1) şeklinde yazılır (Eğer sol taraf mevcut vektör ise).
        // Veya bizim çarpım metodumuza göre soldan sağa:

        return t2 * r2 * s1 * r1 * t1;
    }

    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
    {
        var res = new Matrix4x4
        {
            M00 = a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20 + a.M03 * b.M30,
            M01 = a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21 + a.M03 * b.M31,
            M02 = a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22 + a.M03 * b.M32,
            M03 = a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03 * b.M33,

            M10 = a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20 + a.M13 * b.M30,
            M11 = a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            M12 = a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            M13 = a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,

            M20 = a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20 + a.M23 * b.M30,
            M21 = a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            M22 = a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            M23 = a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,

            M30 = a.M30 * b.M00 + a.M31 * b.M10 + a.M32 * b.M20 + a.M33 * b.M30,
            M31 = a.M30 * b.M01 + a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            M32 = a.M30 * b.M02 + a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            M33 = a.M30 * b.M03 + a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33,
        };
        return res;
    }

    public static bool operator ==(Matrix4x4 a, Matrix4x4 b) =>
        a.M00 == b.M00 && a.M01 == b.M01 && a.M02 == b.M02 && a.M03 == b.M03 &&
        a.M10 == b.M10 && a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 &&
        a.M20 == b.M20 && a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 &&
        a.M30 == b.M30 && a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33;

    public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !(a == b);

    public override bool Equals(object? obj) => obj is Matrix4x4 other && this == other;

    /*
       NE: Değer-tabanlı GetHashCode
       NEDEN: Önceki implementasyon (`_m?.GetHashCode() ?? 0`) array REFERANSININ hash'ini
              döndürüyordu — iki değerce eşit matris (Equals==true) farklı hash kodu üretebiliyordu,
              bu Dictionary/HashSet'te Matrix4x4 kullanılırsa sessizce yanlış sonuç doğurabilirdi
              (kodda şu an Matrix4x4 bir Dictionary/HashSet anahtarı olarak kullanılmıyor, ama
              Equals/GetHashCode sözleşmesini bozan bir latent hataydı, düzeltilirken giderildi).
    */
    public override int GetHashCode()
    {
        var h1 = HashCode.Combine(M00, M01, M02, M03, M10, M11, M12, M13);
        var h2 = HashCode.Combine(M20, M21, M22, M23, M30, M31, M32, M33);
        return HashCode.Combine(h1, h2);
    }
}
