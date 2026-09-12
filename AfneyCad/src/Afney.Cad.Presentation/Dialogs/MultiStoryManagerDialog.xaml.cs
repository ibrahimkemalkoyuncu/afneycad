using System;
using System.Linq;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class MultiStoryManagerDialog : Window
    {
        private readonly CadDatabase _database;
        private readonly MultiStoryBuildingService _buildingService;
        private readonly LevelManager? _levelManager;

        public MultiStoryManagerDialog(CadDatabase database, LevelManager? levelManager = null)
        {
            InitializeComponent();
            _database = database;
            _buildingService = new MultiStoryBuildingService(database);
            _levelManager = levelManager;
        }

        /*
           NE: Kat Yöneticisi (LevelManager) ile senkronizasyon
           NEDEN — GERÇEK BOŞLUK (Session #75 iş akışı denetiminde bulundu, madde 05): Bu
                  ekranın kat listesi (FloorDefinition/mm) ve LevelManagerDialog'un kat
                  listesi (MepLevel/mm) tamamen ayrı, birbirinden habersiz veri modelleriydi.
                  Tam bir veri-modeli birleştirmesi (üçüncü bir model olan DefineBuildingDialog
                  ile birlikte) daha büyük bir yeniden yapılandırma gerektirir — bu yüzden
                  burada güvenli, gerçek bir çift yönlü senkronizasyon köprüsü kuruldu: isim/
                  kot/yükseklik alanları kopyalanıyor (entity/riser atamaları korunmuyor,
                  onlar zaten iki modelde de farklı anlam taşıyor).
        */
        private void ImportFromLevelManager_Click(object sender, RoutedEventArgs e)
        {
            if (_levelManager is null)
            {
                MessageBox.Show("Kat Yöneticisi bu belge için erişilebilir değil.", "Senkronizasyon", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _buildingService.ClearFloors();
            foreach (var level in _levelManager.GetLevels())
                _buildingService.AddFloor(level.Name, level.Elevation, level.Height);

            RefreshGrid();
            InfoText.Text = $"Kat Yöneticisi'nden {_levelManager.GetLevels().Count} kat içe aktarıldı.";
        }

        private void ExportToLevelManager_Click(object sender, RoutedEventArgs e)
        {
            if (_levelManager is null)
            {
                MessageBox.Show("Kat Yöneticisi bu belge için erişilebilir değil.", "Senkronizasyon", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var floors = _buildingService.GetAllFloors();
            if (floors.Count == 0)
            {
                MessageBox.Show("Aktarılacak kat yok — önce 'Standart Bina Oluştur' ile kat listesi oluşturun.", "Senkronizasyon", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _levelManager.Clear();
            foreach (var floor in floors.OrderBy(f => f.Order))
                _levelManager.AddLevel(new MepLevel(floor.Name, floor.Elevation, floor.Height));

            InfoText.Text = $"{floors.Count} kat Kat Yöneticisi'ne aktarıldı.";
        }

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
            if (FloorGrid.SelectedItem is not FloorDefinition source)
            {
                MessageBox.Show("Lütfen Grid'den kaynak katı seçin."); return;
            }
            if (TargetFloorCombo.SelectedItem is not FloorDefinition target)
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
                if (TargetFloorCombo.SelectedItem is FloorDefinition targetFloor)
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
