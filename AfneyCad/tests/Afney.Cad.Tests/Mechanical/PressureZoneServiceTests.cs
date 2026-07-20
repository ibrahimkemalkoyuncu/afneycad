using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: PressureZoneService Testleri
   NEDEN: Çok Katlı Bina kategorisindeki servisler (PressureZoneService dahil) daha önce hiç
          test edilmiyordu. Bu testler, TS EN 806-3 / DIN 1988-300 statik basınç formülünün
          (P = ρ·g·h) ve bölge/PRV sayısı hesabının gerçekten doğru çalıştığını kanıtlar.
*/
public class PressureZoneServiceTests
{
    private const double G = 9.80665;
    private const double Rho = 1000.0;

    [Fact]
    public void Design_LowRiseBuilding_SingleZoneNoPrvNeeded()
    {
        // 10 kat x 3m = 30m. Max bölge yüksekliği (500 kPa limitle) ≈ 51m > 30m → tek bölge, PRV yok.
        var service = new PressureZoneService();
        var input = new PressureZoneService.ZoneDesignInput(
            TotalBuildingHeightM: 30, GroundSupplyPressureKPa: 400, NumberOfFloors: 10, FloorHeightM: 3.0);

        var result = service.Design(input);

        Assert.Equal(1, result.TotalZones);
        Assert.Equal(0, result.PrvCount);
        Assert.False(result.BoosterPumpRequired);
    }

    [Fact]
    public void Design_HighRiseBuilding_RequiresMultipleZonesAndPrv()
    {
        // 30 kat x 3m = 90m. Max bölge yüksekliği ≈ 51m → en az 2 bölge, en az 1 PRV gerekir.
        var service = new PressureZoneService();
        var input = new PressureZoneService.ZoneDesignInput(
            TotalBuildingHeightM: 90, GroundSupplyPressureKPa: 400, NumberOfFloors: 30, FloorHeightM: 3.0);

        var result = service.Design(input);

        Assert.True(result.TotalZones >= 2, $"90m bina için en az 2 bölge beklenir, bulunan: {result.TotalZones}");
        Assert.True(result.PrvCount >= 1);
        Assert.Equal(result.TotalZones - 1, result.PrvCount); // ilk bölge hariç her bölge 1 PRV gerektirir
    }

    [Fact]
    public void Design_GroundZoneBottomPressure_MatchesGroundSupplyPressure()
    {
        // Zemin kotunda (h=0) statik düşüş sıfırdır — ilk bölgenin alt basıncı şebeke basıncına eşit olmalı.
        var service = new PressureZoneService();
        var input = new PressureZoneService.ZoneDesignInput(
            TotalBuildingHeightM: 40, GroundSupplyPressureKPa: 450, NumberOfFloors: 13, FloorHeightM: 3.0);

        var result = service.Design(input);

        Assert.Equal(450, result.Zones[0].StaticPressureBottomKPa, precision: 6);
    }

    [Fact]
    public void Design_TallBuildingWithLowSupplyPressure_RequiresBoosterPump()
    {
        // 150m yükseklik, sadece 200 kPa şebeke basıncı → maxHeightFromSupply ≈ 20.4m << 150m.
        var service = new PressureZoneService();
        var input = new PressureZoneService.ZoneDesignInput(
            TotalBuildingHeightM: 150, GroundSupplyPressureKPa: 200, NumberOfFloors: 50, FloorHeightM: 3.0);

        var result = service.Design(input);

        Assert.True(result.BoosterPumpRequired);
        Assert.True(result.BoosterPumpHeadMSS > 0);
    }

    [Fact]
    public void Design_MaxZoneHeight_MatchesAnalyticalFormula()
    {
        // maxZoneHeightM = maxPressKPa / (ρ·g/1000) — bu formülün servis içinde doğru
        // uygulandığını, tek-bölgeli bir binanın üst sınırına yakın yükseklikte hâlâ tek
        // bölge kalması ama biraz üstünde ikinci bölgeye geçmesiyle dolaylı olarak doğrular.
        double maxZoneHeightM = 500.0 / (Rho * G / 1000.0); // ≈ 50.99 m

        var service = new PressureZoneService();

        var justUnder = service.Design(new PressureZoneService.ZoneDesignInput(
            TotalBuildingHeightM: maxZoneHeightM - 1, GroundSupplyPressureKPa: 400, NumberOfFloors: 16, FloorHeightM: 3.0));
        var justOver = service.Design(new PressureZoneService.ZoneDesignInput(
            TotalBuildingHeightM: maxZoneHeightM + 5, GroundSupplyPressureKPa: 400, NumberOfFloors: 18, FloorHeightM: 3.0));

        Assert.Equal(1, justUnder.TotalZones);
        Assert.True(justOver.TotalZones >= 2);
    }
}
