using System.Windows;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class BuildingPropertiesDialog : Window
    {
        public BuildingPropertiesDialog()
        {
            InitializeComponent();
        }

        /*
       NE: Tamam (OkButton_Click)
       NEDEN: Girilen bina özelliklerini onaylayıp pencereyi kapatmak için.
    */
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Verileri kaydet
            DialogResult = true;
            Close();
        }

        /*
       NE: İptal (CancelButton_Click)
       NEDEN: Bina özelliklerinde yapılan değişiklikleri kaydetmeden pencereden çıkmak için.
    */
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
