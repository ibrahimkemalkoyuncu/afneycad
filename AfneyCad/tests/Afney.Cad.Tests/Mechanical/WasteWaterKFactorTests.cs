using System.Collections.Generic;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: WasteWaterDesignService K-Katsayı Testleri
   NEDEN — GERÇEK, ÖNCEDEN VAR OLAN BİR STANDART UYUMSUZLUĞU: Bir web araştırma ajanı,
          TS EN 12056-2 Tablo 3'ün gerçek K-faktörü kategorileriyle koddaki eşleştirmeyi
          karşılaştırdı. Bulgu: System_IV ("Özel") K=1.0 kullanıyordu ama standardın en üst
          katmanı (özel dikkat gerektiren kullanım — laboratuvar vb.) K=1.2'dir; bu değer
          kodda hiç kullanılmıyordu. Ayrıca System_I'in UI etiketi "Hastane" idi ama hastane
          gerçekte K=0.7 (sık kullanım) kategorisine girer. Bu testler düzeltilmiş
          K-değerlerini standarda göre doğruluyor.
*/
public class WasteWaterKFactorTests
{
    private static List<DrainageUnit> SingleUnit(double du) => new()
    {
        new DrainageUnit { FixtureName = "Test", DU = du, Count = 1 }
    };

    [Theory]
    [InlineData(WasteWaterDesignService.DesignMethod.System_II, 0.5)]   // Konut — seyrek kullanım
    [InlineData(WasteWaterDesignService.DesignMethod.System_III, 0.7)]  // Hastane/Okul/Otel — sık kullanım
    [InlineData(WasteWaterDesignService.DesignMethod.System_I, 1.0)]    // Umumi/yoğun kullanım
    [InlineData(WasteWaterDesignService.DesignMethod.System_IV, 1.2)]   // Özel dikkat gerektiren (TS EN 12056-2 Tablo 3'ün en üst katmanı)
    public void CalculateWasteWaterFlow_KFactor_MatchesEN12056Table3(
        WasteWaterDesignService.DesignMethod method, double expectedK)
    {
        var service = new WasteWaterDesignService(new CadDatabase());
        double totalDU = 16.0; // sqrt(16) = 4 — temiz bir sayı, K'yı doğrudan okumayı kolaylaştırır

        var result = service.CalculateWasteWaterFlow(SingleUnit(totalDU), method);

        Assert.Equal(expectedK, result.FrequencyFactor, precision: 6);
        Assert.Equal(expectedK * Math.Sqrt(totalDU), result.WasteWaterFlow, precision: 6);
    }

    [Fact]
    public void CalculateWasteWaterFlow_SpecialUseSystem_ProducesHighestFlowAmongAllCategories()
    {
        // K=1.2, standardın tanımladığı 4 kategori arasında en yüksek olmalı (özel dikkat
        // gerektiren kullanım en muhafazakar/en yüksek tepe debi varsayımını taşır).
        var service = new WasteWaterDesignService(new CadDatabase());
        var units = SingleUnit(16.0);

        var special = service.CalculateWasteWaterFlow(units, WasteWaterDesignService.DesignMethod.System_IV);
        var congested = service.CalculateWasteWaterFlow(units, WasteWaterDesignService.DesignMethod.System_I);
        var frequent = service.CalculateWasteWaterFlow(units, WasteWaterDesignService.DesignMethod.System_III);
        var dwelling = service.CalculateWasteWaterFlow(units, WasteWaterDesignService.DesignMethod.System_II);

        Assert.True(special.WasteWaterFlow > congested.WasteWaterFlow);
        Assert.True(congested.WasteWaterFlow > frequent.WasteWaterFlow);
        Assert.True(frequent.WasteWaterFlow > dwelling.WasteWaterFlow);
    }
}
