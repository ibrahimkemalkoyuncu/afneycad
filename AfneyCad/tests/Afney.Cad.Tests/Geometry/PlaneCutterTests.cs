using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: PlaneCutter Testleri — CSG Boolean Faz 4 (yarı-uzay SUBTRACT)
   NEDEN: `docs/Roadmap_CSG_Boolean.md`'nin önerdiği ilk Faz 4 senaryosu (A=[0,2000]³ eksi
          B=[1000,3000]×[0,2000]×[0,2000]) analiz edilince tek bir düzlem kesimine indirgendi
          (bkz. PlaneCutter.cs başlığındaki NEDEN notu) — bu testler HEM genel bir kutu-ortadan-
          kesme senaryosunu HEM DE roadmap'in TAM ÖNERDİĞİ senaryoyu, `BRepBuilder.ExtrudeBox`'ın
          bağımsız ürettiği beklenen sonuçla ÇAPRAZ DOĞRULUYOR (hacim + Euler + kapak alanı).
*/
public class PlaneCutterTests
{
    [Fact]
    public void CutWithPlane_CenteredCube_KeepsPositiveHalf_MatchesIndependentExtrudeBox()
    {
        var cube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        Assert.True(cube.IsValid());

        var capFace = PlaneCutter.CutWithPlane(cube, new Vector3D(0, 0, 0), Vector3D.XAxis);

        Assert.True(cube.IsValid(), "Kesim sonrası Solid hâlâ Euler-geçerli olmalı.");

        // Bağımsız doğrulama: [0,1000]x[-1000,1000]x[-1000,1000] kutusuyla hacim/köşe eşleşmeli.
        var expected = BRepBuilder.ExtrudeBox(new Vector3D(0, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);
        Assert.Equal(expected.GetVolume(), cube.GetVolume(), precision: 3);
        // 6 orijinal yüz: X=-1000 yüzü tamamen atılır (-1), X=+1000 yüzü aynen kalır (0),
        // diğer 4 yüz (alt/üst/Y=-1000/Y=+1000) düzlem tarafından kesilip pozitif yarısı
        // tutulur (net 0 — böl-sonra-birini-at), + 1 yeni kapak yüzü eklenir => 6-1+1=6.
        Assert.Equal(6, cube.Faces.Count);

        // Kapak yüzü tam kesit alanı (2000x2000mm) olmalı, dışa dönük normali kalan katının
        // dışına (negatif X yönüne) bakmalı — kalan katı pozitif X tarafında (+X) tutulduğu için.
        Assert.Equal(4_000_000.0, capFace.GetArea(), precision: 1);
        Assert.True(capFace.Normal.X < -0.9, "Kapak normali kalan katının dışına (−X) bakmalı.");
    }

    [Fact]
    public void CutWithPlane_RoadmapScenario_SlabCutReducesToSinglePlaneCut()
    {
        // Roadmap senaryosu: A=[0,2000]³ eksi B=[1000,3000]×[0,2000]×[0,2000]. B'nin Y/Z
        // aralığı A'nınkiyle BİREBİR aynı olduğu için A∖B, A'yı X=1000 düzlemiyle kesip
        // X<1000 tarafını tutmakla MATEMATİKSEL OLARAK ÖZDEŞTİR (bkz. PlaneCutter.cs NEDEN notu).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var capFace = PlaneCutter.CutWithPlane(a, new Vector3D(1000, 0, 0), -Vector3D.XAxis);

        Assert.True(a.IsValid());

        var expected = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);
        Assert.Equal(4_000_000_000.0, expected.GetVolume(), precision: 3); // sağlık kontrolü: 1000*2000*2000mm³
        Assert.Equal(expected.GetVolume(), a.GetVolume(), precision: 3);

        Assert.Equal(4_000_000.0, capFace.GetArea(), precision: 1);
        Assert.True(capFace.Normal.X > 0.9, "Kapak normali X=1000'in ötesine (atılan hacme, +X) bakmalı.");
    }

    [Fact]
    public void CutWithPlane_PlaneMissesSolidEntirely_Throws()
    {
        var cube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        // Düzlem X=5000: kübün tamamı (X∈[-1000,1000]) negatif tarafta -> tüm yüzler atılır,
        // kapak için hiç kiriş kalmaz -> açık hata (sessiz yanlış/boş Solid yerine).
        Assert.Throws<NotSupportedException>(() => PlaneCutter.CutWithPlane(cube, new Vector3D(5000, 0, 0), Vector3D.XAxis));
    }

    [Fact]
    public void CutWithPlane_PlaneEntirelyOnPositiveSide_KeepsSolidUnchanged_NoCapNeeded()
    {
        var cube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        // Düzlem X=-5000: kübün tamamı (X∈[-1000,1000]) pozitif tarafta -> hiçbir yüz atılmaz,
        // ama yine de kiriş yok -> BuildCapFace hâlâ hata verir (kapak yüzü olmadan Solid
        // "kesilmemiş" sayılır; çağıran taraf kesişim olup olmadığını önceden kontrol etmeli).
        Assert.Throws<NotSupportedException>(() => PlaneCutter.CutWithPlane(cube, new Vector3D(-5000, 0, 0), Vector3D.XAxis));
    }
}
