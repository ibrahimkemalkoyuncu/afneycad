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
        
        // QuadTree Init (Devasa bir çalışma alanı: ±1 Trilyon Birim - UTM ve Global Koordinat Desteği)
        _spatialIndex = new QuadTree(new CadBoundingBox(
            new Vector3D(-1000000000000, -1000000000000, -100000000), 
            new Vector3D(1000000000000, 1000000000000, 100000000)
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
                new Vector3D(-1000000000000, -1000000000000, -100000000), 
                new Vector3D(1000000000000, 1000000000000, 100000000)
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
        
        if (!isCrossing)
        {
            // Window (Mavi): Nesnenin BoundingBox'ının TAMAMI range içinde kalmalı
            return candidates.Where(e => range.Contains(e.GetBoundingBox()));
        }

        // Crossing (Yeşil): Kutu içine girmesi VE/VEYA sınırlarıyla kesişmesi yeterli
        var selected = new HashSet<CadEntity>();
        
        foreach (var entity in candidates)
        {
            var bbox = entity.GetBoundingBox();
            
            // Eğer bounding box tamamen içindeyse zaten kesişiyordur
            if (range.Contains(bbox))
            {
                selected.Add(entity);
                continue;
            }
            
            // Geometri bazlı kesişim:
            // Afney.Cad.Database, Mechanical projeye referans vermediği için typeof ile bağımlılık yaratmıyoruz
            // Tüm SnapPoint'lerini çekip, Start/End noktaları üzerinden line-rect kesişim testi yapıyoruz.
            var snaps = entity.GetSnapPoints().ToList();
            var endPoints = snaps.Where(s => s.Type == SnapPointType.Endpoint).ToList();

            if (endPoints.Count >= 2)
            {
                // En az 2 uç noktası olan nesneler (Line, Pipe vb.)
                var p1 = endPoints[0].Position;
                var p2 = endPoints[1].Position;

                if (LineIntersectsRect(p1, p2, range) || range.Contains(p1) || range.Contains(p2))
                {
                    selected.Add(entity);
                    continue;
                }
            }
            
            // Çizgi değilse veya yukarıda bulunamadıysa: BoundingBox kesişimine güven
            if (range.Intersects(bbox))
            {
                selected.Add(entity);
            }
        }
        
        return selected;
    }

    // Yardımcı Geometri: Çizgi-Dikdörtgen Kesişimi (Cohen-Sutherland veya basit sınır testi)
    private bool LineIntersectsRect(Vector3D p1, Vector3D p2, CadBoundingBox rect)
    {
        // Line-Rect intersection
        double minX = rect.Min.X, maxX = rect.Max.X;
        double minY = rect.Min.Y, maxY = rect.Max.Y;
        
        // Find min and max X for the line segment
        double minSegmentX = Math.Min(p1.X, p2.X);
        double maxSegmentX = Math.Max(p1.X, p2.X);

        // Find min and max Y for the line segment
        double minSegmentY = Math.Min(p1.Y, p2.Y);
        double maxSegmentY = Math.Max(p1.Y, p2.Y);
        
        // 1. AABB overlap test first (Fast reject)
        if (maxSegmentX < minX || minSegmentX > maxX || maxSegmentY < minY || minSegmentY > maxY)
            return false;
            
        // 2. Cross product check (if line intersects rect boundaries)
        bool intersectsLine(Vector3D l1, Vector3D l2, Vector3D l3, Vector3D l4)
        {
            double den = (l4.Y - l3.Y) * (l2.X - l1.X) - (l4.X - l3.X) * (l2.Y - l1.Y);
            if (Math.Abs(den) < double.Epsilon) return false;
            
            double ua = ((l4.X - l3.X) * (l1.Y - l3.Y) - (l4.Y - l3.Y) * (l1.X - l3.X)) / den;
            double ub = ((l2.X - l1.X) * (l1.Y - l3.Y) - (l2.Y - l1.Y) * (l1.X - l3.X)) / den;
            
            return (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1);
        }
        
        var r1 = new Vector3D(minX, minY, 0);
        var r2 = new Vector3D(maxX, minY, 0);
        var r3 = new Vector3D(maxX, maxY, 0);
        var r4 = new Vector3D(minX, maxY, 0);
        
        return intersectsLine(p1, p2, r1, r2) || 
               intersectsLine(p1, p2, r2, r3) || 
               intersectsLine(p1, p2, r3, r4) || 
               intersectsLine(p1, p2, r4, r1);
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
