using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı Boru Yönlendirme Servisi (PipingRoutingService)
   NEDEN: İki nokta arasında (Örn: Lavabo - Kolon) duvarları ve diğer engelleri algılayarak en uygun boru rotasını otomatik oluşturmak için.

   NASIL (Mühendislik Detayı — A* Grid Pathfinding):
   1. Çizim alanını geçici bir ızgaraya (Grid) böler.
   2. Obstacle Mapping: Walls (Duvarlar) ve Kolonlar grid üzerinde "Geçilemez" olarak işaretlenir.
   3. A* Algorithm: Başlangıç ve bitiş arasında maliyet hesabı (Mesafe + Dönüş cezası) yaparak rotayı belirler.
   4. Path Smoothing: Zikzakları temizleyerek düz hatlara dönüştürür.
   5. Orthogonal Preference: Mümkün olduğunda 90° açılı dönüşleri tercih eder.
*/
public class PipingRoutingService
{
    private readonly CadDatabase _database;
    private const double GridSize = 200.0;       // 20cm ızgara çözünürlüğü (MEP için ideal)
    private const double PipeClearance = 75.0;    // Boru ve engel arası emniyet mesafesi (mm)
    private const double TurnPenalty = 2.0;       // Dönüş maliyeti çarpanı (düz hat tercih edilsin)

    public PipingRoutingService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: A* ile Rota Bul (FindRoute)
       AMACI: p1 ve p2 arasında engelleri aşan, mühendislik açısından uygun boru noktaları listesi döndürür.
    */
    public List<Vector3D> FindRoute(Vector3D start, Vector3D end)
    {
        // 1. Engelleri topla
        var obstacles = GetRelevantObstacles(start, end);

        // 2. Engellerin BoundingBox'larını çıkar (Clearance ekleyerek)
        var blockedBoxes = obstacles.Select(o =>
        {
            var bb = o.GetBoundingBox();
            return new CadBoundingBox(
                new Vector3D(bb.Min.X - PipeClearance, bb.Min.Y - PipeClearance, 0),
                new Vector3D(bb.Max.X + PipeClearance, bb.Max.Y + PipeClearance, 0));
        }).ToList();

        // 3. A* Grid Pathfinding
        var rawPath = AStarSearch(start, end, blockedBoxes);

        // 4. Yol düzeltme (Smoothing) — gereksiz ara noktaları temizle
        var smoothed = SmoothPath(rawPath, blockedBoxes);

        return smoothed;
    }

    /*
       NE: A* Arama Algoritması
       NEDEN: Engeller arasında en kısa ve en az dönüşlü yolu bulmak için.
       
       ALGORİTMA:
       - Open Set: Keşfedilecek düğümler (Priority Queue benzeri)
       - Closed Set: Zaten değerlendirilmiş düğümler
       - g(n): Başlangıçtan n'ye maliyet
       - h(n): n'den hedefe tahmini maliyet (Manhattan Distance)
       - f(n) = g(n) + h(n)
    */
    private List<Vector3D> AStarSearch(Vector3D start, Vector3D end, List<CadBoundingBox> blockedBoxes)
    {
        // Grid koordinatlarına dönüştür
        var startNode = SnapToGrid(start);
        var endNode = SnapToGrid(end);

        // Open ve Closed setleri
        var openSet = new SortedSet<AStarNode>(new AStarNodeComparer());
        var closedSet = new HashSet<(int, int)>();
        var cameFrom = new Dictionary<(int, int), AStarNode>();
        var gScores = new Dictionary<(int, int), double>();

        var startKey = ToGridKey(startNode);
        var endKey = ToGridKey(endNode);

        var startANode = new AStarNode
        {
            Position = startNode,
            GridKey = startKey,
            GScore = 0,
            HScore = ManhattanDistance(startNode, endNode),
            Parent = null,
            Direction = Vector3D.Zero
        };
        startANode.FScore = startANode.GScore + startANode.HScore;

        openSet.Add(startANode);
        gScores[startKey] = 0;

        int maxIterations = 50_000; // Sonsuz döngü koruması
        int iteration = 0;

        // 8 yönlü hareket (Orthogonal + Diyagonal)
        // Ancak MEP tesisatında 90° rotalar tercih edilir, diyagonal ceza alır
        var directions = new (int dx, int dy, double cost, bool isDiagonal)[]
        {
            (1, 0, 1.0, false), (-1, 0, 1.0, false),
            (0, 1, 1.0, false), (0, -1, 1.0, false),
            (1, 1, 1.414, true), (-1, 1, 1.414, true),
            (1, -1, 1.414, true), (-1, -1, 1.414, true)
        };

        while (openSet.Count > 0 && iteration++ < maxIterations)
        {
            var current = openSet.Min!;
            openSet.Remove(current);

            // Hedefe ulaştık mı?
            if (current.GridKey == endKey)
            {
                return ReconstructPath(current, start, end);
            }

            closedSet.Add(current.GridKey);

            foreach (var (dx, dy, baseCost, isDiagonal) in directions)
            {
                var neighborKey = (current.GridKey.Item1 + dx, current.GridKey.Item2 + dy);

                if (closedSet.Contains(neighborKey)) continue;

                var neighborPos = new Vector3D(neighborKey.Item1 * GridSize, neighborKey.Item2 * GridSize, 0);

                // Engel kontrolü
                if (IsBlocked(neighborPos, blockedBoxes)) continue;

                // Maliyet hesabı
                double moveCost = baseCost * GridSize;

                // Dönüş cezası: Yön değiştiyse ek maliyet (Düz hat tercih edilsin)
                var newDirection = new Vector3D(dx, dy, 0);
                if (current.Direction != Vector3D.Zero && !DirectionsEqual(current.Direction, newDirection))
                {
                    moveCost *= TurnPenalty;
                }

                // Diyagonal ceza (tesisatçılar 90° tercih eder)
                if (isDiagonal) moveCost *= 1.5;

                double tentativeG = current.GScore + moveCost;

                if (gScores.TryGetValue(neighborKey, out double existingG) && tentativeG >= existingG)
                    continue;

                gScores[neighborKey] = tentativeG;

                var neighborNode = new AStarNode
                {
                    Position = neighborPos,
                    GridKey = neighborKey,
                    GScore = tentativeG,
                    HScore = ManhattanDistance(neighborPos, endNode),
                    Parent = current,
                    Direction = newDirection
                };
                neighborNode.FScore = neighborNode.GScore + neighborNode.HScore;

                openSet.Add(neighborNode);
            }
        }

        // A* yol bulamadıysa fallback: Basit Orthogonal L rotası
        Serilog.Log.Warning("[PipingRouting] A* yol bulamadı, basit L-rota kullanılıyor.");
        return FallbackOrthogonalRoute(start, end);
    }

    /*
       NE: Yol Düzeltme (Path Smoothing)
       NEDEN: A* ızgara bazlı çalıştığı için merdiven gibi zikzak kırıklar üretir.
       Bu, görsel amaçlarla ve keskin dönüşlerin azaltılması ile düz boru segmentlerine dönüştürülür.
    */
    private List<Vector3D> SmoothPath(List<Vector3D> path, List<CadBoundingBox> blockedBoxes)
    {
        if (path.Count <= 2) return path;

        var smoothed = new List<Vector3D> { path[0] };
        int current = 0;

        while (current < path.Count - 1)
        {
            // En uzağa doğrudan gidebilecek noktayı bul (Line-of-Sight)
            int furthest = current + 1;
            for (int i = path.Count - 1; i > current + 1; i--)
            {
                if (!IsLineBlocked(path[current], path[i], blockedBoxes))
                {
                    furthest = i;
                    break;
                }
            }

            smoothed.Add(path[furthest]);
            current = furthest;
        }

        return smoothed;
    }

    /*
       NE: Orthogonalize Path
       NEDEN: Düzeltilmiş yolda diyagonal çizgiler varsa bunları 90° L-dönüşlerine çevirmek.
    */
    public List<Vector3D> OrthogonalizePath(List<Vector3D> path)
    {
        if (path.Count <= 1) return path;

        var result = new List<Vector3D> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            var prev = result.Last();
            var curr = path[i];

            // Eğer diyagonal ise L-dönüşü ekle
            if (Math.Abs(curr.X - prev.X) > GridSize * 0.5 && Math.Abs(curr.Y - prev.Y) > GridSize * 0.5)
            {
                // X önce, Y sonra L-dönüşü
                result.Add(new Vector3D(curr.X, prev.Y, prev.Z));
            }

            result.Add(curr);
        }

        return result;
    }

    // --- Yardımcı Metodlar ---

    private Vector3D SnapToGrid(Vector3D p)
        => new Vector3D(Math.Round(p.X / GridSize) * GridSize, Math.Round(p.Y / GridSize) * GridSize, 0);

    private (int, int) ToGridKey(Vector3D p)
        => ((int)Math.Round(p.X / GridSize), (int)Math.Round(p.Y / GridSize));

    private double ManhattanDistance(Vector3D a, Vector3D b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private bool DirectionsEqual(Vector3D a, Vector3D b)
        => Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;

    private bool IsBlocked(Vector3D point, List<CadBoundingBox> blockedBoxes)
    {
        foreach (var box in blockedBoxes)
        {
            if (point.X >= box.Min.X && point.X <= box.Max.X &&
                point.Y >= box.Min.Y && point.Y <= box.Max.Y)
                return true;
        }
        return false;
    }

    private bool IsLineBlocked(Vector3D p1, Vector3D p2, List<CadBoundingBox> blockedBoxes)
    {
        // Çizgi boyunca numune al ve engel kontrolü yap
        double dist = p1.DistanceTo(p2);
        int samples = Math.Max(2, (int)(dist / (GridSize * 0.5)));
        for (int i = 0; i <= samples; i++)
        {
            double t = (double)i / samples;
            var sample = new Vector3D(
                p1.X + t * (p2.X - p1.X),
                p1.Y + t * (p2.Y - p1.Y), 0);
            if (IsBlocked(sample, blockedBoxes)) return true;
        }
        return false;
    }

    private List<Vector3D> ReconstructPath(AStarNode endNode, Vector3D realStart, Vector3D realEnd)
    {
        var path = new List<Vector3D>();
        var current = endNode;
        while (current != null)
        {
            path.Add(current.Position);
            current = current.Parent;
        }
        path.Reverse();

        // Gerçek başlangıç ve bitiş noktalarını geri koy (Grid snap düzeltmesi)
        if (path.Count > 0) path[0] = realStart;
        if (path.Count > 1) path[^1] = realEnd;

        return path;
    }

    private List<Vector3D> FallbackOrthogonalRoute(Vector3D start, Vector3D end)
    {
        var route = new List<Vector3D> { start };
        var mid = new Vector3D(end.X, start.Y, start.Z);
        route.Add(mid);
        route.Add(end);
        return route;
    }

    /*
       NE: Engel Analizi (AnalyzeObstacles)
       NEDEN: Duvarların içinden geçmemek, ancak yanından paralel gitmek için.
    */
    public List<ArchitecturalObstacle> GetRelevantObstacles(Vector3D start, Vector3D end)
    {
        var archService = new ArchitecturalRecognitionService(_database);
        return archService.RecognizeObstacles();
    }

    // --- A* İç Veri Yapıları ---

    private class AStarNode
    {
        public Vector3D Position { get; set; } = Vector3D.Zero;
        public (int, int) GridKey { get; set; }
        public double GScore { get; set; }
        public double HScore { get; set; }
        public double FScore { get; set; }
        public AStarNode? Parent { get; set; }
        public Vector3D Direction { get; set; } = Vector3D.Zero;
    }

    private class AStarNodeComparer : IComparer<AStarNode>
    {
        public int Compare(AStarNode? a, AStarNode? b)
        {
            if (a == null || b == null) return 0;
            int cmp = a.FScore.CompareTo(b.FScore);
            if (cmp != 0) return cmp;
            // Tie-breaker: GridKey ile sıralama (SortedSet duplicate'e izin vermez)
            cmp = a.GridKey.Item1.CompareTo(b.GridKey.Item1);
            if (cmp != 0) return cmp;
            return a.GridKey.Item2.CompareTo(b.GridKey.Item2);
        }
    }
}
