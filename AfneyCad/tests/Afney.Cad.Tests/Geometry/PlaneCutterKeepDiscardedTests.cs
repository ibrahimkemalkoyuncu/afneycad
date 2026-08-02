using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: PlaneCutter.CutWithPlaneKeepDiscarded Testleri — "chord-edge öksüzleşmesi" düzeltmesi
   NEDEN: `docs/Roadmap_CSG_Boolean.md` (2026-08-02 güncellemesi) — `BuildCapFace`'in atılan
          tarafın chord referansını körlemesine ezmesi, atılan Face'i (doğrudan yeni bir Solid'e
          eklenirse) topolojik olarak TUTARSIZ (kenar-komşuluğu kırık) hâle getiriyordu. Bu
          testler, düzeltmenin (a) atılan yarının GERÇEKTEN geçerli bir Solid ürettiğini, HİÇBİR
          öksüz kenar referansı kalmadığını, (b) mirror cap'in ters normal/slot taşıdığını,
          (c) mevcut `CutWithPlane` (ve dolayısıyla `PlaneCutterTests`) davranışının HİÇ
          değişmediğini kanıtlıyor.
*/
public class PlaneCutterKeepDiscardedTests
{
    [Fact]
    public void CutWithPlaneKeepDiscarded_CenteredCube_DiscardedHalfIsTopologicallyValid()
    {
        var cube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var (keptCap, discardedSolid) = PlaneCutter.CutWithPlaneKeepDiscarded(cube, new Vector3D(0, 0, 0), Vector3D.XAxis);

        // Kept taraf (cube, yerinde değişti) hâlâ geçerli olmalı — mevcut CutWithPlane davranışıyla aynı.
        Assert.True(cube.IsValid());
        Assert.Equal(4_000_000.0, keptCap.GetArea(), precision: 1);

        // Atılan yarı: bağımsız bir Solid olarak Euler-geçerli olmalı (V-E+F=2).
        Assert.True(discardedSolid.IsValid(), "Atılan yarı topolojik olarak geçersiz (öksüz kenar referansı olabilir).");

        // Her kenarın Left/Right Face'i, o Face'in KENDİ Loop'unda GERÇEKTEN referans aldığı
        // bir Face olmalı (öksüzleşme = bu tutarlılığın kırılması). IsValid() zaten Left/Right
        // null olmadığını doğruluyor; burada ek olarak "o face gerçekten bu kenarı biliyor mu"
        // kontrol ediliyor.
        foreach (var edge in discardedSolid.GetEdges())
        {
            bool leftKnows = edge.LeftFace!.Loops.Any(l => l.Edges.Contains(edge));
            bool rightKnows = edge.RightFace!.Loops.Any(l => l.Edges.Contains(edge));
            Assert.True(leftKnows, "LeftFace, bu kenarı kendi Loop'unda referans almıyor (öksüz).");
            Assert.True(rightKnows, "RightFace, bu kenarı kendi Loop'unda referans almıyor (öksüz).");
        }

        // Bağımsız doğrulama: atılan yarı, [-1000,0]x[-1000,1000]x[-1000,1000] kutusuyla hacim eşleşmeli.
        var expectedDiscarded = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 2000, 2000);
        Assert.Equal(expectedDiscarded.GetVolume(), discardedSolid.GetVolume(), precision: 3);
    }

    [Fact]
    public void CutWithPlaneKeepDiscarded_MirrorCap_HasOppositeNormalAndArea()
    {
        var cube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var (keptCap, discardedSolid) = PlaneCutter.CutWithPlaneKeepDiscarded(cube, new Vector3D(0, 0, 0), Vector3D.XAxis);

        // NOT: X=-1000 orijinal (tamamen atılan) yan yüz de aynı alana (4_000_000, 2000x2000) sahip
        // ve normali (-1,0,0) — bu yüzden `Normal.X > 0.9` (mutlak değer DEĞİL) ile ayırt ediliyor;
        // mirror cap +X'e, o yüz -X'e bakar.
        var mirrorCap = discardedSolid.Faces.Single(f => f.GetArea() > 1e-6 && f.Normal.X > 0.9 && f != keptCap);

        // Ayna kapak, orijinal kapağın TAM AYNASI: aynı alan, ZIT normal.
        Assert.Equal(keptCap.GetArea(), mirrorCap.GetArea(), precision: 1);
        Assert.True(keptCap.Normal.Dot(mirrorCap.Normal) < -0.9, "Mirror cap normali kept cap'in ZITTI olmalı.");

        // keptCap -X yönüne (atılan hacme) bakıyordu (bkz. PlaneCutterTests) -> mirror cap +X'e bakmalı.
        Assert.True(mirrorCap.Normal.X > 0.9);
    }

    [Fact]
    public void CutWithPlaneKeepDiscarded_KeptSideBehavesIdenticallyToPlainCutWithPlane()
    {
        // Aynı kesim, biri CutWithPlane biri CutWithPlaneKeepDiscarded ile — kept taraf
        // (hacim, Euler, kapak alanı/normali) BİREBİR aynı sonucu vermeli (additive/paralel
        // yol, mevcut davranışı DEĞİŞTİRMEMELİ).
        var cubeA = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var cubeB = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var plainCap = PlaneCutter.CutWithPlane(cubeA, new Vector3D(0, 0, 0), Vector3D.XAxis);
        var (keptCap, _) = PlaneCutter.CutWithPlaneKeepDiscarded(cubeB, new Vector3D(0, 0, 0), Vector3D.XAxis);

        Assert.Equal(cubeA.GetVolume(), cubeB.GetVolume(), precision: 6);
        Assert.Equal(cubeA.Faces.Count, cubeB.Faces.Count);
        Assert.Equal(plainCap.GetArea(), keptCap.GetArea(), precision: 6);
        Assert.Equal(plainCap.Normal.X, keptCap.Normal.X, precision: 6);
        Assert.True(cubeA.IsValid());
        Assert.True(cubeB.IsValid());
    }
}
