using System.Windows.Controls;

namespace Afney.Cad.Presentation.Controls
{
    /*
       NE: Proje Navigatörü (ProjectNavigatorView)
       NEDEN: Bina hiyerarşisini (Katlar, Sistemler, Kolonlar) ağaç yapısında yönetmek için.
    */
    public partial class ProjectNavigatorView : UserControl
    {
        /*
           NE: ProjectNavigatorView Yapıcı Metodu
           NEDEN: Proje ağacı bileşenlerini hazırlar.
        */
        public ProjectNavigatorView()
        {
            InitializeComponent();
        }

        public event System.Action<string, bool>? LayerVisibilityChanged;

        private void Layer_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.Tag is string layerName)
            {
                LayerVisibilityChanged?.Invoke(layerName, true);
            }
        }

        private void Layer_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.Tag is string layerName)
            {
                LayerVisibilityChanged?.Invoke(layerName, false);
            }
        }
    }
}
