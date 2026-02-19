using System;

namespace Afney.Cad.Geometry.Primitives;

public struct Matrix4x4
{
    private double[,] _m;

    /*
       NE: Matrix4x4 Yapıcı Metodu
       NEDEN: Birim matris (Identity Matrix) oluşturarak geometrik dönüşümlere nötr bir başlangıç yapmak için.
    */
    public Matrix4x4()
    {
        _m = new double[4, 4];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                _m[i, j] = (i == j) ? 1.0 : 0.0;
    }

    // Erişim İndeksleyicisi
    public double this[int row, int col]
    {
        get => _m[row, col];
        set => _m[row, col] = value;
    }

    public static Matrix4x4 Identity => new Matrix4x4();

    // Dönüşüm Matrisindeki Öteleme Vektörü (Position)
    public Vector3D Translation => new Vector3D(_m[0, 3], _m[1, 3], _m[2, 3]);

    /*
       NE: Öteleme Matrisi Oluştur (TranslationMatrix)
       NEDEN: Bir nesneyi dünya koordinatlarında X, Y, Z yönlerinde kaydırmak için kullanılan matrisi üretmek için.
    */
    public static Matrix4x4 TranslationMatrix(double x, double y, double z)
    {
        var mat = new Matrix4x4();
        mat._m[0, 3] = x;
        mat._m[1, 3] = y;
        mat._m[2, 3] = z;
        return mat;
    }

    /*
       NE: Ölçekleme Matrisi (Scaling)
       NEDEN: Bir nesneyi tüm eksenlerde (X, Y, Z) üniform (eşit) oranda büyütmek veya küçültmek için kullanılan matrisi üretmek için.
    */
    public static Matrix4x4 Scaling(double s)
    {
        var mat = new Matrix4x4();
        mat._m[0, 0] = s;
        mat._m[1, 1] = s;
        mat._m[2, 2] = s;
        return mat;
    }

    public static Matrix4x4 Scaling(double x, double y, double z)
    {
        var mat = new Matrix4x4(); // Identity
        mat._m[0, 0] = x;
        mat._m[1, 1] = y;
        mat._m[2, 2] = z;
        return mat;
    }

    // Basit bir çarpma operatörü (vektör dönüşümü için)
    /*
       NE: Vektörü Dönüştür (Transform)
       NEDEN: Bir 3D noktayı (Vector3D) mevcut matrisle çarparak; taşıma, döndürme ve ölçekleme işlemlerini koordinatlara uygulamak için.
    */
    public Vector3D Transform(Vector3D v)
    {
        double x = _m[0, 0] * v.X + _m[0, 1] * v.Y + _m[0, 2] * v.Z + _m[0, 3];
        double y = _m[1, 0] * v.X + _m[1, 1] * v.Y + _m[1, 2] * v.Z + _m[1, 3];
        double z = _m[2, 0] * v.X + _m[2, 1] * v.Y + _m[2, 2] * v.Z + _m[2, 3];
        // w bileşenini ihmal ediyoruz (homojen koordinatlar için gerekli olabilir ama şu an basit tutalım)
        return new Vector3D(x, y, z);
    }

    public static Matrix4x4 RotationX(double radians)
    {
        var mat = new Matrix4x4();
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);
        mat._m[1, 1] = c;
        mat._m[1, 2] = -s;
        mat._m[2, 1] = s;
        mat._m[2, 2] = c;
        return mat;
    }

    public static Matrix4x4 RotationY(double radians)
    {
        var mat = new Matrix4x4();
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);
        mat._m[0, 0] = c;
        mat._m[0, 2] = s;
        mat._m[2, 0] = -s;
        mat._m[2, 2] = c;
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
        mat._m[0, 0] = c;
        mat._m[0, 1] = -s;
        mat._m[1, 0] = s;
        mat._m[1, 1] = c;
        return mat;
    }

    // --- UYUMLULUK İÇİN EK METODLAR (AutoCAD/Numerics Tarzı) ---
    public static Matrix4x4 CreateTranslation(Vector3D v) => TranslationMatrix(v.X, v.Y, v.Z);
    public static Matrix4x4 CreateTranslation(double x, double y, double z) => TranslationMatrix(x, y, z);
    public static Matrix4x4 CreateScale(double s) => Scaling(s);
    public static Matrix4x4 CreateScale(double x, double y, double z) => Scaling(x, y, z);
    public static Matrix4x4 CreateRotationZ(double radians) => RotationZ(radians);
    // ----------------------------------------------------------

    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
    {
        var res = new Matrix4x4();
        // Identity metodunda 1 olarak atanıyor, burada sıfırlamamız lazım (veya direkt array oluşturup set etmeli)
        // Ancak constructor Identity çağırıyor, biz üzerine yazacağız.
        // Daha performanslı olması için Identity() çağrısından kaçınan bir constructor eklenebilir ama şimdilik bu yeterli.
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) res._m[i, j] = 0;

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                for (int k = 0; k < 4; k++)
                {
                    res._m[i, j] += a._m[i, k] * b._m[k, j];
                }
            }
        }
        return res;
    }

    public static bool operator ==(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                if (a._m[i, j] != b._m[i, j]) return false;
        return true;
    }

    public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !(a == b);

    public override bool Equals(object? obj) => obj is Matrix4x4 other && this == other;
    public override int GetHashCode() => _m?.GetHashCode() ?? 0;
}
