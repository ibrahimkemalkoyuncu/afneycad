using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Fosseptik Hesap Birleştirme Testleri
   NEDEN: SepticTankService (TS 2873, SepticTankDialog) ve WasteWaterCalcSheetService'in kendi
          fosseptik hesabı (TS 8358, hesap föyü dialogu) önceden birbirinden bağımsız iki farklı
          formülle çalışıyordu — aynı kişi sayısı/su tüketimi için FARKLI hacim/boyut sonuçları
          üretiyorlardı. Artık WasteWaterCalcSheetService.CalculateSepticTank tek motor olan
          SepticTankService'e delege ediyor; bu test ikisinin aynı girdilerle aynı hacmi
          üretmesini doğruluyor.
*/
public class SepticTankConsolidationTests
{
    [Fact]
    public void WasteWaterCalcSheetService_DelegatesToSepticTankService_SameTotalVolume()
    {
        var directEngine = new SepticTankService();
        var directResult = directEngine.CalculateSepticTank(new SepticTankService.SepticTankInput
        {
            PersonCount = 20,
            UnitWaterConsumption = 150.0,
            RetentionTime = 3.0,
            SludgeMarginRatio = 0.5, // WasteWater dialog varsayılanı: SludgeFactor=1.5 -> ratio=0.5
            Type = SepticTankService.TankType.DoubleChamber
        });

        var calcSheetSvc = new WasteWaterCalcSheetService();
        var calcSheetResult = calcSheetSvc.CalculateSepticTank(new WasteWaterCalcSheetService.SepticTankInput
        {
            PersonCount = 20,
            DailyWaterLiters = 150.0,
            RetentionDays = 3.0,
            SludgeFactor = 1.5,
            TankType = "Foseptik"
        });

        Assert.Equal(directResult.TotalVolume, calcSheetResult.TotalVolumeM3, precision: 1);
        Assert.Equal(directResult.Width, calcSheetResult.RecommendedWidthM, precision: 1);
        Assert.Equal(directResult.Length, calcSheetResult.RecommendedLengthM, precision: 1);
    }

    [Fact]
    public void SepticTankService_SludgeMarginRatio_IsConfigurable()
    {
        var svc = new SepticTankService();
        var withDefault = svc.CalculateSepticTank(new SepticTankService.SepticTankInput { PersonCount = 50 });
        var withHigherMargin = svc.CalculateSepticTank(new SepticTankService.SepticTankInput
        {
            PersonCount = 50,
            SludgeMarginRatio = 0.6
        });

        Assert.True(withHigherMargin.TotalVolume > withDefault.TotalVolume);
    }
}
