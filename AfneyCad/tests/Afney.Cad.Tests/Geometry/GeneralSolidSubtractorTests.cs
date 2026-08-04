using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: GeneralSolidSubtractor Testleri — çok-yüzlü genel SUBTRACT montajı
   NEDEN GERÇEK KAPSAM (bu oturumda AMPİRİK olarak netleşti — ajanın önerdiğinden DAHA DAR):
       Tek-düzlem özel durumu (`SolidSubtractor.Subtract` ile birebir) ÇALIŞIYOR. Ama
       çok-düzlem (B, A'yı BİRDEN FAZLA yüzden kesiyor) durumunda İKİ AYRI, BAĞIMSIZ yapısal
       engel bulundu — ikisi de denenip GERÇEKTEN `IsValid()` başarısızlığına yol açtığı
       doğrulandı (teorik değil, ampirik):
         1. **Köşe-çentiği** (B, A'nın bir köşesini örtüyor, sonuç TEK PARÇA/bağlantılı):
            ardışık kesimlerin mirror cap'leri KISMEN örtüşüyor — bir mirror cap'in bir kısmı
            gerçekten A∩B'ye, bir kısmı DİĞER kesilen parçaya (Dⱼ) bitişik. `FaceRegionClassifier`
            İKİLİ (tam/hiç) karar verdiğinden bunu ayıramıyor — Face'in KENDİSİNİN
            (`ConvexPolygonClipper2D` ile yarı-düzlem kırpma) bölünmesi gerekir.
         2. **"Through-slot"** (B, A'yı ortadan ikiye bölecek şekilde kesiyor, sonuç İKİ AYRI
            BAĞLANTISIZ parça): mirror cap'ler örtüşmüyor AMA sonuç Solid'i iki bağımsız
            "kabuk" (shell) içeriyor — `Solid.IsValid()`'in Euler testi (V-E+F==2, GENUS 0,
            TEK bağlantılı bileşen varsayımı) bunu KATEGORİK OLARAK reddediyor (SolidSubtractor'ın
            "cavity kapsam dışı" notuyla AYNI kök sınırlama — çok-kabuklu Solid desteği yok).
       SONUÇ: Bu codebase'in basit kutu-tabanlı Solid modelinde, iki gerçek aday senaryonun
       İKİSİ de ayrı, önceden bilinmeyen bir yapısal engele çarpıyor — güvenli/gerçekleşen bir
       "happy path" çok-düzlem senaryosu YOK. Algoritma (`GeneralSolidSubtractor`) SESSİZ
       yanlış geometri üretmek yerine HER İKİ durumda da açık `InvalidOperationException`
       fırlatıyor (istenen, güvenli davranış) — ama bu, çok-düzlem SUBTRACT'in henüz
       ÇÖZÜLMEDİĞİ anlamına geliyor. Bkz. `docs/Roadmap_CSG_Boolean.md`.
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
    public void Subtract_ThroughSlot_DisconnectedResult_ThrowsExplicitError()
    {
        // Through-slot: mirror cap'ler örtüşmüyor AMA sonuç İKİ AYRI bağlantısız parça
        // (bkz. sınıf başı NEDEN notu, 2. engel — çok-kabuklu Solid desteği yok) -> IsValid()
        // başarısız, açık istisna (SESSİZ yanlış/eksik geometri DEĞİL).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(500, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);

        Assert.Throws<InvalidOperationException>(() => GeneralSolidSubtractor.Subtract(a, b));
    }
}
