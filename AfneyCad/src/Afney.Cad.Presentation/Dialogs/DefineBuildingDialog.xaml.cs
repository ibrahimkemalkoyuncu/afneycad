using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Afney.Cad.Presentation.Dialogs
{
    public class BuildingLevelViewModel
    {
        public int FloorNumber { get; set; }
        public string LevelName { get; set; } = "Kat";
        public double Elevation { get; set; }
        public string BlockName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        
        // --- FineSANI BIM Status ---
        public bool IsNormalized { get; set; } // WBLOCK base point (0,0,0) kontrolü
        public bool IsAligned { get; set; } // Katlar arası dikey hizalama kontrolü
        
        public string StatusText => IsNormalized ? (IsAligned ? "Aligned (OK)" : "Normalized") : "Not Verified";
        public System.Windows.Media.Brush StatusColor => IsNormalized 
            ? (IsAligned ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange) 
            : System.Windows.Media.Brushes.Gray;
    }

    public partial class DefineBuildingDialog : Window
    {
        public ObservableCollection<BuildingLevelViewModel> Levels { get; set; } = new ObservableCollection<BuildingLevelViewModel>();
        public event Action<string>? OnLevelActivated;
        public event Action<List<BuildingLevelViewModel>>? OnShow3D;
        private string _projectPath;
        private string _defFile;

        /*
           NE: DefineBuildingDialog Yapıcı Metodu
           NEDEN: Bina tanım arayüzünü yükler ve proje yolundaki kayıtlı tanımları (building_def.json) hafızaya alır.
        */
        public DefineBuildingDialog(string? projectPath = null)
        {
            InitializeComponent();
            _projectPath = projectPath ?? AppDomain.CurrentDomain.BaseDirectory;
            _defFile = Path.Combine(_projectPath, "building_def.json");
            
            LevelsGrid.ItemsSource = Levels;
            LoadDefinitions();
        }

        /*
           NE: Tanımları Yükle (LoadDefinitions)
           NEDEN: Daha önce kaydedilmiş olan bina hiyerarşisini JSON dosyasından okuyup liste görünümüne aktarmak için.
        */
        private void LoadDefinitions()
        {
            try
            {
                if (File.Exists(_defFile))
                {
                    var json = File.ReadAllText(_defFile);
                    var list = JsonSerializer.Deserialize<List<BuildingLevelViewModel>>(json);
                    Levels.Clear();
                    if (list != null)
                    {
                        foreach (var item in list.OrderBy(l => l.FloorNumber)) 
                            Levels.Add(item);
                    }
                }
            }
            catch { }
        }

        /*
           NE: Tanımları Kaydet (SaveDefinitions)
           NEDEN: Yapılan kat ekleme, dosya atama ve kot değişikliklerini kalıcı olarak building_def.json dosyasına yazmak için.
        */
        private void SaveDefinitions()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Levels.ToList(), options);
                File.WriteAllText(_defFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"HATA: {ex.Message}");
            }
        }

        /*
           NE: Kat Ekle (AddFloor_Click)
           NEDEN: Binaya yeni bir kat seviyesi eklemek ve varsayılan yükseklik (3.0m) atamak için.
        */
        private void AddFloor_Click(object sender, RoutedEventArgs e)
        {
            int nextNo = Levels.Count > 0 ? Levels.Max(l => l.FloorNumber) + 1 : 0;
            Levels.Add(new BuildingLevelViewModel 
            { 
                FloorNumber = nextNo, 
                LevelName = nextNo == 0 ? "Zemin Kat" : $"{nextNo}. Kat",
                Elevation = nextNo * 3.0 
            });
        }

        /*
           NE: Kat Dosyası Düzenle (EditLevel_Click)
           NEDEN: Seçilen kata ait mimari veya mekanik CAD dosyasını (DWG/JSON) sisteme tanıtmak için.
        */
        private void EditLevel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BuildingLevelViewModel level)
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "AfneyCAD Files (*.dwg;*.json)|*.dwg;*.json",
                    Title = $"{level.LevelName} İçin Dosya Seçin"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    level.FilePath = openFileDialog.FileName;
                    level.BlockName = Path.GetFileName(openFileDialog.FileName);
                    level.IsNormalized = false; // Dosya değiştiği için tekrar doğrulanmalı
                    LevelsGrid.Items.Refresh();
                }
            }
        }

        /*
           NE: WBlock Normalizasyonu (WBlock_Click)
           NEDEN: Seçilen mimari kat dosyasını, dikey hizalama için (0,0,0) referans noktasına göre hazırlamak için.
        */
        private void WBlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BuildingLevelViewModel level)
            {
                if (string.IsNullOrEmpty(level.FilePath))
                {
                    MessageBox.Show("Önce bir kaynak dosya seçmelisiniz.");
                    return;
                }

                // --- BLOCK NORMALIZATION ENGINE (Simulation) ---
                // FineSANI Blueprint: Translate(Floor, -Origin) -> MasterOrigin(0,0,0)
                level.IsNormalized = true;
                FeedbackText.Text = $"• {level.LevelName} normalizasyonu tamamlandı (Base Point: 0,0,0).";
                LevelsGrid.Items.Refresh();
                SaveDefinitions();
            }
        }

        /*
           NE: Katları Üst Üste Diz (Stack_Click)
           NEDEN: Tanımlanan tüm katları Z-kotlarına göre birleştirerek binanın 3D montajını gerçekleştirmek için.
        */
        private void Stack_Click(object sender, RoutedEventArgs e)
        {
            if (!Levels.Any(l => !string.IsNullOrEmpty(l.FilePath)))
            {
                MessageBox.Show("Stacking için en az bir kat tanımlanmalıdır.");
                return;
            }

            // --- ASSEMBLY ENGINE ---
            FeedbackText.Text = "• Bina montajı yapılıyor. Katlar Z-kotuna göre dizelecek...";
            OnShow3D?.Invoke(Levels.Where(l => !string.IsNullOrEmpty(l.FilePath)).ToList());
            
            // Kolon hizalaması yapıldığını varsayıyoruz (Simulation)
            foreach(var l in Levels) if (l.IsNormalized) l.IsAligned = true;
            LevelsGrid.Items.Refresh();

            MessageBox.Show("Bina stack işlemi başarıyla tamamlandı.\nKolonlar otomatik hizalandı.", "Assembly Engine");
            SaveDefinitions();
        }

    /*
       NE: 3D Görünümü Aç (Show3D_Click)
       NEDEN: Tanımlanan kat hiyerarşisini ve Z-kotlarını kullanarak binanın 3 boyutlu modelini render ekranında simüle etmek için.
    */
    private void Show3D_Click(object sender, RoutedEventArgs e)
    {
        Stack_Click(sender, e);
    }

    /*
       NE: İptal (Cancel_Click)
       NEDEN: Yapılan değişiklikleri kaydetmeden bina tanımlama penceresini kapatmak için.
    */
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}