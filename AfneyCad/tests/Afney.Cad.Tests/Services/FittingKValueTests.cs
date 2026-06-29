using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

public class FittingKValueTests
{
    [Theory]
    [InlineData(FittingType.Elbow90, 20, 1.5)]
    [InlineData(FittingType.Elbow90, 150, 0.9)]
    [InlineData(FittingType.GateValveOpen, 50, 0.15)]
    [InlineData(FittingType.CheckValveSwing, 25, 2.5)]
    public void GetKValue_ReturnsCorrectRange(FittingType type, double dn, double expected)
    {
        var k = FittingKValueService.GetKValue(type, dn);
        Assert.InRange(k, expected * 0.5, expected * 1.5);
    }

    [Fact]
    public void CalculateLocalLoss_ReturnsPositive()
    {
        double loss = FittingKValueService.CalculateLocalLoss(FittingType.Elbow90, 50, 1.5);
        Assert.True(loss > 0);
    }

    [Fact]
    public void GetAllEntries_Returns26Types()
    {
        var entries = FittingKValueService.GetAllEntries();
        Assert.True(entries.Count >= 26);
    }
}
