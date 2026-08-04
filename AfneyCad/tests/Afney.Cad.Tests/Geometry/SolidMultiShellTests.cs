using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: Solid.IsValid() Çok-Kabuk (Multi-Shell) Testleri
   NEDEN: `docs/Roadmap_CSG_Boolean.md` (2026-08-04, devam) — `GeneralSolidSubtractor`'ın
       "through-slot" senaryosunda GERÇEKTEN İKİ AYRI bağlantısız parça üretmesi, eski TEK
       global `V-E+F==2` Euler testini kategorik olarak (yanlışlıkla) geçersiz sayıyordu.
       `Solid.IsValid()` artık bağlantılı-bileşen (kabuk) başına doğrulama yapıyor — bu
       testler (1) iki bağımsız, kendi içinde geçerli kutunun TEK bir Solid'e toplanınca
       GEÇERLİ sayıldığını, (2) kabuklardan biri GERÇEKTEN bozuksa (bir Face eksik, manifold
       kural ihlali) hâlâ `false` döndüğünü (çok-kabuk desteğinin gerçek hataları maskelemediğini)
       kilitliyor.
*/
public class SolidMultiShellTests
{
    [Fact]
    public void IsValid_TwoDisjointValidBoxesCombinedIntoOneSolid_ReturnsTrue()
    {
        var box1 = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var box2 = BRepBuilder.ExtrudeBox(new Vector3D(5000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        var combined = new Solid("Combined");
        combined.Faces.AddRange(box1.Faces);
        combined.Faces.AddRange(box2.Faces);

        Assert.True(combined.IsValid());
    }

    [Fact]
    public void IsValid_TwoDisjointBoxes_TotalVolumeIsSum()
    {
        var box1 = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var box2 = BRepBuilder.ExtrudeBox(new Vector3D(5000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 1000, 1000);

        var combined = new Solid("Combined");
        combined.Faces.AddRange(box1.Faces);
        combined.Faces.AddRange(box2.Faces);

        double expected = 1000.0 * 1000.0 * 1000.0 + 2000.0 * 1000.0 * 1000.0;
        Assert.Equal(expected, combined.GetVolume(), precision: 3);
    }

    [Fact]
    public void IsValid_OneShellHasNonManifoldEdge_StillReturnsFalse()
    {
        // Çok-kabuk desteği, GERÇEK bir manifold ihlalini (bir kenarın SADECE tek Face'e
        // ait olması) MASKELEMEMELİ — bu, mevcut (kabuk-öncesi) "Her edge'in 2 face'i olmalı"
        // kuralının çok-kabuk değişikliğinden SONRA da hâlâ çalıştığını doğruluyor.
        var validBox = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var brokenBox = BRepBuilder.ExtrudeBox(new Vector3D(5000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        brokenBox.GetEdges().First().RightFace = null; // manifold'u kasıtlı olarak boz

        var combined = new Solid("Combined");
        combined.Faces.AddRange(validBox.Faces);
        combined.Faces.AddRange(brokenBox.Faces);

        Assert.False(combined.IsValid());
    }
}
