using System;
using System.IO;
using System.Linq;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: LevelPersistenceService Testleri
   NEDEN — Session #75 iş akışı denetiminde bulunan boşluğun kapatılması: LevelManager'ın kat
          listesi önceden hiçbir yere kaydedilmiyordu, belge kapatılınca kayboluyordu. Bu
          testler sidecar dosyasının gerçekten kat listesini (Id/Name/Elevation/Height/Order/
          IsActive) round-trip ettiğini, sidecar yokken sessizce no-op olduğunu ve bozuk JSON
          karşısında dosya açmayı ASLA engellemediğini kilitler (LayerStatePersistenceService
          ile aynı sözleşme).
*/
public class LevelPersistenceServiceTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), $"afneycad_test_{Guid.NewGuid():N}.dwg");

    public void Dispose()
    {
        string sidecar = LevelPersistenceService.GetSidecarPath(_projectPath);
        if (File.Exists(sidecar)) File.Delete(sidecar);
        string tmp = sidecar + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsLevelNameElevationHeightAndOrder()
    {
        var source = new LevelManager();
        LevelPersistenceService.Save(_projectPath, source);

        var target = new LevelManager();
        target.Clear();
        LevelPersistenceService.Load(_projectPath, target);

        var sourceLevels = source.GetLevels().OrderBy(l => l.Order).ToList();
        var targetLevels = target.GetLevels().OrderBy(l => l.Order).ToList();

        Assert.Equal(sourceLevels.Count, targetLevels.Count);
        for (int i = 0; i < sourceLevels.Count; i++)
        {
            Assert.Equal(sourceLevels[i].Id, targetLevels[i].Id);
            Assert.Equal(sourceLevels[i].Name, targetLevels[i].Name);
            Assert.Equal(sourceLevels[i].Elevation, targetLevels[i].Elevation);
            Assert.Equal(sourceLevels[i].Height, targetLevels[i].Height);
            Assert.Equal(sourceLevels[i].Order, targetLevels[i].Order);
        }
    }

    [Fact]
    public void SaveThenLoad_PreservesActiveFloorFlag()
    {
        var source = new LevelManager();
        var active = source.GetLevels()[1];
        source.SetActiveFloor(active.Id);
        LevelPersistenceService.Save(_projectPath, source);

        var target = new LevelManager();
        LevelPersistenceService.Load(_projectPath, target);

        Assert.Equal(active.Id, target.GetActiveFloor()!.Id);
    }

    [Fact]
    public void Load_SidecarMissing_LeavesManagerUntouched()
    {
        var manager = new LevelManager();
        int before = manager.GetLevels().Count;

        LevelPersistenceService.Load(_projectPath, manager); // dosya hiç oluşturulmadı

        Assert.Equal(before, manager.GetLevels().Count);
    }

    [Fact]
    public void Load_CorruptJson_DoesNotThrowAndKeepsManagerUsable()
    {
        string sidecar = LevelPersistenceService.GetSidecarPath(_projectPath);
        File.WriteAllText(sidecar, "{ bu gecerli json degil");

        var manager = new LevelManager();
        var exception = Record.Exception(() => LevelPersistenceService.Load(_projectPath, manager));

        Assert.Null(exception);
    }
}
