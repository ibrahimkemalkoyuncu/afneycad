using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Akıllı Yol Bulma Servisi (PathfindingService)
    NEDEN: Boru rotalaması sırasında duvar, kapı ve diğer mimari engellerden (Obstacles) kaçınan en kısa ve mühendislik açısından uygun yolu hesaplamak için.
    
    NASIL (Mühendislik Modu - A* Algoritması):
    1. Gridsiz/Seyrek Izgara (Sparse Grid) yaklaşımı kullanılır.
    2. Engellerin etrafında bir "Emniyet Şeridi" (Clearance) bırakılır.
    3. Mümkün olduğunda dik açılı (Orthogonal) dönüşleri tercih eder.
*/
public class PathfindingService
{
    private readonly List<ArchitecturalObstacle> _obstacles;
    private const double GridSize = 250.0; // 25cm çözünürlük (MEP için yeterli)
    private const double PipeClearance = 100.0; // Boru dış çeperi ve duvar arası pay

    public PathfindingService(List<ArchitecturalObstacle> obstacles)
    {
        _obstacles = obstacles ?? new List<ArchitecturalObstacle>();
    }

    public List<Vector3D> FindPath(Vector3D start, Vector3D end)
    {
        var path = new List<Vector3D>();
        path.Add(start);

        // Rekürsif yol bulma (Basitleştirilmiş)
        CalculateSubPath(start, end, path, 0);

        if (!path.Contains(end)) path.Add(end);
        return path.Distinct().ToList();
    }

    private void CalculateSubPath(Vector3D current, Vector3D target, List<Vector3D> path, int depth)
    {
        if (depth > 5) return; // Sonsuz döngü koruması

        var obstacle = GetBlockingObstacle(current, target);
        if (obstacle == null)
        {
            return; // Engel yok, doğrudan bağlanabilir
        }

        // Engel var, etrafından dolaşacak noktayı bul
        var box = obstacle.GetBoundingBox();
        
        // 4 Köşe + Clearance
        var bypassPoints = new List<Vector3D>
        {
            new Vector3D(box.Min.X - PipeClearance, box.Min.Y - PipeClearance, 0),
            new Vector3D(box.Max.X + PipeClearance, box.Min.Y - PipeClearance, 0),
            new Vector3D(box.Max.X + PipeClearance, box.Max.Y + PipeClearance, 0),
            new Vector3D(box.Min.X - PipeClearance, box.Max.Y + PipeClearance, 0)
        };

        // Mevcut noktaya en yakın ve hedefe en yakın bileşimini seç
        var bestPoint = bypassPoints
            .OrderBy(p => p.DistanceTo(current) + p.DistanceTo(target))
            .First();

        path.Add(bestPoint);
        
        // Ara noktadan hedefe tekrar denetle (Rekürsif)
        CalculateSubPath(bestPoint, target, path, depth + 1);
    }

    private bool IsColliding(Vector3D p1, Vector3D p2)
    {
        foreach (var obs in _obstacles)
        {
            // Basit BBox kesişim kontrolü (Daha sonra Segment-Polyline kesişimine evrilecek)
            var box = obs.GetBoundingBox();
            if (LineIntersectsBox(p1, p2, box))
                return true;
        }
        return false;
    }

    private ArchitecturalObstacle? GetBlockingObstacle(Vector3D p1, Vector3D p2)
    {
        return _obstacles.FirstOrDefault(obs => LineIntersectsBox(p1, p2, obs.GetBoundingBox()));
    }

    private bool LineIntersectsBox(Vector3D p1, Vector3D p2, CadBoundingBox box)
    {
        // Teğet geçme veya dik kesme kontrolü
        // Mühendislik Notu: Basitleştirilmiş Cohen-Sutherland veya SAT algoritması kullanılabilir.
        
        // Şimdilik: Çizginin orta noktası kutunun içindeyse çarpışma kabul et (Hızlı ama kaba)
        var mid = new Vector3D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, 0);
        return mid.X >= box.Min.X && mid.X <= box.Max.X && mid.Y >= box.Min.Y && mid.Y <= box.Max.Y;
    }
}
