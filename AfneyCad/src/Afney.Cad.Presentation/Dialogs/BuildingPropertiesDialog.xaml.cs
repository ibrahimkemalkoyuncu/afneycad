using System.Globalization;
using System.Windows;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Presentation.Dialogs
{
    /*
       NE: Bina Özellikleri Diyaloğu (BuildingPropertiesDialog)
       NEDEN — GERÇEK HATA (Session #75 iş akışı denetiminde bulundu): Bu ekrandaki TextBox'ların
              hiçbirinde `x:Name` yoktu — "Tamam" butonu sadece `DialogResult=true` set edip
              pencereyi kapatıyordu, girilen Proje Adı/Adres/Şehir/Zemin Kotu hiçbir yere
              yazılmıyordu. Kullanıcı verisinin kaydedildiğini sanıyordu, hiçbir şey olmuyordu —
              sessizce veri kaybeden bu tür ekranlar, eksik bir özellikten daha kötüdür (hata
              mesajı bile yok). Artık dialog açılışta verilen `ProjectMetadata`'dan alanları
              dolduruyor, "Tamam"da aynı nesneye geri yazıyor.
    */
    public partial class BuildingPropertiesDialog : Window
    {
        private readonly ProjectMetadata _metadata;

        public BuildingPropertiesDialog(ProjectMetadata metadata)
        {
            InitializeComponent();
            _metadata = metadata;

            TxtProjectName.Text = _metadata.ProjectName;
            TxtAddress.Text = _metadata.Address;
            TxtGroundElevation.Text = _metadata.GroundElevationM.ToString("F2", CultureInfo.InvariantCulture);

            foreach (var item in CboCity.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem cbi &&
                    string.Equals(cbi.Content?.ToString(), _metadata.City, System.StringComparison.OrdinalIgnoreCase))
                {
                    CboCity.SelectedItem = cbi;
                    break;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _metadata.ProjectName = string.IsNullOrWhiteSpace(TxtProjectName.Text) ? _metadata.ProjectName : TxtProjectName.Text.Trim();
            _metadata.Address = TxtAddress.Text.Trim();
            _metadata.City = (CboCity.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? _metadata.City;

            if (double.TryParse(TxtGroundElevation.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double elev))
                _metadata.GroundElevationM = elev;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
