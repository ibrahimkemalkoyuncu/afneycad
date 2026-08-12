using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Afney.Cad.Benchmarks;

/*
   NE: ClashDetectionService.DetectClashes Ölçümleri (Broad-Phase QuadTree)
   NEDEN: ClashDetectionService, boru-boru ve boru-mimari engel çakışma taramasını
          eskiden çift-döngü (O(n*m)) ile yapıyordu; şimdi her tarama fazı için ayrı
          bir QuadTree kurup broad-phase QueryRange ile aday sayısını daraltıyor
          (bkz. ClashDetectionService.cs "Broad-phase" yorumları). Bu ölçüm, boru
          sayısı arttıkça DetectClashes'in gerçekte nasıl ölçeklendiğini gösterir.

   NASIL: N adet boru, çakışmayacak şekilde paralel ama yakın aralıklarla (X ekseninde
          kaydırılmış) bir ızgaraya yerleştirilir; ek olarak sabit sayıda mimari engel
          eklenir. DetectClashes tüm N için çağrılır.
*/
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 2, iterationCount: 3)]
public class ClashDetectionBenchmarks
{
    [Params(50, 500, 2000)]
    public int PipeCount;

    private List<MechanicalEntity> _entities = new();
    private List<ArchitecturalObstacle> _obstacles = new();

    [GlobalSetup]
    public void Setup()
    {
        _entities = new List<MechanicalEntity>(PipeCount);
        var rnd = new Random(7);

        // Borular birbirinden yeterince uzak paralel hatlar olarak dağıtılır (gerçekçi bir
        // proje planında olduğu gibi çoğu boru birbirinden uzaktır, yalnızca komşular
        // broad-phase QuadTree'de aday olarak eşleşir).
        for (int i = 0; i < PipeCount; i++)
        {
            double y = i * 300.0; // 30cm aralıklı paralel hatlar
            var pipe = new PipeEntity(new Vector3D(0, y, 0), new Vector3D(10000, y, 0), 50.0)
            {
                SystemType = i % 2 == 0 ? MechanicalSystemType.DomesticColdWater : MechanicalSystemType.WasteWater
            };
            _entities.Add(pipe);
        }

        // Sabit sayıda mimari engel (duvar/kolon benzeri), bazı boruları kesecek şekilde.
        _obstacles = new List<ArchitecturalObstacle>();
        for (int i = 0; i < 20; i++)
        {
            double x = rnd.Next(0, 10000);
            _obstacles.Add(new ArchitecturalObstacle
            {
                Type = ObstacleType.Wall,
                Boundary = new List<Vector3D>
                {
                    new(x, -500, 0),
                    new(x + 200, -500, 0),
                    new(x + 200, PipeCount * 300.0 + 500, 0),
                    new(x, PipeCount * 300.0 + 500, 0),
                },
                Height = 3000.0
            });
        }
    }

    [Benchmark(Description = "DetectClashes — boru×boru + boru×mimari engel taraması")]
    public int DetectClashes()
    {
        var service = new ClashDetectionService(_obstacles);
        return service.DetectClashes(_entities).Count;
    }
}
