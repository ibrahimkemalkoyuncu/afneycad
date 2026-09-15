using System.Linq;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: SolidEntity Grip Testleri (SolidEntityGripTests)
   NEDEN — "4M FineSANI karşılaştırma raporu"nun bölüm 07/madde 7 bulgusu: SolidEntity
          hiç grip desteklemiyordu (kasıtlı — genel vertex-sürükleme Solid.IsValid()'i
          bozabilirdi). Bu testler, artık SADECE eksene-hizalı kutular için eklenen dar
          ve güvenli 6-yüz-merkezi resize grip özelliğini doğruluyor: doğru grip sayısı/
          konumu, sürükleme sonrası doğru boyut, karşı yüzün sabit kalması, minimum boyut
          korunumu, topolojik geçerliliğin (Solid.IsValid()) HER durumda korunması ve
          döndürülmüş/kutu-olmayan Solid'lerde grip listesinin boş kalması (güvenli
          geri düşüş).
*/
public class SolidEntityGripTests
{
    private static SolidEntity MakeAxisAlignedBox(double lenX = 1000, double lenY = 500, double lenZ = 300)
    {
        var solid = BRepBuilder.ExtrudeBox(Vector3D.Zero, Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, lenX, lenY, lenZ, "TestBox");
        return new SolidEntity(solid);
    }

    [Fact]
    public void AxisAlignedBox_HasSixGripPoints_AtFaceCenters()
    {
        var box = MakeAxisAlignedBox(1000, 500, 300);
        var grips = box.GetGripPoints().ToList();

        Assert.Equal(6, grips.Count);
        Assert.Contains(grips, g => g == new Vector3D(0, 250, 150));    // -X yüzü
        Assert.Contains(grips, g => g == new Vector3D(1000, 250, 150)); // +X yüzü
        Assert.Contains(grips, g => g == new Vector3D(500, 0, 150));    // -Y yüzü
        Assert.Contains(grips, g => g == new Vector3D(500, 500, 150));  // +Y yüzü
        Assert.Contains(grips, g => g == new Vector3D(500, 250, 0));    // -Z yüzü
        Assert.Contains(grips, g => g == new Vector3D(500, 250, 300));  // +Z yüzü
    }

    [Fact]
    public void MoveGripPointAt_PlusXFace_ResizesXOnly_KeepsOppositeFaceFixed()
    {
        var box = MakeAxisAlignedBox(1000, 500, 300);

        // +X yüzünü (indeks 1) X=1000'den X=1500'e sürükle.
        box.MoveGripPointAt(1, new Vector3D(1500, 250, 150));

        var (min, max) = box.Solid.GetBoundingBox();
        Assert.Equal(0, min.X, precision: 3);     // -X yüzü sabit kaldı
        Assert.Equal(1500, max.X, precision: 3);  // +X yüzü yeni konumda
        Assert.Equal(500, max.Y, precision: 3);   // Y boyutu değişmedi
        Assert.Equal(300, max.Z, precision: 3);   // Z boyutu değişmedi
    }

    [Fact]
    public void MoveGripPointAt_MinusXFace_ResizesXOnly_KeepsOppositeFaceFixed()
    {
        var box = MakeAxisAlignedBox(1000, 500, 300);

        // -X yüzünü (indeks 0) X=0'dan X=-200'e sürükle (büyüt).
        box.MoveGripPointAt(0, new Vector3D(-200, 250, 150));

        var (min, max) = box.Solid.GetBoundingBox();
        Assert.Equal(-200, min.X, precision: 3);
        Assert.Equal(1000, max.X, precision: 3); // +X yüzü sabit kaldı
    }

    [Fact]
    public void MoveGripPointAt_AfterResize_SolidRemainsTopologicallyValid()
    {
        var box = MakeAxisAlignedBox(1000, 500, 300);
        box.MoveGripPointAt(3, new Vector3D(500, 900, 150)); // +Y yüzünü büyüt

        Assert.True(box.Solid.IsValid());
        Assert.Equal(8, box.Solid.GetVertices().Count());
        Assert.Equal(12, box.Solid.GetEdges().Count());
    }

    [Fact]
    public void MoveGripPointAt_DragPastOppositeFace_ClampsToMinimumSize()
    {
        var box = MakeAxisAlignedBox(1000, 500, 300);

        // +X yüzünü, -X yüzünün ÇOK ötesine (negatif) sürüklemeye çalış — dejenere/sıfır
        // hacimli kutu oluşmamalı, minimum 10mm boyutta kenetlenmeli.
        box.MoveGripPointAt(1, new Vector3D(-500, 250, 150));

        var (min, max) = box.Solid.GetBoundingBox();
        Assert.True(max.X - min.X >= 10.0 - 1e-6);
        Assert.True(box.Solid.IsValid());
    }

    [Fact]
    public void RotatedBox_HasNoGripPoints_SafeFallback()
    {
        // 45° döndürülmüş bir kutu — eksene hizalı DEĞİL, bu yüzden grip listesi boş kalmalı.
        var solid = BRepBuilder.ExtrudeBox(
            Vector3D.Zero,
            new Vector3D(1, 1, 0), new Vector3D(-1, 1, 0), Vector3D.ZAxis,
            1000, 500, 300, "RotatedBox");
        var box = new SolidEntity(solid);

        Assert.Empty(box.GetGripPoints());
    }

    [Fact]
    public void NonBoxSolid_HasNoGripPoints_SafeFallback()
    {
        // Üçgen kesitli bir ekstrüzyon (6 vertex, kutu değil) — grip listesi boş kalmalı.
        var profile = new[] { new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), new Vector3D(0, 1000, 0) };
        var solid = BRepBuilder.ExtrudePolygon(profile, new Vector3D(0, 0, 500), "Prism");
        var prism = new SolidEntity(solid);

        Assert.Empty(prism.GetGripPoints());
    }
}
