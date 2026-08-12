using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Afney.Cad.Benchmarks;

/*
   NE: SplineEntity.Tessellate() Cache Ölçümleri
   NEDEN: Tessellate(), her Draw() çağrısında NURBSCurve.Evaluate'i segments+1 kez (O(p^2)
          maliyetli) yeniden hesaplamamak için sonucu _cachedTessellation'da önbelleğe alır
          (bkz. SplineEntity.cs Tessellate() yorumu — "PERFORMANS" notu). Bu ölçüm, soğuk
          (ilk çağrı — NURBS hesaplaması yapılır) ile sıcak (cache hit — sadece referans
          döner) çağrı arasındaki gerçek farkı sayısal olarak gösterir.

   NASIL: Farklı kontrol noktası sayılarına (segments = max(20, count*20) formülüyle
          orantılı) sahip spline'lar için, her benchmark iterasyonunda YENİ bir SplineEntity
          oluşturarak "İlkÇağrı" ölçülür; "CacheHit" ise tek bir paylaşılan örnek üzerinde
          Tessellate()'i tekrar tekrar çağırır (ControlPoints hiç değişmediği için cache
          her seferinde geçerlidir).
*/
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 2, iterationCount: 3)]
public class SplineTessellateBenchmarks
{
    [Params(4, 20, 100)]
    public int ControlPointCount;

    private List<Vector3D> _controlPoints = new();
    private SplineEntity _warmSpline = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(3);
        _controlPoints = new List<Vector3D>(ControlPointCount);
        for (int i = 0; i < ControlPointCount; i++)
            _controlPoints.Add(new Vector3D(i * 100.0, rnd.NextDouble() * 500.0, 0));

        _warmSpline = new SplineEntity(_controlPoints, degree: 3);
        _warmSpline.Tessellate(); // cache'i bir kez ısıt
    }

    [Benchmark(Description = "Tessellate — İLK çağrı (cache yok, NURBS Evaluate hesaplanır)")]
    public int Tessellate_ColdCache()
    {
        var spline = new SplineEntity(_controlPoints, degree: 3);
        return spline.Tessellate().Count;
    }

    [Benchmark(Description = "Tessellate — CACHE HIT (ControlPoints değişmedi)")]
    public int Tessellate_WarmCache()
    {
        return _warmSpline.Tessellate().Count;
    }
}
