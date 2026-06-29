using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

public class AdvancedHydraulicsTests
{
    [Fact]
    public void ColebrookWhite_Laminar_Returns64OverRe()
    {
        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(1000, 0.007, 20);
        Assert.InRange(f, 0.063, 0.065); // 64/1000 = 0.064
    }

    [Fact]
    public void ColebrookWhite_Turbulent_ReturnsReasonableValue()
    {
        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(50000, 0.045, 50);
        Assert.InRange(f, 0.01, 0.05);
    }

    [Fact]
    public void PartialFlow_HalfFull_ReturnsExpectedRatio()
    {
        var result = AdvancedHydraulicsService.CalculatePartialFlow(1.0, 100, 2.0);
        Assert.True(result.FillingRatio > 0 && result.FillingRatio < 1);
        Assert.True(result.ActualVelocity > 0);
    }

    [Fact]
    public void PartialFlow_OverCapacity_FlagsViolation()
    {
        var result = AdvancedHydraulicsService.CalculatePartialFlow(100.0, 50, 1.0);
        Assert.True(result.IsOverCapacity);
    }

    [Fact]
    public void WaterHammer_ReturnsPositivePressure()
    {
        var pipe = new Afney.Cad.Mechanical.Entities.PipeEntity(
            new Afney.Cad.Geometry.Primitives.Vector3D(0, 0, 0),
            new Afney.Cad.Geometry.Primitives.Vector3D(10000, 0, 0), 50)
        {
            FlowRate = 3.6,
            PipeMaterialType = Afney.Cad.Mechanical.Enums.PipeMaterial.PPRC_PN20
        };
        pipe.Velocity = pipe.GetVelocity();

        var result = AdvancedHydraulicsService.CalculateWaterHammer(pipe);
        Assert.True(result.PressureSurgebar >= 0);
        Assert.True(result.WaveSpeedMs > 100);
    }

    [Fact]
    public void FixtureUnitTable_Lavabo_Returns05()
    {
        var entry = FixtureUnitTable.GetEntry("Lavabo");
        Assert.NotNull(entry);
        Assert.Equal(0.5, entry!.LoadUnits);
    }

    [Fact]
    public void FixtureUnitTable_WC_Returns25()
    {
        var entry = FixtureUnitTable.GetEntry("WC (Rezervuar)");
        Assert.NotNull(entry);
        Assert.Equal(2.5, entry!.LoadUnits);
    }

    [Fact]
    public void FixtureUnitTable_FuzzyMatch_Works()
    {
        var entry = FixtureUnitTable.GetEntry("Shower");
        Assert.NotNull(entry);
    }
}
