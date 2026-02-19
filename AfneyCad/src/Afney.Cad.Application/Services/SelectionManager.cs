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

        var entities = _database.QueryEntities(selectionBox);
        int addedCount = 0;

        foreach (var entity in entities)
        {
            // Crossing: Bounding box kesişimi varsa seç
            var bbox = entity.GetBoundingBox();
            if (BoundingBoxIntersects(selectionBox, bbox))
            {
                _selectedEntityIds.Add(entity.Id);
                _selectedEntityCache[entity.Id] = entity; // Cache'e ekle
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

        var entities = _database.QueryEntities(selectionBox);
        int addedCount = 0;

        foreach (var entity in entities)
        {
            // Window: Bounding box TAMAMEN içinde olmalı
            var bbox = entity.GetBoundingBox();
            if (BoundingBoxContains(selectionBox, bbox))
            {
                _selectedEntityIds.Add(entity.Id);
                _selectedEntityCache[entity.Id] = entity; // Cache'e ekle
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
            _selectedEntityCache.Remove(entityId);
        }
        else
        {
            _selectedEntityIds.Add(entityId);
            // Cache'e ekle - ancak önce entity'yi bul
            var entity = _database.GetAllEntities().FirstOrDefault(e => e.Id == entityId);
            if (entity != null)
                _selectedEntityCache[entityId] = entity;
        }
    }

    /*
        NE: Tüm Seçimi Temizle
    */
    public void ClearSelection()
    {
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
        var allEntities = _database.GetAllEntities().ToList();
        
        foreach (var id in _selectedEntityIds)
        {
            if (_selectedEntityCache.TryGetValue(id, out var cachedEntity))
            {
                result.Add(cachedEntity);
            }
            else
            {
                var entity = allEntities.FirstOrDefault(e => e.Id == id);
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
    public void DrawSelection(SKCanvas canvas, Func<Vector3D, SKPoint> worldToScreen)
    {
        if (_selectedEntityIds.Count == 0) return;

        // Sarı highlight için paint
        using var highlightPaint = new SKPaint
        {
            Color = new SKColor(255, 255, 0, 120), // Yarı şeffaf Sarı
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };

        var allEntities = _database.GetAllEntities();

        foreach (var id in _selectedEntityIds)
        {
            // Cache'ten veya listeden bul
            if (!_selectedEntityCache.TryGetValue(id, out var entity))
            {
                 entity = allEntities.FirstOrDefault(e => e.Id == id);
            }
            
            if (entity == null) continue;

            var bbox = entity.GetBoundingBox();
            
            var p1 = worldToScreen(bbox.Min);
            var p2 = worldToScreen(bbox.Max);

            var absWidth = Math.Abs(p2.X - p1.X);
            var absHeight = Math.Abs(p2.Y - p1.Y);
            
            // Sıfır boyutluları en azından küçük bir kare gibi çiz
            if (absWidth < 1) absWidth = 5; 
            if (absHeight < 1) absHeight = 5;

            var left = Math.Min(p1.X, p2.X);
            var top = Math.Min(p1.Y, p2.Y);

            var rect = SKRect.Create(left, top, absWidth, absHeight);
            canvas.DrawRect(rect, highlightPaint);
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
