using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: DomainGuardService.CheckSourceConnectivity Testleri
   NEDEN: Bu kontrol önceden "Basitleştirilmiş" olarak yalnızca şebekede EN AZ BİR
          MechanicalLoadNode VAR MI diye bakıyordu — çizimde tamamen izole (hiçbir
          boruya bağlı olmayan) bir giriş noktası bile V-000 hatasını atlatabiliyordu.
          Bu testler artık gerçek bağlantı+ulaşılabilirlik kontrolü yapıldığını doğruluyor.
*/
public class DomainGuardSourceConnectivityTests
{
    private static MechanicalTopologyGraph BuildConnectedNetwork(out MechanicalLoadNode loadNode, out SanitaryFixtureEntity fixture)
    {
        var graph = new MechanicalTopologyGraph();

        loadNode = new MechanicalLoadNode(new Vector3D(0, 0, 0), 5.0);
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25.0);
        fixture = new SanitaryFixtureEntity(new Vector3D(1000, 0, 0), "Washbasin", 1.0);

        graph.AddEntity(loadNode);
        graph.AddEntity(pipe);
        graph.AddEntity(fixture);

        var lnPort = graph.GetNode(loadNode.Id)!.Ports[0];
        var pStart = graph.GetNode(pipe.Id)!.Ports.First(p => p.Name == "Start");
        graph.Connect(lnPort, pStart);

        var pEnd = graph.GetNode(pipe.Id)!.Ports.First(p => p.Name == "End");
        var fixtureColdPort = graph.GetNode(fixture.Id)!.Ports.First(p => p.Name == "ColdWater");
        graph.Connect(pEnd, fixtureColdPort);

        return graph;
    }

    [Fact]
    public void ValidateSystem_LoadNodeConnectedAndReachesFixture_NoSourceConnectivityError()
    {
        var graph = BuildConnectedNetwork(out var loadNode, out var fixture);
        var db = new CadDatabase();
        db.AddEntity(loadNode);
        db.AddEntity(fixture);

        var guard = new DomainGuardService(db, graph);
        var result = guard.ValidateSystem();

        Assert.DoesNotContain(result.Errors, e => e.Contains("V-000"));
    }

    [Fact]
    public void ValidateSystem_LoadNodeCompletelyIsolated_ReportsSourceConnectivityError()
    {
        var graph = new MechanicalTopologyGraph();
        var loadNode = new MechanicalLoadNode(new Vector3D(0, 0, 0), 5.0);
        var fixture = new SanitaryFixtureEntity(new Vector3D(500, 500, 0), "Washbasin", 1.0);

        graph.AddEntity(loadNode);
        graph.AddEntity(fixture);
        // Kasıtlı olarak HİÇBİR bağlantı kurulmuyor — giriş noktası izole.

        var db = new CadDatabase();
        db.AddEntity(loadNode);
        db.AddEntity(fixture);

        var guard = new DomainGuardService(db, graph);
        var result = guard.ValidateSystem();

        Assert.Contains(result.Errors, e => e.Contains("V-000"));
    }

    [Fact]
    public void ValidateSystem_LoadNodeConnectedButNeverReachesFixture_ReportsSourceConnectivityError()
    {
        // Giriş noktası bir boruya bağlı ama o boru hiçbir armatüre ulaşmıyor (açık uç).
        var graph = new MechanicalTopologyGraph();
        var loadNode = new MechanicalLoadNode(new Vector3D(0, 0, 0), 5.0);
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25.0);

        graph.AddEntity(loadNode);
        graph.AddEntity(pipe);

        var lnPort = graph.GetNode(loadNode.Id)!.Ports[0];
        var pStart = graph.GetNode(pipe.Id)!.Ports.First(p => p.Name == "Start");
        graph.Connect(lnPort, pStart);
        // pipe'ın "End" portu hiçbir yere bağlı değil, armatür yok.

        var db = new CadDatabase();
        db.AddEntity(loadNode);
        db.AddEntity(pipe);

        var guard = new DomainGuardService(db, graph);
        var result = guard.ValidateSystem();

        Assert.Contains(result.Errors, e => e.Contains("V-000"));
    }
}
