using System;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    /*
       NE: Çok Katlı Bina Yöneticisi (MultiStoryManagerDialog)
       NEDEN — Session #75 iş akışı denetiminde bulunan veri-modeli parçalanmasının
              kapatılması: bu ekran önceden kendi özel FloorDefinition listesini tutuyordu ve
              LevelManagerDialog'un kat listesinden (MepLevel) tamamen habersizdi — aralarında
              manuel bir "İçe/Dışa Aktar" köprüsü vardı (madde 65). Artık bu ekran doğrudan
              MainWindow'un paylaştığı AYNI LevelManager örneğini görüntülüyor/düzenliyor —
              köprüye gerek kalmadı, iki ekran her zaman aynı veriye bakıyor.
    */
    public partial class MultiStoryManagerDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly LevelManager _levelManager;
        private readonly MultiStoryBuildingService _buildingService;

        public MultiStoryManagerDialog(CadDatabase database, LevelManager levelManager)
        {
            InitializeComponent();
            _database = database;
            _levelManager = levelManager;
            _buildingService = new MultiStoryBuildingService(database, levelManager);

            _levelManager.LevelTableChanged += OnLevelTableChanged;
            Closed += (_, _) => _levelManager.LevelTableChanged -= OnLevelTableChanged;

            RefreshGrid();
        }

        private void OnLevelTableChanged() => RefreshGrid();

        private void CreateBuilding_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int floorCount = int.Parse(FloorCountInput.Text);
                double floorHeight = double.Parse(FloorHeightInput.Text);
                bool hasBasement = BasementCheck.IsChecked == true;

                var floors = _buildingService.InitializeStandardBuilding(floorCount, floorHeight, hasBasement);
                RefreshGrid();
                InfoText.Text = $"Bina oluşturuldu: {floors.Count} kat | Toplam yükseklik: {_buildingService.GetTotalBuildingHeight():F1} m";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bina oluşturma hatası: {ex.Message}");
            }
        }

        private void CopyFloor_Click(object sender, RoutedEventArgs e)
        {
            if (FloorGrid.SelectedItem is not MepLevel source)
            {
                MessageBox.Show("Lütfen Grid'den kaynak katı seçin."); return;
            }
            if (TargetFloorCombo.SelectedItem is not MepLevel target)
            {
                MessageBox.Show("Lütfen 'Hedef Kat' açılır listesinden hedef katı seçin."); return;
            }
            if (source.Id == target.Id)
            {
                MessageBox.Show("Kaynak ve hedef kat aynı olamaz."); return;
            }

            int copied = _buildingService.CopyFloorPlumbing(source.Id, target.Id);
            InfoText.Text = $"{copied} bileşen '{source.Name}' → '{target.Name}' kopyalandı.";
            RefreshGrid();
        }

        private void CreateRiser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sistem tipi
                string sysText = (RiserSystemCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Soğuk Su";
                var systemType = sysText switch
                {
                    "Sıcak Su" => MechanicalSystemType.DomesticHotWater,
                    "Pis Su"   => MechanicalSystemType.WasteWater,
                    "Yangın"   => MechanicalSystemType.FireProtection,
                    "Gaz"      => MechanicalSystemType.Gas,
                    "Yağmur"   => MechanicalSystemType.RainWater,
                    _          => MechanicalSystemType.DomesticColdWater
                };

                // Çap
                string dnText = (RiserDnCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "DN50";
                double diameter = double.TryParse(dnText.Replace("DN", ""), out double d) ? d : 50.0;

                // Hedef kat aralığı
                string? fromFloor = null, toFloor = null;
                if (TargetFloorCombo.SelectedItem is MepLevel targetFloor)
                {
                    toFloor = targetFloor.Name;
                }

                var riserPos = new Vector3D(0, 0, 0);
                var pipes = _buildingService.CreateRiser(riserPos, diameter, systemType, fromFloor, toFloor);
                foreach (var p in pipes) _database.AddEntity(p);
                InfoText.Text = $"{pipes.Count} kolon segmenti oluşturuldu ({dnText}, {sysText}).";
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kolon hatası: {ex.Message}");
            }
        }

        private void RefreshGrid()
        {
            var floors = _buildingService.GetAllFloors();
            FloorGrid.ItemsSource = null;
            FloorGrid.ItemsSource = floors;
            TargetFloorCombo.ItemsSource = null;
            TargetFloorCombo.ItemsSource = floors;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
