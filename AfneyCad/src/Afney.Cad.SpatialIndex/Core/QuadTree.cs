using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.SpatialIndex.Core;

/*
NE:
Konumsal İndeksleme Motoru (QuadTree Implementasyonu).

NE İÇİN:
Milyonlarca nesne içinde arama yaparken O(n) yerine O(log n) performansı sağlamak için.
Özellikle "Ekranda ne görünüyor (Culling)" ve "Mouse nereye tıkladı (Picking)" sorguları için kritik.

NASIL:
- Derinlik Sınırı (MAX_DEPTH) eklenerek StackOverflow hatası engellenmiştir.
- Üst üste binen çok sayıda nesne olduğunda bölünmek yerine yığılma (bucket overflow) stratejisi uygulanır.
*/
public class QuadTree
{
    private readonly CadBoundingBox _bounds;
    private readonly int _capacity;
    private readonly List<CadEntity> _entities;
    private QuadTree? _northWest;
    private QuadTree? _northEast;
    private QuadTree? _southWest;
    private QuadTree? _southEast;
    private bool _divided;

    // NE: Test/Tanılama Amaçlı Salt-Okunur Erişim (IsDivided)
    // NEDEN: Merge/shrink davranışını (çocukların birleşip düğümün tekrar yaprak
    // olduğunu) dışarıdan doğrulayabilmek için (bkz. QuadTreeTests).
    public bool IsDivided => _divided;

    // StackOverflow Koruması için Derinlik Kontrolü
    private readonly int _depth;
    private const int MAX_DEPTH = 12;

    public QuadTree(CadBoundingBox bounds, int capacity = 10, int depth = 0)
    {
        _bounds = bounds;
        _capacity = capacity;
        _depth = depth;
        _entities = new List<CadEntity>(capacity);
        _divided = false;
    }

    public void Insert(CadEntity entity)
    {
        // Kapsama Alanı Kontrolü
        var entBox = entity.GetBoundingBox();
        if (!Intersects(_bounds, entBox)) return;

        // 1. Kapasite dolmadıysa -> Ekle
        // 2. Maksimum derinliğe ulaşıldıysa -> Ekle (Bölünme YOK, Yığılma VAR)
        // 3. Zaten bölünmüşse -> Ekleme yapılmaz, child'lara yönlendirilir.
        if ((_entities.Count < _capacity || _depth >= MAX_DEPTH) && !_divided)
        {
            _entities.Add(entity);
            return; // İşlem tamam
        }

        // Kapasite doldu ve henüz bölünmemiş. Böl!
        if (!_divided) 
        {
            Subdivide();
        }

        // Çocuğa ekle (Recursive)
        // Nesne birden fazla çocuğa girebilir
        bool addedToChild = false;
        
        if (_northWest!.Intersects(_northWest._bounds, entBox)) { _northWest.Insert(entity); addedToChild = true; }
        if (_northEast!.Intersects(_northEast._bounds, entBox)) { _northEast.Insert(entity); addedToChild = true; }
        if (_southWest!.Intersects(_southWest._bounds, entBox)) { _southWest.Insert(entity); addedToChild = true; }
        if (_southEast!.Intersects(_southEast._bounds, entBox)) { _southEast.Insert(entity); addedToChild = true; }
        
        // Hiçbir çocuğa girmezse (teorik olarak imkansız ama floating point hatası olabilir),
        // parent'ta tut.
        if (!addedToChild)
        {
             _entities.Add(entity);
        }
    }

    // NE: Silme (Remove)
    // NEDEN: Veritabanından silinen nesnelerin indeksten de düşülmesi gerekir.
    //
    // ERKEN ÇIKIŞ NOTU: Insert() bir nesneyi birden fazla kesişen çocuğa (boundary-crossing
    // durumunda) ekleyebiliyor. Bu yüzden "ilk bulunduğu yerde dur" tarzı naif bir erken çıkış
    // YANLIŞTIR — diğer çocuklarda kalan referanslar silinmeden kalır (hayalet/stale entity,
    // QueryRange'de silinmiş nesnenin hâlâ görünmesi). Güvenli optimizasyon: entBox'ın kesişmediği
    // çocuklara hiç rekürsif ÇAĞRI yapılmaz (Intersects önceden hesaplanır) — önceden her 4 çocuğa
    // çağrı yapılıp kesişmeyenler kendi içinde erken dönüyordu, şimdi o gereksiz çağrılar tamamen atlanıyor.
    public bool Remove(CadEntity entity)
    {
        var entBox = entity.GetBoundingBox();
        if (!Intersects(_bounds, entBox)) return false;

        bool removed = false;

        // Bu düğümdeki listeden sil
        if (_entities.Remove(entity))
        {
            removed = true;
        }

        // Eğer bölünmüşse ve kesişiyorsa çocuklardan da sil
        if (_divided)
        {
            if (Intersects(_northWest!._bounds, entBox) && _northWest.Remove(entity)) removed = true;
            if (Intersects(_northEast!._bounds, entBox) && _northEast.Remove(entity)) removed = true;
            if (Intersects(_southWest!._bounds, entBox) && _southWest.Remove(entity)) removed = true;
            if (Intersects(_southEast!._bounds, entBox) && _southEast.Remove(entity)) removed = true;

            // Merge (Optimize): Çocukların toplam eleman sayısı kapasitenin altına
            // düştüyse ve çocukların kendileri bölünmemişse (tek seviye), çocukları
            // birleştirip bu düğümü tekrar yaprak yap.
            TryMergeChildren();
        }

        return removed;
    }

    /*
       NE: Çocukları Birleştir (TryMergeChildren)
       NEDEN: Uzun düzenleme oturumlarında (çok sayıda ekle/sil) ağaç sürekli bölünüp hiç
              toparlanmazsa, düğümler zamanla seyrekleşir (çoğu boş/az dolu). Silme sonrası
              4 çocuğun (yalnızca yaprak iseler — grandchild kaybı riskini önlemek için)
              toplam nesne sayısı kapasitenin altına düşerse, çocukları bu düğümde birleştirip
              tekrar yaprak düğüme dönüştürür. Eşik, Insert()'teki bölünme eşiğiyle birebir
              aynıdır (Count < _capacity => yaprak kalır) — bu yüzden merge/split arasında
              salınım (thrashing) oluşmaz.
    */
    private void TryMergeChildren()
    {
        if (!_divided) return;

        // Çocuklardan biri bile kendi içinde bölünmüşse, birleştirme yapılmaz
        // (torun düğümlerdeki nesneleri kaybetmemek için).
        if (_northWest!._divided || _northEast!._divided || _southWest!._divided || _southEast!._divided)
            return;

        var combined = new HashSet<CadEntity>(_northWest._entities);
        combined.UnionWith(_northEast._entities);
        combined.UnionWith(_southWest._entities);
        combined.UnionWith(_southEast._entities);

        // Split ile aynı eşik: Count < _capacity ise yaprak olarak kalınabilir.
        if (combined.Count + _entities.Count >= _capacity) return;

        foreach (var ent in combined)
        {
            if (!_entities.Contains(ent))
                _entities.Add(ent);
        }

        _northWest = null;
        _northEast = null;
        _southWest = null;
        _southEast = null;
        _divided = false;
    }

    /*
       NE: Belirli Alanda Ara (QueryRange)
       NEDEN: Ekranda görünen alanı (Zoom/Pan) veya seçim kutusunu (Window Selection) temsil eden bir dikdörtgen içindeki tüm nesneleri yüksek performanslı (uzamsal sorgu ile) bulmak için.
    */
    public void QueryRange(CadBoundingBox range, HashSet<CadEntity> found)
    {
        if (!Intersects(_bounds, range)) return;

        // Mevcut Node'daki nesneleri kontrol et
        foreach (var ent in _entities)
        {
            if (Intersects(range, ent.GetBoundingBox()))
                found.Add(ent);
        }

        // Eğer bölünmüşse çocuklara bak
        if (_divided)
        {
            _northWest?.QueryRange(range, found);
            _northEast?.QueryRange(range, found);
            _southWest?.QueryRange(range, found);
            _southEast?.QueryRange(range, found);
        }
    }

    /*
       NE: Alanı Böl (Subdivide)
       NEDEN: Mevcut düğümün (node) kapasitesi dolduğunda, alanı daha küçük dört alt bölgeye ayırarak ağaç yapısını derinleştirmek ve arama hızını korumak için.
    */
    private void Subdivide()
    {
        var min = _bounds.Min;
        var max = _bounds.Max;
        var midX = (min.X + max.X) / 2;
        var midY = (min.Y + max.Y) / 2;
        
        int nextDepth = _depth + 1;

        _northWest = new QuadTree(new CadBoundingBox(new Vector3D(min.X, midY, 0), new Vector3D(midX, max.Y, 0)), _capacity, nextDepth);
        _northEast = new QuadTree(new CadBoundingBox(new Vector3D(midX, midY, 0), new Vector3D(max.X, max.Y, 0)), _capacity, nextDepth);
        _southWest = new QuadTree(new CadBoundingBox(new Vector3D(min.X, min.Y, 0), new Vector3D(midX, midY, 0)), _capacity, nextDepth);
        _southEast = new QuadTree(new CadBoundingBox(new Vector3D(midX, min.Y, 0), new Vector3D(max.X, midY, 0)), _capacity, nextDepth);

        _divided = true;

        // "Re-distribute" (Yeniden Dağıtım)
        // Mevcut düğümdeki nesneleri çocuklara aktar.
        // Bu sayede QuadTree dengeli büyür.
        
        var oldEntities = new List<CadEntity>(_entities);
        _entities.Clear(); // Parent düğüm boşalır (Yaprak düğüm prensibi - Leaf Node Principle)
        
        foreach (var ent in oldEntities)
        {
            // Insert metodunu çağırarak çocuklara dağıt
            // _divided=true olduğu için Insert metodu direkt çocuklara yönlendirecek.
            Insert(ent);
        }
    }

    // Basit AABB Kesişim Kontrolü
    private bool Intersects(CadBoundingBox a, CadBoundingBox b)
    {
        if (a.Max.X < b.Min.X || a.Min.X > b.Max.X) return false;
        if (a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y) return false;
        return true;
    }
    
    /*
       NE: Kapsama Kontrolü (Contains)
       NEDEN: Bir geometrik sınırlayıcı kutunun (Bounding Box), diğerini tamamen içine alıp almadığını kontrol ederek uzamsal bölütleme kararlarını vermek için.
    */
    private bool Contains(CadBoundingBox outer, CadBoundingBox inner)
    {
         return (inner.Min.X >= outer.Min.X && inner.Max.X <= outer.Max.X &&
                 inner.Min.Y >= outer.Min.Y && inner.Max.Y <= outer.Max.Y);
    }
}
