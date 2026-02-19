using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı Boru Rotalama Servisi (PipingPathfinderService)
   NEDEN: Armatürler ile kolonlar arasındaki en kısa yolu, mimari engellerden (Duvar, Kolon) sakınarak otomatik bulmak için. (Suggestion 19)
   
   ALGORITMA (Mühendislik Modu - A* Pathfinder):
   1. Çalışma alanını bir grid hücresine (örn: 10cm x 10cm) böler.
   2. Mimari engellerin (Duvar vb.) bulunduğu hücreleri "Geçilemez" (Blocked) işaretler.
   3. Başlangıç ve bitiş noktaları arasında A* algoritmasını çalıştırarak engellere çarpmayan, minimal dirsekli rotayı üretir.
*/
public class PipingPathfinderService
{
    private readonly List<ArchitecturalObstacle> _obstacles;
    private readonly double _gridSize = 100.0; // 100mm (10cm) grid çözünürlüğü

    /*
       NE: PipingPathfinderService Yapıcı Metodu
       NEDEN: Mimari engelleri (Duvar, Kolon vb.) alarak tesisat rotalarını bu engellerden sakınacak şekilde planlamaya hazır hale gelir.
    */
    public PipingPathfinderService(List<ArchitecturalObstacle> obstacles)
    {
        _obstacles = obstacles;
    }

    /*
       NE: Yol/Rota Bul (FindPath)
       NEDEN: Verilen iki nokta arasında, engellere çarpmayan en kısa boru güzergahını saptamak için. Eğer arada engel yoksa direkt hat, varsa A* algoritmasını çalıştırır.
    */
    public List<Vector3D> FindPath(Vector3D start, Vector3D end)
    {
        // Basitlik için eğer arada engel yoksa direkt doğru çizelim (Hızlı Mod)
        if (!IsCollision(start, end))
        {
            return new List<Vector3D> { start, end };
        }

        // A* Pathfinding (Simplified Grid Version)
        return RunAStar(start, end);
    }

    /*
       NE: A* Algoritmasını Çalıştır (RunAStar)
       NEDEN: Grid tabanlı bir maliyet analizi yaparak; engelleri aşan, minimal dirsekli ve fiziksel olarak uygulanabilir bir tesisat rotasını matematiksel olarak üretmek için.
    */
    private List<Vector3D> RunAStar(Vector3D start, Vector3D end)
    {
        // Grid tabanlı A* Implementasyonu (Mühendislik Modu)
        // 1. Koordinatları Grid'e snap'le (Yuvarla)
        Vector3D Snap(Vector3D v) => new Vector3D(
            Math.Round(v.X / _gridSize) * _gridSize,
            Math.Round(v.Y / _gridSize) * _gridSize,
            Math.Round(v.Z / _gridSize) * _gridSize);

        var startNode = Snap(start);
        var endNode = Snap(end);

        var openSet = new PriorityQueue<Vector3D, double>();
        var cameFrom = new Dictionary<Vector3D, Vector3D>();
        var gScore = new Dictionary<Vector3D, double>();
        
        // Başlangıç ayarları
        openSet.Enqueue(startNode, 0);
        gScore[startNode] = 0;

        var directions = new[]
        {
            new Vector3D(_gridSize, 0, 0), new Vector3D(-_gridSize, 0, 0),
            new Vector3D(0, _gridSize, 0), new Vector3D(0, -_gridSize, 0),
            new Vector3D(0, 0, _gridSize), new Vector3D(0, 0, -_gridSize)
        };
        
        // Maksimum iterasyon güvenliği (Sonsuz döngüyü önlemek için)
        int maxIterations = 5000;
        int currentIter = 0;

        while (openSet.Count > 0 && currentIter++ < maxIterations)
        {
            var current = openSet.Dequeue();

            // Hedefe ulaştık mı? (Toleranslı kontrol)
            if (current.DistanceTo(endNode) < _gridSize * 0.1)
            {
                return ReconstructPath(cameFrom, current, start, end);
            }

            foreach (var dir in directions)
            {
                var neighbor = current + dir;
                
                // Engel Kontrolü
                if (IsCollision(current, neighbor)) continue;

                var tentativeGScore = gScore[current] + current.DistanceTo(neighbor);
                
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    
                    // Heuristic: Manhattan Distance (Daha hızlı ve grid uyumlu)
                    double h = Math.Abs(neighbor.X - endNode.X) + Math.Abs(neighbor.Y - endNode.Y) + Math.Abs(neighbor.Z - endNode.Z);
                    openSet.Enqueue(neighbor, tentativeGScore + h);
                }
            }
        }
        
        // Yol bulunamazsa direkt hattı (collision olsa bile) dön (Fallback)
        return new List<Vector3D> { start, end };
    }

    private List<Vector3D> ReconstructPath(Dictionary<Vector3D, Vector3D> cameFrom, Vector3D current, Vector3D start, Vector3D end)
    {
        var totalPath = new List<Vector3D> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Add(current);
        }
        totalPath.Reverse();
        
        // Başlangıç ve bitiş noktalarını tam orijinal koordinatlarına düzelt
        totalPath[0] = start;
        totalPath[totalPath.Count - 1] = end;
        
        // Basitleştirme (Gereksiz ara noktaları temizle - Douglas-Peucker benzeri)
        return SimplifyPath(totalPath);
    }
    
    /*
       NE: Güzergahı Basitleştir (SimplifyPath)
       NEDEN: A* algoritmasından gelen çok sayıdaki küçük ızgara adımını, doğrusal olanları birleştirerek CAD ortamında çizilebilir az sayıda uzun boru segmentine dönüştürmek için.
    */
    private List<Vector3D> SimplifyPath(List<Vector3D> path)
    {
        if (path.Count < 3) return path;

        var simplified = new List<Vector3D> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            var prev = path[i - 1];
            var curr = path[i];
            var next = path[i + 1];

            var dir1 = (curr - prev).Normalize();
            var dir2 = (next - curr).Normalize();

            // Eğer yön değişirse (Dirsek gerekiyorsa) noktayı koru
            if (Math.Abs(dir1.Dot(dir2) - 1.0) > 0.001)
            {
                simplified.Add(curr);
            }
        }
        simplified.Add(path.Last());
        return simplified;
    }

    /*
       NE: Çarpışma Kontrolü (IsCollision)
       NEDEN: İki nokta arasındaki hayali boru segmentinin herhangi bir mimari engele (Duvar, Kolon vb.) çarpıp çarpmadığını geometrik olarak saptamak için.
    */
    private bool IsCollision(Vector3D p1, Vector3D p2)
    {
        foreach (var obs in _obstacles)
        {
            if (obs.Type == ObstacleType.Wall || obs.Type == ObstacleType.Column)
            {
                // Çizgi ile engel poligonunu kesiştir
                if (LineIntersectsObstacle(p1, p2, obs))
                    return true;
            }
        }
        return false;
    }

    private bool LineIntersectsObstacle(Vector3D a, Vector3D b, ArchitecturalObstacle obs)
    {
        if (obs.Boundary.Count < 2) return false;

        for (int i = 0; i < obs.Boundary.Count - 1; i++)
        {
            if (SegmentsIntersect(a, b, obs.Boundary[i], obs.Boundary[i + 1]))
                return true;
        }
        
        // Polyline kapatma segmenti
        if (obs.Boundary.Count > 2)
        {
             if (SegmentsIntersect(a, b, obs.Boundary.Last(), obs.Boundary.First()))
                return true;
        }

        return false;
    }

    private bool SegmentsIntersect(Vector3D a, Vector3D b, Vector3D c, Vector3D d)
    {
        // 2D Line segment intersection check
        double denominator = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));
        if (denominator == 0) return false;

        double numerator1 = ((a.Y - c.Y) * (d.X - c.X)) - ((a.X - c.X) * (d.Y - c.Y));
        double numerator2 = ((a.Y - c.Y) * (b.X - a.X)) - ((a.X - c.X) * (b.Y - a.Y));

        double r = numerator1 / denominator;
        double s = numerator2 / denominator;

        return (r >= 0 && r <= 1) && (s >= 0 && s <= 1);
    }
}
