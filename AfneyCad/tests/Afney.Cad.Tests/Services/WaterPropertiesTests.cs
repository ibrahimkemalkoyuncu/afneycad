using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

public class WaterPropertiesTests
{
    [Fact]
    public void Viscosity_At20C_Returns1004e6()
    {
        var nu = WaterPropertiesService.GetKinematicViscosity(20);
        Assert.InRange(nu, 1.0e-6, 1.01e-6);
    }

    [Theory]
    [InlineData(4, 1.5e-6, 1.6e-6)]
    [InlineData(60, 4.5e-7, 5.0e-7)]
    [InlineData(90, 3.0e-7, 3.5e-7)]
    public void Viscosity_TemperatureDependent(double tempC, double min, double max)
    {
        var nu = WaterPropertiesService.GetKinematicViscosity(tempC);
        Assert.InRange(nu, min, max);
    }

    [Fact]
    public void Density_At20C_ReturnsAround998()
    {
        var rho = WaterPropertiesService.GetDensity(20);
        Assert.InRange(rho, 995, 1001);
    }

    [Fact]
    public void SpecificHeat_ReturnsAround4200()
    {
        var cp = WaterPropertiesService.GetSpecificHeat(20);
        Assert.InRange(cp, 4100, 4300);
    }

    [Fact]
    public void ThermalConductivity_ReturnsPositive()
    {
        var k = WaterPropertiesService.GetThermalConductivity(50);
        Assert.True(k > 0.5 && k < 0.7);
    }
}
