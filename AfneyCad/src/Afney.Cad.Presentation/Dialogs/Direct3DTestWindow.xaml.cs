using System.Windows;

namespace Afney.Cad.Presentation.Dialogs
{
    /*
       NE: D3D11 Motor Test Penceresi (Direct3DTestWindow)
       NEDEN: docs/Roadmap_3D_Render_Motoru.md Faz 1'in görsel doğrulama adımı — sıfırdan
              yazılan Direct3D11 render pipeline'ının (device + D3DImage köprüsü + shader +
              B-Rep kaynaklı mesh) gerçekten ekranda göründüğünü kullanıcının kendi donanımında
              doğrulaması için. Komut satırından "d3dtest" ile açılır (bkz. MainWindow.Commands.cs).
    */
    public partial class Direct3DTestWindow : Window
    {
        public Direct3DTestWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(System.EventArgs e)
        {
            Viewport3D.Dispose();
            base.OnClosed(e);
        }
    }
}
