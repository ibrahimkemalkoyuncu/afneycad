using System;
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

    /*
       NE: QuadTree Broad-Phase ile Brute-Force (O(n^2)) Referans Sonucunun Eşdeğerliği
       NEDEN: ClashDetectionService performans optimizasyonu (Session #58) — entity×obstacle,
              boru×boru ve vana×boru taramaları artık Afney.Cad.SpatialIndex.QuadTree ile
              broad-phase filtrelemesi yapıyor. Bu test, rastgele dağıtılmış büyükçe bir
              varlık kümesinde servisin ürettiği sonuç kümesinin, burada bağımsız olarak
              yeniden yazılmış saf O(n^2) brute-force referans algoritmasıyla BİREBİR aynı
              çift kümesini (aynı Entity/Obstacle id eşleşmeleri) ürettiğini kanıtlar —
              yani algoritmik karmaşıklık iyileşmesi davranışı DEĞİŞTİRMEMİŞTİR.
    */
    [Fact]
    public void DetectClashes_QuadTreeBroadPhase_MatchesBruteForceReference_OnRandomLayout()
    {
        var rnd = new Random(12345); // deterministik tohum

        var obstacles = new List<ArchitecturalObstacle>();
        for (int i = 0; i < 12; i++)
        {
            double x = rnd.Next(-5000, 5000);
            double y = rnd.Next(-5000, 5000);
            double size = rnd.Next(200, 600);
            obstacles.Add(MakeWall(new Vector3D(x, y, 0), new Vector3D(x + size, y + size, 3000),
                i % 3 == 0 ? ObstacleType.Column : ObstacleType.Wall));
        }

        var pipes = new List<PipeEntity>();
        for (int i = 0; i < 40; i++)
        {
            double x1 = rnd.Next(-5000, 5000), y1 = rnd.Next(-5000, 5000), z1 = rnd.Next(0, 3000);
            double len = rnd.Next(300, 1500);
            bool alongX = rnd.Next(2) == 0;
            var start = new Vector3D(x1, y1, z1);
            var end = alongX ? new Vector3D(x1 + len, y1, z1) : new Vector3D(x1, y1 + len, z1);
            double diameter = new[] { 15.0, 25.0, 32.0, 50.0, 63.0 }[rnd.Next(5)];
            pipes.Add(MakePipe(start, end, diameter));
        }

        var valves = new List<ValveEntity>();
        for (int i = 0; i < 8; i++)
        {
            var pos = new Vector3D(rnd.Next(-5000, 5000), rnd.Next(-5000, 5000), rnd.Next(0, 3000));
            valves.Add(new ValveEntity(pos, ValveType.GateValve, 25)
            {
                SystemType = i % 2 == 0 ? MechanicalSystemType.DomesticColdWater : MechanicalSystemType.WasteWater
            });
        }

        var allEntities = pipes.Cast<MechanicalEntity>().Concat(valves).ToList();

        var svc = new ClashDetectionService(obstacles);
        var actual = svc.DetectClashes(allEntities);

        var expectedArchPairs = BruteForceArchitecturalPairs(pipes, obstacles);
        var expectedPipePairs = BruteForcePipePairs(pipes);
        var expectedValvePairs = BruteForceValvePairs(valves, pipes);

        var actualArchPairs = actual
            .Where(r => r.Type == ClashType.MechanicalVsArchitectural)
            .Select(r => (r.EntityA_Id, r.ObstacleId!.Value))
            .ToHashSet();

        var actualMechPairs = actual
            .Where(r => r.Type == ClashType.MechanicalVsMechanical)
            .Select(r => Normalize(r.EntityA_Id, r.EntityB_Id!.Value))
            .ToHashSet();

        Assert.Equal(expectedArchPairs, actualArchPairs);

        var expectedMechPairs = expectedPipePairs.Concat(expectedValvePairs)
            .Select(p => Normalize(p.Item1, p.Item2))
            .ToHashSet();

        Assert.Equal(expectedMechPairs, actualMechPairs);
    }

    private static (Guid, Guid) Normalize(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    private static HashSet<(Guid, Guid)> BruteForceArchitecturalPairs(List<PipeEntity> pipes, List<ArchitecturalObstacle> obstacles)
    {
        var set = new HashSet<(Guid, Guid)>();
        foreach (var pipe in pipes)
        {
            var pipeBox = pipe.GetBoundingBox();
            foreach (var obs in obstacles)
            {
                if (pipeBox.Intersects(obs.GetBoundingBox()))
                    set.Add((pipe.Id, obs.Id));
            }
        }
        return set;
    }

    private static bool IsConnectedRef(PipeEntity p1, PipeEntity p2)
    {
        const double eps = 10.0;
        return p1.StartPoint.DistanceTo(p2.StartPoint) < eps ||
               p1.StartPoint.DistanceTo(p2.EndPoint) < eps ||
               p1.EndPoint.DistanceTo(p2.StartPoint) < eps ||
               p1.EndPoint.DistanceTo(p2.EndPoint) < eps;
    }

    private static double SegmentToSegmentDistanceRef(Vector3D p1, Vector3D p2, Vector3D p3, Vector3D p4)
    {
        var d1 = p2 - p1;
        var d2 = p4 - p3;
        var r = p1 - p3;

        double a = d1.Dot(d1), e = d2.Dot(d2);
        double f = d2.Dot(r);

        double s, t;
        if (a <= 1e-10 && e <= 1e-10) return r.Length();
        if (a <= 1e-10) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            double c2 = d1.Dot(r);
            if (e <= 1e-10) { t = 0; s = Math.Clamp(-c2 / a, 0, 1); }
            else
            {
                double b2 = d1.Dot(d2);
                double denom = a * e - b2 * b2;
                s = denom != 0 ? Math.Clamp((b2 * f - c2 * e) / denom, 0, 1) : 0;
                t = (b2 * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c2 / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b2 - c2) / a, 0, 1); }
            }
        }
        return (p1 + d1 * s - (p3 + d2 * t)).Length();
    }

    private static HashSet<(Guid, Guid)> BruteForcePipePairs(List<PipeEntity> pipes)
    {
        var set = new HashSet<(Guid, Guid)>();
        for (int i = 0; i < pipes.Count; i++)
        {
            for (int j = i + 1; j < pipes.Count; j++)
            {
                var p1 = pipes[i];
                var p2 = pipes[j];
                if (IsConnectedRef(p1, p2)) continue;

                double minClearance = (p1.InnerDiameter + p2.InnerDiameter) / 2.0 + 25.0;
                double dist = SegmentToSegmentDistanceRef(p1.StartPoint, p1.EndPoint, p2.StartPoint, p2.EndPoint);
                if (dist < minClearance)
                    set.Add((p1.Id, p2.Id));
            }
        }
        return set;
    }

    private static HashSet<(Guid, Guid)> BruteForceValvePairs(List<ValveEntity> valves, List<PipeEntity> pipes)
    {
        var set = new HashSet<(Guid, Guid)>();
        foreach (var valve in valves)
        {
            var vBox = valve.GetBoundingBox();
            foreach (var pipe in pipes)
            {
                if (pipe.SystemType == valve.SystemType) continue;
                if (vBox.Intersects(pipe.GetBoundingBox()))
                    set.Add((valve.Id, pipe.Id));
            }
        }
        return set;
    }
}
