using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.SpatialIndex.Core;
using Afney.Cad.Database.Transactions; // EKLEME
using System.Collections.Concurrent;
using Afney.Cad.Domain.Blocks;

namespace Afney.Cad.Database.Core;

/*
   NE: CAD Veritabanı (CadDatabase)
   NEDEN: Çizimdeki tüm nesnelerin (Entity) ve katmanların (Layer) merkezi deposu olarak işlev görür.

   MÜHENDİSLİK DETAYI (Kemal):
   - Bellek içi (In-Memory) nesne yönetimi sağlar.
   - SPATIAL INDEXING: Büyük çizimlerde performans için QuadTree ile entegre çalışır.
   - Ekleme/Silme işlemlerini olay tabanlı olarak bildirir.
   - Ekleme/Silme işlemlerini olay tabanlı olarak bildirir.
   - THREAD SAFETY: Mekansal indeksleme (QuadTree) erişimlerini kilit (lock) mekanizması ile korur.
*/
public class CadDatabase
{
    // Transaction Manager (Undo/Redo ve İşlem Tarihçesi)
    public TransactionManager TransactionManager { get; } = new();

    private readonly ConcurrentDictionary<Guid, CadEntity> _entities = new();
    private readonly ConcurrentDictionary<string, CadLayer> _layers = new(); // Layer Table
    private QuadTree _spatialIndex;
    private readonly object _indexLock = new(); // Thread Safety Lock
    public string ActiveLayerName { get; set; } = "0"; // Mevcut Katman

    // Event Bus
    public event Action<CadEntity>? EntityAdded;
    public event Action<CadEntity>? EntityRemoved;
    public event Action<CadEntity>? EntityUpdated; // YENİ: Güncelleme olayı
    public event Action? LayerTableChanged;

    /*
       NE: Veritabanı Yapıcı Metodu
       NEDEN: Katman tablosunu başlatır ve mekansal sorgulama için QuadTree indeksini kurar.
    */
    public CadDatabase()
    {
        // Standart "0" katmanını oluştur
        _layers.TryAdd("0", new CadLayer("0") { Color = 0xFFFFFFFF });
        
        // QuadTree Init (Devasa bir çalışma alanı: ±100km)
        _spatialIndex = new QuadTree(new CadBoundingBox(
            new Vector3D(-100000000, -100000000, -1000), 
            new Vector3D(100000000, 100000000, 1000)
        ));
    }

    /*
       NE: Veritabanını Temizle (Clear)
       NEDEN: Yeni bir dosya açıldığında mevcut tüm nesne ve katman verilerini bellekten silmek için.
    */
    public void Clear()
    {
        _entities.Clear();
        _layers.Clear();
        _layers.TryAdd("0", new CadLayer("0") { Color = 0xFFFFFFFF });
        
        // İndeksi sıfırla
        lock (_indexLock)
        {
            _spatialIndex = new QuadTree(new CadBoundingBox(
                new Vector3D(-100000000, -100000000, -1000), 
                new Vector3D(100000000, 100000000, 1000)
            ));
        }
    }

    /*
       NE: Varlık Ekle (AddEntity)
       NEDEN: Yeni bir çizim nesnesini veritabanına kaydetmek, QuadTree indeksine eklemek ve ilgili servisleri (Render, Mekanik Kernel vb.) haberdar etmek için.
    */
    public void AddEntity(CadEntity entity)
    {
        if (_entities.TryAdd(entity.Id, entity))
        {
            lock (_indexLock)
            {
                _spatialIndex.Insert(entity); // İndekse ekle
            }
            EntityAdded?.Invoke(entity);
        }
    }

    /*
       NE: Varlık Sil (RemoveEntity)
       NEDEN: Bir nesneyi ID üzerinden veritabanından ve mekansal indisten kaldırarak çizimden silmek için.
    */
    public void RemoveEntity(Guid id)
    {
        if (_entities.TryRemove(id, out var entity))
        {
            lock (_indexLock)
            {
                _spatialIndex.Remove(entity); // İndeksten sil
            }
            EntityRemoved?.Invoke(entity);
        }
    }
    
    /*
    NE: Nesne Güncelleme
    NEDEN: Bir nesne taşındığında (Move), döndürüldüğünde (Rotate) veya özellikleri değiştiğinde spatial index ve bağlı sistemleri güncellemek için.
    
    NASIL:
    1. Spatial Index'ten eski konumu sil
    2. Yeni konumla tekrar ekle
    3. EntityUpdated event'ini tetikle (MechanicalKernel dinleyecek)
    */
    /*
       NE: Nesne Güncelle (UpdateEntity)
       NEDEN: Bir nesne taşındığında, döndürüldüğünde veya özellikleri değiştiğinde mekansal indeksi (QuadTree) tazelemek ve ilgili servisleri haberdar etmek için.
    */
    public void UpdateEntity(CadEntity entity)
    {
        if (_entities.ContainsKey(entity.Id))
        {
            lock (_indexLock)
            {
                // Önce eski konumdan sil
                _spatialIndex.Remove(entity);
                // Yeni konumla ekle
                _spatialIndex.Insert(entity);
            }
            EntityUpdated?.Invoke(entity);
        }
    }
    
    // NE: Konumsal Sorgulama (Spatial Query)
    /*
       NE: Bölgesel Sorgu (QueryEntities)
       NEDEN: Belirli bir dikdörtgen alan içindeki nesneleri hızlı bir şekilde (QuadTree kullanarak) listelemek için.
    */
    public IEnumerable<CadEntity> QueryEntities(CadBoundingBox range)
    {
        var found = new HashSet<CadEntity>();
        lock (_indexLock)
        {
            _spatialIndex.QueryRange(range, found);
        }
        return found;
    }

    /*
    NE: Kutu ile Nesne Seçimi (Selection By Box)
    NEDEN: AutoCAD'deki Pencere (Window - Mavi) ve Kesişim (Crossing - Yeşil) seçim mantığını uygulamak için.
    
    PARAMETRELER:
    - range: Seçim kutusu sınırları
    - isCrossing: true ise dokunanları da al (Yeşil), false ise sadece tamamen içinde olanları al (Mavi)
    */
    /*
       NE: Kutu ile Nesne Seçimi (SelectByBox)
       NEDEN: AutoCAD'deki Pencere (Mavi) ve Kesişim (Yeşil) seçim mantığını (Crossing/Window) simüle etmek için.
    */
    public IEnumerable<CadEntity> SelectByBox(CadBoundingBox range, bool isCrossing)
    {
        var candidates = QueryEntities(range);
        if (isCrossing)
        {
            // Crossing: BoundingBox range ile kesişen her şey (QuadTree zaten bunu getiriyor)
            return candidates;
        }
        else
        {
            // Window: Tamamı range içinde olmalı
            return candidates.Where(e => range.Contains(e.GetBoundingBox()));
        }
    }

    public IEnumerable<CadEntity> GetAllEntities() => _entities.Values;

    /*
       NE: ID ile Nesne Getir (GetEntity)
       NEDEN: Benzersiz kimlik numarası (GUID) bilinen bir nesnenin referansına hızlıca erişmek için.
    */
    public CadEntity? GetEntity(Guid id)
    {
        _entities.TryGetValue(id, out var entity);
        return entity;
    }

    #region Layer Management
    /*
       NE: Katman Ekle (AddLayer)
       NEDEN: Projeye yeni bir katman (layer) tanımı eklemek için.
    */
    public void AddLayer(CadLayer layer)
    {
        if (_layers.TryAdd(layer.Name, layer))
        {
            LayerTableChanged?.Invoke();
        }
    }

    public CadLayer GetLayer(string name)
    {
        if (_layers.TryGetValue(name, out var layer))
        {
            return layer;
        }
        return _layers["0"]; // Fallback
    }

    public IEnumerable<CadLayer> GetLayers() => _layers.Values;
    #endregion

    #region Selection Management
    /*
    NE: Seçimi Temizle (Clear Selection)
    NEDEN: Yeni bir seçim başlatmadan önce veya ESC ile tüm seçimi bırakmak için.
    */
    public void ClearSelection()
    {
        foreach (var entity in _entities.Values)
        {
            if (entity.IsSelected)
            {
                entity.IsSelected = false;
                EntityUpdated?.Invoke(entity);
            }
        }
    }

    public void Select(CadEntity entity)
    {
        if (_entities.ContainsKey(entity.Id))
        {
            entity.IsSelected = true;
            EntityUpdated?.Invoke(entity);
        }
    }

    public void Deselect(CadEntity entity)
    {
        if (_entities.ContainsKey(entity.Id))
        {
            entity.IsSelected = false;
            EntityUpdated?.Invoke(entity);
        }
    }


    public IEnumerable<CadEntity> GetSelectedEntities() => _entities.Values.Where(e => e.IsSelected);
    #endregion

    #region Block Management (YENİ)
    /*
        NE: Blok Yönetimi
        NEDEN: Kullanıcının oluşturduğu blok tanımlarını (CadBlockRecord) saklamak için.
    */
    private readonly ConcurrentDictionary<string, CadBlockRecord> _blocks = new(StringComparer.OrdinalIgnoreCase);

    /*
       NE: Blok Kaydı Ekle (AddBlock)
       NEDEN: Yeni bir sembol veya vitrifiye blok tanımını (Örn: Lavabo, Klozet) kütüphaneye kaydetmek için.
    */
    public void AddBlock(CadBlockRecord block)
    {
        _blocks.TryAdd(block.Name, block);
    }

    /*
       NE: Blok Tanımı Getir (GetBlock)
       NEDEN: İsmi bilinen bir bloğun (Örn: "LAVABO_STANDART") içeriğine ve çizim verilerine erişmek için.
    */
    public CadBlockRecord? GetBlock(string name)
    {
        _blocks.TryGetValue(name, out var block);
        return block;
    }

    /*
       NE: Tüm Blokları Listele (GetBlocks)
       NEDEN: Blok kütüphanesindeki tüm tanımları (Insert menüsü veya diyaloglar için) döndürmek için.
    */
    public IEnumerable<CadBlockRecord> GetBlocks() => _blocks.Values;
    #endregion
}
