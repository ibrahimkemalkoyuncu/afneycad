using System;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: HVAC Ekipman Kütüphanesi Testleri (HvacEquipmentLibraryTests)
   NEDEN: Kullanıcının "kanal ekipman kütüphanesi derinliği" talebiyle eklenen üç yeni parçayı
          (AirTerminalEntity, DamperEntity, SilencerSelectionService) doğrulamak için. Öncesinde
          AfneyCAD'de menfez/difüzör, damper veya susturucu seçim kataloğu hiç yoktu — sadece
          düz kanal (DuctEntity) mevcuttu.
*/
public class HvacEquipmentLibraryTests
{
    // ── AirTerminalEntity ────────────────────────────────────────────────────────

    [Fact]
    public void AirTerminal_GetPorts_ReturnsSingleNeckPort_FacingIntoDuct()
    {
        var terminal = new AirTerminalEntity(new Vector3D(1000, 0, 0), AirTerminalType.SupplyDiffuser, 250)
        {
            Rotation = 0.0,
            InnerDiameter = 200
        };

        var ports = terminal.GetPorts();

        Assert.Single(ports);
        Assert.Equal("Neck", ports[0].Name);
        Assert.Equal(200, ports[0].Diameter);
        // Rotation=0 → duvara bakan yön dünya -X olmalı (menfez içeri, kanala doğru)
        Assert.True(ports[0].Direction.X < 0);
    }

    [Fact]
    public void AirTerminal_MoveGripPoint_UpdatesPosition()
    {
        var terminal = new AirTerminalEntity(new Vector3D(0, 0, 0), AirTerminalType.ReturnGrille, 150);

        terminal.MoveGripPointAt(0, new Vector3D(500, 300, 0));

        Assert.Equal(new Vector3D(500, 300, 0), terminal.Position);
    }

    [Fact]
    public void AirTerminal_Clone_CopiesAcousticFields()
    {
        var terminal = new AirTerminalEntity(new Vector3D(0, 0, 0), AirTerminalType.SupplyDiffuser, 300)
        {
            NeckVelocityMs = 4.5,
            ThrowM = 5.0,
            NCRating = 30
        };

        var clone = (AirTerminalEntity)terminal.Clone();

        Assert.Equal(4.5, clone.NeckVelocityMs);
        Assert.Equal(5.0, clone.ThrowM);
        Assert.Equal(30, clone.NCRating);
        Assert.NotEqual(terminal.Id, clone.Id);
    }

    [Fact]
    public void AirTerminal_FeedsAcousticAnalysisService_TerminalDeviceLoss()
    {
        var terminal = new AirTerminalEntity(new Vector3D(0, 0, 0), AirTerminalType.SupplyDiffuser, 300)
        {
            NeckVelocityMs = 3.0
        };

        double loss = AcousticAnalysisService.TerminalDeviceLoss(terminal.NeckVelocityMs);

        Assert.True(loss > 0);
    }

    // ── DamperEntity ─────────────────────────────────────────────────────────────

    [Fact]
    public void Damper_GetPorts_ReturnsTwoPorts_AtCorrectOffsets()
    {
        var damper = new DamperEntity(new Vector3D(0, 0, 0), DamperType.Volume, 250) { Size = 300 };

        var ports = damper.GetPorts();

        Assert.Equal(2, ports.Count);
        Assert.Equal("Inlet", ports[0].Name);
        Assert.Equal("Outlet", ports[1].Name);
        Assert.Equal(-150, ports[0].Position.X, precision: 3);
        Assert.Equal(150, ports[1].Position.X, precision: 3);
        Assert.Equal(250, ports[0].Diameter);
    }

    [Fact]
    public void Damper_FireType_DefaultsToNinetyMinuteRating()
    {
        var fireDamper = new DamperEntity(new Vector3D(0, 0, 0), DamperType.Fire, 200);
        var volumeDamper = new DamperEntity(new Vector3D(0, 0, 0), DamperType.Volume, 200);

        Assert.Equal(90, fireDamper.FireRatingMin);
        Assert.Equal(0, volumeDamper.FireRatingMin);
    }

    [Fact]
    public void Damper_MoveGripPoint_UpdatesPosition()
    {
        var damper = new DamperEntity(new Vector3D(0, 0, 0), DamperType.Smoke, 200);

        damper.MoveGripPointAt(0, new Vector3D(700, -200, 0));

        Assert.Equal(new Vector3D(700, -200, 0), damper.Position);
    }

    [Fact]
    public void Damper_Clone_CopiesDamperSpecificFields()
    {
        var damper = new DamperEntity(new Vector3D(0, 0, 0), DamperType.Volume, 200)
        {
            DamperPositionPct = 65
        };

        var clone = (DamperEntity)damper.Clone();

        Assert.Equal(65, clone.DamperPositionPct);
        Assert.Equal(DamperType.Volume, clone.DamperType);
        Assert.NotEqual(damper.Id, clone.Id);
    }

    // ── SilencerSelectionService ─────────────────────────────────────────────────

    [Fact]
    public void FindSilencers_FiltersOutUndersizedFlowAndInsufficientAttenuation()
    {
        var results = SilencerSelectionService.FindSilencers(flowM3h: 1200, targetInsertionLossDb: 25, criticalBandHz: 500);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Silencer.MaxFlowM3h >= 1200 * 1.10));
        Assert.All(results, r => Assert.True(r.InsertionLossAtCriticalBandDb >= 25));
    }

    [Fact]
    public void FindSilencers_UnreasonableRequirement_ReturnsEmpty()
    {
        var results = SilencerSelectionService.FindSilencers(flowM3h: 100000, targetInsertionLossDb: 25);

        Assert.Empty(results);
    }

    [Fact]
    public void BestSilencer_ReturnsShortestSufficientOption()
    {
        var best = SilencerSelectionService.BestSilencer(flowM3h: 250, targetInsertionLossDb: 15, criticalBandHz: 500);

        Assert.NotNull(best);
        // Elle doğrulama: kataloğun içinde bu gereksinimi karşılayan en kısa boylu seçenek olmalı
        var allCandidates = SilencerSelectionService.FindSilencers(250, 15, 500);
        Assert.Equal(allCandidates.Min(c => c.Silencer.LengthMm), best!.Silencer.LengthMm);
    }

    [Fact]
    public void ApplyToNoiseBudget_ReadsCorrectOctaveBandValue()
    {
        var silencer = SilencerSelectionService.SilencerCatalog.First(s => s.ModelName == "CS 250-900");

        double il500 = SilencerSelectionService.ApplyToNoiseBudget(silencer, 500);
        double il1000 = SilencerSelectionService.ApplyToNoiseBudget(silencer, 1000);

        Assert.Equal(silencer.InsertionLossDb[3], il500);
        Assert.Equal(silencer.InsertionLossDb[4], il1000);
    }

    [Fact]
    public void ApplyToNoiseBudget_ClosesLoopWithAcousticAnalysisService()
    {
        var silencer = SilencerSelectionService.BestSilencer(flowM3h: 1000, targetInsertionLossDb: 20, criticalBandHz: 500)!.Silencer;
        double il = SilencerSelectionService.ApplyToNoiseBudget(silencer);

        var input = new AcousticInput { SilencerInsertionLossDb = il };
        var svc = new AcousticAnalysisService();
        var result = svc.AnalyzeSystem(input);

        Assert.Equal(il, result.SilencerAttenuationDb);
    }
}
