using System.Linq;
using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;
using static Afney.Cad.Mechanical.Services.AirFilterSelectionService;

namespace Afney.Cad.Tests.Mechanical;

public class FlexConnectorServiceTests
{
    [Theory]
    [InlineData(4000, false, true)]
    [InlineData(4300, false, false)]
    [InlineData(1500, true, true)]
    [InlineData(1600, true, false)]
    [InlineData(0, false, false)]
    public void CheckLength_AppliesImcAndStrictLimits(double lengthMm, bool strict, bool expected)
    {
        Assert.Equal(expected, FlexConnectorService.CheckLength(lengthMm, strict).IsCompliant);
    }

    [Fact]
    public void ImcLimit_Is14Feet()
    {
        Assert.Equal(14 * 304.8, FlexConnectorService.ImcMaxConnectorLengthMm, precision: 1);
    }

    [Fact]
    public void PressureDrop_IncreasesWithFlow_AndIsZeroForInvalidInput()
    {
        double low = FlexConnectorService.EstimatePressureDropPa(200, 1000, 300);
        double high = FlexConnectorService.EstimatePressureDropPa(200, 1000, 600);

        Assert.True(low > 0);
        Assert.True(high > low * 3, "Dinamik basınçla orantılı olarak ~4 kat artmalı.");
        Assert.Equal(0, FlexConnectorService.EstimatePressureDropPa(0, 1000, 300));
    }
}

public class AirFilterSelectionServiceTests
{
    // Eurovent Rec. 4/23 (2022) Tablo 3: satır ODA1..3, sütun SUP1..5.
    [Theory]
    [InlineData(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup1, PmFraction.ePM1, 70)]
    [InlineData(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup2, PmFraction.ePM1, 50)]
    [InlineData(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup3, PmFraction.ePM2_5, 50)]
    [InlineData(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup4, PmFraction.ePM10, 50)]
    [InlineData(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup5, PmFraction.ePM10, 50)]
    [InlineData(OutdoorAirCategory.Oda2, SupplyAirCategory.Sup1, PmFraction.ePM1, 80)]
    [InlineData(OutdoorAirCategory.Oda2, SupplyAirCategory.Sup2, PmFraction.ePM1, 70)]
    [InlineData(OutdoorAirCategory.Oda2, SupplyAirCategory.Sup3, PmFraction.ePM2_5, 70)]
    [InlineData(OutdoorAirCategory.Oda2, SupplyAirCategory.Sup4, PmFraction.ePM10, 80)]
    [InlineData(OutdoorAirCategory.Oda2, SupplyAirCategory.Sup5, PmFraction.ePM10, 50)]
    [InlineData(OutdoorAirCategory.Oda3, SupplyAirCategory.Sup1, PmFraction.ePM1, 90)]
    [InlineData(OutdoorAirCategory.Oda3, SupplyAirCategory.Sup2, PmFraction.ePM1, 80)]
    [InlineData(OutdoorAirCategory.Oda3, SupplyAirCategory.Sup3, PmFraction.ePM2_5, 80)]
    [InlineData(OutdoorAirCategory.Oda3, SupplyAirCategory.Sup4, PmFraction.ePM10, 90)]
    [InlineData(OutdoorAirCategory.Oda3, SupplyAirCategory.Sup5, PmFraction.ePM10, 80)]
    public void RecommendedMinimum_MatchesEuroventTable3(OutdoorAirCategory oda, SupplyAirCategory sup, PmFraction fraction, int pct)
    {
        var r = GetRecommendedMinimum(oda, sup);
        Assert.Equal(fraction, r.Fraction);
        Assert.Equal(pct, r.MinEfficiencyPct);
    }

    [Fact]
    public void FinalStageFootnote_AppliesOnlyToSup1To3At50Percent()
    {
        Assert.True(GetRecommendedMinimum(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup2).FinalStageOnly);
        Assert.True(GetRecommendedMinimum(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup3).FinalStageOnly);
        Assert.False(GetRecommendedMinimum(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup1).FinalStageOnly);
        Assert.False(GetRecommendedMinimum(OutdoorAirCategory.Oda1, SupplyAirCategory.Sup4).FinalStageOnly);
    }

    [Fact]
    public void CumulativeEfficiency_UsesEuroventFormula()
    {
        Assert.Equal(88.0, CumulativeEfficiencyPct(new double[] { 60, 70 }), precision: 6);
        Assert.Equal(50.0, CumulativeEfficiencyPct(new double[] { 50 }), precision: 6);
        Assert.Equal(0.0, CumulativeEfficiencyPct(System.Array.Empty<double>()), precision: 6);
    }

    [Fact]
    public void MeetsRequirement_ComparesAgainstMinimum()
    {
        var req = GetRecommendedMinimum(OutdoorAirCategory.Oda2, SupplyAirCategory.Sup2); // ePM1 70%
        Assert.True(MeetsRequirement(req, CumulativeEfficiencyPct(new double[] { 60, 70 })));
        Assert.False(MeetsRequirement(req, 65));
    }

    [Fact]
    public void FaceAreaAndFanPower_FollowPhysics()
    {
        Assert.Equal(0.4, RequiredFaceAreaM2(3600, 2.5), precision: 6);
        Assert.Equal(2.5, FaceVelocityMs(3600, 0.4), precision: 6);
        Assert.Equal(100.0 / 0.6, FanPowerForPressureDropW(3600, 100, 0.6), precision: 6);
    }
}

public class AirCoilSelectionServiceTests
{
    [Fact]
    public void Heating_CapacityAndWaterFlow_FollowEnergyBalance()
    {
        var r = AirCoilSelectionService.SelectHeating(3600, 5, 30, 80, 60, targetFaceVelocityMs: 3.0);

        Assert.True(r.IsValid);
        Assert.Equal(1.2 * 1.0 * 1.006 * 25, r.CapacityKw, precision: 6);
        Assert.Equal(r.CapacityKw / (4.186 * 20), r.WaterFlowLps, precision: 6);
        Assert.Equal(1.0 / 3.0, r.FaceAreaM2, precision: 6);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Heating_InvalidTemperatures_AreRejected()
    {
        Assert.False(AirCoilSelectionService.SelectHeating(3600, 30, 20, 80, 60).IsValid);
        Assert.False(AirCoilSelectionService.SelectHeating(3600, 5, 30, 60, 80).IsValid);
    }

    [Fact]
    public void Cooling_SplitsSensibleAndLatent_AndCondenses()
    {
        var r = AirCoilSelectionService.SelectCooling(3600, 28, 0.5, 13, 0.9, 7, 12);

        Assert.True(r.IsValid);
        Assert.InRange(r.CapacityKw, 25, 30);
        Assert.True(r.LatentKw > 0);
        Assert.True(r.SensibleKw > 0 && r.SensibleKw < r.CapacityKw);
        Assert.Equal(r.CapacityKw, r.SensibleKw + r.LatentKw, precision: 6);
        Assert.True(r.CondensateKgPerH > 0);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Cooling_HighFaceVelocity_WarnsAboutCarryover()
    {
        var r = AirCoilSelectionService.SelectCooling(3600, 28, 0.5, 13, 0.9, 7, 12, targetFaceVelocityMs: 3.2);
        Assert.NotEmpty(r.Warnings);
    }

    [Fact]
    public void Cooling_HumidificationOrWarmerOutlet_IsRejected()
    {
        Assert.False(AirCoilSelectionService.SelectCooling(3600, 28, 0.3, 13, 0.95, 7, 12).IsValid);
        Assert.False(AirCoilSelectionService.SelectCooling(3600, 20, 0.5, 25, 0.5, 7, 12).IsValid);
    }
}

public class VavCavBoxServiceTests
{
    [Fact]
    public void Cav_HasConstantFlow_AndSmallestStandardInletUnderVelocityLimit()
    {
        var r = VavCavBoxService.Select(VavCavBoxService.BoxKind.Cav, 1000);

        Assert.Equal(1000, r.MinFlowM3h);
        Assert.Equal(1.0, r.TurndownRatio, precision: 6);
        Assert.Equal(250, r.InletDiameterMm); // 200 mm'de 8,84 m/s > 8 m/s
        Assert.True(r.InletVelocityAtMaxMs <= 8.0);
    }

    [Fact]
    public void Vav_MinimumFlow_IsMaxOfThirtyPercentAndOutdoorAir()
    {
        var byFraction = VavCavBoxService.Select(VavCavBoxService.BoxKind.Vav, 2000, minOutdoorAirM3h: 300);
        var byOutdoorAir = VavCavBoxService.Select(VavCavBoxService.BoxKind.Vav, 2000, minOutdoorAirM3h: 800);

        Assert.Equal(600, byFraction.MinFlowM3h, precision: 6);
        Assert.Equal(0.3, byFraction.TurndownRatio, precision: 6);
        Assert.Equal(800, byOutdoorAir.MinFlowM3h, precision: 6);
    }

    [Fact]
    public void Vav_OverrideBelowRule_Warns_AndAboveMax_IsRejected()
    {
        Assert.NotEmpty(VavCavBoxService.Select(VavCavBoxService.BoxKind.Vav, 2000, overrideMinFlowM3h: 200).Warnings);
        Assert.False(VavCavBoxService.Select(VavCavBoxService.BoxKind.Vav, 2000, overrideMinFlowM3h: 2500).IsValid);
        Assert.False(VavCavBoxService.Select(VavCavBoxService.BoxKind.Vav, 0).IsValid);
    }

    [Fact]
    public void Reheat_UsesMinimumFlow()
    {
        var r = VavCavBoxService.Select(VavCavBoxService.BoxKind.Vav, 2000, coldSupplyC: 13, reheatDischargeC: 30);
        Assert.Equal(600.0 / 3600 * 1.2 * 1.006 * 17, r.ReheatCapacityKw, precision: 6);
    }
}

public class DuctEquipmentEntityTests
{
    [Fact]
    public void Ports_AreAtBothEndsOfBody_AlongRotation()
    {
        var eq = new DuctEquipmentEntity(new Vector3D(1000, 0, 0), DuctEquipmentType.Filter, 250) { Rotation = 0 };
        var ports = eq.GetPorts();

        Assert.Equal(2, ports.Count);
        Assert.Equal(new Vector3D(1000 - eq.Size / 2, 0, 0), ports.Single(p => p.Name == "Inlet").Position);
        Assert.Equal(new Vector3D(1000 + eq.Size / 2, 0, 0), ports.Single(p => p.Name == "Outlet").Position);
    }

    [Fact]
    public void Clone_CopiesEngineeringData_WithNewId()
    {
        var eq = new DuctEquipmentEntity(Vector3D.Zero, DuctEquipmentType.HeatingCoil, 315)
        {
            CapacityKw = 12.5, AirFlowM3h = 2000, PressureDropPa = 40, FilterClass = "x"
        };
        var clone = (DuctEquipmentEntity)eq.Clone();

        Assert.NotEqual(eq.Id, clone.Id);
        Assert.Equal(12.5, clone.CapacityKw);
        Assert.Equal(DuctEquipmentType.HeatingCoil, clone.EquipmentType);
        Assert.Equal(MechanicalEntityType.DuctEquipment, clone.EntityType);
    }

    [Fact]
    public void Bom_GroupsEquipmentByTypeAndDiameter()
    {
        var db = new CadDatabase();
        db.AddEntity(new DuctEquipmentEntity(Vector3D.Zero, DuctEquipmentType.Filter, 250) { FilterClass = "ePM1 70%" });
        db.AddEntity(new DuctEquipmentEntity(new Vector3D(2000, 0, 0), DuctEquipmentType.Filter, 250) { FilterClass = "ePM1 70%" });
        db.AddEntity(new DuctEquipmentEntity(new Vector3D(4000, 0, 0), DuctEquipmentType.CavBox, 200));

        var bom = new BomService(db).GenerateBom().Where(b => b.Category == "Kanal Ekipmanı").ToList();

        Assert.Equal(2, bom.Count);
        Assert.Equal(2, bom.Single(b => b.Description.StartsWith("Filter")).Quantity);
        Assert.Equal(1, bom.Single(b => b.Description.StartsWith("CavBox")).Quantity);
    }
}

public class PlaceDuctEquipmentCommandTests
{
    private static (CadDatabase Db, DuctEntity Duct) MakeDb()
    {
        var db = new CadDatabase();
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 250);
        db.AddEntity(duct);
        return (db, duct);
    }

    [Fact]
    public void Place_OnDuct_SplitsLineAroundEquipment()
    {
        var (db, _) = MakeDb();
        var proto = new DuctEquipmentEntity(Vector3D.Zero, DuctEquipmentType.Filter, 250); // Size=400
        var cmd = new PlaceDuctEquipmentCommand(db, db.TransactionManager, proto);

        cmd.OnPointerPressed(new Vector3D(2500, 50, 0));

        var ducts = db.GetAllEntities().OfType<DuctEntity>().OrderBy(d => d.StartPoint.X).ToList();
        var eq = db.GetAllEntities().OfType<DuctEquipmentEntity>().Single();

        Assert.Equal(2, ducts.Count);
        Assert.Equal(2300, ducts[0].EndPoint.X, precision: 3);
        Assert.Equal(2700, ducts[1].StartPoint.X, precision: 3);
        Assert.Equal(2500, eq.Position.X, precision: 3);
        Assert.Equal(0, eq.Rotation, precision: 6);
    }

    [Fact]
    public void Place_ThenUndo_RestoresOriginalDuct()
    {
        var (db, duct) = MakeDb();
        var cmd = new PlaceDuctEquipmentCommand(db, db.TransactionManager,
            new DuctEquipmentEntity(Vector3D.Zero, DuctEquipmentType.VavBox, 250));

        cmd.OnPointerPressed(new Vector3D(2500, 0, 0));
        db.TransactionManager.Undo();

        Assert.Empty(db.GetAllEntities().OfType<DuctEquipmentEntity>());
        Assert.Equal(duct.Id, Assert.Single(db.GetAllEntities().OfType<DuctEntity>()).Id);
    }

    [Fact]
    public void Place_FarFromAnyDuct_PlacesFreeEquipmentWithoutSplitting()
    {
        var (db, _) = MakeDb();
        var cmd = new PlaceDuctEquipmentCommand(db, db.TransactionManager,
            new DuctEquipmentEntity(Vector3D.Zero, DuctEquipmentType.FlexConnector, 200));

        cmd.OnPointerPressed(new Vector3D(2500, 5000, 0));

        Assert.Single(db.GetAllEntities().OfType<DuctEntity>());
        Assert.Single(db.GetAllEntities().OfType<DuctEquipmentEntity>());
    }
}
