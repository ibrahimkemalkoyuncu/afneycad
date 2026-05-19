using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Pis Su / Yağmur Suyu Katman Yönetim Servisi (WasteWaterLayerService)
   NEDEN: OtoNET'teki "Uygulama Katmanlarını Seç" komutunun AfneyCAD karşılığı.
          Farklı sistem türlerine (Temiz Su, Pis Su, Yağmur Suyu) ait entity'lerin
          görünürlüğünü yönetir — projeye ait tüm layer isimleri standart konvansiyona göre atanır.

   KULLANIM:
   - Kullanıcı "Pis Su" modülüne geçtiğinde: SetActiveModule(WasteWater) çağrılır.
   - Temiz su ve pis suyu aynı anda görmek için: ShowLayers([ColdWater, HotWater, WasteWater])
   - Katmanları kapatmak için: HideSystemLayer(DomesticColdWater)

   KATMAN İSİMLENDİRME KONVANSIYONU:
   - "MEP-COLD"     → DomesticColdWater
   - "MEP-HOT"      → DomesticHotWater
   - "MEP-WASTE"    → WasteWater
   - "MEP-RAIN"     → RainWater
   - "MEP-FIRE"     → FireProtection
   - "MEP-GAS"      → Gas
*/
public class WasteWaterLayerService
{
    private readonly CadDatabase _database;

    // Hangi sistem türlerinin şu an görünür olduğu
    private readonly HashSet<MechanicalSystemType> _visibleSystems = new();

    // Aktif modül — kullanıcının şu an çizim yaptığı sistem
    public MechanicalSystemType ActiveModule { get; private set; } = MechanicalSystemType.WasteWater;

    public static readonly Dictionary<MechanicalSystemType, string> LayerNames = new()
    {
        { MechanicalSystemType.DomesticColdWater, "MEP-COLD"  },
        { MechanicalSystemType.DomesticHotWater,  "MEP-HOT"   },
        { MechanicalSystemType.WasteWater,        "MEP-WASTE" },
        { MechanicalSystemType.RainWater,         "MEP-RAIN"  },
        { MechanicalSystemType.FireProtection,    "MEP-FIRE"  },
        { MechanicalSystemType.Gas,               "MEP-GAS"   },
        { MechanicalSystemType.Ventilation,       "MEP-VENT"  },
    };

    public WasteWaterLayerService(CadDatabase database)
    {
        _database = database;
        // Varsayılan: yalnızca pis su görünür
        _visibleSystems.Add(MechanicalSystemType.WasteWater);
    }

    /*
       NE: Aktif Modülü Değiştir (SetActiveModule)
       NEDEN: OtoNET'te "Uygulama Seç → Pis Su" seçildiğinde yalnızca o modülün katmanları
              aktif hale gelir. Burada da aynı davranış sağlanır.
    */
    public void SetActiveModule(MechanicalSystemType module)
    {
        ActiveModule = module;
        _visibleSystems.Clear();
        _visibleSystems.Add(module);
        ApplyVisibility();
    }

    /*
       NE: Birden Fazla Sistemi Göster (ShowSystems)
       NEDEN: OtoNET'te "Uygulama Katmanlarını Seç" ile temiz su + pis su aynı anda görülebilir.
    */
    public void ShowSystems(IEnumerable<MechanicalSystemType> systems)
    {
        foreach (var s in systems)
            _visibleSystems.Add(s);
        ApplyVisibility();
    }

    public void HideSystem(MechanicalSystemType system)
    {
        _visibleSystems.Remove(system);
        ApplyVisibility();
    }

    public void ShowAllSystems()
    {
        foreach (var s in LayerNames.Keys)
            _visibleSystems.Add(s);
        ApplyVisibility();
    }

    public bool IsVisible(MechanicalSystemType system) => _visibleSystems.Contains(system);

    public IReadOnlySet<MechanicalSystemType> VisibleSystems => _visibleSystems;

    /*
       NE: Görünürlüğü Veritabanına Yansıt (ApplyVisibility)
       NEDEN: _visibleSystems değiştiğinde CadDatabase içindeki ilgili layer'ların
              IsVisible flag'ini güncellemek için.
    */
    private void ApplyVisibility()
    {
        foreach (var (systemType, layerName) in LayerNames)
        {
            var layer = _database.LayerTable.Layers
                .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));

            if (layer != null)
                layer.IsVisible = _visibleSystems.Contains(systemType);
        }
    }

    /*
       NE: Sistem Türüne Göre Doğru Katman Adını Al
       NEDEN: Yeni entity oluşturulurken hangi katmana atanacağını belirlemek için.
    */
    public static string GetLayerName(MechanicalSystemType systemType)
        => LayerNames.TryGetValue(systemType, out var name) ? name : "MEP-UNDEFINED";

    /*
       NE: Gerekli Katmanları Veritabanında Oluştur (EnsureLayers)
       NEDEN: Proje ilk açıldığında MEP katmanları henüz yoksa oluşturulması gerekir.
              Varsa mevcut haliyle bırakılır.
    */
    public void EnsureLayers()
    {
        var layerColors = new Dictionary<MechanicalSystemType, SkiaSharp.SKColor>
        {
            { MechanicalSystemType.DomesticColdWater, new SkiaSharp.SKColor(0,   150, 255) },
            { MechanicalSystemType.DomesticHotWater,  new SkiaSharp.SKColor(255, 80,  80)  },
            { MechanicalSystemType.WasteWater,        new SkiaSharp.SKColor(139, 90,  43)  },
            { MechanicalSystemType.RainWater,         new SkiaSharp.SKColor(30,  144, 255) },
            { MechanicalSystemType.FireProtection,    new SkiaSharp.SKColor(220, 20,  60)  },
            { MechanicalSystemType.Gas,               new SkiaSharp.SKColor(255, 165, 0)   },
            { MechanicalSystemType.Ventilation,       new SkiaSharp.SKColor(100, 200, 100) },
        };

        foreach (var (systemType, layerName) in LayerNames)
        {
            bool exists = _database.LayerTable.Layers
                .Any(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                layerColors.TryGetValue(systemType, out var color);
                _database.LayerTable.AddLayer(new Domain.Tables.CadLayer
                {
                    Name = layerName,
                    Color = color,
                    IsVisible = _visibleSystems.Contains(systemType)
                });
            }
        }
    }
}
