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
        private readonly MultiStoryEnhancementService _enhancementService;

        public MultiStoryManagerDialog(CadDatabase database, LevelManager levelManager)
        {
            InitializeComponent();
            _database = database;
            _levelManager = levelManager;
            _buildingService = new MultiStoryBuildingService(database, levelManager);
            _enhancementService = new MultiStoryEnhancementService(database, levelManager);

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

        /*
           NE/NEDEN — GERÇEK HAYALET ÖZELLİK (bu turda bulundu): `MultiStoryEnhancementService`
           tam çalışan, test edilmiş (`MultiStoryEnhancementServiceTests`), hatta kendi içinde
           gerçek bir dejenere-sıfır-uzunluklu-bağlantı-borusu hatasının düzeltmesini taşıyan
           bir servisti — ama HİÇBİR yerden çağrılmıyordu (ne bir dialog ne bir komut). Bu buton
           artık `MultiStoryBuildingService.CopyFloorPlumbing` (basit, bağlantı korumayan kopya)
           yerine `CopyFloorWithConnections`'ı kullanıyor: kopyalanan borular arasındaki mevcut
           bağlantıları koruyor VE kat-arası kolon bağlantısını (sadece gerçek bir boşluk varsa)
           otomatik kuruyor.
        */
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

            var result = _enhancementService.CopyFloorWithConnections(source, target);
            InfoText.Text = $"{result.CopiedCount} bileşen '{source.Name}' → '{target.Name}' kopyalandı " +
                             $"({result.ConnectionsPreserved} bağlantı korundu, {result.RiserConnectionsCreated} kolon bağlantısı kuruldu).";
            RefreshGrid();
        }

        /*
           NE: Katlar Arası Kolonları Otomatik Bağla (AutoConnectRisers_Click)
           NEDEN: `MultiStoryEnhancementService.AutoConnectInterFloorRisers` — aynı XY konumunda,
                  aynı sistem tipindeki, katlar arasında kalan riser boşluklarını otomatik
                  bağlantı borusuyla dolduran, test edilmiş bir yetenekti ama hiçbir ekrandan
                  erişilemiyordu.
        */
        private void AutoConnectRisers_Click(object sender, RoutedEventArgs e)
        {
            int connections = _enhancementService.AutoConnectInterFloorRisers();
            InfoText.Text = connections > 0
                ? $"{connections} katlar arası kolon bağlantısı kuruldu."
                : "Bağlanacak eşleşen riser bulunamadı (aynı XY konumu + aynı sistem tipi gerekli).";
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
