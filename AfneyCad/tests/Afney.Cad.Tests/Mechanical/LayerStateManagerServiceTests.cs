using System.Collections.Generic;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: LayerStateManagerService Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 mimari denetiminde bulundu): Rapor "Layer State Manager:
          katman görünürlüğü persist ediliyor, isimlendirilmiş çoklu-state yönetimi yok" olarak
          işaretlemişti — eski mekanizma (MainWindow.FileOps.cs: SaveLayerState/LoadLayerState)
          sadece tek, isimsiz bir görünürlük listesi tutuyordu. Bu testler yeni servisin
          Save/Apply/Delete akışını ve Frozen/Locked/Visible bayraklarının doğru
          yakalandığını/geri yüklendiğini doğrular.
*/
public class LayerStateManagerServiceTests
{
    private static CadDatabase MakeDbWithLayers(params (string name, bool frozen, bool locked)[] layers)
    {
        var db = new CadDatabase();
        foreach (var (name, frozen, locked) in layers)
        {
            db.AddLayer(new CadLayer(name) { IsFrozen = frozen, IsLocked = locked });
        }
        return db;
    }

    [Fact]
    public void SaveCurrentState_CapturesVisibleFrozenLockedForEveryLayer()
    {
        var db = MakeDbWithLayers(("Duvar", false, false), ("Elektrik", true, false), ("Sıhhi", false, true));
        var hidden = new HashSet<string> { "Elektrik" };
        var svc = new LayerStateManagerService();

        var snapshot = svc.SaveCurrentState("Görünüm A", db, hidden);

        // CadDatabase kurucusu varsayılan "0" katmanını otomatik ekler -> 3 eklenen + 1 varsayılan.
        Assert.Equal(4, snapshot.Layers.Count);
        Assert.True(snapshot.Layers["Duvar"].Visible);
        Assert.False(snapshot.Layers["Duvar"].Frozen);
        Assert.False(snapshot.Layers["Duvar"].Locked);

        Assert.False(snapshot.Layers["Elektrik"].Visible); // hidden set'te
        Assert.True(snapshot.Layers["Elektrik"].Frozen);

        Assert.True(snapshot.Layers["Sıhhi"].Visible);
        Assert.True(snapshot.Layers["Sıhhi"].Locked);
    }

    [Fact]
    public void SaveCurrentState_SameName_OverwritesInsteadOfDuplicating()
    {
        var db = MakeDbWithLayers(("Duvar", false, false));
        var hidden = new HashSet<string>();
        var svc = new LayerStateManagerService();

        svc.SaveCurrentState("Görünüm A", db, hidden);
        hidden.Add("Duvar");
        svc.SaveCurrentState("Görünüm A", db, hidden); // aynı isim -> güncelle

        Assert.Single(svc.Snapshots);
        Assert.False(svc.Find("Görünüm A")!.Layers["Duvar"].Visible);
    }

    [Fact]
    public void ApplyState_RestoresVisibleFrozenLocked()
    {
        var db = MakeDbWithLayers(("Duvar", false, false), ("Elektrik", false, false));
        var hidden = new HashSet<string>();
        var svc = new LayerStateManagerService();

        // Elektrik'i gizli+dondurulmuş+kilitli olarak kaydet.
        hidden.Add("Elektrik");
        db.GetLayer("Elektrik")!.IsFrozen = true;
        db.GetLayer("Elektrik")!.IsLocked = true;
        var saved = svc.SaveCurrentState("Elektrik Kapalı", db, hidden);

        // Durumu değiştir (Elektrik'i tekrar görünür/açık yap).
        hidden.Remove("Elektrik");
        db.GetLayer("Elektrik")!.IsFrozen = false;
        db.GetLayer("Elektrik")!.IsLocked = false;

        // Kaydedilen state'i geri uygula.
        svc.ApplyState(saved, db, hidden);

        Assert.Contains("Elektrik", hidden);
        Assert.True(db.GetLayer("Elektrik")!.IsFrozen);
        Assert.True(db.GetLayer("Elektrik")!.IsLocked);
    }

    [Fact]
    public void ApplyState_LayerNoLongerInDatabase_IsSkippedSilently()
    {
        var db = MakeDbWithLayers(("Duvar", false, false));
        var hidden = new HashSet<string>();
        var svc = new LayerStateManagerService();
        var saved = svc.SaveCurrentState("Eski Durum", db, hidden);

        // Katmanı veritabanından tamamen kaldırmanın bir yolu yoksa (CadDatabase katman silmeyi
        // desteklemeyebilir), snapshot'a manuel olarak var olmayan bir katman ekleyelim.
        saved.Layers["SilinmisKatman"] = new LayerStateManagerService.LayerFlags { Visible = false };

        // Exception atmadan uygulanabilmeli.
        var ex = Record.Exception(() => svc.ApplyState(saved, db, hidden));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_ExistingState_RemovesItAndReturnsTrue()
    {
        var db = MakeDbWithLayers(("Duvar", false, false));
        var svc = new LayerStateManagerService();
        svc.SaveCurrentState("Görünüm A", db, new HashSet<string>());

        bool deleted = svc.Delete("Görünüm A");

        Assert.True(deleted);
        Assert.Empty(svc.Snapshots);
    }

    [Fact]
    public void Delete_NonExistentState_ReturnsFalse()
    {
        var svc = new LayerStateManagerService();
        Assert.False(svc.Delete("Yok Böyle Bir Şey"));
    }

    [Fact]
    public void ToJson_ThenLoadFromJson_RoundTripsAllSnapshots()
    {
        var db = MakeDbWithLayers(("Duvar", false, false), ("Elektrik", true, true));
        var hidden = new HashSet<string> { "Elektrik" };
        var svc = new LayerStateManagerService();
        svc.SaveCurrentState("Görünüm A", db, hidden);
        svc.SaveCurrentState("Görünüm B", db, new HashSet<string>());

        string json = svc.ToJson();

        var restored = new LayerStateManagerService();
        restored.LoadFromJson(json);

        Assert.Equal(2, restored.Snapshots.Count);
        Assert.NotNull(restored.Find("Görünüm A"));
        Assert.NotNull(restored.Find("Görünüm B"));
        Assert.False(restored.Find("Görünüm A")!.Layers["Elektrik"].Visible);
        Assert.True(restored.Find("Görünüm A")!.Layers["Elektrik"].Frozen);
    }

    [Fact]
    public void LoadFromJson_CorruptJson_LeavesManagerEmptyWithoutThrowing()
    {
        var svc = new LayerStateManagerService();
        var ex = Record.Exception(() => svc.LoadFromJson("{ bu gecerli bir json degil"));

        Assert.Null(ex);
        Assert.Empty(svc.Snapshots);
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        var db = MakeDbWithLayers(("Duvar", false, false));
        var svc = new LayerStateManagerService();
        svc.SaveCurrentState("Görünüm A", db, new HashSet<string>());

        Assert.NotNull(svc.Find("görünüm a"));
        Assert.NotNull(svc.Find("GÖRÜNÜM A"));
    }
}
