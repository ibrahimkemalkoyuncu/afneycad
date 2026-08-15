using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: SegmentBasedSubdivider Testleri — CSG Boolean, `docs/Roadmap_CSG_Boolean.md` 2026-08-14
       girdisinin 2. yapı taşı (segment-tabanlı subdivide, `FaceSplitter.SplitAtPolylineChord`'un
       ilk gerçek kullanıcısı).
   NOT: Canlı test sırasında `FaceIntersection.Intersect`'in coplanar KISMEN-örtüşen Face
        çiftlerinde TUTARSIZ davrandığı keşfedildi (bazen boş, bazen sınır-kesişim segmenti
        döndürüyor — yön/kenar sırasına bağlı). Bu yüzden "segments.Count==0 → güvenle
        bölünmeden sınıflandır" varsayımı SADECE gerçek etkileşimsizlik durumunda geçerli;
        coplanar+izdüşüm-örtüşmesi durumunda `HasAmbiguousCoplanarOverlap` açık bir
        `NotSupportedException` fırlatır (sessizce yanlış sonuç ÜRETMEZ). Aşağıdaki testler bu
        sınırı KİLİTLER.
*/
public class SegmentBasedSubdividerTests
{
    [Fact]
    public void SubdivideAndClassifyOutside_TrueCornerNotch_ThreePlanes_SplitsXFaceAndClassifiesCorrectly()
    {
        // A=[0,2000]^3, B=[1500,3000]^3 — roadmap'in "gerçek köşe" (3-düzlemli, Z aralığı FARKLI,
        // coplanar sorunu yok) senaryosu, `GeneralSolidSubtractorTests.
        // Subtract_TrueCornerNotch_ThreePlanes_...` ile AYNI girdi.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);

        var outsideFragments = SegmentBasedSubdivider.SubdivideAndClassifyOutside(a, b);

        Assert.NotEmpty(outsideFragments);

        // A'nın X=2000 yüzü, B'nin Y=1500 ve Z=1500 yüzleriyle kesişip roadmap'in elle
        // doğruladığı (2000,1500,2000)->(2000,1500,1500)->(2000,2000,1500) polyline'ı boyunca
        // BÖLÜNMÜŞ olmalı — bu yüzden X=2000 yüzünden İKİ fragman türemiş olmalı (biri B'nin
        // dışında/kept, biri B'nin içinde/discarded), TEK bölünmemiş Face DEĞİL.
        var xFaceFragments = outsideFragments
            .Where(f => Math.Abs(f.Normal.X - 1.0) < 1e-6)
            .ToList();
        Assert.Single(xFaceFragments); // sadece dışarıda kalan (büyük) parça listede olmalı

        // X=2000 fragmanının alanı TAM olarak `FaceSplitterPolylineChordTests`'in elle
        // hesapladığı büyük-parça alanıyla (3.750.000) eşleşmeli (AYNI polyline, AYNI Face,
        // farklı bir çağrı yolundan üretildi).
        Assert.Equal(3_750_000.0, xFaceFragments[0].GetArea(), precision: 1);

        // A'nın X=0/Y=0/Z=0 gibi B ile hiç kesişmeyen yüzleri BÖLÜNMEDEN, TAM olarak outside
        // listesine dahil olmalı (2000x2000 = 4.000.000 mm^2).
        var xZeroFace = outsideFragments.FirstOrDefault(f => Math.Abs(f.Normal.X + 1.0) < 1e-6);
        Assert.NotNull(xZeroFace);
        Assert.Equal(4_000_000.0, xZeroFace!.GetArea(), precision: 1);
    }

    [Fact]
    public void SubdivideAndClassifyOutside_SingleFaceIntersection_NoHiddenCoplanarity_SplitsOneFaceOnly()
    {
        // Tek-yüzey kesişimi (basit durum): A=[0,2000]^3, B'nin X aralığı A'yı [1000,..] noktasında
        // kesiyor AMA B'nin Y/Z aralığı BİLEREK A'nınkinden GENİŞ ve KAYDIRILMIŞ ([-500,2500])
        // — bu yüzden A'nın Y/Z yüzleri B'nin HİÇBİR yüzüyle coplanar DEĞİL (yalnızca B'nin
        // X=1000 yüzü A'yı transversal kesiyor). İlk versiyonda B'nin Y/Z aralığı A ile TAM
        // aynıydı (0..2000) — bu, "basit tek-yüzey" senaryosunun aslında GİZLİ bir coplanar
        // kısmi-örtüşme (Y=0/Y=2000/Z=0/Z=2000 yüzlerinde) içerdiği anlamına geliyordu ve
        // `HasAmbiguousCoplanarOverlap` bunu (doğru biçimde) NotSupportedException ile
        // reddediyordu — bkz. `SubdivideAndClassifyOutside_HiddenCoplanarOverlap_ThrowsInsteadOfMisclassifying`.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1000, -500, -500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 3000, 3000, 3000);

        var outsideFragments = SegmentBasedSubdivider.SubdivideAndClassifyOutside(a, b);

        // A'nın X=2000 yüzü TAMAMEN B'nin içinde (X:[1000,4000] aralığında) — discarded, listede
        // YOK.
        Assert.DoesNotContain(outsideFragments, f => Math.Abs(f.Normal.X - 1.0) < 1e-6);

        var xZeroFace = outsideFragments.FirstOrDefault(f => Math.Abs(f.Normal.X + 1.0) < 1e-6);
        Assert.NotNull(xZeroFace);
        Assert.Equal(4_000_000.0, xZeroFace!.GetArea(), precision: 1); // bölünmemiş, tam alan

        // Y=0 yüzü (normal (0,-1,0)) B'nin X=1000 yüzüyle transversal kesişip BÖLÜNMÜŞ olmalı —
        // X<1000 parçası outside listesinde, alanı 1000x2000 = 2.000.000 olmalı.
        var yZeroFragments = outsideFragments.Where(f => Math.Abs(f.Normal.Y + 1.0) < 1e-6).ToList();
        Assert.Single(yZeroFragments);
        Assert.Equal(2_000_000.0, yZeroFragments[0].GetArea(), precision: 1);
    }

    [Fact]
    public void SubdivideAndClassifyOutside_HiddenCoplanarOverlap_ThrowsInsteadOfMisclassifying()
    {
        // Köşe-çentiği: A=[0,2000]^3, B=[1500,3000]^2x[0,2000] — Z aralığı AYNI (A'nın Z=0/Z=2000
        // yüzleri B'nin Z=0/Z=2000 yüzleriyle coplanar VE izdüşümleri KISMEN örtüşüyor). Canlı
        // testte `FaceIntersection.Intersect`'in bu durumda TUTARSIZ davrandığı (bazı yüz
        // çiftlerinde segment üretip bazılarında üretmediği) keşfedildi — "segments.Count==0 ise
        // güvenle bölünmeden sınıflandır" varsayımı burada YANLIŞ sonuç üretebiliyordu. Düzeltme:
        // bu belirsiz durumda artık sessizce yanlış sınıflandırmak YERİNE açık `NotSupportedException`
        // fırlatılıyor (convex-convex 2D union primitifi — roadmap'in SONRAKİ, ayrı adımı —
        // olmadan güvenle çözülemez).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        Assert.Throws<NotSupportedException>(() => SegmentBasedSubdivider.SubdivideAndClassifyOutside(a, b));
    }

    [Fact]
    public void SubdivideAndClassifyOutside_NoIntersection_AllFacesClassifiedOutsideUnsplit()
    {
        // B tamamen A'nın dışında — hiçbir A-Face, B'nin hiçbir Face'iyle GERÇEKTEN kesişmiyor
        // (ne transversal ne coplanar). Tüm A Face'leri BÖLÜNMEDEN outside listesine dahil
        // olmalı (through-slot benzeri "hiç etkileşim yok" uç durumu).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        var outsideFragments = SegmentBasedSubdivider.SubdivideAndClassifyOutside(a, b);

        Assert.Equal(6, outsideFragments.Count); // kutu = 6 yüz, hiçbiri bölünmedi
        Assert.All(outsideFragments, f => Assert.Equal(4, f.GetOuterLoop()!.GetOrderedVertices().Count));
        Assert.Equal(6_000_000.0, outsideFragments.Sum(f => f.GetArea()), precision: 1); // 6 x 1000x1000
    }
}
