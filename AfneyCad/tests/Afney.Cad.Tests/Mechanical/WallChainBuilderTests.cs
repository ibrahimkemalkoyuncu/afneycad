using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: WallChainBuilder Testleri — Kendi Kendini Kesme (Self-Intersection) Koruması
   NEDEN: Kullanıcı gerçek bir "Manuel Mahal" testinde yakaladı — karmaşık (teras/çatı katı)
          bir odada eksik/yanlış duvar seçimi, greedy zincirlemenin odayı ÇAPRAZLAYAN bir kenar
          üretmesine yol açtı ("bowtie" poligon). Shoelace formülü bunu sessizce çok küçük/yanlış
          bir alana (5,55 m², görsel olarak çok daha büyük bir sınır için) hesaplıyordu. Bu
          testler, self-intersection tespitinin GERÇEKTEN çapraz poligonları reddettiğini VE
          normal (dışbükey olmayan L-şekli dahil) geçerli odaları YANLIŞLIKLA reddetmediğini
          kanıtlar.
*/
public class WallChainBuilderTests
{
    [Fact]
    public void Build_SimpleRectangle_ReturnsCorrectClosedPolygon()
    {
        var builder = new WallChainBuilder();
        var segments = new List<(Vector3D P1, Vector3D P2)>
        {
            (new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0)),
            (new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0)),
            (new Vector3D(4000, 3000, 0), new Vector3D(0, 3000, 0)),
            (new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0)),
        };

        var chain = builder.Build(segments, out string status);

        Assert.NotNull(chain);
        Assert.Equal(4, chain!.Count);
        Assert.Contains("oluşturuldu", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_NonConvexLShapedRoom_IsNotFalselyRejectedAsSelfIntersecting()
    {
        // Klasik L-şekli — dışbükey değil ama KENDİ KENDİNİ KESMİYOR (geçerli basit poligon).
        var builder = new WallChainBuilder();
        var segments = new List<(Vector3D P1, Vector3D P2)>
        {
            (new Vector3D(0, 0, 0), new Vector3D(6000, 0, 0)),
            (new Vector3D(6000, 0, 0), new Vector3D(6000, 3000, 0)),
            (new Vector3D(6000, 3000, 0), new Vector3D(3000, 3000, 0)),
            (new Vector3D(3000, 3000, 0), new Vector3D(3000, 6000, 0)),
            (new Vector3D(3000, 6000, 0), new Vector3D(0, 6000, 0)),
            (new Vector3D(0, 6000, 0), new Vector3D(0, 0, 0)),
        };

        var chain = builder.Build(segments, out string status);

        Assert.NotNull(chain);
        Assert.Equal(6, chain!.Count);
    }

    [Fact]
    public void Build_SelfIntersectingBowtieChain_IsRejectedWithClearError()
    {
        // Eksik/yanlış duvar seçimi senaryosu: 4 köşe A(0,0),B(4000,3000),C(4000,0),D(0,3000)
        // A→B→C→D→A sırasıyla zincirlendiğinde KLASİK bir "bowtie" (kelebek/çapraz) poligon
        // oluşur — A-B kenarı C-D kenarını ortada keser. Gerçek bir odada bu, kullanıcının
        // odanın tüm duvarlarını seçmediği (zincirin yanlış bir uzak noktaya "atladığı")
        // durumu simüle ediyor.
        var builder = new WallChainBuilder();
        var a = new Vector3D(0, 0, 0);
        var b = new Vector3D(4000, 3000, 0);
        var c = new Vector3D(4000, 0, 0);
        var d = new Vector3D(0, 3000, 0);
        var segments = new List<(Vector3D P1, Vector3D P2)>
        {
            (a, b),
            (b, c),
            (c, d),
            (d, a),
        };

        var chain = builder.Build(segments, out string status);

        Assert.Null(chain);
        Assert.Contains("kesen", status, StringComparison.OrdinalIgnoreCase);
    }
}
