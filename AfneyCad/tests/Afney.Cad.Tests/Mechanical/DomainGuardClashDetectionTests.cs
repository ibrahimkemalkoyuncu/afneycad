using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: DomainGuardService Fiziksel Çakışma Kontrolü Testleri
   NEDEN: ClashDetectionService (boru↔duvar/kolon geometrik çakışma) tamamen çalışan ayrı bir
          servisti ama "Hesapla" öncesi kapıdan (DomainGuardService.ValidateSystem) hiç
          geçmiyordu — sadece topolojik/akış kuralları kontrol ediliyordu. Bu testler artık
          kritik bir fiziksel çakışmanın (boru bir kolonun içinden geçiyor) gerçekten
          ValidateSystem() sonucunu IsValid=false yaptığını doğruluyor.
*/
public class DomainGuardClashDetectionTests
{
    private static ArchitecturalObstacle MakeColumnAt(double x, double y, double size = 400)
    {
        return new ArchitecturalObstacle
        {
            Type = ObstacleType.Column,
            Height = 3000,
            Boundary = new List<Vector3D>
            {
                new(x - size / 2, y - size / 2, 0),
                new(x + size / 2, y - size / 2, 0),
                new(x + size / 2, y + size / 2, 0),
                new(x - size / 2, y + size / 2, 0),
            }
        };
    }

    [Fact]
    public void ValidateSystem_PipeThroughColumn_ReportsErrorAndInvalidatesSystem()
    {
        var db = new CadDatabase();
        var graph = new MechanicalTopologyGraph();

        // Boru, tam bir kolonun içinden geçiyor.
        var pipe = new PipeEntity(new Vector3D(-1000, 0, 0), new Vector3D(1000, 0, 0), 50.0);
        db.AddEntity(pipe);
        graph.AddEntity(pipe);

        var obstacles = new List<ArchitecturalObstacle> { MakeColumnAt(0, 0) };

        var guard = new DomainGuardService(db, graph, obstacles);
        var result = guard.ValidateSystem();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("V-CLASH"));
    }

    [Fact]
    public void ValidateSystem_NoObstacles_DoesNotReportClashError()
    {
        var db = new CadDatabase();
        var graph = new MechanicalTopologyGraph();

        var pipe = new PipeEntity(new Vector3D(-1000, 0, 0), new Vector3D(1000, 0, 0), 50.0);
        db.AddEntity(pipe);
        graph.AddEntity(pipe);

        // obstacles parametresi verilmiyor (backward-compatible default).
        var guard = new DomainGuardService(db, graph);
        var result = guard.ValidateSystem();

        Assert.DoesNotContain(result.Errors, e => e.Contains("V-CLASH"));
    }

    [Fact]
    public void ValidateSystem_PipeFarFromObstacle_NoClashReported()
    {
        var db = new CadDatabase();
        var graph = new MechanicalTopologyGraph();

        var pipe = new PipeEntity(new Vector3D(5000, 5000, 0), new Vector3D(6000, 5000, 0), 50.0);
        db.AddEntity(pipe);
        graph.AddEntity(pipe);

        var obstacles = new List<ArchitecturalObstacle> { MakeColumnAt(0, 0) };

        var guard = new DomainGuardService(db, graph, obstacles);
        var result = guard.ValidateSystem();

        Assert.DoesNotContain(result.Errors, e => e.Contains("V-CLASH"));
    }
}
