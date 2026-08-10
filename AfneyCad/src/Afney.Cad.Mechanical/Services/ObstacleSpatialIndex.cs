using System;
using System.Collections.Generic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Mimari Engeller İçin Hafif Uzamsal İndeks (Broad-Phase Bounding-Box Grid)
   NEDEN: PipingPathfinderService.IsCollision ve PathfindingService.FindFirstBlockingObstacle /
          IsPointInsideAnyObstacle, A* içindeki HER açılan komşu için (max 5000 iterasyon × 6 yön
          = 30.000 komşu) _obstacles listesinin TAMAMINI doğrusal (O(n)) taramaktaydı — büyük
          projelerde AutoBranchingService.ConnectFixturesToPipe her cihaz×port için bu maliyeti
          tekrar tekrar ödüyor, UI'ı kilitleyebiliyordu.

          Afney.Cad.SpatialIndex.QuadTree tam olarak bu amaç için var (bkz. ClashDetectionService),
          ANCAK QuadTree.Insert(CadEntity) imzasıyla SIKI ŞEKİLDE CadEntity'ye bağlı — ve
          ArchitecturalObstacle bir CadEntity DEĞİL, düz bir Model sınıfı (Afney.Cad.Mechanical.
          Models). Onu CadEntity'ye dönüştürmek (Draw/Move/Transform/Clone/GetSnapPoints gibi
          soyut üyeleri sahte doldurmak) gereksiz ve riskli bir refactor olurdu.

          Bu yüzden AYNI DESEN (broad-phase bounding-box filtre + narrow-phase hassas geometri),
          SpaceDetectionEngine.ResolveIntersections'daki SegmentGrid ile BİREBİR AYNI teknikle
          (hücre boyutunda bir Dictionary<(long,long), List<int>> grid-hash) burada da uygulanır.

   NASIL: Her obstacle'ın KENDİ (genişletilmemiş) bounding box'ı kapladığı hücrelere kaydedilir.
          Sorgu sırasında yalnızca test edilen (gerekirse tolerans payı ile genişletilmiş) aralığın
          kapladığı hücrelerdeki adaylar döndürülür. AABB-AABB kesişim testi simetriktir — yani
          "A payı kadar genişletilmiş A, B ile kesişiyor mu" testi "A, B payı kadar genişletilmiş B
          ile kesişiyor mu" testiyle matematiksel olarak birebir eşdeğerdir — bu sayede çağıran
          taraf marjı SORGU kutusuna uygulayabilir, indeksteki obstacle kutuları asla değişmez.
*/
internal sealed class ObstacleSpatialIndex
{
    private readonly double _cellSize;
    private readonly Dictionary<(long, long), List<int>> _cells = new();
    private readonly IReadOnlyList<ArchitecturalObstacle> _obstacles;
    private readonly int _builtForCount;

    public ObstacleSpatialIndex(IReadOnlyList<ArchitecturalObstacle> obstacles, double cellSize = 3000.0)
    {
        _obstacles = obstacles;
        _builtForCount = obstacles.Count;
        _cellSize = Math.Max(cellSize, 1.0);

        for (int i = 0; i < obstacles.Count; i++)
        {
            var box = obstacles[i].GetBoundingBox();
            var (minX, minY, maxX, maxY) = CellRange(box);
            for (long cx = minX; cx <= maxX; cx++)
                for (long cy = minY; cy <= maxY; cy++)
                {
                    if (!_cells.TryGetValue((cx, cy), out var list))
                    {
                        list = new List<int>();
                        _cells[(cx, cy)] = list;
                    }
                    list.Add(i);
                }
        }
    }

    /*
       NE: Bayat mı? (IsStaleFor)
       NEDEN: _obstacles listesi çağıran taraf tarafından yaşam döngüsü boyunca (Add/Clear/
              AddRange ile) mutasyona uğrayabilir (bkz. MechanicalKernel.ArchitecturalObstacles) —
              indeks bir kez kurulup sonsuza dek yeniden kullanılırsa yeni/silinen engeller
              yanlışlıkla dikkate alınmaz. Count karşılaştırması ucuz (O(1)) bir "bayat mı"
              denetimidir; çağıran taraf her FindPath/IsCollision girişinde bunu kontrol edip
              SADECE gerçekten değiştiyse yeniden kurar — 30.000 komşu için TEK bir O(1) kontrol,
              TEK bir (gerekirse) yeniden inşa.
    */
    public bool IsStaleFor(IReadOnlyList<ArchitecturalObstacle> obstacles)
        => !ReferenceEquals(obstacles, _obstacles) || obstacles.Count != _builtForCount;

    private (long MinX, long MinY, long MaxX, long MaxY) CellRange(CadBoundingBox box)
    {
        return ((long)Math.Floor(box.Min.X / _cellSize), (long)Math.Floor(box.Min.Y / _cellSize),
                (long)Math.Floor(box.Max.X / _cellSize), (long)Math.Floor(box.Max.Y / _cellSize));
    }

    /*
       NE: Aday Engel İndekslerini Sorgula (QueryIndices)
       NEDEN: Çağıran taraf (avoidedObstacles gibi) orijinal liste index'ine ihtiyaç duyabildiği
              için (PathfindingService), obstacle referansı yerine _obstacles listesindeki index'i
              döner.

       ÖNEMLİ (grid-hash false-positive tuzağı — QuadTree.QueryRange'den FARKI): Bir hücreyi
       PAYLAŞMAK, iki AABB'nin GERÇEKTEN kesiştiği anlamına gelmez (ör. aynı büyük hücrenin zıt
       köşelerindeki iki küçük kutu, hücreyi paylaşır ama birbirine değmez). QuadTree.QueryRange
       bunu her aday için `Intersects(range, ent.GetBoundingBox())` ile telafi ediyordu — bu
       sınıf da AYNI garantiyi vermek için, hücre-eşleşen adaylar üzerinde gerçek
       `range.Intersects(obstacleBox)` testini here (tek yerde, merkezi olarak) uygular. Bu
       sayede QueryIndices/Query'nin döndürdüğü küme her zaman "range ile GERÇEKTEN kesişen
       obstacle'lar" olur — çağıranların ayrıca bunu doğrulaması gerekmez (narrow-phase'leri
       sadece kendi hassas geometrilerine — segment/polygon, Liang-Barsky vb. — odaklanabilir).
    */
    public IEnumerable<int> QueryIndices(CadBoundingBox range)
    {
        var (minX, minY, maxX, maxY) = CellRange(range);
        var found = new SortedSet<int>();
        for (long cx = minX; cx <= maxX; cx++)
            for (long cy = minY; cy <= maxY; cy++)
                if (_cells.TryGetValue((cx, cy), out var list))
                    foreach (var idx in list)
                    {
                        if (found.Contains(idx)) continue;
                        if (range.Intersects(_obstacles[idx].GetBoundingBox()))
                            found.Add(idx);
                    }
        return found;
    }

    /// <summary>QueryIndices'in obstacle nesnelerine (tekrarsız) çözülmüş kısayolu.</summary>
    public IEnumerable<ArchitecturalObstacle> Query(CadBoundingBox range)
    {
        foreach (var idx in QueryIndices(range))
            yield return _obstacles[idx];
    }
}
