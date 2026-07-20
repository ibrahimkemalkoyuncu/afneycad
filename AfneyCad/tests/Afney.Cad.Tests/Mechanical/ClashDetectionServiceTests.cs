using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: ClashDetectionService (Boru↔Mimari / Boru↔Boru Çakışma Analizi) Testleri
   NEDEN: Servis hiç test edilmemişti. Bu testler; boru-engel BoundingBox kesişimini,
          bağlı (ortak uçlu) boruların çakışma sayılmadığını, minimum boşluk (D1/2+D2/2+25mm)
          kuralının 3D segment mesafesiyle doğru uygulandığını ve gerçekten kesişen borular
          ile sadece "çok yakın" borular arasındaki Critical/Warning ayrımını doğruluyor.
*/
public class ClashDetectionServiceTests
{
    private static ArchitecturalObstacle MakeWall(Vector3D min, Vector3D max, ObstacleType type = ObstacleType.Wall)
    {
        return new ArchitecturalObstacle
        {
            Type = type,
            Height = max.Z - min.Z,
            Boundary = new List<Vector3D>
            {
                new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z),
                new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z),
            }
        };
    }

    private static PipeEntity MakePipe(Vector3D start, Vector3D end, double diameterMm = 25)
        => new(start, end, diameterMm) { SystemType = MechanicalSystemType.DomesticColdWater };

    [Fact]
    public void DetectClashes_PipeCrossingWall_ReportsMechanicalVsArchitecturalClash()
    {
        // Duvar: X=[900,1100], Y=[-500,500], Z=[0,3000]. Boru bu duvarın içinden geçiyor.
        var wall = MakeWall(new Vector3D(900, -500, 0), new Vector3D(1100, 500, 3000));
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle> { wall });

        var pipe = MakePipe(new Vector3D(0, 0, 1000), new Vector3D(2000, 0, 1000));

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe });

        Assert.Single(results);
        Assert.Equal(ClashType.MechanicalVsArchitectural, results[0].Type);
        Assert.Equal(ClashSeverity.Warning, results[0].Severity); // Duvar (Kolon değil) -> Warning
        Assert.True(pipe.HasHydraulicViolation);
    }

    [Fact]
    public void DetectClashes_PipeNotIntersectingWall_ReportsNoClash()
    {
        var wall = MakeWall(new Vector3D(900, -500, 0), new Vector3D(1100, 500, 3000));
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle> { wall });

        // Boru duvardan tamamen uzak (Y ekseninde 5m öteye kaymış).
        var pipe = MakePipe(new Vector3D(0, 5000, 1000), new Vector3D(2000, 5000, 1000));

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe });

        Assert.Empty(results);
        Assert.False(pipe.HasHydraulicViolation);
    }

    [Fact]
    public void DetectClashes_ColumnObstacle_IsAlwaysCriticalSeverity()
    {
        var column = MakeWall(new Vector3D(0, 0, 0), new Vector3D(300, 300, 3000), ObstacleType.Column);
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle> { column });
        var pipe = MakePipe(new Vector3D(-500, 150, 1000), new Vector3D(500, 150, 1000));

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe });

        Assert.Single(results);
        Assert.Equal(ClashSeverity.Critical, results[0].Severity);
    }

    [Fact]
    public void DetectClashes_NonColumnObstacle_IsWarningSeverity()
    {
        var wall = MakeWall(new Vector3D(0, 0, 0), new Vector3D(300, 300, 3000), ObstacleType.Wall);
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle> { wall });
        var pipe = MakePipe(new Vector3D(-500, 150, 1000), new Vector3D(500, 150, 1000));

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe });

        Assert.Single(results);
        Assert.Equal(ClashSeverity.Warning, results[0].Severity);
    }

    [Fact]
    public void DetectClashes_TwoConnectedPipesAtSharedEndpoint_AreNotFlaggedAsClashing()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());

        // İki boru aynı noktada birleşiyor (T bağlantısı benzeri) — bu bir çakışma DEĞİL, bağlantıdır.
        var pipe1 = MakePipe(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), diameterMm: 25);
        var pipe2 = MakePipe(new Vector3D(1000, 0, 0), new Vector3D(1000, 1000, 0), diameterMm: 25);

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe1, pipe2 });

        Assert.Empty(results);
    }

    [Fact]
    public void DetectClashes_TwoParallelPipesCloserThanClearance_ReportsWarningNotCrossing()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());

        // İki paralel boru (25mm ve 32mm çap), minClearance = (25+32)/2+25 = 53.5mm.
        // Aralarındaki mesafe 30mm — kesişmiyorlar (paralel) ama yeterli boşluk da yok.
        var pipe1 = MakePipe(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), diameterMm: 25);
        var pipe2 = MakePipe(new Vector3D(0, 30, 0), new Vector3D(1000, 30, 0), diameterMm: 32);

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe1, pipe2 });

        var clash = Assert.Single(results);
        Assert.Equal(ClashType.MechanicalVsMechanical, clash.Type);
        Assert.Equal(ClashSeverity.Warning, clash.Severity); // Paralel, kesişmiyor → Warning
        Assert.True(pipe1.HasHydraulicViolation);
        Assert.True(pipe2.HasHydraulicViolation);
    }

    [Fact]
    public void DetectClashes_TwoPipesActuallyCrossingInPlan_ReportsCriticalSeverity()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());

        // Biri X ekseninde, diğeri Y ekseninde — (500,500,0) noktasında gerçekten kesişiyorlar (Z aynı, bağlı değiller).
        var pipe1 = MakePipe(new Vector3D(0, 500, 0), new Vector3D(1000, 500, 0), diameterMm: 25);
        var pipe2 = MakePipe(new Vector3D(500, 0, 0), new Vector3D(500, 1000, 0), diameterMm: 25);

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe1, pipe2 });

        var clash = Assert.Single(results);
        Assert.Equal(ClashSeverity.Critical, clash.Severity);
    }

    [Fact]
    public void DetectClashes_TwoParallelPipesWithSufficientClearance_ReportsNoClash()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());

        // minClearance = (25+25)/2+25 = 50mm. Aralık 200mm → yeterli boşluk var.
        var pipe1 = MakePipe(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), diameterMm: 25);
        var pipe2 = MakePipe(new Vector3D(0, 200, 0), new Vector3D(1000, 200, 0), diameterMm: 25);

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe1, pipe2 });

        Assert.Empty(results);
    }

    [Fact]
    public void ResolveClash_MechanicalVsMechanical_CreatesFiveSegmentByPassAroundClashPoint()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());
        var pipeA = MakePipe(new Vector3D(0, 500, 0), new Vector3D(1000, 500, 0), diameterMm: 25);
        var pipeB = MakePipe(new Vector3D(500, 0, 0), new Vector3D(500, 1000, 0), diameterMm: 25);

        var clashes = svc.DetectClashes(new MechanicalEntity[] { pipeA, pipeB });
        var clash = Assert.Single(clashes);

        var newSegments = svc.ResolveClash(clash, new MechanicalEntity[] { pipeA, pipeB });

        Assert.Equal(5, newSegments.Count);
        Assert.All(newSegments, s => Assert.IsType<PipeEntity>(s));

        // Uçtan uca zincir sürekliliği: her segmentin bitiş noktası bir sonrakinin başlangıcı olmalı.
        var pipes = newSegments.Cast<PipeEntity>().ToList();
        for (int i = 0; i < pipes.Count - 1; i++)
        {
            Assert.Equal(pipes[i].EndPoint.X, pipes[i + 1].StartPoint.X, precision: 6);
            Assert.Equal(pipes[i].EndPoint.Y, pipes[i + 1].StartPoint.Y, precision: 6);
            Assert.Equal(pipes[i].EndPoint.Z, pipes[i + 1].StartPoint.Z, precision: 6);
        }

        // İlk segment orijinal başlangıçtan, son segment orijinal bitişe kadar gitmeli.
        Assert.Equal(pipeA.StartPoint.X, pipes[0].StartPoint.X, precision: 6);
        Assert.Equal(pipeA.EndPoint.X, pipes[^1].EndPoint.X, precision: 6);

        // Orta iki segment Z-atlaması yapmalı (temiz su → +200mm yukarı).
        Assert.True(pipes[1].EndPoint.Z > pipeA.StartPoint.Z);
        Assert.True(pipes[2].StartPoint.Z > pipeA.StartPoint.Z);
    }

    [Fact]
    public void ResolveClash_WasteWaterPipe_ByPassGoesDownwardNotUpward()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());
        var pipeA = MakePipe(new Vector3D(0, 500, 0), new Vector3D(1000, 500, 0), diameterMm: 50);
        pipeA.SystemType = MechanicalSystemType.WasteWater;
        var pipeB = MakePipe(new Vector3D(500, 0, 0), new Vector3D(500, 1000, 0), diameterMm: 25);

        var clashes = svc.DetectClashes(new MechanicalEntity[] { pipeA, pipeB });
        var clash = clashes.First(c => c.EntityA_Id == pipeA.Id || c.EntityB_Id == pipeA.Id);

        var newSegments = svc.ResolveClash(clash, new MechanicalEntity[] { pipeA, pipeB });
        var pipes = newSegments.Cast<PipeEntity>().ToList();

        // Pis su hattı çakışmadan AŞAĞIDAN (negatif Z) dolaşmalı.
        Assert.True(pipes[1].EndPoint.Z < pipeA.StartPoint.Z);
    }

    [Fact]
    public void DetectClashes_ValveOverlappingDifferentSystemPipe_ReportsWarning()
    {
        var svc = new ClashDetectionService(new List<ArchitecturalObstacle>());
        var pipe = MakePipe(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), diameterMm: 25);
        pipe.SystemType = MechanicalSystemType.DomesticColdWater;

        var valve = new ValveEntity(new Vector3D(500, 0, 0), ValveType.GateValve, 25)
        {
            SystemType = MechanicalSystemType.WasteWater // farklı sistem → çakışma sayılmalı
        };

        var results = svc.DetectClashes(new MechanicalEntity[] { pipe, valve });

        Assert.Contains(results, r => r.Type == ClashType.MechanicalVsMechanical && r.Severity == ClashSeverity.Warning);
    }
}
