using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: FaceSplitter.SplitAtPolylineChord Testleri — CSG Boolean, `docs/Roadmap_CSG_Boolean.md`
       2026-08-14 girdisinin "somut, net gereksinim" bölümünün 1. maddesi (polyline-chord
       genellemesi, UNION için gereken ilk yapı taşı).

   SENARYO (b/c testleri) — roadmap'in ELLE doğrulanmış 3-düzlemli köşe örneği:
       A=[0,2000]³ küpünün X=2000 yüzünde (Y,Z) yerel koordinatlarıyla:
       - Yüzün kendi köşeleri (Y,Z): (0,0),(2000,0),(2000,2000),(0,2000)
       - Kesişim polyline'ı: (Y=1500,Z=2000) → (Y=1500,Z=1500) → (Y=2000,Z=1500)
         İlk nokta üst kenarda (Z=2000), son nokta arka kenarda (Y=2000), ORTA nokta
         Face'in İÇİNDE.
       Bu, `BRepBuilder.ExtrudeBox`'ın X=2000 yüzü için ürettiği GERÇEK Loop sırasıyla elle
       izlenip (bkz. oturum notları) İKİ alt-Face'in ALANLARI elle hesaplandı:
       - FaceA (büyük parça, köşe çentiği ÇIKARILMIŞ): 3.750.000 mm²
       - FaceB (küçük üçgenimsi köşe parçası): 250.000 mm²
       - Toplam: 4.000.000 mm² = 2000×2000 (orijinal yüzün alanı) — KORUNMALI.
*/
public class FaceSplitterPolylineChordTests
{
    [Fact]
    public void SplitAtPolylineChord_TwoPointPolyline_MatchesSplitAtChord()
    {
        // Kiriş (chord) yolu: mevcut SplitAtChord testiyle AYNI senaryo (bottom face, front/back mid).
        var boxChord = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var boxPoly = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var bottomFaceChord = boxChord.Faces.First(f => f.Normal.Z < -0.9);
        var bottomFacePoly = boxPoly.Faces.First(f => f.Normal.Z < -0.9);

        var frontEdgeChord = boxChord.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Y) < 1e-6 && Math.Abs(e.EndVertex.Position.Y) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6);
        var backEdgeChord = boxChord.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Y - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.Y - 2000) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6);
        var (frontMid, _, _) = EdgeSplitter.SplitEdgeAt(boxChord, frontEdgeChord, new Vector3D(1000, 0, 0));
        var (backMid, _, _) = EdgeSplitter.SplitEdgeAt(boxChord, backEdgeChord, new Vector3D(1000, 2000, 0));
        var (faceAChord, faceBChord, _) = FaceSplitter.SplitAtChord(boxChord, bottomFaceChord, frontMid, backMid);

        var frontEdgePoly = boxPoly.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Y) < 1e-6 && Math.Abs(e.EndVertex.Position.Y) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6);
        var backEdgePoly = boxPoly.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.Y - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.Y - 2000) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z) < 1e-6 && Math.Abs(e.EndVertex.Position.Z) < 1e-6);
        EdgeSplitter.SplitEdgeAt(boxPoly, frontEdgePoly, new Vector3D(1000, 0, 0));
        EdgeSplitter.SplitEdgeAt(boxPoly, backEdgePoly, new Vector3D(1000, 2000, 0));

        var polylinePoints = new List<Vector3D> { new Vector3D(1000, 0, 0), new Vector3D(1000, 2000, 0) };
        var (faceAPoly, faceBPoly, chordEdges) = FaceSplitter.SplitAtPolylineChord(boxPoly, bottomFacePoly, polylinePoints);

        Assert.Single(chordEdges); // Tek segment → tek kiriş kenarı (SplitAtChord'un tek `Chord`'u ile aynı desen).

        Assert.Equal(boxChord.GetVertices().Count(), boxPoly.GetVertices().Count());
        Assert.Equal(boxChord.GetEdges().Count(), boxPoly.GetEdges().Count());
        Assert.Equal(boxChord.Faces.Count, boxPoly.Faces.Count);
        Assert.True(boxPoly.IsValid());
        Assert.Equal(boxChord.GetVolume(), boxPoly.GetVolume(), precision: 3);

        Assert.Equal(faceAChord.GetArea(), faceAPoly.GetArea(), precision: 3);
        Assert.Equal(faceBChord.GetArea(), faceBPoly.GetArea(), precision: 3);
        Assert.Equal(2_000_000.0, faceAPoly.GetArea(), precision: 1);
        Assert.Equal(2_000_000.0, faceBPoly.GetArea(), precision: 1);
    }

    /*
       Yardımcı: X=2000 yüzünü, üzerindeki iki sınır kenarını (üst kenar Z=2000, arka kenar
       Y=2000) EdgeSplitter ile bölerek "kesişim segmenti" senaryosu için hazırlar; polyline'ın
       ilk/son noktası olarak kullanılacak GERÇEK sınır noktalarını döner.
    */
    private static (Solid Solid, Face RightFace) BuildCornerScenario()
    {
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var rightFace = box.Faces.First(f => f.Normal.X > 0.9);

        var topEdge = box.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.X - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.X - 2000) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Z - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.Z - 2000) < 1e-6);
        var backEdge = box.GetEdges().First(e =>
            Math.Abs(e.StartVertex.Position.X - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.X - 2000) < 1e-6 &&
            Math.Abs(e.StartVertex.Position.Y - 2000) < 1e-6 && Math.Abs(e.EndVertex.Position.Y - 2000) < 1e-6);

        EdgeSplitter.SplitEdgeAt(box, topEdge, new Vector3D(2000, 1500, 2000));
        EdgeSplitter.SplitEdgeAt(box, backEdge, new Vector3D(2000, 2000, 1500));

        return (box, rightFace);
    }

    [Fact]
    public void SplitAtPolylineChord_TwoSegmentChain_ThreePlaneCorner_SplitsWithHandVerifiedAreas()
    {
        var (box, rightFace) = BuildCornerScenario();

        int vBefore = box.GetVertices().Count(); // 2 sınır bölme SONRASI (yüz bölmeden ÖNCE)
        int eBefore = box.GetEdges().Count();
        int fBefore = box.Faces.Count;
        double volumeBefore = box.GetVolume();

        var polylinePoints = new List<Vector3D>
        {
            new Vector3D(2000, 1500, 2000),
            new Vector3D(2000, 1500, 1500), // ARA nokta — Face'in İÇİNDE, sınırında DEĞİL
            new Vector3D(2000, 2000, 1500),
        };

        var (faceA, faceB, chordEdges) = FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints);

        Assert.Equal(2, chordEdges.Count); // 3 nokta → 2 kiriş kenarı

        // Yüz bölme: ΔV=+1 (tek ARA nokta yeni Vertex), ΔE=+2 (2 yeni kiriş kenarı), ΔF=+1.
        Assert.Equal(vBefore + 1, box.GetVertices().Count());
        Assert.Equal(eBefore + 2, box.GetEdges().Count());
        Assert.Equal(fBefore + 1, box.Faces.Count);
        Assert.True(box.IsValid(), "Polyline-chord bölme sonrası Solid hâlâ Euler-geçerli olmalı.");
        Assert.Equal(volumeBefore, box.GetVolume(), precision: 3); // Saf topolojik işlem — hacim DEĞİŞMEMELİ.

        Assert.DoesNotContain(rightFace, box.Faces);

        // Elle hesaplanmış alanlar (bkz. sınıf başlığı NE bölümü).
        Assert.Equal(3_750_000.0, faceA.GetArea(), precision: 1);
        Assert.Equal(250_000.0, faceB.GetArea(), precision: 1);
        Assert.Equal(4_000_000.0, faceA.GetArea() + faceB.GetArea(), precision: 1); // Alan korunumu.

        // Köşe sayısı doğrulaması: FaceA 6 köşeli (pentagon + kiriş ucu = hexagon), FaceB 4 köşeli (quad).
        Assert.Equal(6, faceA.GetOuterLoop()!.GetOrderedVertices().Count);
        Assert.Equal(4, faceB.GetOuterLoop()!.GetOrderedVertices().Count);
    }

    [Fact]
    public void SplitAtPolylineChord_ThreeSegmentChain_CollinearExtraVertex_PreservesAreasAndValidity()
    {
        var (box, rightFace) = BuildCornerScenario();

        int vBefore = box.GetVertices().Count();
        int eBefore = box.GetEdges().Count();
        int fBefore = box.Faces.Count;
        double volumeBefore = box.GetVolume();

        // Aynı köşe senaryosu, ama orta segment iki parçaya bölünmüş (kolinear ekstra nokta) —
        // 3 segmentli zincir, alan/şekil DEĞİŞMEMELİ.
        var polylinePoints = new List<Vector3D>
        {
            new Vector3D(2000, 1500, 2000),
            new Vector3D(2000, 1500, 1500),
            new Vector3D(2000, 1750, 1500), // kolinear ekstra ARA nokta
            new Vector3D(2000, 2000, 1500),
        };

        var (faceA, faceB, chordEdges) = FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints);

        Assert.Equal(3, chordEdges.Count); // 4 nokta → 3 kiriş kenarı

        // ΔV=+2 (2 ARA nokta), ΔE=+3 (3 yeni kiriş kenarı), ΔF=+1.
        Assert.Equal(vBefore + 2, box.GetVertices().Count());
        Assert.Equal(eBefore + 3, box.GetEdges().Count());
        Assert.Equal(fBefore + 1, box.Faces.Count);
        Assert.True(box.IsValid());
        Assert.Equal(volumeBefore, box.GetVolume(), precision: 3);

        // Kolinear ekstra nokta şekli DEĞİŞTİRMEMELİ — alanlar 2-segmentli testle AYNI.
        Assert.Equal(3_750_000.0, faceA.GetArea(), precision: 1);
        Assert.Equal(250_000.0, faceB.GetArea(), precision: 1);

        Assert.Equal(7, faceA.GetOuterLoop()!.GetOrderedVertices().Count); // hexagon + 1 kolinear nokta
        Assert.Equal(5, faceB.GetOuterLoop()!.GetOrderedVertices().Count); // quad + 1 kolinear nokta
    }

    [Fact]
    public void SplitAtPolylineChord_FirstPointNotOnBoundary_ThrowsArgumentException()
    {
        var (box, rightFace) = BuildCornerScenario();

        var polylinePoints = new List<Vector3D>
        {
            new Vector3D(2000, 1500, 1500), // sınırda DEĞİL — Face'in içinde bir nokta ilk eleman olarak verildi
            new Vector3D(2000, 2000, 1500),
        };

        Assert.Throws<ArgumentException>(() => FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints));
    }

    [Fact]
    public void SplitAtPolylineChord_LastPointNotOnBoundary_ThrowsArgumentException()
    {
        var (box, rightFace) = BuildCornerScenario();

        var polylinePoints = new List<Vector3D>
        {
            new Vector3D(2000, 1500, 2000),
            new Vector3D(2000, 1500, 1500), // sınırda DEĞİL — son eleman olarak verildi
        };

        Assert.Throws<ArgumentException>(() => FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints));
    }

    [Fact]
    public void SplitAtPolylineChord_TooFewPoints_ThrowsArgumentException()
    {
        var (box, rightFace) = BuildCornerScenario();
        var polylinePoints = new List<Vector3D> { new Vector3D(2000, 1500, 2000) };

        Assert.Throws<ArgumentException>(() => FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints));
    }

    [Fact]
    public void SplitAtPolylineChord_DuplicateConsecutivePoints_ThrowsArgumentException()
    {
        var (box, rightFace) = BuildCornerScenario();

        var polylinePoints = new List<Vector3D>
        {
            new Vector3D(2000, 1500, 2000),
            new Vector3D(2000, 1500, 2000), // ilk noktayla ÇAKIŞAN dejenere ikinci nokta
            new Vector3D(2000, 2000, 1500),
        };

        Assert.Throws<ArgumentException>(() => FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints));
    }

    [Fact]
    public void SplitAtPolylineChord_SelfIntersectingPolyline_ThrowsArgumentException()
    {
        var (box, rightFace) = BuildCornerScenario();

        // Bir "bowtie" (kelebek) şekli: segment 0 (A→P1) ile segment 2 (P2→D) matematiksel
        // olarak KESİŞECEK şekilde elle doğrulanmış (orientation/cross-product testiyle,
        // ikisi de karşı işaretli d1/d2 VE d3/d4 üretiyor — GERÇEK bir kesişim, sadece
        // dokunma değil) — kendi kendini kesen dejenere polyline.
        var polylinePoints = new List<Vector3D>
        {
            new Vector3D(2000, 1500, 2000),   // A: sınır (üst kenar)
            new Vector3D(2000, 1900, 1500),   // P1: içeri
            new Vector3D(2000, 1500, 1600),   // P2: içeri (segment A-P1 ile segment P2-D kesişiyor)
            new Vector3D(2000, 2000, 1500),   // D: sınır (arka kenar)
        };

        Assert.Throws<ArgumentException>(() => FaceSplitter.SplitAtPolylineChord(box, rightFace, polylinePoints));
    }
}
