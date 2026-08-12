using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.SpatialIndex.Core;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Afney.Cad.Benchmarks;

/*
   NE: QuadTree Uzamsal İndeks Ölçümleri
   NEDEN: Afney.Cad.SpatialIndex.Core.QuadTree, CadDatabase'in seçim/culling/picking
          sorgularının O(n) doğrusal taramadan O(log n)'e indiği iddiasının temel dayanağı
          (bkz. Session geçmişi "spatial index'e bağlandı"). Bu iddia şimdiye kadar hiç
          gerçek ölçümle doğrulanmamıştı.

   NASIL: Dünya sınırları içine rastgele dağıtılmış N adet LineEntity ile bir QuadTree kurulur.
          Insert, sabit boyutlu bir bölgede QueryRange (tipik bir "ekranda görünen alan" sorgusu)
          ve Remove maliyeti N büyüklüğüne göre (100/1.000/10.000) ölçülür.
*/
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 2, iterationCount: 3)]
public class QuadTreeBenchmarks
{
    private static readonly CadBoundingBox WorldBounds = new(
        new Vector3D(-100000, -100000, 0), new Vector3D(100000, 100000, 0));

    [Params(100, 1000, 10000)]
    public int N;

    private List<LineEntity> _entities = new();
    private QuadTree _prebuilt = new(WorldBounds);
    private CadBoundingBox _queryRange;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(42);
        _entities = new List<LineEntity>(N);
        for (int i = 0; i < N; i++)
        {
            double x = rnd.NextDouble() * 200000 - 100000;
            double y = rnd.NextDouble() * 200000 - 100000;
            _entities.Add(new LineEntity(new Vector3D(x, y, 0), new Vector3D(x + 500, y + 500, 0)));
        }

        _prebuilt = new QuadTree(WorldBounds);
        foreach (var e in _entities) _prebuilt.Insert(e);

        // Sabit boyutlu "ekran görünümü" sorgusu: dünyanın ~%1'lik bir bölgesi.
        _queryRange = new CadBoundingBox(new Vector3D(-10000, -10000, 0), new Vector3D(10000, 10000, 0));
    }

    [Benchmark(Description = "Insert N entity (boş QuadTree'ye)")]
    public QuadTree Insert()
    {
        var tree = new QuadTree(WorldBounds);
        foreach (var e in _entities) tree.Insert(e);
        return tree;
    }

    [Benchmark(Description = "QueryRange (sabit ~%1 bölge, N'e göre ölçekleniyor)")]
    public int QueryRange()
    {
        var found = new HashSet<Domain.Abstractions.CadEntity>();
        _prebuilt.QueryRange(_queryRange, found);
        return found.Count;
    }

    [Benchmark(Description = "Remove (tüm entity'leri sırayla sil)")]
    public QuadTree Remove()
    {
        var tree = new QuadTree(WorldBounds);
        foreach (var e in _entities) tree.Insert(e);
        foreach (var e in _entities) tree.Remove(e);
        return tree;
    }
}
