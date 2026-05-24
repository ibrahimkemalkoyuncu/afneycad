using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class UserFixtureCatalogDialog : Window
{
    private readonly FixtureLibraryService _library;
    private readonly ObservableCollection<FixtureLibraryService.FixtureDefinition> _items = [];
    private bool _dirty;

    public UserFixtureCatalogDialog(FixtureLibraryService library)
    {
        InitializeComponent();
        _library = library;
        Reload();
    }

    private void Reload()
    {
        _items.Clear();
        foreach (var def in _library.GetAll())
            _items.Add(def);
        CatalogGrid.ItemsSource = _items;
        CountText.Text = $"{_items.Count} cihaz";
        _dirty = false;
    }

    private void CatalogGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void CatalogGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        => _dirty = true;

    private void AddNew_Click(object sender, RoutedEventArgs e)
    {
        var newDef = new FixtureLibraryService.FixtureDefinition
        {
            Id = $"USR-{DateTime.Now:HHmmss}",
            NameTR = "Yeni Cihaz",
            NameEN = "New Device",
            Category = "Diğer",
            LoadUnit = 1.0,
            MinColdWaterDN = 15,
            WasteDN = 40,
            FlowRateLps = 0.05,
            SymbolWidth = 400,
            SymbolHeight = 400,
            Standard = "TS 1258"
        };
        _items.Add(newDef);
        CatalogGrid.SelectedItem = newDef;
        CatalogGrid.ScrollIntoView(newDef);
        _dirty = true;
        TxtStatus.Text = "Yeni cihaz eklendi — düzenleyip Kaydet'e basın.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is not FixtureLibraryService.FixtureDefinition def) return;
        var ans = MessageBox.Show($"'{def.NameTR}' silinsin mi?", "Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;
        _items.Remove(def);
        _dirty = true;
        TxtStatus.Text = $"'{def.NameTR}' listeden kaldırıldı.";
        CountText.Text = $"{_items.Count} cihaz";
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is not FixtureLibraryService.FixtureDefinition src) return;
        var copy = new FixtureLibraryService.FixtureDefinition
        {
            Id = $"{src.Id}-KOPYA",
            NameTR = src.NameTR + " (Kopya)",
            NameEN = src.NameEN + " (Copy)",
            Category = src.Category,
            LoadUnit = src.LoadUnit,
            MinColdWaterDN = src.MinColdWaterDN,
            MinHotWaterDN = src.MinHotWaterDN,
            WasteDN = src.WasteDN,
            FlowRateLps = src.FlowRateLps,
            RequiresHotWater = src.RequiresHotWater,
            RequiresVent = src.RequiresVent,
            SymbolType = src.SymbolType,
            SymbolWidth = src.SymbolWidth,
            SymbolHeight = src.SymbolHeight,
            Standard = src.Standard
        };
        _items.Add(copy);
        CatalogGrid.SelectedItem = copy;
        CatalogGrid.ScrollIntoView(copy);
        _dirty = true;
        TxtStatus.Text = $"'{src.NameTR}' kopyalandı.";
    }

    private void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Vitrifiye Kataloğu JSON Aktar",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = ".json"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var ans = MessageBox.Show("Mevcut katalogla birleştir?\n(Hayır = Tümünü değiştir)",
                "JSON Aktar", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Cancel) return;

            _library.ImportJson(dlg.FileName, merge: ans == MessageBoxResult.Yes);
            Reload();
            TxtStatus.Text = $"JSON aktarıldı: {System.IO.Path.GetFileName(dlg.FileName)} — {_items.Count} cihaz.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"JSON aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Vitrifiye Kataloğu JSON Ver",
            Filter = "JSON (*.json)|*.json",
            FileName = $"FixtureCatalog_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".json"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            CommitGridEdits();
            _library.ExportJson(dlg.FileName);
            TxtStatus.Text = $"JSON verildi: {System.IO.Path.GetFileName(dlg.FileName)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                System.IO.Path.GetDirectoryName(dlg.FileName)!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"JSON verme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var ans = MessageBox.Show("Katalog fabrika varsayılanlarına sıfırlansın mı?\nTüm özel cihazlar silinecek.",
            "Sıfırla", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;
        _library.ResetToDefaults();
        Reload();
        TxtStatus.Text = "Katalog fabrika varsayılanlarına sıfırlandı.";
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        CommitGridEdits();
        foreach (var def in _items)
            _library.AddOrUpdate(def);

        // Silinen öğeleri kaldır
        var currentIds = _items.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in _library.GetAll().ToList())
            if (!currentIds.Contains(existing.Id))
                _library.Delete(existing.Id);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_dirty)
        {
            var ans = MessageBox.Show("Değişiklikler kaydedilmeyecek. Çıkmak istiyor musunuz?",
                "İptal", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans != MessageBoxResult.Yes) return;
        }
        DialogResult = false;
        Close();
    }

    private void CommitGridEdits()
    {
        CatalogGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
    }
}
