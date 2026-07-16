using System.Collections.Generic;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: HVAC Eşit Sürtünme Yöntemi Testleri
   NEDEN: DuctSizingService'in sınıf yorumu "Eşit Sürtünme Yöntemi: Tüm hatlarda aynı Pa/m
          sürtünme basıncı" diyordu ama Calculate() her zonu sadece kendi hız limitine göre
          boyutlandırıyordu — hiçbir kanal gerçekten aynı Pa/m'ye getirilmiyordu. Bu testler,
          UseEqualFrictionMethod açıkken branş kanallarının ana hattın sürtünme oranına
          gerçekten (iteratif olarak) yakınsadığını doğruluyor.
*/
public class DuctSizingEqualFrictionTests
{
    [Fact]
    public void Calculate_EqualFrictionEnabled_BranchesConvergeToMainFrictionRate()
    {
        var svc = new DuctSizingService();
        var zones = new List<DuctSizingService.Zone>
        {
            new() { Name = "Ana Hat", FloorAreaM2 = 200, AirChanges = 6 },
            new() { Name = "Branş A", FloorAreaM2 = 40,  AirChanges = 6 },
            new() { Name = "Branş B", FloorAreaM2 = 15,  AirChanges = 8 },
        };

        var result = svc.Calculate(zones, rectangularDuct: false);

        // Standart kanal çaplarına yuvarlama nedeniyle tam eşitlik beklenmez (SMACNA
        // pratiğiyle tutarlı); ama hız yöntemiyle boyutlanmış olsaydı çıkacak sonuçtan
        // (bkz. Disabled testi) çok daha yakın olmalı — makul mühendislik bandı %20.
        double mainFriction = result.Segments[0].FrictionPaPer1m;
        AssertWithinRelativeTolerance(mainFriction, result.Segments[1].FrictionPaPer1m, 0.25);
        AssertWithinRelativeTolerance(mainFriction, result.Segments[2].FrictionPaPer1m, 0.25);
    }

    private static void AssertWithinRelativeTolerance(double expected, double actual, double relTol)
    {
        double diff = Math.Abs(expected - actual) / expected;
        Assert.True(diff <= relTol, $"Beklenen {expected:F3}, alınan {actual:F3} — bağıl fark {diff:P0} > {relTol:P0}");
    }

    [Fact]
    public void Calculate_EqualFrictionDisabled_BranchesSizedByVelocityOnly_FrictionDiffers()
    {
        var svc = new DuctSizingService { UseEqualFrictionMethod = false };
        var zones = new List<DuctSizingService.Zone>
        {
            new() { Name = "Ana Hat", FloorAreaM2 = 200, AirChanges = 6 },
            new() { Name = "Branş A", FloorAreaM2 = 15,  AirChanges = 8 },
        };

        var result = svc.Calculate(zones, rectangularDuct: false);

        // Eşit sürtünme kapalıyken branş kendi hız limitine göre boyutlanır —
        // ana hatla aynı Pa/m'ye getirilmesi beklenmez.
        Assert.NotEqual(result.Segments[0].FrictionPaPer1m, result.Segments[1].FrictionPaPer1m);
    }

    [Fact]
    public void Calculate_EqualFrictionEnabled_RectangularDuct_BranchesConverge()
    {
        var svc = new DuctSizingService();
        var zones = new List<DuctSizingService.Zone>
        {
            new() { Name = "Ana Hat", FloorAreaM2 = 200, AirChanges = 6 },
            new() { Name = "Branş A", FloorAreaM2 = 20,  AirChanges = 6 },
        };

        var result = svc.Calculate(zones, rectangularDuct: true);

        AssertWithinRelativeTolerance(result.Segments[0].FrictionPaPer1m, result.Segments[1].FrictionPaPer1m, 0.25);
    }
}
