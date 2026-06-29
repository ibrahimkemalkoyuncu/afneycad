using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

public class HeatLoadTests
{
    [Fact]
    public void Calculate_BasicRoom_ReturnsPositiveLoad()
    {
        var svc = new HeatLoadCalculationService();
        var input = new HeatLoadInput
        {
            City = "Ankara",
            IndoorDesignTemp = 22,
            RoomVolume = 50,
            FloorArea = 20,
            Surfaces = new()
            {
                new BuildingSurface { Name = "Dış Duvar", Area = 15, UValue = 0.4 },
                new BuildingSurface { Name = "Pencere", Area = 4, UValue = 2.8 },
            }
        };

        var result = svc.Calculate(input);
        Assert.True(result.TotalHeatLoadKW > 0);
        Assert.True(result.TransmissionLossW > 0);
        Assert.True(result.VentilationLossW > 0);
        Assert.Equal("Ankara", result.City);
    }

    [Fact]
    public void OutdoorTemp_Ankara_ReturnsMinus12()
    {
        var svc = new HeatLoadCalculationService();
        Assert.Equal(-12, svc.GetOutdoorDesignTemp("Ankara"));
    }

    [Fact]
    public void OutdoorTemp_Erzurum_ReturnsMinus21()
    {
        var svc = new HeatLoadCalculationService();
        Assert.Equal(-21, svc.GetOutdoorDesignTemp("Erzurum"));
    }

    [Fact]
    public void PsychrometricState_20C50RH_ReturnsValid()
    {
        var state = PsychrometricService.CalculateState(20, 0.5);
        Assert.InRange(state.EnthalpyKJkg, 30, 50);
        Assert.InRange(state.HumidityRatio, 0.005, 0.01);
        Assert.InRange(state.DewPointC, 5, 15);
    }

    [Fact]
    public void EnergySimulation_Istanbul_ReturnsAnnualData()
    {
        var svc = new EnergySimulationService();
        var result = svc.Simulate(new EnergySimulationInput { City = "İstanbul", FloorAreaM2 = 100 });

        Assert.Equal(12, result.MonthlyData.Count);
        Assert.True(result.AnnualTotalKWh > 0);
        Assert.False(string.IsNullOrEmpty(result.EnergyClass));
    }
}
