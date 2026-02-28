using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class FixtureLibraryDialog : Window
    {
        private readonly FixtureLibraryService _library = new();

        public FixtureLibraryDialog()
        {
            InitializeComponent();
            LoadCategories();
            LoadFixtures();
        }

        private void LoadCategories()
        {
            foreach (var cat in _library.GetCategories())
            {
                CategoryFilter.Items.Add(new ComboBoxItem { Content = cat });
            }
        }

        private void LoadFixtures(string? category = null, string? search = null)
        {
            if (FixtureGrid == null) return; // XAML henüz yüklenmemiş olabilir
            
            var items = _library.GetAll();
            if (!string.IsNullOrEmpty(category) && category != "Tümü")
                items = items.Where(f => f.Category == category).ToList();
            if (!string.IsNullOrEmpty(search))
                items = items.Where(f => (f.NameTR ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                        (f.NameEN ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            FixtureGrid.ItemsSource = items;
            if (CountText != null) CountText.Text = $"{items.Count} cihaz";
        }

        private void CategoryFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryFilter.SelectedItem is ComboBoxItem item)
                LoadFixtures(item.Content?.ToString(), SearchBox?.Text);
        }

        private void SearchBox_Changed(object sender, TextChangedEventArgs e)
        {
            var cat = (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
            LoadFixtures(cat, SearchBox.Text);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
