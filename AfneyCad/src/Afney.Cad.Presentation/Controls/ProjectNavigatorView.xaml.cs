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
    }
}
