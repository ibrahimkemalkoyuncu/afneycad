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
        try
        {
            var name = NameTextBox.Text;
            var elevation = double.Parse(ElevationTextBox.Text);
            var height = double.Parse(HeightTextBox.Text);
            
            _levelManager.AddLevel(new MepLevel(name, elevation, height));
            MessageBox.Show("Kat eklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
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
            try
            {
                var name = NameTextBox.Text;
                var elevation = double.Parse(ElevationTextBox.Text);
                var height = double.Parse(HeightTextBox.Text);
                
                _levelManager.UpdateLevel(selected.Name, name, elevation, height);
                // MessageBox.Show("Kat güncellendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information); 
                // Artık otomatik refresh event'i tetikleniyor.
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
                _levelManager.RemoveLevel(selected.Name);
                // MessageBox.Show("Kat silindi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
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
}
