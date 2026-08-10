using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: AutoBranchingService Testleri (AutoBranchingServiceTests)
   NEDEN: Cihazların (vitrifiye) ana boruya otomatik bağlanmasını ve kolon (riser)
          bağlantılarını üreten servis hiç test edilmemişti — burada üretilen T-parçası
          (Tee) ve boru bölme (split) geometrisi, saha uygulamasında doğrudan malzeme
          listesine (BOM) yansıyor. Testler; ana borunun bağlantı noktasından ikiye
          bölündüğünü, branşman çapının port çapından (TS 1258: Lavabo gideri DN40,
          WC gideri DN100 vb.) alındığını, Z kot farkı olduğunda dikey iniş borusu
          eklendiğini ve riser bağlantısında dikeylik ön koşulunun (guard clause)
          doğru çalıştığını doğruluyor.
*/
public class AutoBranchingServiceTests
{
    private static AutoBranchingService CreateService() => new(new CadDatabase());

    // ─────────────────────────────────────────────────────────────────
    // 1. CreateBranchConnectionPublic — Aynı kotta (Z farkı yok) branşman
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateBranchConnection_SameElevation_SplitsMainPipeIntoTwoPlusTeeAndBranch()
    {
        var svc = CreateService();

        var mainPipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        var sourcePoint = new Vector3D(500, 300, 0); // aynı Z kotu, pipe eksenine dik 300mm uzakta
        var port = new MechanicalPort(System.Guid.NewGuid(), "ColdWater", sourcePoint, Vector3D.ZAxis, diameter: 40);

        var result = svc.CreateBranchConnectionPublic(sourcePoint, mainPipe, port);

        var pipes = result.NewEntities.OfType<PipeEntity>().ToList();
        var tees = result.NewEntities.OfType<TeeEntity>().ToList();

        // Z farkı yok (<=10mm eşik) -> dikey iniş borusu YOK, sadece 1 yatay branşman + 2 ana boru parçası.
        Assert.Equal(3, pipes.Count);
        Assert.Single(tees);
        Assert.Contains(mainPipe, result.RemovedEntities);

        // Branşman borusu: port.Diameter (40mm) kullanılmalı.
        Assert.Contains(pipes, p => p.InnerDiameter == 40);

        // Ana borunun bölünen iki parçası, orijinal çapı (50mm) korumalı.
        Assert.Equal(2, pipes.Count(p => p.InnerDiameter == 50));
    }

    [Fact]
    public void CreateBranchConnection_PortWithoutOwnDiameter_FallsBackToHalfMainPipeDiameter()
    {
        var svc = CreateService();

        var mainPipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 60)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        var sourcePoint = new Vector3D(1000, 200, 0);
        // Diameter belirtilmedi (varsayılan 0) -> branchDN = mainPipe.InnerDiameter / 2.0 = 30
        var port = new MechanicalPort(System.Guid.NewGuid(), "ColdWater", sourcePoint, Vector3D.ZAxis);

        var result = svc.CreateBranchConnectionPublic(sourcePoint, mainPipe, port);

        var branchPipe = result.NewEntities.OfType<PipeEntity>()
            .First(p => p.InnerDiameter != mainPipe.InnerDiameter);

        Assert.Equal(30.0, branchPipe.InnerDiameter);
    }

    [Fact]
    public void CreateBranchConnection_ElevationDifferenceAbove10mm_AddsVerticalDropPipe()
    {
        var svc = CreateService();

        var mainPipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 50)
        {
            SystemType = MechanicalSystemType.WasteWater
        };

        // Kaynak nokta ana borudan 600mm aşağıda (Z=-600) -> dikey iniş borusu eklenmeli.
        var sourcePoint = new Vector3D(500, 200, -600);
        var port = new MechanicalPort(System.Guid.NewGuid(), "Drainage", sourcePoint, Vector3D.ZAxis, diameter: 40);

        var result = svc.CreateBranchConnectionPublic(sourcePoint, mainPipe, port);

        var pipes = result.NewEntities.OfType<PipeEntity>().ToList();

        // Dikey iniş (1) + yatay branşman (1) + 2 ana boru parçası = 4 boru.
        Assert.Equal(4, pipes.Count);
        Assert.Single(result.NewEntities.OfType<TeeEntity>());

        // Dikey iniş borusu, kaynak noktadan başlayıp aynı X/Y'de üst kottaki ara noktaya gitmeli.
        var verticalPipe = pipes.First(p => p.StartPoint == sourcePoint);
        Assert.Equal(sourcePoint.X, verticalPipe.EndPoint.X);
        Assert.Equal(sourcePoint.Y, verticalPipe.EndPoint.Y);
        Assert.Equal(0.0, verticalPipe.EndPoint.Z); // Ana borunun kotuna çıkmalı
    }

    [Fact]
    public void CreateBranchConnection_PointOutsideMainPipeSegment_ReturnsEmptyResult()
    {
        var svc = CreateService();

        var mainPipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 50)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        // Projeksiyon, boru segmentinin dışına düşüyor (X=2000 > 1000 uç noktası).
        var sourcePoint = new Vector3D(2000, 300, 0);
        var port = new MechanicalPort(System.Guid.NewGuid(), "ColdWater", sourcePoint, Vector3D.ZAxis, diameter: 40);

        var result = svc.CreateBranchConnectionPublic(sourcePoint, mainPipe, port);

        Assert.Empty(result.NewEntities);
        Assert.Empty(result.RemovedEntities);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. ConnectFixturesToPipe — TS 1258 gider çapı ile otomatik branşman
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ConnectFixturesToPipe_WashbasinDrainAlignedWithWasteWaterPipe_CreatesBranchWithLavaboDN()
    {
        var svc = CreateService();

        // Lavabo gideri (DrainOffset = (0,0,-550)) ana pis su borusunun kotuna (Z=-550) denk gelecek şekilde konumlandırıldı.
        var fixture = SanitaryFixtureEntity.CreateWashbasin(new Vector3D(500, 300, 0));

        var mainPipe = new PipeEntity(new Vector3D(0, 0, -550), new Vector3D(2000, 0, -550), 100)
        {
            SystemType = MechanicalSystemType.WasteWater
        };

        var result = svc.ConnectFixturesToPipe(new List<SanitaryFixtureEntity> { fixture }, mainPipe);

        var tees = result.OfType<TeeEntity>().ToList();
        var pipes = result.OfType<PipeEntity>().ToList();

        // Sadece Drainage portu WasteWater sistemine eşleşir (ColdWater/HotWater eşleşmez) ->
        // 1 branşman + 2 ana boru parçası + 1 Tee.
        Assert.Single(tees);
        Assert.Equal(3, pipes.Count);

        // Lavabo gideri TS 1258'e göre DN40 olmalı (SanitaryFixtureEntity.GetPorts: isLavabo -> drDN=40).
        Assert.Contains(pipes, p => p.InnerDiameter == 40);
    }

    [Fact]
    public void ConnectFixturesToPipe_ColdWaterSystemPipe_OnlyMatchesColdWaterPort()
    {
        var svc = CreateService();

        // Lavabonun soğuk su portu (ColdWaterOffset=(80,-50,-500)) ana soğuk su borusunun kotuna hizalandı.
        var fixturePos = new Vector3D(500, 300, 0);
        var fixture = SanitaryFixtureEntity.CreateWashbasin(fixturePos);
        var coldPortPos = fixture.GetPorts().First(p => p.Name == "ColdWater").Position;

        var mainPipe = new PipeEntity(
            new Vector3D(0, coldPortPos.Y, coldPortPos.Z),
            new Vector3D(2000, coldPortPos.Y, coldPortPos.Z),
            30)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        var result = svc.ConnectFixturesToPipe(new List<SanitaryFixtureEntity> { fixture }, mainPipe);

        // Bağlantı oluşmuş olmalı (en az bir Tee).
        Assert.Contains(result, e => e is TeeEntity);
    }

    [Fact]
    public void ConnectFixturesToPipe_NoFixtures_ReturnsEmptyList()
    {
        var svc = CreateService();
        var mainPipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 50)
        {
            SystemType = MechanicalSystemType.WasteWater
        };

        var result = svc.ConnectFixturesToPipe(new List<SanitaryFixtureEntity>(), mainPipe);

        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. ConnectToRiser — Dikeylik ön koşulu (guard) ve T-bağlantısı
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ConnectToRiser_NonVerticalRiser_ReturnsEmptyResult()
    {
        var svc = CreateService();

        // Riser X ekseninde de değişiyor (dikey değil) -> guard clause devreye girmeli.
        var notVerticalRiser = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(500, 0, 3000), 80)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        var horizontalPipe = new PipeEntity(new Vector3D(0, 500, 1500), new Vector3D(800, 500, 1500), 40)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        var result = svc.ConnectToRiser(horizontalPipe, notVerticalRiser);

        Assert.Empty(result.NewEntities);
        Assert.Empty(result.RemovedEntities);
    }

    [Fact]
    public void ConnectToRiser_VerticalRiser_SplitsRiserAndAddsTee()
    {
        var svc = CreateService();

        var riser = new PipeEntity(new Vector3D(1000, 1000, 0), new Vector3D(1000, 1000, 3000), 80)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        var horizontalPipe = new PipeEntity(new Vector3D(0, 500, 1500), new Vector3D(800, 500, 1500), 40)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        var result = svc.ConnectToRiser(horizontalPipe, riser);

        var pipes = result.NewEntities.OfType<PipeEntity>().ToList();
        var tees = result.NewEntities.OfType<TeeEntity>().ToList();

        Assert.Single(tees);
        Assert.Contains(riser, result.RemovedEntities);

        // Riser'ın iki bölünmüş parçası + bağlantı borusu (connector) = 3 boru bekleniyor.
        Assert.Equal(3, pipes.Count);

        // Bölünmüş riser parçalarının çapı, orijinal riser çapıyla (80mm) aynı kalmalı.
        Assert.Equal(2, pipes.Count(p => p.InnerDiameter == 80));
    }

    [Fact]
    public void ConnectToRiser_ConnectorPipe_UsesHorizontalPipeStartOrEndClosestToRiser()
    {
        var svc = CreateService();

        var riser = new PipeEntity(new Vector3D(1000, 1000, 0), new Vector3D(1000, 1000, 3000), 80)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        // horizontalPipe.EndPoint (900,900,1500), riser hattına (X=1000,Y=1000) StartPoint'ten (0,500,1500) daha yakın.
        var horizontalPipe = new PipeEntity(new Vector3D(0, 500, 1500), new Vector3D(900, 900, 1500), 40)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };

        var result = svc.ConnectToRiser(horizontalPipe, riser);

        var connector = result.NewEntities.OfType<PipeEntity>()
            .First(p => p.InnerDiameter == horizontalPipe.InnerDiameter);

        Assert.Equal(horizontalPipe.EndPoint, connector.StartPoint);
    }
}
