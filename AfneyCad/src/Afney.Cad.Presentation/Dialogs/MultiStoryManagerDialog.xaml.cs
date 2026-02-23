using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class MultiStoryManagerDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly MultiStoryBuildingService _buildingService;

        public MultiStoryManagerDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            _buildingService = new MultiStoryBuildingService(database);
        }

        private void CreateBuilding_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int floorCount = int.Parse(FloorCountInput.Text);
                double floorHeight = double.Parse(FloorHeightInput.Text);
                bool hasBasement = BasementCheck.IsChecked == true;

                var floors = _buildingService.InitializeStandardBuilding(floorCount, floorHeight, hasBasement);
                FloorGrid.ItemsSource = null;
                FloorGrid.ItemsSource = floors;
                InfoText.Text = $"Bina oluşturuldu: {floors.Count} kat | Toplam yükseklik: {_buildingService.GetTotalBuildingHeight():F1} m";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bina oluşturma hatası: {ex.Message}");
            }
        }

        private void CopyFloor_Click(object sender, RoutedEventArgs e)
        {
            if (FloorGrid.SelectedItem is not FloorDefinition source)
            {
                MessageBox.Show("Lütfen kaynak katı seçin."); return;
            }
            var floors = _buildingService.GetAllFloors();
            if (floors.Count < 2)
            {
                MessageBox.Show("En az 2 kat gerekli."); return;
            }
            // İlk farklı katı hedef olarak seç
            FloorDefinition? target = null;
            foreach (var f in floors)
            {
                if (f.Id != source.Id) { target = f; break; }
            }
            if (target == null) return;

            int copied = _buildingService.CopyFloorPlumbing(source.Id, target.Id);
            MessageBox.Show($"{copied} adet tesisat bileşeni '{source.Name}' → '{target.Name}' katına kopyalandı.");
            RefreshGrid();
        }

        private void CreateRiser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var riserPos = new Vector3D(1000, 1000, 0);
                var pipes = _buildingService.CreateRiser(riserPos, 100, MechanicalSystemType.DomesticColdWater);
                foreach (var p in pipes) _database.AddEntity(p);
                MessageBox.Show($"{pipes.Count} adet dikey kolon segmenti oluşturuldu (DN100, Soğuk Su).");
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kolon hatası: {ex.Message}");
            }
        }

        private void RefreshGrid()
        {
            FloorGrid.ItemsSource = null;
            FloorGrid.ItemsSource = _buildingService.GetAllFloors();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
