using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Kritik Yol (FindCriticalPath) Çift-Süpürme Testleri
   NEDEN: FindCriticalPath önceden şebekedeki yaprak (derece=1) düğümlerin sadece İLK 4'ünden
          arama yapıyordu ("Performans için max 4 kaynak"). Bir hub'dan 4'ten fazla dal
          çıkan bir şebekede, gerçek en uzun yolun uçları "ilk 4" arasında olmayabilirdi —
          bu durumda kritik yol yanlış (kısa) hesaplanırdı. Bu test tam olarak o senaryoyu
          kurup düzeltmenin gerçekten çalıştığını doğruluyor.
*/
public class CriticalPathDoubleSweepTests
{
    [Fact]
    public void Analyze_HubWithSixBranches_FindsTrueLongestPathEvenWhenNotAmongFirstFourLeaves()
    {
        var db = new CadDatabase();
        var hub = new Vector3D(0, 0, 0);

        // İlk 4 dal KISA (1m) — eski kod bunlarla sınırlı kalırdı.
        db.AddEntity(new PipeEntity(hub, new Vector3D(1000, 0, 0), 25.0));
        db.AddEntity(new PipeEntity(hub, new Vector3D(0, 1000, 0), 25.0));
        db.AddEntity(new PipeEntity(hub, new Vector3D(-1000, 0, 0), 25.0));
        db.AddEntity(new PipeEntity(hub, new Vector3D(0, -1000, 0), 25.0));

        // 5. ve 6. dallar UZUN (5m) — gerçek kritik yol bunların arasında (toplam 10m).
        db.AddEntity(new PipeEntity(hub, new Vector3D(5000, 5000, 0), 25.0));
        db.AddEntity(new PipeEntity(hub, new Vector3D(-5000, -5000, 0), 25.0));

        var svc = new NetworkTopologyAnalysisService(db);
        var result = svc.Analyze();

        // Gerçek çap: iki uzun dal arasındaki mesafe ≈ 7071mm + 7071mm ≈ 14.14m
        // (5000,5000) ve (-5000,-5000) arası hub üzerinden: √(5000²+5000²)*2
        double expectedM = (Math.Sqrt(5000.0 * 5000 + 5000 * 5000) * 2) / 1000.0;
        Assert.Equal(expectedM, result.CriticalPathM, precision: 1);
    }

    [Fact]
    public void Analyze_SimpleTwoLeafChain_FindsCorrectPath()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25.0));
        db.AddEntity(new PipeEntity(new Vector3D(1000, 0, 0), new Vector3D(2500, 0, 0), 25.0));

        var svc = new NetworkTopologyAnalysisService(db);
        var result = svc.Analyze();

        Assert.Equal(2.5, result.CriticalPathM, precision: 1);
        Assert.Equal(2, result.CriticalPathPipes.Count);
    }

    [Fact]
    public void Analyze_MultipleDisconnectedComponents_FindsBestPathAcrossAllComponents()
    {
        var db = new CadDatabase();
        // Bileşen 1: kısa (1m)
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25.0));

        // Bileşen 2 (tamamen ayrı, uzak bir yerde): uzun (8m)
        db.AddEntity(new PipeEntity(new Vector3D(100000, 100000, 0), new Vector3D(108000, 100000, 0), 25.0));

        var svc = new NetworkTopologyAnalysisService(db);
        var result = svc.Analyze();

        Assert.True(result.HasDisconnected);
        Assert.Equal(8.0, result.CriticalPathM, precision: 1); // uzun bileşen kazanmalı
    }
}
