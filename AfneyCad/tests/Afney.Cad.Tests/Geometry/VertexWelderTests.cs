using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: VertexWelder Testleri — CSG Boolean, 1. yapı taşı (izole, öncekini bozmayan adım)
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — genel iki-katı SUBTRACT'in ön koşullarından biri
          (coplanar yüz birleştirme ayrı bir oturuma bırakıldı, bkz. Kullanici_kitabi.md).
          Bu testler, iki BAĞIMSIZ `Solid`'in (ör. ayrı ayrı `ExtrudeBox` ile üretilmiş, aynı
          köşe konumunu paylaşan iki kutu) kaynaşma sonrası TEK bir ortak Vertex nesnesi
          paylaştığını, ama geometrilerinin (hacim, Euler geçerliliği) DEĞİŞMEDİĞİNİ kanıtlıyor.
*/
public class VertexWelderTests
{
    [Fact]
    public void Weld_TwoIndependentBoxesSharingOneCorner_MergesExactlyOneVertexPair()
    {
        // Kutu A: [0,1000]³ — üst-uzak köşesi (1000,1000,1000).
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        // Kutu B: [1000,2000]³ — alt-yakın köşesi TAM OLARAK A'nın üst-uzak köşesiyle aynı konumda,
        // ama BAĞIMSIZ üretildiği için FARKLI bir Vertex nesnesi.
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(1000, 1000, 1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        double volumeABefore = boxA.GetVolume();
        double volumeBBefore = boxB.GetVolume();
        int distinctBefore = boxA.GetVertices().Concat(boxB.GetVertices()).Distinct().Count();
        Assert.Equal(16, distinctBefore); // 8+8, HENÜZ hiçbir ortak REFERANS yok (konum aynı olsa da)

        VertexWelder.Weld(new[] { boxA, boxB }, tolerance: 1e-6);

        int distinctAfter = boxA.GetVertices().Concat(boxB.GetVertices()).Distinct().Count();
        Assert.Equal(15, distinctAfter); // tam olarak 1 çift kaynaştı (16 - 1)

        // Geometri (pozisyonlar/hacim) DEĞİŞMEMELİ — sadece kimlik birleşti.
        Assert.Equal(volumeABefore, boxA.GetVolume(), precision: 3);
        Assert.Equal(volumeBBefore, boxB.GetVolume(), precision: 3);
        Assert.True(boxA.IsValid());
        Assert.True(boxB.IsValid());

        // Paylaşılan köşe: A'nın (1000,1000,1000) konumundaki vertex'i ile B'nin aynı konumdaki
        // vertex'i artık AYNI REFERANS olmalı.
        var sharedInA = boxA.GetVertices().Single(v => v.Position.DistanceTo(new Vector3D(1000, 1000, 1000)) < 1e-6);
        var sharedInB = boxB.GetVertices().Single(v => v.Position.DistanceTo(new Vector3D(1000, 1000, 1000)) < 1e-6);
        Assert.Same(sharedInA, sharedInB);
    }

    [Fact]
    public void Weld_NoCoincidentVertices_ChangesNothing()
    {
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(5000, 5000, 5000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        int distinctBefore = boxA.GetVertices().Concat(boxB.GetVertices()).Distinct().Count();

        VertexWelder.Weld(new[] { boxA, boxB }, tolerance: 1e-6);

        int distinctAfter = boxA.GetVertices().Concat(boxB.GetVertices()).Distinct().Count();
        Assert.Equal(distinctBefore, distinctAfter); // hiçbir şey kaynaşmadı
        Assert.True(boxA.IsValid());
        Assert.True(boxB.IsValid());
    }

    [Fact]
    public void Weld_SingleSolid_SelfWeldDoesNotBreakValidity()
    {
        // Tek bir Solid'in kendi içinde zaten paylaşılan vertex'leri var (BRepBuilder zaten
        // aynı nesneyi referanslıyor) — kendi kendine weld çağrısı hiçbir şeyi bozmamalı
        // (tolerans çok küçük olduğu için zaten-aynı-nesne olan çiftler tekrar işlenmiyor).
        var box = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        double volumeBefore = box.GetVolume();

        VertexWelder.Weld(box, tolerance: 1e-6);

        Assert.Equal(volumeBefore, box.GetVolume(), precision: 3);
        Assert.True(box.IsValid());
        Assert.Equal(8, box.GetVertices().Count());
    }
}
