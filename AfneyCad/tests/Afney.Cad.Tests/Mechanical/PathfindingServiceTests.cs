using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: PathfindingService (Bypass/Rekürsif Rota Bulucu) Testleri
   NEDEN: Servis hiç test edilmemişti (ve şu an hiçbir çağıranı yok — Session #62 denetiminde
          bulundu). FindFirstBlockingObstacle ve IsPointInsideAnyObstacle, doğrusal (O(n)) engel
          taramasından ObstacleSpatialIndex broad-phase grid-hash + narrow-phase geometri
          desenine geçirildi. Bu testler temel davranışı ve — asıl kritik denetim olarak —
          optimize edilmiş sonuçların bağımsız brute-force referansla BİREBİR eşdeğerliğini
          kanıtlar (ClashDetectionServiceTests'teki desenin aynısı).
*/
public class PathfindingServiceTests
{
    private const double PipeClearance = 100.0;

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

    private static (ArchitecturalObstacle? obstacle, int index) InvokeFindFirstBlockingObstacle(
        PathfindingService svc, Vector3D p1, Vector3D p2, HashSet<int> avoided)
    {
        var method = typeof(PathfindingService).GetMethod("FindFirstBlockingObstacle",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = method.Invoke(svc, new object[] { p1, p2, avoided })!;
        // NOT: (ArchitecturalObstacle? obstacle, int index) isimli tuple'ın gerçek reflection
        // alan adları derleyici tarafından her zaman Item1/Item2'dir — "obstacle"/"index"
        // isimleri sadece kaynak koddaki söz dizimsel şeker (TupleElementNames), gerçek CLR
        // alan adı değildir.
        var obstacle = (ArchitecturalObstacle?)result.GetType().GetField("Item1")!.GetValue(result);
        var index = (int)result.GetType().GetField("Item2")!.GetValue(result)!;
        return (obstacle, index);
    }

    private static bool InvokeIsPointInsideAnyObstacle(PathfindingService svc, Vector3D point)
    {
        var method = typeof(PathfindingService).GetMethod("IsPointInsideAnyObstacle",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(svc, new object[] { point })!;
    }

    [Fact]
    public void FindPath_NoObstacles_ReturnsPathContainingStartAndEnd()
    {
        var svc = new PathfindingService(new List<ArchitecturalObstacle>());
        var start = new Vector3D(0, 0, 0);
        var end = new Vector3D(2000, 0, 0);

        var path = svc.FindPath(start, end);

        Assert.Equal(start.X, path[0].X, precision: 6);
        Assert.Equal(end.X, path[^1].X, precision: 6);
    }

    [Fact]
    public void FindPath_WallBlockingRoute_BypassesAroundObstacle()
    {
        var wall = MakeWall(new Vector3D(900, -500, 0), new Vector3D(1100, 500, 3000));
        var svc = new PathfindingService(new List<ArchitecturalObstacle> { wall });

        var start = new Vector3D(0, 0, 0);
        var end = new Vector3D(2000, 0, 0);

        var path = svc.FindPath(start, end);

        Assert.True(path.Count > 2); // bypass noktaları eklenmiş olmalı
        Assert.Equal(start.X, path[0].X, precision: 3);
        Assert.Equal(end.X, path[^1].X, precision: 3);
    }

    /*
       NE: FindFirstBlockingObstacle — Broad-Phase / Brute-Force Eşdeğerliği
    */
    [Fact]
    public void FindFirstBlockingObstacle_SpatialIndexBroadPhase_MatchesBruteForceReference_OnRandomLayout()
    {
        var rnd = new Random(99011);

        var obstacles = new List<ArchitecturalObstacle>();
        for (int i = 0; i < 15; i++)
        {
            double x = rnd.Next(-5000, 5000);
            double y = rnd.Next(-5000, 5000);
            double size = rnd.Next(200, 800);
            obstacles.Add(MakeWall(new Vector3D(x, y, 0), new Vector3D(x + size, y + size, 3000)));
        }

        var svc = new PathfindingService(obstacles);
        var noAvoided = new HashSet<int>();

        for (int t = 0; t < 150; t++)
        {
            var p1 = new Vector3D(rnd.Next(-6000, 6000), rnd.Next(-6000, 6000), 0);
            var p2 = new Vector3D(rnd.Next(-6000, 6000), rnd.Next(-6000, 6000), 0);

            var (actualObs, actualIdx) = InvokeFindFirstBlockingObstacle(svc, p1, p2, noAvoided);
            var (expectedObs, expectedIdx) = BruteForceFindFirstBlockingObstacle(p1, p2, obstacles, noAvoided);

            Assert.True(actualIdx == expectedIdx,
                $"Uyuşmazlık: p1={p1.X},{p1.Y} p2={p2.X},{p2.Y} beklenenIdx={expectedIdx} bulunanIdx={actualIdx}");
            Assert.Equal(expectedObs?.Id, actualObs?.Id);
        }
    }

    [Fact]
    public void IsPointInsideAnyObstacle_SpatialIndexBroadPhase_MatchesBruteForceReference_OnRandomLayout()
    {
        var rnd = new Random(77531);

        var obstacles = new List<ArchitecturalObstacle>();
        for (int i = 0; i < 15; i++)
        {
            double x = rnd.Next(-5000, 5000);
            double y = rnd.Next(-5000, 5000);
            double size = rnd.Next(200, 800);
            obstacles.Add(MakeWall(new Vector3D(x, y, 0), new Vector3D(x + size, y + size, 3000)));
        }

        var svc = new PathfindingService(obstacles);

        for (int t = 0; t < 300; t++)
        {
            var pt = new Vector3D(rnd.Next(-6000, 6000), rnd.Next(-6000, 6000), 0);

            bool actual = InvokeIsPointInsideAnyObstacle(svc, pt);
            bool expected = BruteForceIsPointInsideAnyObstacle(pt, obstacles);

            Assert.True(actual == expected,
                $"Uyuşmazlık: pt={pt.X},{pt.Y} beklenen={expected} bulunan={actual}");
        }
    }

    // ── Bağımsız Brute-Force Referanslar ─────────────────────────────────────────────────
    private static (ArchitecturalObstacle? obstacle, int index) BruteForceFindFirstBlockingObstacle(
        Vector3D p1, Vector3D p2, List<ArchitecturalObstacle> obstacles, HashSet<int> avoided)
    {
        double minDist = double.MaxValue;
        ArchitecturalObstacle? closest = null;
        int closestIdx = -1;

        for (int i = 0; i < obstacles.Count; i++)
        {
            if (avoided.Contains(i)) continue;

            var box = obstacles[i].GetBoundingBox();
            var expandedBox = new CadBoundingBox(
                new Vector3D(box.Min.X - PipeClearance, box.Min.Y - PipeClearance, 0),
                new Vector3D(box.Max.X + PipeClearance, box.Max.Y + PipeClearance, 0));

            if (SegmentIntersectsAABBRef(p1, p2, expandedBox))
            {
                double dist = p1.DistanceTo(new Vector3D(
                    (box.Min.X + box.Max.X) / 2, (box.Min.Y + box.Max.Y) / 2, 0));
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = obstacles[i];
                    closestIdx = i;
                }
            }
        }

        return (closest, closestIdx);
    }

    private static bool BruteForceIsPointInsideAnyObstacle(Vector3D point, List<ArchitecturalObstacle> obstacles)
    {
        foreach (var obs in obstacles)
        {
            var box = obs.GetBoundingBox();
            if (point.X >= box.Min.X - PipeClearance && point.X <= box.Max.X + PipeClearance &&
                point.Y >= box.Min.Y - PipeClearance && point.Y <= box.Max.Y + PipeClearance)
                return true;
        }
        return false;
    }

    private static bool SegmentIntersectsAABBRef(Vector3D p1, Vector3D p2, CadBoundingBox box)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;

        double tMin = 0.0;
        double tMax = 1.0;

        if (Math.Abs(dx) < 1e-9)
        {
            if (p1.X < box.Min.X || p1.X > box.Max.X) return false;
        }
        else
        {
            double t1 = (box.Min.X - p1.X) / dx;
            double t2 = (box.Max.X - p1.X) / dx;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        if (Math.Abs(dy) < 1e-9)
        {
            if (p1.Y < box.Min.Y || p1.Y > box.Max.Y) return false;
        }
        else
        {
            double t1 = (box.Min.Y - p1.Y) / dy;
            double t2 = (box.Max.Y - p1.Y) / dy;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        return true;
    }
}
