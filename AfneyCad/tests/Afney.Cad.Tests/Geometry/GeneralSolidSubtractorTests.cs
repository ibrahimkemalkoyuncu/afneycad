using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: GeneralSolidSubtractor Testleri — çok-yüzlü genel SUBTRACT montajı
   NEDEN GERÇEK KAPSAM (2026-08-06 güncellemesi): Tek-düzlem özel durumu (`SolidSubtractor.
       Subtract` ile birebir) ÇALIŞIYOR. Çok-düzlem (B, A'yı BİRDEN FAZLA yüzden kesiyor)
       durumunda ÖNCEKİ oturumlarda bulunan İKİ yapısal engel, algoritmanın klasik "subdivide
       → classify → reconstruct" (Requicha & Voelcker) yaklaşımıyla YENİDEN YAZILMASIYLA
       ikisi de ÇÖZÜLDÜ:
         1. **Köşe-çentiği** (B, A'nın bir köşesini örtüyor, sonuç TEK PARÇA/bağlantılı):
            ÖNCEDEN (`FaceRegionClassifier`'ın ikili mirror-cap sınıflandırmasıyla) mirror
            cap'lerin KISMİ örtüşmesi `IsValid()`'i bozuyordu. **ÇÖZÜLDÜ** — yeni algoritma
            her kapağı DİĞER aday düzlemlerin yarı-uzaylarına göre kırpıyor
            (`ClipPolygonByHalfSpace`) ve kırpma sınırında oluşan YENİ kenarları
            `OpenEdgeStitcher` ile otomatik dikiyor (bkz. `Subtract_CornerNotch_*` testleri).
         2. **"Through-slot"** (B, A'yı ortadan ikiye bölecek şekilde kesiyor, sonuç İKİ AYRI
            BAĞLANTISIZ parça): `Solid.IsValid()`'in kabuk-başına Euler doğrulamasıyla
            (2026-08-04) ÇÖZÜLDÜ, yeni algoritmayla da GEÇERLİ.
       Bkz. `docs/Roadmap_CSG_Boolean.md` (2026-08-06 güncellemesi) — araştırma, tasarım ve
       algoritmanın kaynağı (klasik B-Rep boundary-evaluation literatürü).
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
    public void Subtract_CornerNotch_ProducesValidResultWithCorrectVolume()
    {
        // Köşe-çentiği: B, A'nın [1500,2000]x[1500,2000]x[0,2000] köşesini örtüyor (2 aday
        // düzlem: B'nin X=1500 ve Y=1500 yüzleri). Sonuç TEK bağlantılı parça (L-şeklinde
        // taban kesiti) — `ClipPolygonByHalfSpace` + `OpenEdgeStitcher` ile artık GEÇERLİ.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        var result = GeneralSolidSubtractor.Subtract(a, b);

        Assert.True(result.IsValid());

        // A_hacim - kesişim_hacmi = sonuç_hacmi (A∩B = [1500,2000]x[1500,2000]x[0,2000] = 500x500x2000).
        double expectedVolume = 2000.0 * 2000.0 * 2000.0 - 500.0 * 500.0 * 2000.0;
        Assert.Equal(expectedVolume, result.GetVolume(), precision: 3);
    }

    [Fact]
    public void Subtract_CornerNotch_ResultContainsRemainderNotCorner()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        var result = GeneralSolidSubtractor.Subtract(a, b);

        // Kesilen köşenin (A∩B) merkezi -> sonuç Solid'in DIŞINDA olmalı.
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1750, 1000)));

        // A'nın geri kalanının (köşe hariç) çeşitli noktaları -> sonuç Solid'in İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1000, 1000, 1000)));
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1000, 1000)));
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1000, 1750, 1000)));
    }

    [Fact]
    public void Subtract_TrueCornerNotch_ThreePlanes_ProducesValidResultWithCorrectVolume()
    {
        // B, A'nın GERÇEK bir 3D köşesini (X, Y VE Z eksenlerinin ÜÇÜNÜ birden) örtüyor —
        // 3 aday düzlem (X=1500, Y=1500, Z=1500). Her kapağın DİĞER İKİ düzlemin yarı-uzayına
        // göre kırpılması gerekiyor (`ClipPolygonByHalfSpace`'in çift-kırpma yolu) — 2-düzlem
        // köşe-çentiği testinden DAHA GENEL bir doğrulama.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);

        var result = GeneralSolidSubtractor.Subtract(a, b);

        Assert.True(result.IsValid());

        double expectedVolume = 2000.0 * 2000.0 * 2000.0 - 500.0 * 500.0 * 500.0;
        Assert.Equal(expectedVolume, result.GetVolume(), precision: 3);
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
