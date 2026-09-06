using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Afney.Cad.Database.Core;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Katman Durumu Yöneticisi (LayerStateManagerService)
   NEDEN — GERÇEK BOŞLUK (denetim raporunda "Layer State Manager: katman görünürlüğü persist
          ediliyor, isimlendirilmiş çoklu-state yönetimi yok, 4/10" olarak işaretlenmişti):
          Önceki mekanizma (MainWindow.FileOps.cs: SaveLayerState/LoadLayerState, "<dosya>.layerstate")
          sadece TEK, isimsiz bir görünürlük listesi (gizli katmanlar) tutuyordu — AutoCAD'in Layer
          States Manager'ının sunduğu "Elektrik Görünümü", "Sıhhi Tesisat Görünümü", "Baskı Görünümü"
          gibi İSİMLENDİRİLMİŞ, birden fazla, geri çağrılabilir anlık görüntü (Frozen+Locked+Visible
          birlikte) kavramı hiç yoktu. Bu servis o boşluğu kapatır.

   NASIL: Her adlandırılmış "state" (anlık görüntü), o an veritabanındaki HER katman için
          Visible (CadLayer/HiddenLayers'tan), Frozen (CadLayer.IsFrozen) ve Locked
          (CadLayer.IsLocked) bayraklarını kaydeder. SaveCurrentState aynı isimle tekrar
          çağrılırsa var olan state ÜZERİNE YAZAR (üstünkörü çoğaltma yerine güncelleme).
          ApplyState, snapshot'taki katmanları geri yükler; snapshot'ta OLMAYAN (state
          kaydedildikten SONRA eklenmiş) katmanlara dokunmaz — bilinçli sınır, "eksik veri
          = mevcut durumu koru" ilkesi.

   KALICILIK: LayerStatePersistenceService (bu dosyanın devamında) — SheetSetPersistenceService
          ile AYNI sidecar-JSON deseni ("<dosya>.layerstates.json"), gerçek DWG/DXF formatına
          dokunmaz.
*/
public class LayerStateManagerService
{
    public class LayerFlags
    {
        public bool Visible { get; set; } = true;
        public bool Frozen  { get; set; }
        public bool Locked  { get; set; }
    }

    public class LayerStateSnapshot
    {
        public string Name { get; set; } = "";
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
        public Dictionary<string, LayerFlags> Layers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly List<LayerStateSnapshot> _snapshots = new();

    public IReadOnlyList<LayerStateSnapshot> Snapshots => _snapshots;

    /// <summary>Verilen isimde bir state var mı bulur (case-insensitive).</summary>
    public LayerStateSnapshot? Find(string name)
        => _snapshots.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Veritabanındaki TÜM katmanların o anki Visible/Frozen/Locked durumunu, verilen isim
    /// altında kaydeder (aynı isim varsa üzerine yazar). Visible durumu HiddenLayers'tan (viewport
    /// katmanında tutulan gerçek görünürlük kaynağı) türetilir.
    /// </summary>
    public LayerStateSnapshot SaveCurrentState(string name, CadDatabase database, ISet<string> hiddenLayers)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("State ismi boş olamaz.", nameof(name));

        var snapshot = new LayerStateSnapshot { Name = name.Trim(), SavedAtUtc = DateTime.UtcNow };
        foreach (var layer in database.GetLayers())
        {
            snapshot.Layers[layer.Name] = new LayerFlags
            {
                Visible = !hiddenLayers.Contains(layer.Name),
                Frozen  = layer.IsFrozen,
                Locked  = layer.IsLocked
            };
        }

        int existingIdx = _snapshots.FindIndex(s => string.Equals(s.Name, snapshot.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIdx >= 0)
            _snapshots[existingIdx] = snapshot;
        else
            _snapshots.Add(snapshot);

        return snapshot;
    }

    /// <summary>Adlandırılmış bir state'i siler. Bulunamazsa false döner.</summary>
    public bool Delete(string name)
    {
        int idx = _snapshots.FindIndex(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;
        _snapshots.RemoveAt(idx);
        return true;
    }

    /// <summary>
    /// Bir state'i geri yükler: snapshot'ta bulunan her katman için Frozen/Locked bayrakları
    /// CadLayer üzerine, Visible bayrağı hiddenLayers set'ine yazılır. Snapshot'ta OLMAYAN
    /// katmanlar (state kaydedildikten sonra eklenmiş) dokunulmadan bırakılır.
    /// </summary>
    public void ApplyState(LayerStateSnapshot snapshot, CadDatabase database, ISet<string> hiddenLayers)
    {
        foreach (var (layerName, flags) in snapshot.Layers)
        {
            var layer = database.GetLayer(layerName);
            if (layer == null) continue; // Katman artık çizimde yok — sessizce atla.

            layer.IsFrozen = flags.Frozen;
            layer.IsLocked = flags.Locked;

            if (flags.Visible)
                hiddenLayers.Remove(layerName);
            else
                hiddenLayers.Add(layerName);
        }
    }

    // ── Kalıcılık (JSON) ──────────────────────────────────────────────────────────

    private class PersistedState
    {
        public List<LayerStateSnapshot> Snapshots { get; set; } = [];
    }

    /// <summary>Servisin tüm durumunu (adlandırılmış state listesi) JSON'a dönüştürür.</summary>
    public string ToJson()
    {
        var state = new PersistedState { Snapshots = _snapshots };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Daha önce ToJson() ile üretilmiş bir durumu geri yükler. Bozuk/eksik JSON durumunda
    /// mevcut boş duruma sessizce geri döner (proje dosyasının açılmasını engellemez).
    /// </summary>
    public void LoadFromJson(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<PersistedState>(json);
            if (state == null) return;

            _snapshots.Clear();
            _snapshots.AddRange(state.Snapshots);
        }
        catch { /* Bozuk JSON — mevcut (boş) durumla devam et */ }
    }
}
