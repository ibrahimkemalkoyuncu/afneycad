using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: GeneralSolidSubtractor Testleri — çok-yüzlü genel SUBTRACT montajı
   NEDEN GERÇEK KAPSAM (bu oturumda AMPİRİK olarak netleşti): Tek-düzlem özel durumu
       (`SolidSubtractor.Subtract` ile birebir) ÇALIŞIYOR. Çok-düzlem (B, A'yı BİRDEN FAZLA
       yüzden kesiyor) durumunda İKİ AYRI, BAĞIMSIZ yapısal engel bulunmuştu:
         1. **Köşe-çentiği** (B, A'nın bir köşesini örtüyor, sonuç TEK PARÇA/bağlantılı):
            ardışık kesimlerin mirror cap'leri KISMEN örtüşüyor — bir mirror cap'in bir kısmı
            gerçekten A∩B'ye, bir kısmı DİĞER kesilen parçaya (Dⱼ) bitişik. `FaceRegionClassifier`
            İKİLİ (tam/hiç) karar verdiğinden bunu ayıramıyor — Face'in KENDİSİNİN
            (`ConvexPolygonClipper2D` ile yarı-düzlem kırpma) bölünmesi gerekir. **HÂLÂ KAPSAM
            DIŞI** (bkz. `Subtract_CornerNotch_PartiallyOverlappingMirrorCaps_ThrowsExplicitError`).
         2. **"Through-slot"** (B, A'yı ortadan ikiye bölecek şekilde kesiyor, sonuç İKİ AYRI
            BAĞLANTISIZ parça): mirror cap'ler örtüşmüyor AMA sonuç Solid'i iki bağımsız
            "kabuk" (shell) içeriyordu. **ÇÖZÜLDÜ** (2026-08-04, devam) — `Solid.IsValid()`
            artık bağlantılı-bileşen (kabuk) başına Euler doğrulaması yapıyor, TEK global
            `V-E+F==2` varsayımı kaldırıldı. Bkz. `docs/Roadmap_CSG_Boolean.md`.
*/
public class GeneralSolidSubtractorTests
{
    [Fact]
    public void Subtract_SinglePlaneCase_DelegatesAndMatchesSolidSubtractor()
    {
        // Tek-düzlem özel durumunda (SolidSubtractorTests'teki AYNI slab senaryosu)
        // GeneralSolidSubtractor, SolidSubtractor.Subtract ile BİREBİR aynı hacmi vermeli.
        var aGeneral = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var aDirect = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var bGeneral = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var bDirect = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var resultGeneral = GeneralSolidSubtractor.Subtract(aGeneral, bGeneral);
        SolidSubtractor.Subtract(aDirect, bDirect);

        Assert.True(resultGeneral.IsValid());
        Assert.Equal(aDirect.GetVolume(), resultGeneral.GetVolume(), precision: 6);
    }

    [Fact]
    public void Subtract_BCompletelyOutsideA_NoPlaneIntersectsBoundary_ThrowsNotSupported()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        Assert.Throws<NotSupportedException>(() => GeneralSolidSubtractor.Subtract(a, b));
    }

    [Fact]
    public void Subtract_CornerNotch_PartiallyOverlappingMirrorCaps_ThrowsExplicitError()
    {
        // Köşe-çentiği: sonuç TEK PARÇA olurdu ama mirror cap'ler KISMEN örtüşüyor
        // (bkz. sınıf başı NEDEN notu, 1. engel) -> IsValid() başarısız, açık istisna.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        Assert.Throws<InvalidOperationException>(() => GeneralSolidSubtractor.Subtract(a, b));
    }

    [Fact]
    public void Subtract_ThroughSlot_TwoParallelPlanesFullySpanOtherAxes_ProducesValidDisconnectedSolid()
    {
        // A=[0,2000]^3. B=[500,1500]x[0,2000]x[0,2000] — A'yı X ekseninde ORTADAN bir "slot"
        // gibi kesiyor, Y/Z aralığı A ile BİREBİR aynı (coplanar, aday değil) -> SADECE X=500
        // ve X=1500 düzlemleri aday. Sonuç İKİ AYRI bağlantısız parça (X<500 ve X>1500) —
        // `Solid.IsValid()`'in çok-kabuklu desteğiyle (2026-08-04) artık GEÇERLİ sayılıyor.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(500, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);

        var result = GeneralSolidSubtractor.Subtract(a, b);

        Assert.True(result.IsValid());

        // A\B = X:[0,500] parçası + X:[1500,2000] parçası (her biri 500x2000x2000).
        double expectedVolume = 2 * (500.0 * 2000.0 * 2000.0);
        Assert.Equal(expectedVolume, result.GetVolume(), precision: 3);
    }

    [Fact]
    public void Subtract_ThroughSlot_ResultContainsRemainderNotSlot()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(500, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);

        var result = GeneralSolidSubtractor.Subtract(a, b);

        // Slot'un (B∩A) merkezi -> sonuç Solid'in DIŞINDA olmalı.
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(1000, 1000, 1000)));

        // Kalan iki parçanın merkezleri -> sonuç Solid'in İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(250, 1000, 1000)));
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1000, 1000)));
    }
}
