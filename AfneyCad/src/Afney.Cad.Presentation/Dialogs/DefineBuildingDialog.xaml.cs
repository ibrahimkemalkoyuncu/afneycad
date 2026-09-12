using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;
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
        
        public string StatusText => IsNormalized ? (IsAligned ? "Hizalı (OK)" : "Tanımlı") : "Doğrulanmadı";
        public System.Windows.Media.Brush StatusColor => IsNormalized 
            ? (IsAligned ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange) 
            : System.Windows.Media.Brushes.Gray;
    }

    public partial class DefineBuildingDialog : Window
    {
        public ObservableCollection<BuildingLevelViewModel> Levels { get; set; } = new ObservableCollection<BuildingLevelViewModel>();
        public event Action<string>? OnLevelActivated;
        public event Action<List<BuildingLevelViewModel>>? OnShow3D;

        /*
           NE: Montaj Tamamlanma Bildirimi (ReportAssemblyCompleted)
           NEDEN — GERÇEK HATA (Session #75 iş akışı denetiminde bulundu): `Stack_Click`
                  önceden `OnShow3D`'yi (asenkron, fire-and-forget) tetikleyip HEMEN ARDINDAN,
                  gerçek montaj işi arka planda daha BAŞLAMADAN/BİTMEDEN, tüm normalize
                  katları "hizalandı" işaretleyip başarı mesajı gösteriyordu. Artık bu metod
                  çağıranın (MainWindow) gerçek montaj tamamlandığında GERÇEK kolon bağlantı
                  sayısıyla çağırması için dışa açık — sadece o zaman durum güncellenir.
        */
        public void ReportAssemblyCompleted(int connectedRiserCount)
        {
            foreach (var l in Levels) l.IsAligned = l.IsNormalized && connectedRiserCount > 0;
            LevelsGrid.Items.Refresh();
            SaveDefinitions();

            FeedbackText.Text = connectedRiserCount > 0
                ? $"• Montaj tamamlandı: {connectedRiserCount} kolon bağlantısı gerçekten kuruldu."
                : "• Montaj tamamlandı ama hiçbir kolon eşleşmedi (katlar arası dikey boru bulunamadı) — hizalama yapılmadı.";

            MessageBox.Show(
                connectedRiserCount > 0
                    ? $"Bina montajı tamamlandı.\n{connectedRiserCount} kolon bağlantısı kuruldu."
                    : "Bina montajı tamamlandı ama hiçbir kolon bağlantısı bulunamadı.\nKatlar arası dikey boru (riser) çizili mi kontrol edin.",
                "Montaj Motoru", MessageBoxButton.OK,
                connectedRiserCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        private string _projectPath;
        private string _defFile;
        private readonly LevelManager? _levelManager;

        /*
           NE: DefineBuildingDialog Yapıcı Metodu
           NEDEN: Bina tanım arayüzünü yükler ve proje yolundaki kayıtlı tanımları (building_def.json) hafızaya alır.
                  `levelManager` opsiyoneldir — verildiğinde "Kat Yöneticisi'nden Al" butonu
                  canlı belgenin kat isim/kotlarını tek yönlü ön-doldurma için kullanabilir
                  (bkz. ImportNamesFromLevelManager_Click).
        */
        public DefineBuildingDialog(string? projectPath = null, LevelManager? levelManager = null)
        {
            InitializeComponent();
            _projectPath = projectPath ?? AppDomain.CurrentDomain.BaseDirectory;
            _defFile = Path.Combine(_projectPath, "building_def.json");
            _levelManager = levelManager;

            LevelsGrid.ItemsSource = Levels;
            LoadDefinitions();
        }

        /*
           NE: Kat Yöneticisi'nden İsim/Kot Al (ImportNamesFromLevelManager_Click)
           NEDEN — Session #75 iş akışı denetiminde bulunan boşluğun kapatılması: bu ekranın kat
                  listesi (dosya-başına-kat, `building_def.json`) ile canlı belgenin kat listesi
                  (`LevelManager`/`MepLevel`) arasında hiçbir bağlantı yoktu — kullanıcı aynı kat
                  isimlerini/kotlarını üçüncü kez elle giriyordu. Bu TEK YÖNLÜ bir ön-doldurma —
                  canlı senkronizasyon DEĞİL: `building_def.json` birden fazla AYRI dosyayı
                  birleştirmeyi tanımlar, bu yüzden otomatik/sürekli senkron yanıltıcı olurdu.
                  Sadece henüz dosya atanmamış (FilePath boş) satırlar dolduruluyor — kullanıcının
                  zaten yapılandırdığı satırlara asla dokunulmuyor.
        */
        private void ImportNamesFromLevelManager_Click(object sender, RoutedEventArgs e)
        {
            if (_levelManager is null)
            {
                MessageBox.Show("Kat Yöneticisi bu belge için erişilebilir değil.", "Kat Yöneticisi'nden Al", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceLevels = _levelManager.GetLevels().OrderBy(l => l.Order).ToList();
            if (sourceLevels.Count == 0)
            {
                MessageBox.Show("Kat Yöneticisi'nde henüz tanımlı kat yok.", "Kat Yöneticisi'nden Al", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int filled = 0;
            var unassigned = Levels.Where(l => string.IsNullOrEmpty(l.FilePath)).ToList();

            for (int i = 0; i < unassigned.Count && i < sourceLevels.Count; i++)
            {
                unassigned[i].LevelName = sourceLevels[i].Name;
                unassigned[i].Elevation = sourceLevels[i].Elevation / 1000.0; // mm -> m (bu dialog metre kullanıyor)
                filled++;
            }

            // Bu ekranda hiç satır yoksa veya sourceLevels daha fazlaysa, kalanlar için yeni satır ekle.
            for (int i = unassigned.Count; i < sourceLevels.Count; i++)
            {
                int nextNo = Levels.Count > 0 ? Levels.Max(l => l.FloorNumber) + 1 : 0;
                Levels.Add(new BuildingLevelViewModel
                {
                    FloorNumber = nextNo,
                    LevelName = sourceLevels[i].Name,
                    Elevation = sourceLevels[i].Elevation / 1000.0
                });
                filled++;
            }

            LevelsGrid.Items.Refresh();
            SaveDefinitions();
            FeedbackText.Text = $"• Kat Yöneticisi'nden {filled} kat için isim/kot alındı (dosya ataması yapılmadı — henüz dosya atanmış satırlara dokunulmadı).";
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
            catch (Exception ex)
            {
                Serilog.Log.Warning("[Bina Tanımları] building_def.json okunamadı: {Error}", ex.Message);
            }
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
           NEDEN — GERÇEK HATA (Session #75 iş akışı denetiminde bulundu): Bu metod kod içinde
                  "(Simulation)" diye işaretliydi — dosyayı hiç açmadan, hiçbir gerçek geometrik
                  işlem yapmadan `IsNormalized=true` set edip "tamamlandı" diyordu. Artık dosya
                  gerçekten okunuyor (`CadSerializer`), tüm entity'lerin ortak bounding box'ının
                  XY min köşesi hesaplanıp entity'ler (0,0,z) taban noktasına göre GERÇEKTEN
                  taşınıyor ve dosya bu haliyle geri yazılıyor — FineSANI Blueprint'in kendi
                  tarif ettiği "Translate(Floor, -Origin) -> MasterOrigin(0,0,0)" işlemi artık
                  gerçekten uygulanıyor, sadece bir bayrak çevrilmiyor.
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

                try
                {
                    if (!File.Exists(level.FilePath))
                    {
                        MessageBox.Show($"Dosya bulunamadı: {level.FilePath}");
                        return;
                    }

                    var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
                    var data = serializer.Deserialize(File.ReadAllText(level.FilePath));
                    if (data?.Entities == null || data.Entities.Count == 0)
                    {
                        MessageBox.Show("Dosyada normalize edilecek nesne bulunamadı.");
                        return;
                    }

                    // Ortak bounding box'ın XY min köşesini bul.
                    double minX = double.MaxValue, minY = double.MaxValue;
                    foreach (var ent in data.Entities)
                    {
                        var bb = ent.GetBoundingBox();
                        minX = Math.Min(minX, bb.Min.X);
                        minY = Math.Min(minY, bb.Min.Y);
                    }

                    if (Math.Abs(minX) < 0.5 && Math.Abs(minY) < 0.5)
                    {
                        level.IsNormalized = true;
                        FeedbackText.Text = $"• {level.LevelName} zaten (0,0) taban noktasında — taşımaya gerek yok.";
                    }
                    else
                    {
                        var translate = Afney.Cad.Geometry.Primitives.Matrix4x4.TranslationMatrix(-minX, -minY, 0);
                        foreach (var ent in data.Entities)
                            ent.Transform(translate);

                        File.WriteAllText(level.FilePath, serializer.Serialize(data));
                        level.IsNormalized = true;
                        FeedbackText.Text = $"• {level.LevelName} normalize edildi: ({minX:F0},{minY:F0}) → (0,0) taşındı, dosyaya kaydedildi.";
                    }

                    LevelsGrid.Items.Refresh();
                    SaveDefinitions();
                }
                catch (Exception ex)
                {
                    level.IsNormalized = false;
                    MessageBox.Show($"Normalizasyon hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /*
           NE: Katları Üst Üste Diz (Stack_Click)
           NEDEN — GERÇEK HATA (Session #75 iş akışı denetiminde bulundu): Bu metod, `OnShow3D`
                  (gerçek montajı arka planda asenkron yapan) event'ini tetikledikten HEMEN
                  SONRA — o iş daha bitmeden, hatta başlamadan — tüm katları "hizalandı" işaretleyip
                  "başarıyla tamamlandı" mesajı gösteriyordu. Artık burada sadece montaj
                  BAŞLATILIYOR; gerçek durum güncellemesi ve başarı/uyarı mesajı, işlem
                  gerçekten bitince `ReportAssemblyCompleted`'da (MainWindow tarafından, gerçek
                  kolon bağlantı sayısıyla) veriliyor.
        */
        private void Stack_Click(object sender, RoutedEventArgs e)
        {
            if (!Levels.Any(l => !string.IsNullOrEmpty(l.FilePath)))
            {
                MessageBox.Show("Stacking için en az bir kat tanımlanmalıdır.");
                return;
            }

            FeedbackText.Text = "• Bina montajı başlatıldı. Katlar Z-kotuna göre diziliyor, lütfen bekleyin...";
            OnShow3D?.Invoke(Levels.Where(l => !string.IsNullOrEmpty(l.FilePath)).ToList());
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