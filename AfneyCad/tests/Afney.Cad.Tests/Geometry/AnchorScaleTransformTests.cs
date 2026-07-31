using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: Anchor (Sabit Nokta) Etrafında Ölçekleme Matrisi Testleri
   NEDEN: Yeni "Ölçek Doğrula" özelliği (mimardan gelen DWG'nin yanlış birimini kullanıcının
          bildiği bir ölçüyle düzeltmesi) TÜM çizimi seçilen 1. nokta (anchor) etrafında
          ölçekliyor — anchor'ın KENDİSİ yerinde kalmalı, geri kalan her şey ondan uzaklığıyla
          orantılı ölçeklenmeli (AutoCAD SCALE komutunun "base point" davranışıyla aynı).
          Bu matris kompozisyonu (translate→scale→translate-geri) MainWindow.Commands.cs'te
          yeni yazıldı; burada hem ileri işlem hem de Undo (ters matris) doğrulanıyor.
*/
public class AnchorScaleTransformTests
{
    private static (Matrix4x4 Transform, Matrix4x4 Inverse) BuildAnchorScale(Vector3D anchor, double factor)
    {
        var toOrigin     = Matrix4x4.TranslationMatrix(-anchor.X, -anchor.Y, -anchor.Z);
        var scale        = Matrix4x4.Scaling(factor, factor, factor);
        var backToAnchor = Matrix4x4.TranslationMatrix(anchor.X, anchor.Y, anchor.Z);
        var transform    = backToAnchor * scale * toOrigin;

        var toOriginInv     = Matrix4x4.TranslationMatrix(anchor.X, anchor.Y, anchor.Z);
        var scaleInv        = Matrix4x4.Scaling(1.0 / factor, 1.0 / factor, 1.0 / factor);
        var backToAnchorInv = Matrix4x4.TranslationMatrix(-anchor.X, -anchor.Y, -anchor.Z);
        var inverse = toOriginInv * scaleInv * backToAnchorInv;

        return (transform, inverse);
    }

    [Fact]
    public void AnchorPoint_StaysFixed_AfterScaling()
    {
        var anchor = new Vector3D(5000, 3000, 0);
        var (transform, _) = BuildAnchorScale(anchor, 2.0);

        var result = transform.Transform(anchor);

        Assert.True(result.DistanceTo(anchor) < 1e-6, "Anchor noktası ölçeklemeden sonra yerinde kalmalı.");
    }

    [Fact]
    public void PointAtKnownOffset_ScalesProportionallyFromAnchor()
    {
        // Anchor'dan 1000mm uzaktaki bir nokta, ×3 ölçekte anchor'dan 3000mm uzakta olmalı.
        var anchor = new Vector3D(0, 0, 0);
        var farPoint = new Vector3D(1000, 0, 0);
        var (transform, _) = BuildAnchorScale(anchor, 3.0);

        var result = transform.Transform(farPoint);

        Assert.True(System.Math.Abs(result.X - 3000.0) < 1e-6);
        Assert.True(System.Math.Abs(result.Y) < 1e-6);
    }

    [Fact]
    public void InverseTransform_UndoesScalingExactly()
    {
        var anchor = new Vector3D(2500, -1500, 0);
        var original = new Vector3D(8000, 6000, 0);
        var (transform, inverse) = BuildAnchorScale(anchor, 0.001); // mm→m tipi yanlış ölçek düzeltmesi senaryosu

        var scaled = transform.Transform(original);
        var restored = inverse.Transform(scaled);

        Assert.True(restored.DistanceTo(original) < 1e-6,
            $"Undo sonrası nokta orijinaline dönmeli — beklenen {original}, gerçek {restored}.");
    }

    [Fact]
    public void TransformEntityOperation_DoAndUndo_RoundTripsLineEntity()
    {
        var anchor = new Vector3D(0, 0, 0);
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0)); // "yanlışlıkla 1000 birim" çizilmiş
        var (transform, inverse) = BuildAnchorScale(anchor, 1000.0); // 1000x düzeltme (ör. m yerine mm sanılmış)

        var op = new TransformEntityOperation(line, transform, inverse, null);

        op.Do();
        Assert.True(System.Math.Abs(line.EndPoint.X - 1_000_000.0) < 1e-3);

        op.Undo();
        Assert.True(System.Math.Abs(line.EndPoint.X - 1000.0) < 1e-3);
    }
}
