using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Akıllı Yol Bulma Servisi (PathfindingService)
    NEDEN: Boru rotalaması sırasında duvar, kapı ve diğer mimari engellerden (Obstacles) kaçınan en kısa
           ve mühendislik açısından uygun yolu hesaplamak için.
    
    NASIL (Mühendislik Modu — Segment-Segment Intersection + Recursive Bypassing):
    1. Kaynak ve hedef arasında doğrudan bir yol test edilir.
    2. Engel varsa, engelin 4 köşesine (Clearance eklenmiş) bypass noktaları hesaplanır.
    3. En düşük toplam mesafeyi veren bypass rotası seçilir.
    4. Rekürsif olarak yeni segmentlerdeki engeller de kontrol edilir.
    5. Tüm çarpışma testleri gerçek Segment-Segment kesişim algoritmasıyla yapılır (SAT/Parametrik).
*/
public class PathfindingService
{
    private readonly List<ArchitecturalObstacle> _obstacles;
    private const double GridSize = 250.0;       // 25cm çözünürlük
    private const double PipeClearance = 100.0;   // Boru dış çeperi ve duvar arası pay (mm)
    private const int MaxRecursionDepth = 8;      // Sonsuz döngü koruması

    // NE/NEDEN: Bkz. PipingPathfinderService — aynı broad-phase grid-hash deseni, aynı
    // "lazy rebuild sadece liste değiştiyse" stratejisi. FindFirstBlockingObstacle ve
    // IsPointInsideAnyObstacle eskiden HER çağrıda _obstacles'ı tam taramaktaydı.
    private ObstacleSpatialIndex? _spatialIndex;

    private ObstacleSpatialIndex GetSpatialIndex()
    {
        if (_spatialIndex == null || _spatialIndex.IsStaleFor(_obstacles))
            _spatialIndex = new ObstacleSpatialIndex(_obstacles);
        return _spatialIndex;
    }

    public PathfindingService(List<ArchitecturalObstacle> obstacles)
    {
        _obstacles = obstacles ?? new List<ArchitecturalObstacle>();
    }

    public List<Vector3D> FindPath(Vector3D start, Vector3D end)
    {
        var path = new List<Vector3D> { start };
        FindSubPath(start, end, path, 0, new HashSet<int>());
        if (!path.Contains(end)) path.Add(end);
        return CleanPath(path);
    }

    /*
       NE: Rekürsif Alt-Yol Bulma
       NEDEN: Doğrudan gidilemeyen segmentlerde engelleri tespit edip en kısa bypass rotasını hesaplamak.
    */
    private void FindSubPath(Vector3D current, Vector3D target, List<Vector3D> path, int depth, HashSet<int> avoidedObstacles)
    {
        if (depth > MaxRecursionDepth) return;

        // Segment-Segment kesişim ile engel bul
        var (obstacle, obstacleIndex) = FindFirstBlockingObstacle(current, target, avoidedObstacles);

        if (obstacle == null)
        {
            // Engel yok — doğrudan hedefe gidilebilir
            return;
        }

        // Engelin genişletilmiş BoundingBox'ından bypass noktaları hesapla
        var box = obstacle.GetBoundingBox();
        double cx = PipeClearance;

        var bypassCandidates = new List<Vector3D>
        {
            new(box.Min.X - cx, box.Min.Y - cx, 0), // Sol-Alt
            new(box.Max.X + cx, box.Min.Y - cx, 0), // Sağ-Alt
            new(box.Max.X + cx, box.Max.Y + cx, 0), // Sağ-Üst
            new(box.Min.X - cx, box.Max.Y + cx, 0), // Sol-Üst
        };

        // Her bypass noktası için maliyet hesapla ve en iyisini seç
        double bestCost = double.MaxValue;
        Vector3D? bestBypass = null;
        int bestRoute = -1;

        for (int i = 0; i < bypassCandidates.Count; i++)
        {
            var bp = bypassCandidates[i];
            double cost = current.DistanceTo(bp) + bp.DistanceTo(target);

            // Bypass noktası başka bir engelin içinde mi?
            if (IsPointInsideAnyObstacle(bp))
            {
                cost += 100000; // Çok yüksek ceza
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                bestBypass = bp;
                bestRoute = i;
            }
        }

        if (bestBypass == null) return;

        // İki bypass noktası üzerinden L-şekli rota (90° dönüşler tercih)
        // Engelin 2 kenarından geçen rota daha gerçekçi
        var secondBypass = bypassCandidates[(bestRoute + 1) % 4];
        double costWith2 = current.DistanceTo(bestBypass.Value) + bestBypass.Value.DistanceTo(secondBypass) + secondBypass.DistanceTo(target);
        double costWith1 = current.DistanceTo(bestBypass.Value) + bestBypass.Value.DistanceTo(target);

        var newAvoided = new HashSet<int>(avoidedObstacles) { obstacleIndex };

        if (costWith2 < costWith1 * 1.3) // 2-noktalı rota çok daha pahalı değilse tercih et
        {
            path.Add(bestBypass.Value);
            FindSubPath(bestBypass.Value, secondBypass, path, depth + 1, newAvoided);
            path.Add(secondBypass);
            FindSubPath(secondBypass, target, path, depth + 1, newAvoided);
        }
        else
        {
            path.Add(bestBypass.Value);
            FindSubPath(bestBypass.Value, target, path, depth + 1, newAvoided);
        }
    }

    /*
       NE: İlk Engelleyen Engeli Bul (Segment-Segment Intersection)
       NEDEN: Gerçek çizgi-kutu çarpışma testleri ile doğru engel tespiti yapmak.
    */
    private (ArchitecturalObstacle? obstacle, int index) FindFirstBlockingObstacle(Vector3D p1, Vector3D p2, HashSet<int> avoidedObstacles)
    {
        double minDist = double.MaxValue;
        ArchitecturalObstacle? closest = null;
        int closestIdx = -1;

        if (_obstacles.Count == 0) return (null, -1);

        // Broad-phase: AABB-AABB kesişim simetriktir, yani "obstacle kutusu PipeClearance kadar
        // genişletilmiş ve segment kutusuyla kesişiyor mu" testi, "segment kutusu PipeClearance
        // kadar genişletilmiş ve (genişletilmemiş) obstacle kutusuyla kesişiyor mu" testiyle
        // matematiksel olarak birebir eşdeğerdir. Bu sayede indeksteki obstacle kutuları
        // değişmeden, sadece sorgu kutusu genişletilerek doğru aday kümesi elde edilir.
        var segBox = new CadBoundingBox(
            new Vector3D(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), 0),
            new Vector3D(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y), 0)).Expand(PipeClearance);

        foreach (int i in GetSpatialIndex().QueryIndices(segBox))
        {
            if (avoidedObstacles.Contains(i)) continue;

            var box = _obstacles[i].GetBoundingBox();
            // Narrow-phase: Clearance eklenmiş kutu ile gerçek (Liang-Barsky) segment testi
            var expandedBox = new CadBoundingBox(
                new Vector3D(box.Min.X - PipeClearance, box.Min.Y - PipeClearance, 0),
                new Vector3D(box.Max.X + PipeClearance, box.Max.Y + PipeClearance, 0));

            if (SegmentIntersectsAABB(p1, p2, expandedBox))
            {
                double dist = p1.DistanceTo(new Vector3D(
                    (box.Min.X + box.Max.X) / 2, (box.Min.Y + box.Max.Y) / 2, 0));
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = _obstacles[i];
                    closestIdx = i;
                }
            }
        }

        return (closest, closestIdx);
    }

    /*
       NE: Segment-AABB Çarpışma Testi (Cohen-Sutherland / Liang-Barsky Parametrik)
       NEDEN: Orta-nokta kontrolü yerine, çizginin Axis-Aligned Bounding Box ile gerçek kesişimini test eder.
       
       ALGORİTMA: Liang-Barsky parametrik çizgi kırpma
       - t parametresi [0, 1] aralığında: p1 + t*(p2-p1)
       - Kutunun 4 kenarı için t değerleri hesaplanır
       - tEnter ve tExit aralığı [0,1] ile kesişiyorsa çarpışma vardır
    */
    private bool SegmentIntersectsAABB(Vector3D p1, Vector3D p2, CadBoundingBox box)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;

        double tMin = 0.0;
        double tMax = 1.0;

        // X ekseni (Sol ve Sağ kenarlar)
        if (Math.Abs(dx) < 1e-9)
        {
            // Çizgi dikeyse, X range'de olmalı
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

        // Y ekseni (Alt ve Üst kenarlar)
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

        return true; // Kesişim var
    }

    /*
       NE: Nokta Engel İçinde Mi?
       NEDEN: Bypass noktalarının başka bir engelin içinde kalmamasını garanti etmek.
    */
    private bool IsPointInsideAnyObstacle(Vector3D point)
    {
        if (_obstacles.Count == 0) return false;

        // Broad-phase: hücre-hash sadece GÜVENLİ BİR ÜST KÜME (aynı hücreyi paylaşan iki AABB
        // her zaman GERÇEKTEN kesişiyor demek DEĞİLDİR — bkz. ObstacleSpatialIndex yorumu).
        // Bu yüzden adaylar üzerinde narrow-phase olarak ORİJİNAL tam AABB-genişletilmiş-kutu
        // içerme testi (aşağıda) MUTLAKA tekrar uygulanır; davranış eskisiyle birebir aynıdır.
        var pointBox = new CadBoundingBox(point, point).Expand(PipeClearance);
        foreach (var idx in GetSpatialIndex().QueryIndices(pointBox))
        {
            var box = _obstacles[idx].GetBoundingBox();
            if (point.X >= box.Min.X - PipeClearance && point.X <= box.Max.X + PipeClearance &&
                point.Y >= box.Min.Y - PipeClearance && point.Y <= box.Max.Y + PipeClearance)
                return true;
        }
        return false;
    }

    /*
       NE: Yol Temizleme
       NEDEN: Çok yakın noktaları birleştirmek ve tekrarları silmek.
    */
    private List<Vector3D> CleanPath(List<Vector3D> path)
    {
        if (path.Count <= 2) return path;

        var cleaned = new List<Vector3D> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i].DistanceTo(cleaned.Last()) > GridSize * 0.25)
                cleaned.Add(path[i]);
        }

        // Son nokta her zaman dahil olsun
        if (cleaned.Last().DistanceTo(path.Last()) > 1.0)
            cleaned.Add(path.Last());

        return cleaned;
    }
}
