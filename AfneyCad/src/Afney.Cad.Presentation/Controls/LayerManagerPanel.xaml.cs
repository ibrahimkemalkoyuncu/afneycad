using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Tables;

namespace Afney.Cad.Presentation.Controls;

/*
   NE: Katman Yönetici Paneli (Layer Manager Panel)
   NEDEN: AutoCAD'deki "Layer Properties Manager" benzeri, katman görünürlüğünü,
          dondurma ve kilit durumunu tek panelden yönetmek için.

   MÜHENDİSLİK DETAYI:
   - Auto-detect: CadDatabase + tüm entity.Layer değerlerinden katman listesini otomatik türetir.
   - INotifyPropertyChanged: CadLayer tüm değişiklikleri ListView'e otomatik yansıtır.
   - Event tabanlı: MainWindow ile sadece event üzerinden iletişim kurar (coupling yok).
*/
public partial class LayerManagerPanel : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────

    // Ana görünürlük değişti → MainWindow Viewport.HiddenLayers'ı günceller
    public event Action<string, bool>? LayerVisibilityChanged;

    // Kilit durumu değişti → MainWindow seçim filtresini günceller
    public event Action<string, bool>? LayerLockChanged;

    // Dondurma durumu değişti → Viewport re-render tetikler
    public event Action<string, bool>? LayerFreezeChanged;

    // Katman eklendi/silindi → DB sync
    public event Action<string>? LayerCreated;
    public event Action<string>? LayerDeleted;

    // ── State ─────────────────────────────────────────────────────────────────

    private CadDatabase? _database;
    private readonly ObservableCollection<CadLayer> _layers = new();
    private string _searchText = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LayerManagerPanel()
    {
        InitializeComponent();
        LayerList.ItemsSource = _layers;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /*
       NE: Katman Panelini Güncelle (Refresh)
       NEDEN: Viewport değiştiğinde (tab geçişi) veya DB güncellenmesinde
              katman listesini taze veri ile doldurmak için.
              
       NASIL:
       1. DB'deki kayıtlı CadLayer tanımlarını al
       2. Entity.Layer string'lerinden yeni katmanları otomatik ekle (AutoDetect)
       3. Filter uygula
    */
    public void RefreshLayers(CadDatabase database)
    {
        _database = database;

        // Mevcut listeyi temizle
        _layers.Clear();

        // 1. DB'deki tanımlı katmanlar
        var dbLayers = database.GetLayers().ToList();

        // Bir set tut (case-insensitive)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in dbLayers.OrderBy(l => l.Name))
        {
            seen.Add(layer.Name);
            if (PassesFilter(layer.Name))
                _layers.Add(layer);
        }

        // 2. AutoDetect: Tüm entity.Layer değerlerini tara
        foreach (var entity in database.GetAllEntities())
        {
            if (string.IsNullOrWhiteSpace(entity.Layer)) continue;
            if (seen.Contains(entity.Layer)) continue;

            // DB'de tanım yok ama entity kullanıyor → örtük katmanı oluştur
            var newLayer = new CadLayer(entity.Layer)
            {
                Color = 0xFFCCCCCC // Varsayılan gri
            };
            database.AddLayer(newLayer); // DB'ye kaydet
            seen.Add(entity.Layer);

            if (PassesFilter(entity.Layer))
                _layers.Add(newLayer);
        }

        Serilog.Log.Information("[LayerManagerPanel] RefreshLayers: {Count} katman yüklendi.", _layers.Count);
    }

    /*
       NE: Dış Görünürlük Senkronizasyonu (SyncHiddenLayers)
       NEDEN: Başka bir kaynaktan (örn: eski layer toggle kodları) HiddenLayers değiştirildiğinde
              panel ikonlarını senkronize etmek için.
    */
    public void SyncHiddenLayers(IEnumerable<string> hiddenLayerNames)
    {
        var hidden = new HashSet<string>(hiddenLayerNames, StringComparer.OrdinalIgnoreCase);
        foreach (var layer in _layers)
        {
            layer.IsVisible = !hidden.Contains(layer.Name);
        }
    }

    // ── UI Event Handlers ─────────────────────────────────────────────────────

    // 💡 Görünürlük Toggle
    private void OnVisibilityToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string layerName)
        {
            var layer = FindLayer(layerName);
            if (layer == null) return;

            layer.IsVisible = !layer.IsVisible;

            Serilog.Log.Information("[LayerManager] '{Layer}' görünürlük: {V}", layerName, layer.IsVisible);
            LayerVisibilityChanged?.Invoke(layerName, layer.IsVisible);
        }
    }

    // ❄ Dondur Toggle
    private void OnFreezeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string layerName)
        {
            var layer = FindLayer(layerName);
            if (layer == null) return;

            layer.IsFrozen = !layer.IsFrozen;
            LayerFreezeChanged?.Invoke(layerName, layer.IsFrozen);
        }
    }

    // 🔒 Kilit Toggle
    private void OnLockToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string layerName)
        {
            var layer = FindLayer(layerName);
            if (layer == null) return;

            layer.IsLocked = !layer.IsLocked;
            LayerLockChanged?.Invoke(layerName, layer.IsLocked);
        }
    }

    // ■ Renk kutusu tıklaması
    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string layerName)
        {
            var layer = FindLayer(layerName);
            if (layer == null) return;

            // Hex renk girişi (basit ve Forms referansı gerektirmeyen yaklaşım)
            string current = $"#{(layer.Color >> 16) & 0xFF:X2}{(layer.Color >> 8) & 0xFF:X2}{layer.Color & 0xFF:X2}";
            var dlg = new Dialogs.InputDialog("Renk Seç", "Hex renk girin (örn: #FF6600):", current);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                var hex = dlg.InputText.Trim().TrimStart('#');
                if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
                {
                    layer.Color = 0xFF000000 | rgb;
                    Serilog.Log.Information("[LayerManager] '{Layer}' renk: #{C:X6}", layerName, rgb);
                }
                else
                {
                    MessageBox.Show("Geçersiz hex renk kodu. Örnek: FF6600", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    // ⟳ Yenile
    private void OnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_database != null)
            RefreshLayers(_database);
    }

    // 🔍 Arama kutusu
    private void OnSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text?.Trim() ?? string.Empty;
        if (_database != null)
            RefreshLayers(_database);
    }

    // + Yeni Katman
    private void OnNewLayer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Dialogs.InputDialog("Yeni Katman", "Katman adını girin:", $"Layer{_layers.Count + 1}");
        if (dlg.ShowDialog() == true)
        {
            var name = dlg.InputText.Trim();
            if (string.IsNullOrEmpty(name)) return;

            if (_database != null && _database.GetLayers().Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"'{name}' katmanı zaten mevcut.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newLayer = new CadLayer(name) { Color = 0xFFCCCCCC };
            _database?.AddLayer(newLayer);
            _layers.Add(newLayer);
            LayerCreated?.Invoke(name);
            Serilog.Log.Information("[LayerManager] Yeni katman: '{Name}'", name);
        }
    }

    // ✎ Yeniden Adlandır
    private void OnRename_Click(object sender, RoutedEventArgs e)
    {
        if (LayerList.SelectedItem is not CadLayer selected) return;
        if (selected.Name == "0")
        {
            MessageBox.Show("'0' katmanı yeniden adlandırılamaz.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Dialogs.InputDialog("Yeniden Adlandır", "Yeni isim:", selected.Name);
        if (dlg.ShowDialog() == true)
        {
            var newName = dlg.InputText.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != selected.Name)
            {
                // Tüm entity'lerin Layer alanını güncelle
                if (_database != null)
                {
                    foreach (var entity in _database.GetAllEntities().Where(en => en.Layer == selected.Name))
                        entity.Layer = newName;
                }
                selected.Name = newName; // INotifyPropertyChanged yok Name'de, refresh yeterli
                RefreshLayers(_database!);
            }
        }
    }

    // 🗑 Sil
    private void OnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LayerList.SelectedItem is not CadLayer selected) return;
        if (selected.Name == "0")
        {
            MessageBox.Show("'0' katmanı silinemez.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int entityCount = _database?.GetAllEntities().Count(en => en.Layer == selected.Name) ?? 0;
        string msg = entityCount > 0
            ? $"'{selected.Name}' katmanını silmek istiyor musunuz?\n{entityCount} nesne '0' katmanına taşınacak."
            : $"'{selected.Name}' katmanını silmek istiyor musunuz?";

        if (MessageBox.Show(msg, "Katmanı Sil", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            // Entity'leri '0' katmanına taşı
            if (_database != null)
                foreach (var entity in _database.GetAllEntities().Where(en => en.Layer == selected.Name))
                    entity.Layer = "0";

            _layers.Remove(selected);
            LayerDeleted?.Invoke(selected.Name);
        }
    }

    // ☀ Tümünü Göster
    private void OnShowAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in _layers)
        {
            if (!layer.IsVisible)
            {
                layer.IsVisible = true;
                LayerVisibilityChanged?.Invoke(layer.Name, true);
            }
        }
    }

    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    public void HighlightLayer(string? layerName)
    {
        if (string.IsNullOrEmpty(layerName) || LayerList.ItemsSource == null) return;

        foreach (var item in LayerList.Items)
        {
            if (item is CadLayer layer && layer.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
            {
                LayerList.SelectedItem = item;
                LayerList.ScrollIntoView(item);
                return;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CadLayer? FindLayer(string name)
        => _layers.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? _database?.GetLayers().FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private bool PassesFilter(string name)
        => string.IsNullOrEmpty(_searchText)
        || name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
}
