using System.Collections.Generic;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

public class SystemLayerService
{
    // ── Sistem Tipi → Katman Adı ──────────────────────────────────────────────
    public static readonly Dictionary<MechanicalSystemType, string> LayerNames = new()
    {
        [MechanicalSystemType.DomesticColdWater] = "MEP_TEMIZ_SU",
        [MechanicalSystemType.DomesticHotWater]  = "MEP_SICAK_SU",
        [MechanicalSystemType.WasteWater]        = "MEP_PIS_SU",
        [MechanicalSystemType.RainWater]         = "MEP_YAGMUR_SU",
        [MechanicalSystemType.FireProtection]    = "MEP_YANGIN",
        [MechanicalSystemType.Gas]               = "MEP_GAZ",
        [MechanicalSystemType.Ventilation]       = "MEP_HAVALANDIRMA",
        [MechanicalSystemType.Undefined]         = "MEP_GENEL",
    };

    // ── Katman → Renk (ARGB) ─────────────────────────────────────────────────
    private static readonly Dictionary<string, uint> LayerColors = new()
    {
        ["MEP_TEMIZ_SU"]      = 0xFF00AAFF, // mavi
        ["MEP_SICAK_SU"]      = 0xFFFF4400, // kırmızı-turuncu
        ["MEP_PIS_SU"]        = 0xFFAA7700, // kahve
        ["MEP_YAGMUR_SU"]     = 0xFF00CCAA, // yeşil-cyan
        ["MEP_YANGIN"]        = 0xFFFF0000, // kırmızı
        ["MEP_GAZ"]           = 0xFFFFDD00, // sarı
        ["MEP_HAVALANDIRMA"]  = 0xFF99AAFF, // açık mor
        ["MEP_GENEL"]         = 0xFFCCCCCC, // gri
    };

    public static string GetLayerName(MechanicalSystemType type)
        => LayerNames.TryGetValue(type, out var name) ? name : "MEP_GENEL";

    // ── Katmanları veritabanında oluştur/güncelle ─────────────────────────────
    public void EnsureLayersInDatabase(CadDatabase database)
    {
        foreach (var (_, layerName) in LayerNames)
        {
            if (database.GetLayer(layerName) != null) continue;

            uint color = LayerColors.TryGetValue(layerName, out var c) ? c : 0xFFFFFFFF;
            database.AddLayer(new CadLayer(layerName) { Color = color });
        }
    }

    // ── Tüm mekanik entitylerin katman adını sistem tipine göre ata ──────────
    public int SyncEntityLayers(CadDatabase database)
    {
        EnsureLayersInDatabase(database);
        int updated = 0;

        foreach (var entity in database.GetAllEntities())
        {
            MechanicalSystemType? sysType = entity switch
            {
                PipeEntity            p => p.SystemType,
                SanitaryFixtureEntity f => f.SystemType,
                MechanicalEntity      m => MechanicalSystemType.Undefined,
                _                       => null
            };

            if (sysType is null) continue;

            string target = GetLayerName(sysType.Value);
            if (entity.Layer == target) continue;

            entity.Layer = target;
            updated++;
        }

        return updated;
    }

    // ── UI için grup bilgisi ──────────────────────────────────────────────────
    public record SystemLayerInfo(
        MechanicalSystemType SystemType,
        string LayerName,
        string DisplayName,
        string Icon,
        uint Color);

    public static readonly SystemLayerInfo[] All =
    [
        new(MechanicalSystemType.DomesticColdWater, "MEP_TEMIZ_SU",     "Temiz Su",     "💧", 0xFF00AAFF),
        new(MechanicalSystemType.DomesticHotWater,  "MEP_SICAK_SU",     "Sıcak Su",     "🔴", 0xFFFF4400),
        new(MechanicalSystemType.WasteWater,        "MEP_PIS_SU",       "Pis Su",       "🟤", 0xFFAA7700),
        new(MechanicalSystemType.RainWater,         "MEP_YAGMUR_SU",    "Yağmur Suyu",  "🌧", 0xFF00CCAA),
        new(MechanicalSystemType.FireProtection,    "MEP_YANGIN",       "Yangın",       "🔥", 0xFFFF0000),
        new(MechanicalSystemType.Gas,               "MEP_GAZ",          "Gaz",          "💨", 0xFFFFDD00),
        new(MechanicalSystemType.Ventilation,       "MEP_HAVALANDIRMA", "Havalandırma", "🌀", 0xFF99AAFF),
    ];
}
