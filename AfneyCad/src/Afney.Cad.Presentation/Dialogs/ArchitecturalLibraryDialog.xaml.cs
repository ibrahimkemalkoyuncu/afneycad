using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ArchitecturalLibraryDialog
{
    private readonly ArchitecturalLibraryService _library = new();
    private readonly MechanicalKernel _kernel;

    // View-model ile binding için lambda wrapper
    private class ItemRow
    {
        public ArchitecturalLibraryService.ArchitecturalItem Item { get; set; } = null!;
        public string Id        => Item.Id;
        public string NameTR    => Item.NameTR;
        public string SizeLabel => $"{Item.Width:F0}×{Item.Depth:F0}";
        public double Height    => Item.Height;
        public string ULabel    => Item.UValue > 0 ? $"{Item.UValue:F2}" : "—";
    }

    public ArchitecturalLibraryDialog(MechanicalKernel kernel)
    {
        InitializeComponent();
        _kernel = kernel;

        // Kategoriler
        CategoryCombo.Items.Add(new ComboBoxItem { Content = "— Tümü —", IsSelected = true });
        foreach (var cat in _library.GetCategories())
            CategoryCombo.Items.Add(new ComboBoxItem { Content = cat });

        LoadItems(null);
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e) =>
        LoadItems((CategoryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString());

    private void LoadItems(string? category)
    {
        var items = (category == null || category.StartsWith("—"))
            ? _library.GetAll()
            : _library.GetByCategory(category);

        ItemGrid.ItemsSource = items.Select(i => new ItemRow { Item = i }).ToList();
    }

    private void ItemGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemGrid.SelectedItem is ItemRow row)
        {
            UpdateDetail(row.Item);
            BtnInsert.IsEnabled = true;
        }
    }

    private void ItemGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemGrid.SelectedItem is ItemRow) Insert_Click(sender, e);
    }

    private void UpdateDetail(ArchitecturalLibraryService.ArchitecturalItem item)
    {
        DtlName.Text  = item.NameTR;
        DtlId.Text    = item.Id;
        DtlWidth.Text = $"{item.Width:F0} mm";
        DtlDepth.Text = $"{item.Depth:F0} mm";
        DtlHeight.Text = $"{item.Height:F0} mm";
        DtlU.Text     = item.UValue > 0 ? $"{item.UValue:F2} W/m²K" : "—";
        DtlFire.Text  = item.FireResistanceMin > 0 ? $"REI {item.FireResistanceMin} ({item.FireRating})" : "—";
        DtlDesc.Text  = item.Description;
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (ItemGrid.SelectedItem is not ItemRow row) return;

        var obstacle = _library.CreateObstacle(row.Item, Vector3D.Zero);
        _kernel.ArchitecturalObstacles.Add(obstacle);

        StatusText.Text = $"✓ {row.Item.NameTR} BIM olarak eklendi.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
