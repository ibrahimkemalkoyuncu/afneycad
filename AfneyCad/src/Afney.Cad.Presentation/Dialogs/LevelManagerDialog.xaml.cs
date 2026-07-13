using System.Collections.ObjectModel;
using System.Windows;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

/*
NE: Kat Yönetici Penceresi (Level Manager Dialog)
NEDEN: Kullanıcının proje katlarını görsel olarak yönetmesi için (FINE SANI benzeri).
*/
public partial class LevelManagerDialog : Window
{
    private readonly LevelManager _levelManager;
    public ObservableCollection<MepLevel> Levels { get; set; }
    
    /*
       NE: LevelManagerDialog Yapıcı Metodu
       NEDEN: Kat bilgilerini servis üzerinden alır, UI listesine bağlar ve değişiklikleri dinlemeye başlar.
    */
    public LevelManagerDialog(LevelManager levelManager)
    {
        InitializeComponent();
        _levelManager = levelManager;
        
        // Observable collection for DataGrid binding
        Levels = new ObservableCollection<MepLevel>(_levelManager.GetLevels());
        DataContext = this;
        
        // Event listener
        _levelManager.LevelTableChanged += RefreshLevels;
    }
    
    /*
       NE: Kat Listesini Tazele (RefreshLevels)
       NEDEN: Veritabanında veya modelde bir kat eklendiğinde/silindiğinde arayüzdeki listeyi güncel tutmak için.
    */
    private void RefreshLevels()
    {
        Levels.Clear();
        foreach (var level in _levelManager.GetLevels())
        {
            Levels.Add(level);
        }
    }
    
    /*
       NE: Kat Ekle (AddButton_Click)
       NEDEN: Formdan gelen isim, kot ve yükseklik verileriyle sisteme yeni bir kat seviyesi kaydetmek için.
    */
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text;
        if (!double.TryParse(ElevationTextBox.Text, out var elevation))
        {
            MessageBox.Show("Kot alanına geçerli bir sayı girin.", "Geçersiz Değer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!double.TryParse(HeightTextBox.Text, out var height))
        {
            MessageBox.Show("Yükseklik alanına geçerli bir sayı girin.", "Geçersiz Değer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _levelManager.AddLevel(new MepLevel(name, elevation, height));
            StatusText.Text = $"'{name}' katı eklendi.";
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /*
       NE: Kat Güncelle (UpdateButton_Click)
       NEDEN: Listeden seçilen katın değiştirilen bilgilerini veritabanına/modele yansıtmak için.
    */
    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (LevelsGrid.SelectedItem is MepLevel selected)
        {
            var name = NameTextBox.Text;
            if (!double.TryParse(ElevationTextBox.Text, out var elevation))
            {
                MessageBox.Show("Kot alanına geçerli bir sayı girin.", "Geçersiz Değer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(HeightTextBox.Text, out var height))
            {
                MessageBox.Show("Yükseklik alanına geçerli bir sayı girin.", "Geçersiz Değer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _levelManager.UpdateLevel(selected.Name, name, elevation, height);
                StatusText.Text = $"'{name}' katı güncellendi.";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /*
       NE: Kat Sil (DeleteButton_Click)
       NEDEN: Seçili olan katı projeden tamamen kaldırmak için.
    */
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (LevelsGrid.SelectedItem is MepLevel selected)
        {
            var result = MessageBox.Show($"'{selected.Name}' katını silmek istediğinize emin misiniz?", 
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                var removedName = selected.Name;
                _levelManager.RemoveLevel(removedName);
                StatusText.Text = $"'{removedName}' katı silindi.";
            }
        }
    }
    
    /*
       NE: Izgara Seçimi Değişti (LevelsGrid_SelectionChanged)
       NEDEN: Listeden bir kat seçildiğinde, form alanlarını bu katın değerleriyle doldurmak için.
    */
    private void LevelsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LevelsGrid.SelectedItem is MepLevel selected)
        {
            NameTextBox.Text = selected.Name;
            ElevationTextBox.Text = selected.Elevation.ToString("F0");
            HeightTextBox.Text = selected.Height.ToString("F0");
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /*
       NE: Kaydet (SaveButton_Click)
       NEDEN: Yapılan tüm kat değişikliklerini projeye kaydeder.
    */
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // LevelManager zaten in-memory değişiklikleri tutuyor.
            // Bu buton kullanıcıya görsel geri bildirim verir.
            RefreshLevels();
            StatusText.Text = $"Toplam {Levels.Count} kat bilgisi kaydedildi.";
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /*
       NE: İsim Değiştir (RenameButton_Click)
       NEDEN: Seçili katın ismini değiştirmek için bir giriş penceresi açar.
    */
    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (LevelsGrid.SelectedItem is not MepLevel selected)
        {
            MessageBox.Show("Lütfen isim değiştirmek istediğiniz katı seçin.",
                "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Basit bir isim değiştirme: InputBox yerine mevcut NameTextBox'u kullan
        var newName = NameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show("Yeni kat adı boş olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (newName == selected.Name)
        {
            MessageBox.Show("Yeni isim mevcut isimle aynı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var oldName = selected.Name;
            _levelManager.UpdateLevel(oldName, newName, selected.Elevation, selected.Height);
            StatusText.Text = $"'{oldName}' → '{newName}' olarak değiştirildi.";
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
