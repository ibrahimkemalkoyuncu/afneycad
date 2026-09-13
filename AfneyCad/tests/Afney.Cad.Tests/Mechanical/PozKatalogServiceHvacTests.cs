using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: PozKatalogService GRUP 30 (Havalandırma) Testleri
   NEDEN — Session #75 iş akışı denetiminde bulunan boşluk (madde 04/68): Poz kataloğu sadece
          tesisat/mimariyi kapsıyordu, HVAC (kanal/terminal/damper) hiç fiyatlandırılamıyordu.
          Bu testler yeni FindForDuct/FindForAirTerminal/FindForDamper metodlarının GRUP 30'dan
          gerçek, sıfırdan farklı bir birim fiyat döndürdüğünü ve şekle göre (dikdörtgen/dairesel)
          doğru poz kalemini seçtiğini kilitler.
*/
public class PozKatalogServiceHvacTests
{
    [Fact]
    public void FindForDuct_Rectangular_ReturnsGrup30Item()
    {
        var svc = new PozKatalogService();
        var poz = svc.FindForDuct(DuctShape.Rectangular);

        Assert.NotNull(poz);
        Assert.StartsWith("30.", poz!.PozNo);
        Assert.Equal("m2", poz.Birim);
        Assert.True(poz.BirimFiyat > 0);
    }

    [Fact]
    public void FindForDuct_Circular_ReturnsDifferentItemThanRectangular()
    {
        var svc = new PozKatalogService();
        var rect = svc.FindForDuct(DuctShape.Rectangular);
        var circ = svc.FindForDuct(DuctShape.Circular);

        Assert.NotEqual(rect!.PozNo, circ!.PozNo);
    }

    [Fact]
    public void FindForAirTerminal_ReturnsPricedItemInAdetUnit()
    {
        var svc = new PozKatalogService();
        var poz = svc.FindForAirTerminal();

        Assert.NotNull(poz);
        Assert.Equal("adet", poz!.Birim);
        Assert.True(poz.BirimFiyat > 0);
    }

    [Fact]
    public void FindForDamper_ReturnsPricedItemInAdetUnit()
    {
        var svc = new PozKatalogService();
        var poz = svc.FindForDamper();

        Assert.NotNull(poz);
        Assert.Equal("adet", poz!.Birim);
        Assert.True(poz.BirimFiyat > 0);
    }
}
