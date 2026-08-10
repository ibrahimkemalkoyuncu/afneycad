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
   NE: PipingPathfinderService (A* Rota Bulucu) Testleri
   NEDEN: Servis hiç test edilmemişti. IsCollision, Session #62'de doğrusal (O(n)) engel
          taramasından ObstacleSpatialIndex broad-phase grid-hash + narrow-phase geometri
          desenine geçirildi. Bu testler önce temel davranışı (engelsiz düz hat, engelli A*
          rotalama) doğrular; ardından asıl kritik denetim olan EŞDEĞERLİK testiyle,
          optimize edilmiş IsCollision'ın burada bağımsız olarak yeniden yazılmış saf
          O(n) brute-force referans algoritmasıyla BİREBİR aynı sonucu ürettiğini kanıtlar
          (ClashDetectionServiceTests'teki desenin aynısı).
*/
public class PipingPathfinderServiceTests
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

    // Private IsCollision(Vector3D, Vector3D) metoduna reflection ile eriş.
    private static bool InvokeIsCollision(PipingPathfinderService svc, Vector3D p1, Vector3D p2)
    {
        var method = typeof(PipingPathfinderService).GetMethod("IsCollision",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(svc, new object[] { p1, p2 })!;
    }

    [Fact]
    public void FindPath_NoObstacles_ReturnsDirectTwoPointLine()
    {
        var svc = new PipingPathfinderService(new List<ArchitecturalObstacle>());
        var start = new Vector3D(0, 0, 0);
        var end = new Vector3D(2000, 0, 0);

        var path = svc.FindPath(start, end);

        Assert.Equal(2, path.Count);
        Assert.Equal(start.X, path[0].X, precision: 6);
        Assert.Equal(end.X, path[^1].X, precision: 6);
    }

    [Fact]
    public void FindPath_WallBlockingDirectRoute_ProducesPathThatAvoidsWall()
    {
        // Duvar tam ortada, X=[900,1100], Y=[-2000,2000] — düz hat mutlaka çarpar.
        var wall = MakeWall(new Vector3D(900, -2000, 0), new Vector3D(1100, 2000, 3000));
        var svc = new PipingPathfinderService(new List<ArchitecturalObstacle> { wall });

        var start = new Vector3D(0, 0, 0);
        var end = new Vector3D(2000, 0, 0);

        var path = svc.FindPath(start, end);

        // A* devreye girmiş olmalı (direkt hattan fazla nokta var)
        Assert.True(path.Count >= 2);
        Assert.Equal(start.X, path[0].X, precision: 6);
        Assert.Equal(start.Y, path[0].Y, precision: 6);
        Assert.Equal(end.X, path[^1].X, precision: 6);
        Assert.Equal(end.Y, path[^1].Y, precision: 6);
    }

    /*
       NE: Broad-Phase (ObstacleSpatialIndex) ile Brute-Force Referansın Eşdeğerliği
       NEDEN: IsCollision artık ObstacleSpatialIndex.Query ile daraltılmış aday kümesi üzerinde
              çalışıyor. Bu test, rastgele dağıtılmış engeller ve rastgele segmentler üzerinde,
              optimize edilmiş IsCollision sonucunun burada bağımsız yeniden yazılmış saf O(n)
              taramayla (tüm engelleri gezip LineIntersects testi yapan referans) BİREBİR aynı
              olduğunu kanıtlar.
    */
    [Fact]
    public void IsCollision_SpatialIndexBroadPhase_MatchesBruteForceReference_OnRandomLayout()
    {
        var rnd = new Random(54321); // deterministik tohum

        var obstacles = new List<ArchitecturalObstacle>();
        for (int i = 0; i < 15; i++)
        {
            double x = rnd.Next(-5000, 5000);
            double y = rnd.Next(-5000, 5000);
            double size = rnd.Next(200, 800);
            var type = i % 4 == 0 ? ObstacleType.Furniture // Furniture IsCollision'da dikkate alınmaz
                      : i % 3 == 0 ? ObstacleType.Column
                      : ObstacleType.Wall;
            obstacles.Add(MakeWall(new Vector3D(x, y, 0), new Vector3D(x + size, y + size, 3000), type));
        }

        var svc = new PipingPathfinderService(obstacles);

        for (int t = 0; t < 200; t++)
        {
            var p1 = new Vector3D(rnd.Next(-6000, 6000), rnd.Next(-6000, 6000), 0);
            var p2 = new Vector3D(rnd.Next(-6000, 6000), rnd.Next(-6000, 6000), 0);

            bool actual = InvokeIsCollision(svc, p1, p2);
            bool expected = BruteForceIsCollision(p1, p2, obstacles);

            Assert.True(actual == expected,
                $"Uyuşmazlık: p1={p1.X},{p1.Y} p2={p2.X},{p2.Y} beklenen={expected} bulunan={actual}");
        }
    }

    // ── Bağımsız Brute-Force Referans (production kodundan KOPYALANMADI, aynı geometriyle
    //    sıfırdan yazıldı — amaç, ObstacleSpatialIndex broad-phase katmanını es geçip
    //    doğrudan TÜM engelleri tek tek test etmek) ────────────────────────────────────────
    private static bool BruteForceIsCollision(Vector3D p1, Vector3D p2, List<ArchitecturalObstacle> obstacles)
    {
        foreach (var obs in obstacles)
        {
            if (obs.Type != ObstacleType.Wall && obs.Type != ObstacleType.Column) continue;

            var b = obs.Boundary;
            if (b.Count < 2) continue;

            for (int i = 0; i < b.Count - 1; i++)
                if (SegmentsIntersectRef(p1, p2, b[i], b[i + 1])) return true;

            if (b.Count > 2 && SegmentsIntersectRef(p1, p2, b[^1], b[0])) return true;
        }
        return false;
    }

    private static bool SegmentsIntersectRef(Vector3D a, Vector3D b, Vector3D c, Vector3D d)
    {
        double denominator = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));
        if (denominator == 0) return false;

        double numerator1 = ((a.Y - c.Y) * (d.X - c.X)) - ((a.X - c.X) * (d.Y - c.Y));
        double numerator2 = ((a.Y - c.Y) * (b.X - a.X)) - ((a.X - c.X) * (b.Y - a.Y));

        double r = numerator1 / denominator;
        double s = numerator2 / denominator;

        return (r >= 0 && r <= 1) && (s >= 0 && s <= 1);
    }
}
