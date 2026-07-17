using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Ölçü Stili Yöneticisi Dialogu (DimensionStyleManagerDialog)
   NEDEN: DimensionStyleService yazılmıştı (Standard/ISO-25/Compact/Large stilleri) ve bu
          oturumda Dim* komutlarına bağlandı, ama kullanıcının yeni stil oluşturması,
          mevcut bir stili düzenlemesi veya stilleri dosyaya kaydedip başka projede tekrar
          yüklemesi için hiçbir ekran yoktu ("stil şablonları" iddiası bu yüzden yanıltıcıydı).
          Bu dialog o eksik yönetim arayüzünü sağlıyor.
*/
public partial class DimensionStyleManagerDialog : Window
{
    private readonly DimensionStyleService _service;
    private DimensionStyle? _selected;
    private bool _suppressEvents;

    private static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfneyCAD", "dimension_styles.json");

    public string? ActiveStyleName { get; private set; }

    public DimensionStyleManagerDialog(DimensionStyleService service)
    {
        InitializeComponent();
        _service = service;
        RefreshList();
    }

    private void RefreshList()
    {
        string? previouslySelected = _selected?.Name;
        StyleList.ItemsSource = null;
        StyleList.ItemsSource = _service.StyleNames.ToList();

        if (previouslySelected != null && _service.StyleNames.Contains(previouslySelected))
            StyleList.SelectedItem = previouslySelected;
        else
            StyleList.SelectedItem = _service.ActiveStyleName;
    }

    private void StyleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StyleList.SelectedItem is not string name) return;
        _selected = _service.GetStyle(name);
        if (_selected == null) return;

        _suppressEvents = true;
        TxtName.Text        = _selected.Name;
        TxtTextHeight.Text  = _selected.TextHeight.ToString("F1");
        TxtArrowSize.Text   = _selected.ArrowSize.ToString("F1");
        TxtExtLineGap.Text  = _selected.ExtLineGap.ToString("F1");
        TxtExtLineOver.Text = _selected.ExtLineOver.ToString("F1");
        TxtPrecision.Text   = _selected.Precision.ToString();
        ChkShowUnits.IsChecked = _selected.ShowUnits;
        SelectComboByContent(CmbUnitFormat, _selected.UnitFormat);
        SelectComboByContent(CmbArrowStyle, _selected.ArrowStyle);
        _suppressEvents = false;

        TxtStatus.Text = $"Düzenleniyor: {_selected.Name}" + (_selected.Name == _service.ActiveStyleName ? " (aktif)" : "");
    }

    private static void SelectComboByContent(ComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void TxtName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selected == null) return;
        string newName = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == _selected.Name) return;

        bool wasActive = _selected.Name == _service.ActiveStyleName;
        _service.RemoveStyle(_selected.Name);
        _selected.Name = newName;
        _service.AddStyle(_selected);
        if (wasActive) _service.SetActiveStyle(newName);
        RefreshList();
    }

    private void Field_LostFocus(object sender, RoutedEventArgs e) => ApplyFieldsToSelected();
    private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFieldsToSelected();
    private void Field_CheckChanged(object sender, RoutedEventArgs e) => ApplyFieldsToSelected();

    private void ApplyFieldsToSelected()
    {
        if (_suppressEvents || _selected == null) return;

        if (double.TryParse(TxtTextHeight.Text, out double th)) _selected.TextHeight = th;
        if (double.TryParse(TxtArrowSize.Text, out double az)) _selected.ArrowSize = az;
        if (double.TryParse(TxtExtLineGap.Text, out double gap)) _selected.ExtLineGap = gap;
        if (double.TryParse(TxtExtLineOver.Text, out double over)) _selected.ExtLineOver = over;
        if (int.TryParse(TxtPrecision.Text, out int prec)) _selected.Precision = prec;
        _selected.ShowUnits = ChkShowUnits.IsChecked == true;
        _selected.UnitFormat = (CmbUnitFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "mm";
        _selected.ArrowStyle = (CmbArrowStyle.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Filled";

        TxtStatus.Text = $"'{_selected.Name}' güncellendi.";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        string baseName = "YeniStil";
        string name = baseName;
        int i = 1;
        while (_service.StyleNames.Contains(name)) name = $"{baseName}{i++}";

        _service.AddStyle(new DimensionStyle { Name = name });
        RefreshList();
        StyleList.SelectedItem = name;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (_selected.Name == "Standard")
        {
            MessageBox.Show("Standard stili silinemez.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _service.RemoveStyle(_selected.Name);
        _selected = null;
        RefreshList();
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if (StyleList.SelectedItem is not string name) return;
        _service.SetActiveStyle(name);
        ActiveStyleName = name;
        TxtStatus.Text = $"'{name}' aktif stil olarak ayarlandı.";
        RefreshList();
    }

    private void SaveToFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = Path.GetDirectoryName(DefaultFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(DefaultFilePath, _service.ExportToJson());
            TxtStatus.Text = $"Kaydedildi: {DefaultFilePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadFromFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(DefaultFilePath))
            {
                MessageBox.Show("Kayıtlı stil dosyası bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _service.ImportFromJson(File.ReadAllText(DefaultFilePath));
            RefreshList();
            TxtStatus.Text = "Stiller dosyadan yüklendi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
