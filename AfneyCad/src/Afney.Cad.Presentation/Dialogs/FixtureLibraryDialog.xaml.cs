using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class FixtureLibraryDialog : Window
    {
        private readonly FixtureLibraryService _library = new();
        private readonly CadDatabase? _database;
        private FixtureLibraryService.FixtureDefinition? _selected;

        public FixtureLibraryDialog(CadDatabase? database = null)
        {
            InitializeComponent();
            _database = database;
            LoadFixtures();
        }

        private void LoadFixtures(string? category = null, string? search = null)
        {
            if (FixtureGrid == null) return;

            var items = _library.GetAll();
            if (!string.IsNullOrEmpty(category) && category != "Tümü")
                items = items.Where(f => f.Category == category).ToList();
            if (!string.IsNullOrEmpty(search))
                items = items.Where(f =>
                    (f.NameTR ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (f.NameEN ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (f.Id ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

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

        private void FixtureGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = FixtureGrid.SelectedItem as FixtureLibraryService.FixtureDefinition;
            UpdateDetailPanel(_selected);
            BtnInsert.IsEnabled = _selected != null && _database != null;
        }

        private void FixtureGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selected != null && _database != null)
                Insert_Click(sender, e);
        }

        private void Insert_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _database == null) return;

            double x = ParseDouble(TxtInsertX.Text, 0.0);
            double y = ParseDouble(TxtInsertY.Text, 0.0);
            double rotDeg = ParseDouble(TxtRotation.Text, 0.0);

            try
            {
                var pos = new Vector3D(x, y, 0);
                var entity = _library.CreateEntity(_selected.Id, pos);
                entity.Rotation = rotDeg * Math.PI / 180.0;
                _database.AddEntity(entity);

                TxtStatus.Text = $"✓ '{_selected.NameTR}' çizime eklendi ({x:F0}, {y:F0})";

                // Sonraki ekleme için Y ofset öner
                TxtInsertY.Text = (y + _selected.SymbolHeight + 100).ToString("F0");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Hata: {ex.Message}";
            }
        }

        private void UpdateDetailPanel(FixtureLibraryService.FixtureDefinition? def)
        {
            if (def == null)
            {
                DtlName.Text = "—";
                DtlId.Text = "";
                DtlLU.Text = "—";
                DtlColdDN.Text = "—";
                DtlHotDN.Text = "—";
                DtlWasteDN.Text = "—";
                DtlFlow.Text = "—";
                DtlHotReq.Text = "—";
                DtlVent.Text = "—";
                DtlStd.Text = "—";
                return;
            }

            DtlName.Text = def.NameTR;
            DtlId.Text = $"ID: {def.Id}  [{def.NameEN}]";
            DtlLU.Text = def.LoadUnit.ToString("F1");
            DtlColdDN.Text = def.MinColdWaterDN > 0 ? $"DN{def.MinColdWaterDN}" : "—";
            DtlHotDN.Text = def.MinHotWaterDN > 0 ? $"DN{def.MinHotWaterDN}" : "—";
            DtlWasteDN.Text = def.WasteDN > 0 ? $"DN{def.WasteDN}" : "—";
            DtlFlow.Text = def.FlowRateLps > 0 ? $"{def.FlowRateLps:F2} l/s" : "—";
            DtlHotReq.Text = def.RequiresHotWater ? "Evet" : "Hayır";
            DtlVent.Text = def.RequiresVent ? "Gerekli" : "Gerekli değil";
            DtlStd.Text = def.Standard;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private static double ParseDouble(string s, double fallback)
            => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
    }
}
