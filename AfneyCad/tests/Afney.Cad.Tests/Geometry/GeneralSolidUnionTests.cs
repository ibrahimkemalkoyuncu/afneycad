using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: GeneralSolidUnion Testleri — çok-yüzlü genel UNION (A∪B) montajı, roadmap'in Faz 5
       hedefinin 4. (ve şimdilik son) yapı taşı.
   NEDEN: `docs/Roadmap_CSG_Boolean.md`, "Güncelleme — 2026-08-15 (Session #68)" — SADECE
       coplanar-OLMAYAN (temiz, transversal kesişimli) senaryolar test edilir. Coplanar durum
       (`SegmentBasedSubdivider`'ın kendi `HasAmbiguousCoplanarOverlap` koruması) BİLİNÇLİ olarak
       kapsam dışı — `GeneralSolidUnion` bu istisnayı yakalamadan yukarı fırlatmalı.
*/
public class GeneralSolidUnionTests
{
    [Fact]
    public void Union_TrueCornerNotch_ThreePlanes_ProducesValidResultWithCorrectVolume()
    {
        // A=[0,2000]^3, B=[1500,3000]^3 — roadmap'in "gerçek köşe" (3-düzlemli, Z aralığı A'dan
        // FARKLI, coplanar sorunu YOK) senaryosu — `GeneralSolidSubtractorTests`/
        // `GeneralSolidIntersectorTests`/`SegmentBasedSubdividerTests`'in
        // `..._TrueCornerNotch_ThreePlanes_...` testleriyle AYNI girdi.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);

        var result = GeneralSolidUnion.Union(a, b);

        Assert.True(result.IsValid());

        // A_hacim + B_hacim - kesişim_hacmi. Kesişim = [1500,2000]^3 (A'nın üst sınırı 2000,
        // B'nin alt sınırı 1500 olduğundan her eksende 500 birimlik ortak aralık) = 500^3 =
        // 125.000.000 mm^3 — GÖREV TANIMININ elle hesabı ("500³" diyip ama 421.875.000 [=750³]
        // kullanması) kendi içinde TUTARSIZDI (500³ ≠ 421.875.000); buradaki 500³=125.000.000
        // matematiksel olarak doğru değerdir (A/B koordinatlarından doğrudan türetildi).
        double intersectionVolume = 500.0 * 500.0 * 500.0;
        Assert.Equal(125_000_000.0, intersectionVolume, precision: 3);
        double expectedVolume = 2000.0 * 2000.0 * 2000.0 + 1500.0 * 1500.0 * 1500.0 - intersectionVolume;
        Assert.Equal(11_250_000_000.0, expectedVolume, precision: 3);
        Assert.Equal(expectedVolume, result.GetVolume(), precision: 3);
    }

    [Fact]
    public void Union_TrueCornerNotch_ThreePlanes_OriginalSolidsAreNotMutated()
    {
        // GeneralSolidSubtractor/Intersector'ın aksine (çağıran `a`'yı sonuç olarak kullanmamalı
        // uyarısı), Union HER İKİ girdinin de bağımsız çalışma kopyalarını kullanır — orijinal
        // A/B çağıran tarafta DEĞİŞMEDEN kalmalı.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);

        double aVolumeBefore = a.GetVolume();
        double bVolumeBefore = b.GetVolume();
        int aFaceCountBefore = a.Faces.Count;
        int bFaceCountBefore = b.Faces.Count;

        GeneralSolidUnion.Union(a, b);

        Assert.Equal(aVolumeBefore, a.GetVolume(), precision: 6);
        Assert.Equal(bVolumeBefore, b.GetVolume(), precision: 6);
        Assert.Equal(aFaceCountBefore, a.Faces.Count);
        Assert.Equal(bFaceCountBefore, b.Faces.Count);
        Assert.True(a.IsValid());
        Assert.True(b.IsValid());
    }

    [Fact]
    public void Union_TrueCornerNotch_ThreePlanes_ResultContainsBothOriginalCornersAndTheGap()
    {
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);

        var result = GeneralSolidUnion.Union(a, b);

        // A'nın kendi içi (B'nin dışında kalan bir nokta) -> Union'ın İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(500, 500, 500)));
        // B'nin kendi içi (A'nın dışında kalan bir nokta) -> Union'ın İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(2500, 2500, 2500)));
        // A∩B'nin içi (her ikisinin de içi) -> Union'ın İÇİNDE olmalı.
        Assert.True(SolidClassifier.IsPointInside(result, new Vector3D(1750, 1750, 1750)));
        // Hem A'nın hem B'nin TAMAMEN dışındaki bir nokta -> Union'ın DIŞINDA olmalı.
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(-500, -500, -500)));
        Assert.False(SolidClassifier.IsPointInside(result, new Vector3D(3500, 3500, 3500)));
    }

    [Fact]
    public void Union_NoIntersection_TwoSeparateBoxes_ProducesValidTwoShellResult()
    {
        // B tamamen A'nın dışında — SegmentBasedSubdivider hiçbir Face'i bölmeden TAMAMEN dışarıda
        // sınıflandırır (her iki solid kendi TAM sınırıyla listeye girer). Sonuç, iki bağlantısız
        // kabuktan oluşan GEÇERLİ bir çok-kabuklu Solid olmalı (`Solid.IsValid()`'in kabuk-başına
        // Euler doğrulaması, Session #64).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        var result = GeneralSolidUnion.Union(a, b);

        Assert.True(result.IsValid());
        Assert.Equal(2 * 1000.0 * 1000.0 * 1000.0, result.GetVolume(), precision: 6);
    }

    [Fact]
    public void Union_CoplanarOverlappingCorner_ThrowsNotSupportedInsteadOfWrongGeometry()
    {
        // Köşe-çentiği: A=[0,2000]^3, B=[1500,3000]^2x[0,2000] — Z aralığı AYNI, üst/alt yüzler
        // coplanar VE izdüşümleri KISMEN örtüşüyor. `SegmentBasedSubdivider.HasAmbiguousCoplanarOverlap`
        // koruması burada devreye girmeli — `GeneralSolidUnion` bunu YAKALAMADAN yukarı fırlatmalı
        // (görev tanımının açık isteği).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);

        Assert.Throws<NotSupportedException>(() => GeneralSolidUnion.Union(a, b));
    }
}
