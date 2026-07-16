using System.Collections.Generic;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Doğalgaz Hesabı Colebrook/Reynolds Testleri
   NEDEN: GasCalcSheetService'de sürtünme faktörü (λ) önceden sadece çaptan türetilen kaba
          bir sabit yaklaşımdı — Reynolds sayısı hiç hesaplanmıyor, laminer/türbülanslı ayrımı
          yapılmıyordu. Artık gerçek Colebrook-White iterasyonu (türbülanslı) ve λ=64/Re
          (laminer) kullanılıyor. Bu testler, basınç düşümünün akış debisine göre fiziksel
          olarak tutarlı (debi arttıkça ΔP artan) davrandığını ve viskozite parametresinin
          gerçekten sonucu etkilediğini doğruluyor.
*/
public class GasCalcColebrookTests
{
    private static GasCalcSheetService.CalcSheetResult RunSingleSegment(double powerKw, double viscosity = 1.3e-5)
    {
        var svc = new GasCalcSheetService();
        var devices = new List<GasCalcSheetService.GasDevice>
        {
            new() { Name = "Kombi", NominalPowerKw = powerKw }
        };
        var segments = new List<(string, double, double[])> { ("Segment 1", 10.0, new double[] { 0 }) };
        var opts = new GasCalcSheetService.CalcOptions { GasKinematicViscosityM2s = viscosity };

        return svc.Calculate(devices, segments, opts);
    }

    [Fact]
    public void Calculate_HigherFlow_ProducesHigherPressureDrop()
    {
        var low = RunSingleSegment(10.0);
        var high = RunSingleSegment(40.0);

        Assert.True(high.Rows[0].PressureDropMbar > low.Rows[0].PressureDropMbar);
    }

    [Fact]
    public void Calculate_DifferentViscosity_ChangesPressureDrop()
    {
        var baseline = RunSingleSegment(25.0, viscosity: 1.3e-5);
        var higherViscosity = RunSingleSegment(25.0, viscosity: 3.0e-5);

        Assert.NotEqual(baseline.Rows[0].PressureDropMbar, higherViscosity.Rows[0].PressureDropMbar);
    }

    [Fact]
    public void Calculate_TypicalResidentialLoad_ProducesPhysicallyReasonableResult()
    {
        var result = RunSingleSegment(20.0);
        var row = result.Rows[0];

        Assert.True(row.VelocityMs > 0 && row.VelocityMs < 20);
        Assert.True(row.PressureDropMbar > 0 && row.PressureDropMbar < 5);
    }
}
