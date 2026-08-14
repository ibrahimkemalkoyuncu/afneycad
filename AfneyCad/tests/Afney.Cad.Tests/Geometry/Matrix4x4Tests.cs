using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: Matrix4x4 Değer-Tipi Kopyalama Testleri
   NEDEN: Matrix4x4 önceden `private double[,] _m` (heap array) kullanıyordu — struct
          kopyalandığında (atama/parametre/dönüş) sadece array REFERANSI kopyalanıyordu,
          yani "bağımsız" iki kopya AYNI arka plan array'ini paylaşıyordu; biri indexer ile
          mutasyona uğrarsa diğeri de sessizce değişiyordu. 16 ayrı alana geçilerek düzeltildi
          (Session #66) — bu testler gerçek değer-tipi izolasyonunu kilitler.
*/
public class Matrix4x4Tests
{
    [Fact]
    public void Copy_Then_Mutate_DoesNotAffectOriginal()
    {
        var original = Matrix4x4.Identity;
        var copy = original;

        copy[0, 0] = 99.0;

        Assert.Equal(1.0, original[0, 0]);
        Assert.Equal(99.0, copy[0, 0]);
    }

    [Fact]
    public void PassedByValue_MutationInsideMethod_DoesNotAffectCaller()
    {
        var m = Matrix4x4.Identity;
        MutateLocalCopy(m);
        Assert.Equal(1.0, m[1, 1]);

        static void MutateLocalCopy(Matrix4x4 mat)
        {
            mat[1, 1] = 42.0;
        }
    }

    [Fact]
    public void Multiplication_TranslationThenScale_TransformsPointCorrectly()
    {
        var scale = Matrix4x4.Scaling(2.0);
        var translate = Matrix4x4.TranslationMatrix(10, 0, 0);
        var combined = translate * scale;

        var result = combined.Transform(new Vector3D(1, 1, 1));

        Assert.Equal(12.0, result.X, 9);
        Assert.Equal(2.0, result.Y, 9);
        Assert.Equal(2.0, result.Z, 9);
    }

    [Fact]
    public void Equals_ValueEquality_HoldsAcrossIndependentCopies()
    {
        var a = Matrix4x4.RotationZ(0.5);
        var b = a; // kopya
        b[0, 0] = b[0, 0]; // no-op mutasyon, ama a ile b artik ayni array'i PAYLASMIYOR

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentMatrices_AreNotEqual()
    {
        var a = Matrix4x4.Identity;
        var b = Matrix4x4.Scaling(2.0);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
