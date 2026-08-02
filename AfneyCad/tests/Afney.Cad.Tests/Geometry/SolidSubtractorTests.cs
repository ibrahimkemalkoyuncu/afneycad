using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: SolidSubtractor Testleri — DAR KAPSAMLI (tek-düzlem) genel SUBTRACT sarmalayıcısı
   NEDEN: `docs/Roadmap_CSG_Boolean.md` (2026-08-02, ikinci araştırma turu) — çok-yüzlü genel
          SUBTRACT bilinçli olarak kapsam dışı bırakıldı (SolidClassifier tam entegrasyonu
          gerektiriyor); ama roadmap'in kendi tespiti gereği B'nin A'nın sınırını SADECE TEK
          BİR düzlemde kestiği durum `PlaneCutter.CutWithPlane` ile ZATEN çözülüyor. Bu testler
          (1) box-minus-box slab senaryosunun `SolidSubtractor.Subtract` ile `PlaneCutter.
          CutWithPlane`'in DOĞRUDAN çağrılmasıyla AYNI sonucu verdiğini, (2) B'nin A'yı BİRDEN
          FAZLA yüzden kestiği (köşe-çentiği) genel durumda AÇIK hata fırlatıldığını doğruluyor.
*/
public class SolidSubtractorTests
{
    [Fact]
    public void Subtract_BoxMinusBoxSlab_MatchesDirectPlaneCutterCall()
    {
        // Roadmap senaryosu: A=[0,2000]^3 eksi B=[1000,3000]x[0,2000]x[0,2000].
        var aViaSubtractor = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var aViaDirectCutter = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var capFromSubtractor = SolidSubtractor.Subtract(aViaSubtractor, b);
        var capFromDirectCutter = PlaneCutter.CutWithPlane(aViaDirectCutter, new Vector3D(1000, 0, 0), -Vector3D.XAxis);

        Assert.True(aViaSubtractor.IsValid());
        Assert.True(aViaDirectCutter.IsValid());

        var expected = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);
        Assert.Equal(expected.GetVolume(), aViaSubtractor.GetVolume(), precision: 3);

        // SolidSubtractor.Subtract ile doğrudan PlaneCutter.CutWithPlane BİREBİR AYNI sonucu vermeli.
        Assert.Equal(aViaDirectCutter.GetVolume(), aViaSubtractor.GetVolume(), precision: 6);
        Assert.Equal(aViaDirectCutter.Faces.Count, aViaSubtractor.Faces.Count);
        Assert.Equal(capFromDirectCutter.GetArea(), capFromSubtractor.GetArea(), precision: 6);
        Assert.Equal(capFromDirectCutter.Normal.X, capFromSubtractor.Normal.X, precision: 6);
        Assert.Equal(capFromDirectCutter.Normal.Y, capFromSubtractor.Normal.Y, precision: 6);
        Assert.Equal(capFromDirectCutter.Normal.Z, capFromSubtractor.Normal.Z, precision: 6);
    }

    [Fact]
    public void Subtract_CornerNotch_MultiplePlanesIntersectBoundary_ThrowsNotSupported()
    {
        // A=[0,2000]^3. B, A'nın bir köşesini örtüyor: X ve Y aralıkları A ile SADECE KISMEN
        // örtüşüyor (Z aralığı A ile birebir aynı, coplanar/dejenere) -> B'nin X=1500 VE
        // Y=1500 yüzleri, A'nın sınırını AYRI AYRI transversal kesiyor (2 aday düzlem).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        Assert.Throws<NotSupportedException>(() => SolidSubtractor.Subtract(a, b));
    }

    [Fact]
    public void Subtract_BCompletelyOutsideA_NoPlaneIntersectsBoundary_ThrowsNotSupported()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        Assert.Throws<NotSupportedException>(() => SolidSubtractor.Subtract(a, b));
    }
}
