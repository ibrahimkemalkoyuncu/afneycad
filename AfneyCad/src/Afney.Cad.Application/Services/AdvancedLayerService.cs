using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Application.Services;

// Gelişmiş Layer Yönetim Servisi — renk/linetype/lineweight atama, toplu işlem, filtre, arama
public class AdvancedLayerService
{
    private readonly CadDatabase _database;

    // TS 7363 / ISO 13567 — Standart MEP katman isimlendirme
    public static readonly Dictionary<string, LayerTemplate> StandardLayers = new()
    {
        ["0"] = new("0", "#FFFFFF", "Varsayılan katman", "Continuous", 0.25),
        ["ARCH-WALL"] = new("ARCH-WALL", "#888888", "Mimari duvarlar", "Continuous", 0.50),
        ["ARCH-DOOR"] = new("ARCH-DOOR", "#00AAFF", "Kapılar", "Continuous", 0.25),
        ["ARCH-WINDOW"] = new("ARCH-WINDOW", "#00CCFF", "Pencereler", "Continuous", 0.25),
        ["ARCH-COLUMN"] = new("ARCH-COLUMN", "#666666", "Kolonlar", "Continuous", 0.50),
        ["MEP_TEMIZ_SU"] = new("MEP_TEMIZ_SU", "#4488FF", "Temiz soğuk su", "Continuous", 0.35),
        ["MEP_SICAK_SU"] = new("MEP_SICAK_SU", "#FF4444", "Sıcak su", "Continuous", 0.35),
        ["MEP_PIS_SU"] = new("MEP_PIS_SU", "#888844", "Pis su / gider", "HIDDEN", 0.35),
        ["MEP_YANGIN"] = new("MEP_YANGIN", "#FF0000", "Yangın tesisatı", "Continuous", 0.50),
        ["MEP_GAZ"] = new("MEP_GAZ", "#FFFF00", "Doğalgaz", "DASHDOT", 0.35),
        ["MEP_HAVALANDIRMA"] = new("MEP_HAVALANDIRMA", "#44FF44", "HVAC kanal", "Continuous", 0.35),
        ["MEP_FIXTURES"] = new("MEP_FIXTURES", "#00CCFF", "Vitrifiye/cihazlar", "Continuous", 0.25),
        ["DIM"] = new("DIM", "#AAAAAA", "Boyutlandırma", "Continuous", 0.18),
        ["TEXT"] = new("TEXT", "#FFFFFF", "Metin", "Continuous", 0.18),
        ["RISER_DIAGRAM"] = new("RISER_DIAGRAM", "#FF8800", "Kolon şeması", "Continuous", 0.25),
        ["Space_Tags"] = new("Space_Tags", "#00FF00", "Mahal etiketleri", "Continuous", 0.18),
        ["XREF"] = new("XREF", "#CCCCCC", "Dış referans", "Continuous", 0.13),
        ["IFC_WALL"] = new("IFC_WALL", "#888888", "IFC duvar", "Continuous", 0.35),
        ["IFC_SLAB"] = new("IFC_SLAB", "#666666", "IFC döşeme", "Continuous", 0.35),
    };

    public AdvancedLayerService(CadDatabase database) => _database = database;

    // Standart MEP katmanlarını otomatik oluştur
    public int CreateStandardLayers()
    {
        int created = 0;
        foreach (var kvp in StandardLayers)
        {
            if (_database.GetLayer(kvp.Key) == null)
            {
                var layer = new Afney.Cad.Domain.Tables.CadLayer(kvp.Key);
                _database.AddLayer(layer);
                created++;
            }
        }
        return created;
    }

    // Katman arama (fuzzy)
    public List<string> SearchLayers(string query)
    {
        return _database.GetLayers()
            .Where(l => l.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Name)
            .OrderBy(n => n)
            .ToList();
    }

    // Katmandaki tüm entity'leri seç
    public List<CadEntity> SelectEntitiesByLayer(string layerName)
        => _database.GetAllEntities().Where(e => e.Layer == layerName).ToList();

    // Katman birleştir (merge)
    public int MergeLayers(string sourceName, string targetName)
    {
        int moved = 0;
        foreach (var entity in _database.GetAllEntities().Where(e => e.Layer == sourceName))
        {
            entity.Layer = targetName;
            _database.UpdateEntity(entity);
            moved++;
        }
        return moved;
    }

    // Kullanılmayan katmanları temizle (purge)
    public List<string> PurgeUnusedLayers()
    {
        var usedLayers = _database.GetAllEntities().Select(e => e.Layer).Distinct().ToHashSet();
        var allLayers = _database.GetLayers().Select(l => l.Name).ToList();
        var unused = allLayers.Where(l => l != "0" && !usedLayers.Contains(l)).ToList();

        // Not: CadDatabase.RemoveLayer henüz mevcut değil, sadece listeyi döndür
        return unused;
    }

    // Katman bazlı istatistik raporu
    public List<LayerStatistics> GetStatistics()
    {
        return _database.GetAllEntities()
            .GroupBy(e => e.Layer ?? "0")
            .Select(g =>
            {
                var layer = _database.GetLayer(g.Key);
                return new LayerStatistics
                {
                    LayerName = g.Key,
                    EntityCount = g.Count(),
                    Color = layer?.ColorBrush ?? "#FFFFFF",
                    TypeBreakdown = g.GroupBy(e => e.GetType().Name)
                        .ToDictionary(t => t.Key, t => t.Count()),
                    IsStandard = StandardLayers.ContainsKey(g.Key)
                };
            })
            .OrderByDescending(s => s.EntityCount)
            .ToList();
    }

    // Toplu renk değiştir
    public int BatchChangeColor(string layerName, uint newColor)
    {
        int changed = 0;
        foreach (var entity in _database.GetAllEntities().Where(e => e.Layer == layerName))
        {
            entity.Color = newColor;
            changed++;
        }
        return changed;
    }

    // Toplu katman taşı
    public int BatchMoveToLayer(IEnumerable<CadEntity> entities, string targetLayer)
    {
        int moved = 0;
        foreach (var entity in entities)
        {
            entity.Layer = targetLayer;
            _database.UpdateEntity(entity);
            moved++;
        }
        return moved;
    }
}

public record LayerTemplate(string Name, string Color, string Description, string Linetype, double Lineweight);

public class LayerStatistics
{
    public string LayerName { get; set; } = "";
    public int EntityCount { get; set; }
    public string Color { get; set; } = "";
    public Dictionary<string, int> TypeBreakdown { get; set; } = new();
    public bool IsStandard { get; set; }
}
