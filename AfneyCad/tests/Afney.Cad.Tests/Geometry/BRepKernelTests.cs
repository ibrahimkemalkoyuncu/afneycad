using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: B-Rep Kernel Doğruluk Testleri (BRepKernelTests)
   NEDEN: Topology.Solid/Face/Loop/TopologyEdge/Vertex bu oturuma kadar hiç örneklenmiyordu
          (ölü kod). Bu testler, BRepBuilder'ın ürettiği katı cisimlerin GERÇEKTEN topolojik
          olarak geçerli (Euler formülü) ve GERÇEKTEN doğru hacimli (analitik referansla
          çapraz doğrulama) olduğunu kanıtlar — "gerçek B-Rep" iddiasının somut kanıtı.
*/
public class BRepKernelTests
{
    [Fact]
    public void ExtrudeBox_ProducesEulerValidManifoldSolid()
    {
        var solid = BRepBuilder.ExtrudeBox(
            new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis,
            2000, 200, 3000);

        Assert.True(solid.IsValid(), "Euler formülü (V-E+F=2) veya manifold/kapalı-loop kontrolü başarısız — B-Rep geçersiz.");
        Assert.Equal(8, solid.GetVertices().Count());
        Assert.Equal(12, solid.GetEdges().Count());
        Assert.Equal(6, solid.Faces.Count);
    }

    [Fact]
    public void ExtrudeBox_VolumeMatchesAnalyticalBoxVolume()
    {
        double lenU = 2000, lenV = 200, lenW = 3000;
        var solid = BRepBuilder.ExtrudeBox(
            new Vector3D(500, 100, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis,
            lenU, lenV, lenW);

        double expected = lenU * lenV * lenW;
        double actual = solid.GetVolume();

        Assert.Equal(expected, actual, precision: 3);
    }

    [Fact]
    public void ExtrudeBox_WithNonAxisAlignedFrame_VolumeStillMatches()
    {
        // Duvar senaryosu: eksene hizalı olmayan yerel çerçeve (45 derece döndürülmüş uAxis/vAxis).
        double angle = Math.PI / 4;
        var u = new Vector3D(Math.Cos(angle), Math.Sin(angle), 0);
        var v = new Vector3D(-Math.Sin(angle), Math.Cos(angle), 0);
        var w = Vector3D.ZAxis;

        double lenU = 4000, lenV = 200, lenW = 2700;
        var solid = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), u, v, w, lenU, lenV, lenW);

        Assert.True(solid.IsValid());
        Assert.Equal(lenU * lenV * lenW, solid.GetVolume(), precision: 3);
    }

    [Fact]
    public void FaceGetArea_ForVerticalFace_IsCorrect_NotXYProjectionZero()
    {
        // Eski hatalı kod sadece (X,Y)'ye izdüşüm alıyordu — düşey bir yüzeyde (normal=(1,0,0))
        // izdüşüm bir doğruya çöker ve alan ~0 dönerdi. Bir kutunun yan yüzeyi (2000x3000mm,
        // XZ düzleminde) burada gerçek alanı (6.000.000 mm²) vermeli.
        var solid = BRepBuilder.ExtrudeBox(
            new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis,
            2000, 200, 3000);

        var verticalFace = solid.Faces.First(f => Math.Abs(f.Normal.Z) < 1e-6 && Math.Abs(f.Normal.Y) > 0.9);

        Assert.Equal(2000.0 * 3000.0, verticalFace.GetArea(), precision: 3);
    }

    [Fact]
    public void Tessellate_Box_Produces8SharedVertices_And12Triangles()
    {
        var solid = BRepBuilder.ExtrudeBox(
            new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis,
            2000, 200, 3000);

        var (vertices, faces) = BRepTessellator.Tessellate(solid);

        Assert.Equal(8, vertices.Count); // dedup edilmiş — 24 değil
        Assert.Equal(12, faces.Count);   // 6 yüz x 2 üçgen
    }

    /*
       NE/NEDEN: BRepBuilder.ExtrudePolygon konvekslik VARSAYMAZ (topoloji kurucu, sadece
       kapalı bir nokta dizisi bekler) — konkav (dış bükey olmayan) profillerin de doğru
       ekstrude edilebilmesi GEREKİR. PolygonTriangulator'ın ear-clipping algoritması bunu
       teorik olarak destekliyordu ama önceki testler sadece konveks bir kutu (dikdörtgen)
       kullanıyordu — konkav durum hiç KANITLANMAMIŞTI. Bu test, SpaceDetectionEngine'in
       artık tespit edebildiği L-şekilli (köşesi kesik) gerçek mahal ayak izlerine benzer bir
       profili ekstrude ederek hem topolojik geçerliliği hem de hacim/üçgenleme doğruluğunu
       kanıtlıyor — B-Rep kernel'in "sadece kutu" sınırını gerçekten aştığının somut kanıtı.

       GEOMETRİ: 4000x4000mm kare, sağ-üst köşesinden 2000x2000mm bir parça kesilmiş (L-şekli).
       Alan = 16 m² − 4 m² = 12 m² (elle Shoelace ile doğrulandı). 6 köşeli konkav poligon.
    */
    [Fact]
    public void ExtrudePolygon_ConcaveLShape_IsValidWithCorrectVolumeAndTessellation()
    {
        var profile = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 2000, 0),
            new(2000, 2000, 0), new(2000, 4000, 0), new(0, 4000, 0),
        };
        double heightMm = 3000;

        var solid = BRepBuilder.ExtrudePolygon(profile, new Vector3D(0, 0, heightMm), "LShapeTest");

        Assert.True(solid.IsValid(), "Konkav profilden üretilen Solid Euler açısından geçersiz.");
        // V=12 (6 alt+6 üst), E=18 (6 alt+6 üst+6 dikey), F=8 (2 kapak+6 yan) → V-E+F=2
        Assert.Equal(12, solid.GetVertices().Count());
        Assert.Equal(18, solid.GetEdges().Count());
        Assert.Equal(8, solid.Faces.Count);

        double expectedAreaMm2 = 12_000_000; // 12 m² (16 m² kare − 4 m² kesik köşe)
        double expectedVolume = expectedAreaMm2 * heightMm;
        double relativeError = Math.Abs(solid.GetVolume() - expectedVolume) / expectedVolume;
        Assert.True(relativeError < 1e-6, $"Relative error too high: {relativeError}");

        var (vertices, faces) = BRepTessellator.Tessellate(solid);
        Assert.Equal(12, vertices.Count); // dedup edilmiş
        // Kapak yüzleri: 6-köşeli konkav poligon → ear-clipping (n-2)=4 üçgen/kapak.
        // Yan yüzler: 6 kenar x 2 üçgen = 12. Toplam = 4+4+12 = 20.
        Assert.Equal(20, faces.Count);
    }

    /*
       NE/NEDEN: BRepBuilder.FromTriangleSoup — DXF (3DFACE listesi) / IFC (IFCPOLYGONALFACESET
       tessellation) içeri aktarımından SolidEntity'yi yeniden kurmanın tek yolu. Bu test,
       BRepTessellator'ın (İLERİ yön: Solid→üçgen) ÇIKTISINI FromTriangleSoup'a (GERİ yön:
       üçgen→Solid) VERİP, sonucun hâlâ topolojik olarak geçerli (Euler) VE hacim/vertex/edge
       sayısının orijinal kutuyla eşleştiğini kanıtlar — round-trip'in matematiksel temeli.
    */
    [Fact]
    public void FromTriangleSoup_RoundTripsTessellatedBox_ProducesValidSolidWithMatchingVolume()
    {
        double lenU = 2000, lenV = 200, lenW = 3000;
        var original = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, lenU, lenV, lenW);
        var (verts, faces) = BRepTessellator.Tessellate(original);

        var rebuilt = BRepBuilder.FromTriangleSoup(verts, faces, "RoundTripBox");

        Assert.True(rebuilt.IsValid(), "Üçgen çorbasından yeniden kurulan Solid Euler açısından geçersiz.");
        Assert.Equal(8, rebuilt.GetVertices().Count());   // Kaynaşma (weld) sonrası 24 değil, 8 benzersiz köşe.
        Assert.Equal(18, rebuilt.GetEdges().Count());     // 6 kenarın her biri 2 üçgene bölündüğünden 6 orijinal + 12 köşegen = 18.
        Assert.Equal(12, rebuilt.Faces.Count);             // 6 yüz x 2 üçgen = 12 üçgen face.

        double expectedVolume = lenU * lenV * lenW;
        Assert.Equal(expectedVolume, rebuilt.GetVolume(), precision: 0);

        var (origMin, origMax) = original.GetBoundingBox();
        var (newMin, newMax) = rebuilt.GetBoundingBox();
        Assert.Equal(origMin.X, newMin.X, precision: 3);
        Assert.Equal(origMax.Z, newMax.Z, precision: 3);
    }

    [Fact]
    public void FromTriangleSoup_ConcaveLShape_RoundTripsWithMatchingVolume()
    {
        var profile = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 2000, 0),
            new(2000, 2000, 0), new(2000, 4000, 0), new(0, 4000, 0),
        };
        double heightMm = 3000;
        var original = BRepBuilder.ExtrudePolygon(profile, new Vector3D(0, 0, heightMm), "LShapeRoundTrip");
        var (verts, faces) = BRepTessellator.Tessellate(original);

        var rebuilt = BRepBuilder.FromTriangleSoup(verts, faces, "LShapeRebuilt");

        Assert.True(rebuilt.IsValid());
        double expectedVolume = 12_000_000 * heightMm;
        double relativeError = Math.Abs(rebuilt.GetVolume() - expectedVolume) / expectedVolume;
        Assert.True(relativeError < 1e-6, $"Relative error too high: {relativeError}");
    }
}
