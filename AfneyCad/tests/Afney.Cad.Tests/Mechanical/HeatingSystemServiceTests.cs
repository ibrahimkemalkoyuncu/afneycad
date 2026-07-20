using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: HeatingSystemService (TS 825 / TS EN 12831 ısıtma hesabı) Testleri
   NEDEN: Servis hiç test edilmemişti. Bu testler; iletim kaybı (U×A×ΔT), havalandırma
          kaybı (0.34×n×V×ΔT), %20 güvenlik payı, kazan kapasitesi seçimi, boru DN seçimi
          ve radyatör (Delta50→Delta30 dönüşümü) seçim mantığının elle hesaplanmış
          değerlerle örtüştüğünü doğruluyor.
*/
public class HeatingSystemServiceTests
{
    private static HeatingSystemService.Room MakeRoom(
        double areaM2, double extWallM2, double windowM2, double designTempC = 20.0)
    {
        return new HeatingSystemService.Room
        {
            Name = "Test Odası",
            FloorAreaM2 = areaM2,
            HeightM = 2.8,
            DesignTempC = designTempC,
            ExternalWallM2 = extWallM2,
            WindowM2 = windowM2,
            ExternalRoofM2 = 0,
            PartitionWallM2 = 0,
            // Varsayılan U değerleri (0.6 / 2.4 / 0.4 / 0.8) ve %50 hava değişimi kullanılıyor.
        };
    }

    [Fact]
    public void Calculate_SingleRoom_TransmissionAndVentilationLossMatchHandCalculation()
    {
        var svc = new HeatingSystemService(); // OutdoorDesignTempC=-12, Safety=1.20 varsayılan
        var room = MakeRoom(areaM2: 10, extWallM2: 4, windowM2: 1, designTempC: 20.0);

        var result = svc.Calculate(new List<HeatingSystemService.Room> { room });
        var r = result.Rooms.Single();

        // ΔT = 20 - (-12) = 32
        // qTrans = 4*0.6*32 + 1*2.4*32 + 0*0.4*32 + 10*0.8*32 + 0 = 76.8+76.8+0+256 = 409.6
        // qVent  = 0.34 * 0.5 * (10*2.8) * 32 = 0.34*0.5*28*32 = 152.32
        Assert.Equal(409.6, r.TransmissionLossW, precision: 1);
        Assert.Equal(152.32, r.VentilationLossW, precision: 1);

        // qTotal = (409.6+152.32) * 1.20 = 674.304
        Assert.Equal(674.304, r.TotalHeatLossW, precision: 1);
        Assert.Equal(674.304 / 1000.0, r.TotalHeatLossKw, precision: 3);
    }

    [Fact]
    public void Calculate_BoilerCapacity_Is20PercentAboveTotalHeatLoad()
    {
        var svc = new HeatingSystemService();
        var rooms = new List<HeatingSystemService.Room>
        {
            MakeRoom(20, 10, 2, 22.0),
            MakeRoom(15, 8, 1.5, 20.0),
        };

        var result = svc.Calculate(rooms);

        double expectedBoiler = result.TotalHeatKw * 1.20;
        Assert.Equal(expectedBoiler, result.BoilerCapacityKw, precision: 1);
        Assert.True(result.BoilerCapacityKw > result.TotalHeatKw);
    }

    [Theory]
    [InlineData(20.0, "24 kW")]   // <=24 kW
    [InlineData(30.0, "32 kW")]   // <=32 kW
    [InlineData(45.0, "48 kW")]   // <=48 kW
    public void Calculate_BoilerSelection_PicksCorrectCapacityTier(double approxTotalKw, string expectedFragment)
    {
        // Kazan kapasitesi TotalHeatKw*1.20 üzerinden seçildiği için, hedef kW'a ~%20 payla ulaşacak
        // tek büyük oda kullanıyoruz (küçük yuvarlama sapmaları toleransla karşılanır).
        // qTotal(W) = (ExtWall*0.6 + Window*2.4 + Floor*0.8) * ΔT * 1.20  — ΔT = 20-(-12) = 32
        // Basitlik için tek dış duvar alanı ile hedef W'ye yaklaşıyoruz.
        double targetBoilerW = approxTotalKw * 1000.0; // BoilerCapacityKw hedefi
        double targetTotalW = targetBoilerW / 1.20;      // TotalHeatLossW (odalar toplamı, safety dahil olduğundan basitleştiriyoruz)

        // qTotal = extWall*0.6*32*1.20 (yaklaşık) → extWall = qTotal/(0.6*32*1.20)
        double extWall = targetTotalW / (0.6 * 32 * 1.20);
        var svc = new HeatingSystemService();
        var room = MakeRoom(areaM2: 1, extWallM2: extWall, windowM2: 0, designTempC: 20.0);
        room.UFloor = 0; // Sadece dış duvar kaybını izole ediyoruz

        var result = svc.Calculate(new List<HeatingSystemService.Room> { room });

        Assert.Contains(expectedFragment, result.RecommendedBoiler);
    }

    [Fact]
    public void Calculate_VeryLowHeatLoss_AddsWarning()
    {
        var svc = new HeatingSystemService();
        // Çok küçük oda, çok az dış yüzey → 200W altı ısı ihtiyacı
        var room = MakeRoom(areaM2: 1, extWallM2: 0.2, windowM2: 0, designTempC: 20.0);
        room.UFloor = 0; // Zemin kaybını da minimize et

        var result = svc.Calculate(new List<HeatingSystemService.Room> { room });

        Assert.True(result.Rooms.Single().TotalHeatLossW < 200);
        Assert.True(result.WarningCount >= 1);
        Assert.Contains(result.Warnings, w => w.Contains("çok düşük"));
    }

    [Theory]
    [InlineData(0.02, 10)]   // < 0.03 m3/h -> DN10
    [InlineData(0.05, 12)]   // < 0.06 -> DN12
    [InlineData(0.10, 15)]   // < 0.12 -> DN15
    [InlineData(0.20, 18)]   // < 0.25 -> DN18
    public void SelectPipeDN_ThresholdsMatchCatalog_ViaRoomFlowRate(double desiredFlowM3h, double expectedDN)
    {
        // RequiredFlowM3h = qTotal / (4186*998*ΔTSystem) * 3600  → qTotal'ı istenen debiye göre geriye çözüyoruz.
        var svc = new HeatingSystemService(); // SupplyTemp=80, ReturnTemp=60 → ΔTSystem=20
        double deltaTSystem = svc.SupplyTempC - svc.ReturnTempC;
        double qTotalTarget = desiredFlowM3h * (4186 * 998 * deltaTSystem) / 3600.0;

        // qTotal = (extWall*0.6*ΔT)*1.20  → ΔT = DesignTempC - OutdoorDesignTempC = 20-(-12) = 32
        double extWall = qTotalTarget / (1.20 * 0.6 * 32);
        var room = MakeRoom(areaM2: 1, extWallM2: extWall, windowM2: 0, designTempC: 20.0);
        room.UFloor = 0;

        var result = svc.Calculate(new List<HeatingSystemService.Room> { room });
        var r = result.Rooms.Single();

        Assert.Equal(desiredFlowM3h, r.RequiredFlowM3h, precision: 2);
        Assert.Equal(expectedDN, r.RecommendedDN);
    }

    [Fact]
    public void Calculate_ExtremeHeatLoss_FallsBackToLargestCatalogRadiator()
    {
        // Katalogdaki en yüksek çıktı 1575 W (Delta50) → 60/40°C'de 1575*0.69 ≈ 1086.75 W.
        // Bu kapasiteyi aşan bir ısı ihtiyacı, "en büyük radyatörü seç" fallback'ini tetiklemeli.
        var svc = new HeatingSystemService();
        var room = MakeRoom(areaM2: 40, extWallM2: 60, windowM2: 20, designTempC: 24.0);

        var result = svc.Calculate(new List<HeatingSystemService.Room> { room });
        var r = result.Rooms.Single();

        Assert.True(r.TotalHeatLossW > 1575 * 0.69); // katalogdaki en yüksek çıktıyı (Delta50) bile aşan talep
        Assert.NotNull(r.Radiator);
        Assert.Equal(1000, r.Radiator!.Width);
        Assert.Equal(900, r.Radiator.Height);
        Assert.Equal("Panel 33", r.Radiator.Type);
    }

    [Fact]
    public void Calculate_ModerateHeatLoss_SelectsSmallestSufficientRadiator()
    {
        var svc = new HeatingSystemService();
        var room = MakeRoom(areaM2: 10, extWallM2: 4, windowM2: 1, designTempC: 20.0);

        var result = svc.Calculate(new List<HeatingSystemService.Room> { room });
        var r = result.Rooms.Single();

        Assert.NotNull(r.Radiator);
        // Seçilen radyatörün gerçek çıktısı (60/40°C) oda ısı ihtiyacını karşılamalı.
        Assert.True(r.Radiator!.OutputW >= r.TotalHeatLossW);
    }
}
