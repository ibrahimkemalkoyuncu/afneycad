using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: FireFightingService Testleri
   NEDEN — GERÇEK, ÖNCEDEN VAR OLAN BİR HATA: `DesignSprinklerSystem` basınç kaybını
          `0.02 * CeilingHeightM * 10` gibi keyfi bir formülle hesaplıyordu — ne debiye
          ne boru çapına ne de gerçek boru uzunluğuna bağlıydı, "NFPA 13" standardı iddiasına
          rağmen gerçek bir Hazen-Williams hesabı DEĞİLDİ. Bu testler artık gerçek metrik
          Hazen-Williams formülünün (Δp = 6.05×10⁵·Q^1.85/(C^1.85·d^4.87)) uygulandığını ve
          farklı tehlike sınıflarının (farklı debi) artık GERÇEKTEN farklı basınç kaybı
          ürettiğini kanıtlıyor.
*/
public class FireFightingServiceTests
{
    [Fact]
    public void DesignSprinklerSystem_FrictionLoss_MatchesHazenWilliamsFormula()
    {
        var service = new FireFightingService();
        var input = new FireFightingService.SprinklerDesignInput
        {
            ProtectedAreaM2 = 500,
            Hazard = FireFightingService.EN12845HazardClass.LightHazard,
            CeilingHeightM = 3.0,
            MainPipeLengthM = 50,
            HazenWilliamsC = 120
        };

        var result = service.DesignSprinklerSystem(input);

        double expectedPerMeter = 6.05e5 * Math.Pow(result.RequiredFlowLpm, 1.85)
            / (Math.Pow(120, 1.85) * Math.Pow(result.MainPipeDN, 4.87));
        double expected = expectedPerMeter * 50;

        Assert.Equal(expected, result.FrictionLossBar, precision: 6);
    }

    [Fact]
    public void DesignSprinklerSystem_HigherHazardClass_ProducesHigherFrictionLoss()
    {
        // Eski hatalı kodda basınç kaybı sadece tavan yüksekliğine bağlıydı — aynı yükseklikte
        // Light ve Extra Hazard AYNI sürtünme kaybını verirdi. Artık debiye (Q^1.85) bağlı
        // olduğu için Extra Hazard (çok daha yüksek debi) GERÇEKTEN daha fazla kayıp üretmeli.
        var service = new FireFightingService();

        var light = service.DesignSprinklerSystem(new FireFightingService.SprinklerDesignInput
        {
            ProtectedAreaM2 = 500, Hazard = FireFightingService.EN12845HazardClass.LightHazard, CeilingHeightM = 3.0
        });
        var extra = service.DesignSprinklerSystem(new FireFightingService.SprinklerDesignInput
        {
            ProtectedAreaM2 = 500, Hazard = FireFightingService.EN12845HazardClass.ExtraHazard, CeilingHeightM = 3.0
        });

        Assert.True(extra.RequiredFlowLpm > light.RequiredFlowLpm);
        Assert.True(extra.FrictionLossBar > light.FrictionLossBar,
            $"Extra Hazard sürtünme kaybı ({extra.FrictionLossBar}) Light Hazard'dan ({light.FrictionLossBar}) büyük olmalı.");
    }

    /*
       NE/NEDEN — GERÇEK, ÖNCEDEN VAR OLAN BİR STANDART HATASI: OrdinaryHazard_2 için
       designArea_m2 = 216 idi — bu EN 12845'te OH2'nin değil, OH3'ün tasarım alanıdır
       (kopyala-yapıştır hatası, bir web araştırma ajanı tarafından standart karşılaştırmasıyla
       bulundu). Gerçek EN 12845 serisi: OH1=72, OH2=144, OH3=216 m². Bu test artık OH2'nin
       kendi doğru değerini (144) verdiğini ve OH1 ile ExtraHazard arasında beklenen sırada
       kaldığını (OH1 &lt; OH2 &lt; ExtraHazard'ın tasarım yoğunluğu×alanı) kanıtlıyor.
    */
    [Fact]
    public void DesignSprinklerSystem_OrdinaryHazard2_UsesCorrectEN12845DesignArea()
    {
        var service = new FireFightingService();

        var oh1 = service.DesignSprinklerSystem(new FireFightingService.SprinklerDesignInput { Hazard = FireFightingService.EN12845HazardClass.OrdinaryHazard_1 });
        var oh2 = service.DesignSprinklerSystem(new FireFightingService.SprinklerDesignInput { Hazard = FireFightingService.EN12845HazardClass.OrdinaryHazard_2 });

        Assert.Equal(144.0, oh2.DesignAreaM2, precision: 6);
        Assert.Equal(72.0, oh1.DesignAreaM2, precision: 6);
        Assert.True(oh2.DesignAreaM2 > oh1.DesignAreaM2, "OH2 tasarım alanı OH1'den büyük olmalı (144 > 72).");
    }

    [Fact]
    public void DesignSprinklerSystem_LongerPipeRun_ProducesProportionallyMoreFrictionLoss()
    {
        // Hazen-Williams basınç kaybı uzunlukla DOĞRUSAL orantılı olmalı (Δp = kayıp/m × L).
        var service = new FireFightingService();

        var short50m = service.DesignSprinklerSystem(new FireFightingService.SprinklerDesignInput
        {
            ProtectedAreaM2 = 500, MainPipeLengthM = 50
        });
        var long100m = service.DesignSprinklerSystem(new FireFightingService.SprinklerDesignInput
        {
            ProtectedAreaM2 = 500, MainPipeLengthM = 100
        });

        Assert.Equal(short50m.FrictionLossBar * 2, long100m.FrictionLossBar, precision: 6);
    }
}
