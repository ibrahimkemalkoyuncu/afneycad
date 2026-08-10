using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using SkiaSharp;

namespace Afney.Cad.Application.Services;

/*
    NE: Seçim Yöneticisi (SelectionManager) - PERFORMANS OPTİMİZE EDİLMİŞ
    NEDEN: AutoCAD tarzı entity seçme, highlight rendering, kopyala/yapıştır/sil işlemleri için.
    
    NASIL (AutoCAD Mantığı):
    1. **Crossing Selection** (Sağdan Sola): Değen tüm entityler seçilir → Yeşil highlight
    2. **Window Selection** (Soldan Sağa): Tamamen içinde kalan entityler → Mavi highlight
    3. Seçili entityler **Sarı** highlight ile vurgulanır
    4. Clipboard: Ctrl+C/V/X işlemleri
    
    PERFORMANS: Seçili entityler cache'te saklanır, her frame'de database taranmaz!
*/
public class SelectionManager
{
    private readonly CadDatabase _database;
    private readonly HashSet<Guid> _selectedEntityIds = new();
    
    // PERFORMANS: Seçili entityleri cache'te tut
    private readonly Dictionary<Guid, CadEntity> _selectedEntityCache = new();
    
    // Clipboard için
    private List<CadEntity>? _clipboardEntities;
    private Vector3D _clipboardBasePoint;

    /*
       NE: SelectionManager Yapıcı Metodu
       NEDEN: Veritabanı referansını alarak seçim cache'ini yönetmeye ve silme olaylarını dinlemeye başlar.
    */
    public SelectionManager(CadDatabase database)
    {
        _database = database;
        
        // Database event'lerine subscribe ol - cache'i güncel tut
        _database.EntityRemoved += OnEntityRemoved;
    }

    /*
        NE: Entity silindi event handler - cache'ten kaldır
    */
    private void OnEntityRemoved(CadEntity entity)
    {
        if (_selectedEntityIds.Contains(entity.Id))
        {
            _selectedEntityIds.Remove(entity.Id);
            _selectedEntityCache.Remove(entity.Id);
        }
    }

    /*
        NE: Seçili Entity Sayısı
    */
    public int SelectedCount => _selectedEntityIds.Count;

    /*
       NE: Seçili mi? (IsSelected)
       NEDEN: Verilen nesne ID'sinin (Guid) halihazırda seçilen nesneler listesinde olup olmadığını hızlıca öğrenmek için. (Örn: Hover glow çalışmadan önce)
    */
    public bool IsSelected(Guid entityId)
    {
        return _selectedEntityIds.Contains(entityId);
    }

    /*
        NE: Tekil Seçime Ekle (AddToSelection)
        PERFORMANS: entity.IsSelected sadece bir görsel bayraktır, geometriyi değiştirmez.
        _database.UpdateEntity(entity) ÇAĞRILMAZ — o metod QuadTree'de Remove+Insert
        (spatial index churn) VE MechanicalKernel.OnEntityUpdatedInDatabase üzerinden
        gereksiz hidrolik yeniden hesaplama tetikler. Seçim sadece hafif bir state
        değişikliğidir, spatial index'e veya mühendislik hesaplarına dokunmamalı.
    */
    public void AddToSelection(CadEntity entity)
    {
        if (!_selectedEntityIds.Contains(entity.Id))
        {
            _selectedEntityIds.Add(entity.Id);
            _selectedEntityCache[entity.Id] = entity;
            entity.IsSelected = true;
        }
    }

    /*
        NE: Crossing Selection (Sağdan Sola - Değen Herşey)
        AMACI: Seçim kutusuna değen TÜM entityleri seçmek.
    */
    /*
       NE: Kesişimle Seç (SelectByCrossing)
       NEDEN: Seçim kutusuna (sağdan sola) değen veya kutu içinde kalan tüm nesneleri AutoCAD standartlarında toplu olarak seçmek için.
    */
    public void SelectByCrossing(CadBoundingBox selectionBox)
    {
        Serilog.Log.Information("🟢 CROSSING SELECTION: ({MinX},{MinY}) → ({MaxX},{MaxY})", 
            selectionBox.Min.X, selectionBox.Min.Y, selectionBox.Max.X, selectionBox.Max.Y);

        var entities = _database.SelectByBox(selectionBox, isCrossing: true);
        int addedCount = 0;

        foreach (var entity in entities)
        {
            if (!_selectedEntityIds.Contains(entity.Id))
            {
                _selectedEntityIds.Add(entity.Id);
                _selectedEntityCache[entity.Id] = entity; // Cache'e ekle
                entity.IsSelected = true; // PERFORMANS: UpdateEntity ÇAĞRILMAZ (bkz. AddToSelection notu)
                addedCount++;
            }
        }

        Serilog.Log.Information("✅ Crossing Selection: {Count} entity eklendi", addedCount);
    }

    /*
        NE: Window Selection (Soldan Sağa - Tamamen İçinde)
        AMACI: Seçim kutusunun TAMAMEN içinde kalan entityleri seçmek.
    */
    /*
       NE: Alanla Seç (SelectByWindow)
       NEDEN: Seçim kutusunun (soldan sağa) TAMAMEN içinde kalan nesneleri AutoCAD standartlarında toplu olarak seçmek için.
    */
    public void SelectByWindow(CadBoundingBox selectionBox)
    {
        Serilog.Log.Information("🔵 WINDOW SELECTION: ({MinX},{MinY}) → ({MaxX},{MaxY})", 
            selectionBox.Min.X, selectionBox.Min.Y, selectionBox.Max.X, selectionBox.Max.Y);

        var entities = _database.SelectByBox(selectionBox, isCrossing: false);
        int addedCount = 0;

        foreach (var entity in entities)
        {
            if (!_selectedEntityIds.Contains(entity.Id))
            {
                _selectedEntityIds.Add(entity.Id);
                _selectedEntityCache[entity.Id] = entity; // Cache'e ekle
                entity.IsSelected = true; // PERFORMANS: UpdateEntity ÇAĞRILMAZ (bkz. AddToSelection notu)
                addedCount++;
            }
        }

        Serilog.Log.Information("✅ Window Selection: {Count} entity eklendi", addedCount);
    }

    /*
        NE: Tek Entity Seç/Seçimi Kaldır (Toggle)
    */
    public void ToggleEntity(Guid entityId)
    {
        if (_selectedEntityIds.Contains(entityId))
        {
            _selectedEntityIds.Remove(entityId);
            if (_selectedEntityCache.TryGetValue(entityId, out var entityToRemove))
            {
                entityToRemove.IsSelected = false; // PERFORMANS: UpdateEntity ÇAĞRILMAZ (bkz. AddToSelection notu)
            }
            _selectedEntityCache.Remove(entityId);
        }
        else
        {
            _selectedEntityIds.Add(entityId);
            // Cache'e ekle - ancak önce entity'yi bul
            var entity = _database.GetEntity(entityId);
            if (entity != null)
            {
                _selectedEntityCache[entityId] = entity;
                entity.IsSelected = true; // PERFORMANS: UpdateEntity ÇAĞRILMAZ (bkz. AddToSelection notu)
            }
        }
    }

    /*
        NE: Tüm Seçimi Temizle
    */
    public void ClearSelection()
    {
        foreach (var entity in _selectedEntityCache.Values)
        {
            entity.IsSelected = false; // PERFORMANS: UpdateEntity ÇAĞRILMAZ (bkz. AddToSelection notu)
        }
        _selectedEntityIds.Clear();
        _selectedEntityCache.Clear();
        Serilog.Log.Information("🧹 Seçim temizlendi");
    }

    /*
        NE: Seçili Entityleri Al - PERFORMANS OPTİMİZE
        ÖNCE: Her çağrıda GetAllEntities() ve FirstOrDefault
        SONRA: Sadece cache'ten al
    */
    public List<CadEntity> GetSelectedEntities()
    {
        // Önce cache'ten dene
        if (_selectedEntityCache.Count == _selectedEntityIds.Count)
        {
            return _selectedEntityCache.Values.ToList();
        }
        
        // Cache eksikse, sadece eksik olanları bul
        var result = new List<CadEntity>();
        
        foreach (var id in _selectedEntityIds)
        {
            if (_selectedEntityCache.TryGetValue(id, out var cachedEntity))
            {
                result.Add(cachedEntity);
            }
            else
            {
                var entity = _database.GetEntity(id);
                if (entity != null)
                {
                    result.Add(entity);
                    _selectedEntityCache[id] = entity; // Cache'e ekle
                }
            }
        }
        
        return result;
    }

    /*
        NE: Seçili Entityleri Clipboard'a Kopyala (Ctrl+C)
        NEDEN: AutoCAD'deki Copy komutu ile aynı mantık
    */
    /*
       NE: Panoya Kopyala (CopyToClipboard)
       NEDEN: Seçili nesnelerin klonlarını bir referans noktasına göre belleğe alarak daha sonra başka bir yere yapıştırılmasını sağlamak için.
    */
    public void CopyToClipboard(Vector3D basePoint)
    {
        _clipboardEntities = GetSelectedEntities().Select(e => e.Clone()).ToList();
        _clipboardBasePoint = basePoint;
        
        Serilog.Log.Information("📋 {Count} entity clipboard'a kopyalandı", _clipboardEntities.Count);
    }

    /*
        NE: Clipboard'dan Yapıştır (Ctrl+V)
    */
    /*
       NE: Panodan Yapıştır (PasteFromClipboard)
       NEDEN: Kopyalanan nesneleri, yeni hedef noktaya (delta mesafesi kadar kaydırarak) veritabanına eklemek için.
    */
    public List<CadEntity> PasteFromClipboard(Vector3D targetPoint)
    {
        if (_clipboardEntities == null || _clipboardEntities.Count == 0)
        {
            Serilog.Log.Warning("⚠️  Clipboard boş!");
            return new List<CadEntity>();
        }

        var delta = new Vector3D(
            targetPoint.X - _clipboardBasePoint.X,
            targetPoint.Y - _clipboardBasePoint.Y,
            targetPoint.Z - _clipboardBasePoint.Z
        );

        var pastedEntities = new List<CadEntity>();
        foreach (var entity in _clipboardEntities)
        {
            var clone = entity.Clone();
            clone.Move(delta);
            _database.AddEntity(clone);
            pastedEntities.Add(clone);
        }

        Serilog.Log.Information("✅ {Count} entity yapıştırıldı", pastedEntities.Count);
        return pastedEntities;
    }

    /*
        NE: Seçili Entityleri Sil (Delete)
    */
    /*
       NE: Seçili Olanları Sil (DeleteSelected)
       NEDEN: Kullanıcının seçtiği nesneleri veritabanından toplu olarak kaldırmak için.
    */
    public void DeleteSelected()
    {
        var toDelete = GetSelectedEntities();
        foreach (var entity in toDelete)
        {
            _database.RemoveEntity(entity.Id);
        }
        
        Serilog.Log.Information("🗑️  {Count} entity silindi", toDelete.Count);
        ClearSelection();
    }

    /*
        NE: Seçili Entityleri Highlight Çiz (Sarı Renk)
        NEDEN: Kullanıcı seçili entityleri görebilmeli
        
        worldToScreen: Delegate fonksiyon - Vector3D'yi ekran koordinatına çevirir
    */
    /*
        NE: Seçili Entityleri Highlight Çiz (Sarı Renk)
        NEDEN: Kullanıcı seçili entityleri görebilmeli
        PERFORMANS: Reflection kaldırıldı, SKPoint kullanılıyor.
    */
    public void DrawSelection(IRenderContext renderContext, HashSet<string>? hiddenLayers = null)
    {
        if (_selectedEntityIds.Count == 0) return;

        renderContext.IsHighlightMode = true;

        foreach (var id in _selectedEntityIds)
        {
            // Cache'ten veya veritabanından bul
            if (!_selectedEntityCache.TryGetValue(id, out var entity))
            {
                 entity = _database.GetEntity(id);
                 if (entity != null) _selectedEntityCache[id] = entity;
            }

            if (entity != null)
            {
                // Gizli katmandaki entity'leri highlight etme
                if (hiddenLayers != null && entity.Layer != null && hiddenLayers.Contains(entity.Layer))
                    continue;

                entity.Draw(renderContext);
            }
        }

        renderContext.IsHighlightMode = false;
    }

    /*
       NE: Grip Noktalarını Çiz (DrawGrips)
       NEDEN: Seçili nesnelerin önemli noktalarında AutoCAD standartlarındaki mavi kontrol uçlarını oluşturmak için.
    */
    public void DrawGrips(SKCanvas canvas, Func<Vector3D, SKPoint> worldToScreen)
    {
        if (_selectedEntityIds.Count == 0) return;

        using var gripPaintBorder = new SKPaint { Color = SKColors.Navy, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        using var gripPaintFill = new SKPaint { Color = new SKColor(0, 100, 255), Style = SKPaintStyle.Fill, IsAntialias = true }; // Mavi dolgu

        float gripSize = 6f; // 6x6 pixel kare
        float halfSize = gripSize / 2f;

        foreach (var id in _selectedEntityIds)
        {
            if (_selectedEntityCache.TryGetValue(id, out var entity))
            {
                foreach (var gripPos in entity.GetGripPoints())
                {
                    var p = worldToScreen(gripPos);
                    var rect = SKRect.Create(p.X - halfSize, p.Y - halfSize, gripSize, gripSize);
                    canvas.DrawRect(rect, gripPaintFill);
                    canvas.DrawRect(rect, gripPaintBorder);
                }
            }
        }
    }

    // === HELPER METHODS ===

    private bool BoundingBoxIntersects(CadBoundingBox box1, CadBoundingBox box2)
    {
        return !(box1.Max.X < box2.Min.X || box1.Min.X > box2.Max.X ||
                 box1.Max.Y < box2.Min.Y || box1.Min.Y > box2.Max.Y);
    }

    private bool BoundingBoxContains(CadBoundingBox outer, CadBoundingBox inner)
    {
        return outer.Min.X <= inner.Min.X && outer.Max.X >= inner.Max.X &&
               outer.Min.Y <= inner.Min.Y && outer.Max.Y >= inner.Max.Y;
    }
}
