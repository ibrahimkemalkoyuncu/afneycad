using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Afney.Cad.Benchmarks;

/*
   NE: PathfindingService.FindPath Ölçümleri (Broad-Phase ObstacleSpatialIndex)
   NEDEN: PathfindingService.FindFirstBlockingObstacle ve IsPointInsideAnyObstacle,
          eskiden A* benzeri rekürsif bypass araması sırasında HER çağrıda _obstacles
          listesinin TAMAMINI doğrusal (O(n)) taradı (bkz. ObstacleSpatialIndex.cs
          içindeki "NE/NEDEN" yorumu). Grid-hash tabanlı broad-phase indekse geçişin
          gerçek kazancını göstermek için, obstacle sayısı arttıkça FindPath maliyetinin
          nasıl ölçeklendiği ölçülür — eğer indeks çalışıyorsa artış doğrusaldan çok
          daha yavaş olmalıdır (start/end civarındaki birkaç engel dışında tüm engeller
          sorgu kutusunun dışında kalıp elenir).

   NASIL: Geniş bir alana (100m x 100m grid) düzenli aralıklarla yerleştirilmiş N adet
          küçük kare (kolon benzeri) engel arasından, sadece BAŞLANGIÇ/HEDEF hattı
          üzerindeki birkaç engeli atlayan gerçekçi bir FindPath çağrısı yapılır.
*/
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 2, iterationCount: 3)]
public class PathfindingBenchmarks
{
    [Params(100, 1000, 5000)]
    public int ObstacleCount;

    private List<ArchitecturalObstacle> _obstacles = new();
    private Vector3D _start;
    private Vector3D _end;

    [GlobalSetup]
    public void Setup()
    {
        _obstacles = new List<ArchitecturalObstacle>(ObstacleCount);

        // Engelleri, başlangıç-hedef doğrusundan UZAKTA (Y > 5000) geniş bir alana yay —
        // böylece FindPath her N için gerçekten "dolaşması gereken" sabit sayıda engelle
        // karşılaşır (yol karmaşıklığı N'e bağlı artmaz), yalnızca broad-phase sorgu
        // maliyetinin N'e göre ölçeklenmesi izole edilmiş olur.
        int side = (int)Math.Ceiling(Math.Sqrt(ObstacleCount));
        double spacing = 4000.0;
        int idx = 0;
        for (int row = 0; row < side && idx < ObstacleCount; row++)
        {
            for (int col = 0; col < side && idx < ObstacleCount; col++, idx++)
            {
                double cx = col * spacing;
                double cy = 20000 + row * spacing; // start/end hattından uzak bölge
                _obstacles.Add(new ArchitecturalObstacle
                {
                    Type = ObstacleType.Column,
                    Boundary = new List<Vector3D>
                    {
                        new(cx, cy, 0),
                        new(cx + 400, cy, 0),
                        new(cx + 400, cy + 400, 0),
                        new(cx, cy + 400, 0),
                    },
                    Height = 3000.0
                });
            }
        }

        // Başlangıç/hedef doğrudan test edilecek gerçek rota, y=0 hattında kalır (engelsiz
        // bölge) — bu senaryo "engelin çoğu sorgu dışında kalmalı" davranışını test eder.
        _start = new Vector3D(0, 0, 0);
        _end = new Vector3D(side * spacing, 0, 0);
    }

    [Benchmark(Description = "FindPath — engellerin çoğu rota dışında (broad-phase eleme)")]
    public List<Vector3D> FindPath_ClearRoute()
    {
        var service = new PathfindingService(_obstacles);
        return service.FindPath(_start, _end);
    }

    [Benchmark(Description = "FindPath — rota engellerin ORTASINDAN geçiyor (bypass hesaplanır)")]
    public List<Vector3D> FindPath_ThroughObstacles()
    {
        var service = new PathfindingService(_obstacles);
        double midY = 20000 + (Math.Sqrt(ObstacleCount) / 2) * 4000.0;
        return service.FindPath(new Vector3D(-2000, midY, 0), new Vector3D((int)Math.Ceiling(Math.Sqrt(ObstacleCount)) * 4000.0 + 2000, midY, 0));
    }
}
