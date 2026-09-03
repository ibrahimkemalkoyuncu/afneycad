using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;
using Xunit;

namespace Afney.Cad.Tests.Geometry;

/*
   NE: `GeneralSolidUnion`'ın bilinçli kapsam dışı bıraktığı 2 kenar durumun testleri.
   NEDEN: Session #70'te araştırıldı — `MergeCoplanarOverlappingFacesInto` (Session #69) SADECE
          AYNI YÖNLÜ (`na·nb>0`), bire-bir coplanar-örtüşen Face çiftlerini birleştiriyor. Bu
          testler, KAPSAM DIŞI durumların (zıt yönlü coplanar, bir A-Face'in birden fazla B-Face
          ile örtüşmesi) SESSİZCE YANLIŞ GEOMETRİ ÜRETMEDİĞİNİ, açık bir hatayla GÜVENLE
          reddedildiğini kilitler — ileride bu davranış yanlışlıkla "sessiz yanlış sonuca" gerilerse
          (ör. korumalardan biri gevşetilirse) bu testler kırılıp haber verir.
          Bkz. `docs/Roadmap_CSG_Boolean.md` "Güncelleme — 2026-08-XX (Session #70)".
*/
public class GeneralSolidUnionUnsupportedCasesTests
{
    [Fact]
    public void Union_OppositeDirectionCoplanarFullOverlap_TouchingBoxes_ThrowsInsteadOfWrongGeometry()
    {
        // İki kutu X=1000 düzleminde TAM bitişik (ortak yüz TAM ÇAKIŞIK, na·nb<0 — A'nın +X
        // yüzü ile B'nin -X yüzü aynı düzlemde, zıt yönlü). En temel "iki bitişik hacmi birleştir"
        // senaryosu gibi görünse de, `MergeCoplanarOverlappingFacesInto` bunu KASITLI OLARAK
        // yakalamıyor (sadece aynı yönlü çiftler) — normal `SegmentBasedSubdivider` akışına düşüp
        // `HasAmbiguousCoplanarOverlap` ile güvenle reddediliyor.
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var b = BRepBuilder.ExtrudeBox(new Vector3D(1000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);

        Assert.Throws<NotSupportedException>(() => GeneralSolidUnion.Union(a, b));
    }

    [Fact]
    public void Union_MultiShellCavity_ExternalBoxCoplanarWithCavityWall_ThrowsInsteadOfWrongGeometry()
    {
        // B çok-kabuklu: dış kabuk [0,1000]^3 + içine gömülü ayrı bir iç kabuk (cavity) [200,800]^3
        // (Session #64'ün çok-kabuklu `Solid.IsValid()` desteğiyle topolojik olarak geçerli). A,
        // cavity'nin bir duvarıyla (X=800) coplanar. Bu, `MergeCoplanarOverlappingFacesInto`'nun
        // kapsamı dışında bıraktığı "iç boşluk duvarı" durumu — güvenle reddedilmeli.
        var bOuter = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var bCavity = BRepBuilder.ExtrudeBox(new Vector3D(200, 200, 200), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 600, 600, 600);
        var b = new Solid("B_with_cavity");
        b.Faces.AddRange(bOuter.Faces);
        b.Faces.AddRange(bCavity.Faces);
        Assert.True(b.IsValid());

        var a = BRepBuilder.ExtrudeBox(new Vector3D(800, 300, 300), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 800, 400, 400);

        Assert.Throws<NotSupportedException>(() => GeneralSolidUnion.Union(a, b));
    }

    [Fact]
    public void Union_SingleAFaceCoplanarOverlapsTwoSeparateBFaces_ThrowsInsteadOfWrongGeometry()
    {
        // A'nın tek büyük üst yüzü (Z=2000, footprint [0,2000]x[0,2000]), B'nin İKİ AYRI (bağımsız
        // kabuk) "kolonunun" ikisinin de üst yüzüyle AYNI YÖNLÜ coplanar-örtüşüyor. `Merge
        // CoplanarOverlappingFacesInto` her aFace/bFace'i EN FAZLA BİR kez eşleştiriyor — A'nın
        // üst yüzü col1 ile birleşip tükeniyor, col2'nin üst yüzü ise A'nın artık silinmiş üst
        // yüzünü bir daha bulamıyor, montaj sonu topolojik doğrulama (`Solid.IsValid()`) bunu
        // yakalayıp güvenle reddediyor (sessiz dejenere geometri üretmek yerine).
        var a = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);

        var col1 = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 2000);
        var col2 = BRepBuilder.ExtrudeBox(new Vector3D(1500, -500, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1000, 2000);
        var b = new Solid("B_two_columns");
        b.Faces.AddRange(col1.Faces);
        b.Faces.AddRange(col2.Faces);
        Assert.True(b.IsValid());

        Assert.Throws<InvalidOperationException>(() => GeneralSolidUnion.Union(a, b));
    }
}
