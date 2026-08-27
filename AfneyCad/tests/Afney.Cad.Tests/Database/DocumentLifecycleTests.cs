using System;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Persistence;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Database;

/*
   NE: Doküman / Oturum Yaşam Döngüsü Testleri (Document Lifecycle)
   NEDEN: MainWindow.CreateNewDocument() akışının (yeni proje → içerik ekleme/düzenleme →
          undo/redo → kaydet → aç → temizle/kapat) WPF'siz karşılığını Database/Domain/
          Infrastructure katmanlarında uçtan uca doğrulamak için. MainWindow'un kendisi
          net10.0-windows (WPF) hedefli olduğundan bu Linux ortamında derlenemiyor; bu
          testler MainWindow'un CadDatabase + TransactionManager + CadSerializer üzerinden
          yürüttüğü akışı UI katmanına hiç dokunmadan kanıtlıyor.

   DENETİM BULGUSU (bu test dosyası yazılırken keşfedildi):
   `Afney.Cad.Database.Persistence.CadSerializer` — AutoSaveService, crash-recovery
   (MainWindow.xaml.cs CheckCrashRecovery) ve çok katlı bina (MainWindow.Commands.cs
   OnDefineBuilding) akışlarında kullanılan GERÇEK üretim serileştiricisi — nesne
   (CadEntity) round-trip'ini desteklemiyor: ProjectData.Entities alanı List<CadEntity>
   (soyut taban tip) olarak bildirildiğinden System.Text.Json türetilmiş tip bilgisini
   (StartPoint/EndPoint vb.) serileştirmeden atıyor ve Deserialize soyut CadEntity tipini
   canlandıramadığı için NotSupportedException fırlatıyor. Yani içinde nesne bulunan HER
   proje şu an AutoSave/crash-recovery/multi-story akışında bozuluyor. Bu dosyadaki
   SaveThenOpen_EntitiesViaProductionSerializer_CurrentlyThrowsNotSupportedException testi
   bu MEVCUT (istenmeyen) davranışı kanıtlıyor; üretim kodu bu görev kapsamında
   değiştirilmedi (sadece test ekleme talep edildi) — ama biri bu hatayı düzelttiğinde bu
   test kırılacak ve düzeltmenin fark edilmesini/onaylanmasını sağlayacak. Katman
   (CadLayer) round-trip'i ise sorunsuz çalışıyor (SaveThenOpen_LayersRoundTrip... testi).
*/
public class DocumentLifecycleTests
{
    private static LineEntity MakeLine(double x0 = 0, double y0 = 0) =>
        new(new Vector3D(x0, y0, 0), new Vector3D(x0 + 100, y0, 0));

    // 1) Yeni doküman oluşturma: MainWindow.CreateNewDocument() -> new CadDatabase()
    [Fact]
    public void NewCadDatabase_HasDefaultLayerAndNoEntities()
    {
        var db = new CadDatabase();

        Assert.Empty(db.GetAllEntities());
        Assert.Equal("0", db.ActiveLayerName);

        var layer = Assert.Single(db.GetLayers());
        Assert.Equal("0", layer.Name);
    }

    // 2) İçerik ekleme + event tetiklenmesi
    [Fact]
    public void AddEntity_RaisesEntityAddedEvent_AndEntityBecomesQueryable()
    {
        var db = new CadDatabase();
        var line = MakeLine();

        CadEntityFired? fired = null;
        db.EntityAdded += e => fired = new CadEntityFired(e);

        db.AddEntity(line);

        Assert.NotNull(fired);
        Assert.Same(line, fired!.Entity);
        Assert.Same(line, db.GetEntity(line.Id));
        Assert.Same(line, Assert.Single(db.GetAllEntities()));
    }

    // Basit sarmalayıcı: event delegesinin yakaladığı referansı test gövdesine taşımak için.
    private sealed class CadEntityFired
    {
        public Afney.Cad.Domain.Abstractions.CadEntity Entity { get; }
        public CadEntityFired(Afney.Cad.Domain.Abstractions.CadEntity entity) => Entity = entity;
    }

    // 3) Undo/Redo döngüsü: Ekleme + Taşıma işlemleri TransactionManager üzerinden geri
    //    alınıp yinelenebilmeli (MainWindow.ViewControls.cs OnUndo/OnRedo akışının karşılığı).
    [Fact]
    public void UndoRedo_AddThenMoveEntity_RestoresAndReappliesState()
    {
        var db = new CadDatabase();
        var tm = db.TransactionManager;
        var line = MakeLine();
        var originalStart = line.StartPoint;
        var originalEnd = line.EndPoint;
        var delta = new Vector3D(25, 10, 0);

        tm.Submit(new AddEntityOperation(db, line));
        Assert.Same(line, Assert.Single(db.GetAllEntities()));

        tm.Submit(new MoveEntityOperation(line, delta, db));
        Assert.Equal(originalStart + delta, line.StartPoint);
        Assert.Equal(originalEnd + delta, line.EndPoint);

        // Undo #1: Taşıma geri alınır, nesne hâlâ veritabanında kalmalı.
        tm.Undo();
        Assert.Equal(originalStart, line.StartPoint);
        Assert.Equal(originalEnd, line.EndPoint);
        Assert.Same(line, Assert.Single(db.GetAllEntities()));

        // Undo #2: Ekleme geri alınır, nesne veritabanından silinmeli.
        tm.Undo();
        Assert.Empty(db.GetAllEntities());
        Assert.False(tm.CanUndo);

        // Redo #1: Ekleme yeniden uygulanır.
        tm.Redo();
        Assert.Same(line, Assert.Single(db.GetAllEntities()));
        Assert.Equal(originalStart, line.StartPoint); // Henüz taşıma redo edilmedi.

        // Redo #2: Taşıma yeniden uygulanır.
        tm.Redo();
        Assert.Equal(originalStart + delta, line.StartPoint);
        Assert.Equal(originalEnd + delta, line.EndPoint);
        Assert.False(tm.CanRedo);
    }

    [Fact]
    public void UndoRedo_RemoveEntity_RestoresEntityOnUndo()
    {
        var db = new CadDatabase();
        var tm = db.TransactionManager;
        var line = MakeLine();

        db.AddEntity(line); // Doğrudan ekleme (bu işlem undo geçmişinde değil).
        tm.Submit(new RemoveEntityOperation(db, line));
        Assert.Empty(db.GetAllEntities());

        tm.Undo();
        Assert.Same(line, Assert.Single(db.GetAllEntities()));

        tm.Redo();
        Assert.Empty(db.GetAllEntities());
    }

    // 4a) Kaydet -> Aç round-trip: Katman tablosu (CadLayer), üretimde kullanılan gerçek
    //     CadSerializer (Afney.Cad.Database.Persistence) ile dosyaya yazılıp geri okunuyor.
    [Fact]
    public void SaveThenOpen_LayersRoundTrip_PreservesLayerProperties()
    {
        var db = new CadDatabase();
        db.AddLayer(new CadLayer("TEMIZ_SU") { Color = 0xFF00A2FF, IsVisible = false, IsLocked = true, IsFrozen = true, LineWeight = 0.35 });

        var serializer = new CadSerializer();
        string tempDir = Directory.CreateTempSubdirectory("afneycad_doclifecycle_").FullName;
        string path = Path.Combine(tempDir, "proje.afney");

        try
        {
            // Kaydet (MainWindow.FileOps / AutoSaveService'in izlediği yolun sadeleştirilmiş hâli)
            string json = serializer.Serialize(new ProjectData
            {
                Entities = new(),
                Layers = db.GetLayers().ToList()
            });
            File.WriteAllText(path, json);

            // Aç: Yeni bir doküman (yeni CadDatabase) içine geri yükle.
            string readBackJson = File.ReadAllText(path);
            var data = serializer.Deserialize(readBackJson);

            var reopened = new CadDatabase();
            reopened.Clear(); // Varsayılan "0" katmanıyla temiz başla (yeni dosya açma akışı)

            // "0" katmanı zaten var (AddLayer onu ezmez); diğer katmanları geri yükle.
            foreach (var layer in data.Layers.Where(l => l.Name != "0"))
            {
                reopened.AddLayer(layer);
            }

            Assert.Equal(2, reopened.GetLayers().Count()); // "0" + "TEMIZ_SU"

            var restored = reopened.GetLayer("TEMIZ_SU");
            Assert.Equal("TEMIZ_SU", restored.Name);
            Assert.Equal(0xFF00A2FFu, restored.Color);
            Assert.False(restored.IsVisible);
            Assert.True(restored.IsLocked);
            Assert.True(restored.IsFrozen);
            Assert.Equal(0.35, restored.LineWeight);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // 4b) DENETİM BULGUSU TESTİ: Üretimde kullanılan CadSerializer, içinde nesne (entity)
    //     bulunan bir dokümanı şu an round-trip edemiyor (bkz. dosya başındaki NE/NEDEN notu).
    //     Bu test mevcut davranışı belgeler; asıl amaç ileride birisi bunu düzelttiğinde bu
    //     testin kırılıp fark edilmesini sağlamaktır.
    [Fact]
    public void SaveThenOpen_EntitiesViaProductionSerializer_CurrentlyThrowsNotSupportedException()
    {
        var db = new CadDatabase();
        db.AddEntity(MakeLine());

        var serializer = new CadSerializer();
        string tempDir = Directory.CreateTempSubdirectory("afneycad_doclifecycle_").FullName;
        string path = Path.Combine(tempDir, "proje_nesneli.afney");

        try
        {
            string json = serializer.Serialize(db);
            File.WriteAllText(path, json);

            string readBackJson = File.ReadAllText(path);

            // Bilinen sınırlama: CadEntity soyut olduğu ve JSON tip ayırt edici (discriminator)
            // kaydı olmadığı için deserialize soyut tipi canlandıramıyor.
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(readBackJson));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // 5) Temizle/Kapat: MainWindow'un sekme kapatma / yeni dosya açma akışının temel
    //    varsayımı — Clear() sonrası veritabanı sıfır nesne + tek "0" katmanıyla başlar.
    [Fact]
    public void Clear_AfterPopulatingDatabase_ResetsToFreshDocumentState()
    {
        var db = new CadDatabase();
        db.AddLayer(new CadLayer("PIS_SU"));
        db.AddEntity(MakeLine());
        db.AddEntity(MakeLine(200));
        Assert.Equal(2, db.GetAllEntities().Count());
        Assert.Equal(2, db.GetLayers().Count());

        db.Clear();

        Assert.Empty(db.GetAllEntities());
        var layer = Assert.Single(db.GetLayers());
        Assert.Equal("0", layer.Name);
        Assert.NotNull(db.GetLayer("0"));
    }

    // 6) Çoklu doküman (MDI sekme) izolasyonu: Her sekme kendi CadDatabase örneğine
    //    sahiptir (bkz. MainWindow.CreateNewDocument) — birine yapılan ekleme diğerini
    //    etkilememeli.
    [Fact]
    public void TwoIndependentDatabases_AreFullyIsolated()
    {
        var tab1 = new CadDatabase();
        var tab2 = new CadDatabase();

        tab1.AddEntity(MakeLine());
        tab1.AddLayer(new CadLayer("TAB1_KATMAN"));

        Assert.Single(tab1.GetAllEntities());
        Assert.Empty(tab2.GetAllEntities());

        Assert.Equal(2, tab1.GetLayers().Count()); // "0" + "TAB1_KATMAN"
        Assert.Single(tab2.GetLayers()); // Sadece "0"

        tab2.AddEntity(MakeLine(500));
        Assert.Single(tab1.GetAllEntities());
        Assert.Single(tab2.GetAllEntities());
        Assert.NotEqual(tab1.GetAllEntities().First().Id, tab2.GetAllEntities().First().Id);
    }
}
