using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Katman Durumu Yöneticisi Diyaloğu (LayerStateManagerDialog)
   NEDEN: LayerStateManagerService'in (Session #75) arayüz karşılığı — denetim raporunun
          "isimlendirilmiş çoklu-state yönetimi yok" bulgusunu tam olarak kapatır: kullanıcı
          mevcut görünürlük/dondurma/kilit durumunu bir isim altında kaydedebilir, listeden
          seçip tek tıkla geri uygulayabilir veya silebilir.
*/
public partial class LayerStateManagerDialog : Window
{
    private readonly CadDatabase _database;
    private readonly LayerStateManagerService _manager;
    private readonly ISet<string> _hiddenLayers;
    private readonly Action? _onApplied;

    private class Row
    {
        public string Name { get; set; } = "";
        public int LayerCount { get; set; }
        public string SavedAtDisplay { get; set; } = "";
        public LayerStateManagerService.LayerStateSnapshot Snapshot { get; set; } = null!;
    }

    public LayerStateManagerDialog(CadDatabase database, LayerStateManagerService manager, ISet<string> hiddenLayers, Action? onApplied = null)
    {
        InitializeComponent();
        _database = database;
        _manager = manager;
        _hiddenLayers = hiddenLayers;
        _onApplied = onApplied;
        Refresh();
    }

    private void Refresh()
    {
        StateGrid.ItemsSource = _manager.Snapshots
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new Row
            {
                Name = s.Name,
                LayerCount = s.Layers.Count,
                SavedAtDisplay = s.SavedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                Snapshot = s
            })
            .ToList();
    }

    private Row? Selected => StateGrid.SelectedItem as Row;

    private void StateGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        bool hasSelection = Selected != null;
        BtnApply.IsEnabled = hasSelection;
        BtnDelete.IsEnabled = hasSelection;
    }

    private void SaveCurrent_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("Durumu Kaydet", "Durum ismi:", "Görünüm 1") { Owner = this };
        if (dlg.ShowDialog() != true) return;

        string name = dlg.InputText.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Geçerli bir isim girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool overwriting = _manager.Find(name) != null;
        _manager.SaveCurrentState(name, _database, _hiddenLayers);
        Refresh();
        TxtInfo.Text = overwriting ? $"'{name}' güncellendi." : $"'{name}' kaydedildi.";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null) return;

        _manager.ApplyState(row.Snapshot, _database, _hiddenLayers);
        _onApplied?.Invoke();
        TxtInfo.Text = $"'{row.Name}' uygulandı.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null) return;

        if (MessageBox.Show($"'{row.Name}' durumu silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _manager.Delete(row.Name);
        Refresh();
        TxtInfo.Text = $"'{row.Name}' silindi.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
