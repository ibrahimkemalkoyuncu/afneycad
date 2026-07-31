using System.Windows;
using Afney.Cad.Database.Core;

namespace Afney.Cad.Presentation.Dialogs
{
    /*
       NE: D3D11 Motor Test Penceresi (Direct3DTestWindow)
       NEDEN: docs/Roadmap_3D_Render_Motoru.md — Faz 1'in görsel doğrulama adımıydı (sıfırdan
              yazılan Direct3D11 render pipeline'ının gerçekten ekranda göründüğünü doğrulama).
              Faz 2 ile birlikte artık sabit test küpü yerine AÇIK PROJENİN GERÇEK VERİSİNİ
              (`Direct3DViewportControl.LoadFromDatabase`) render ediyor — komut satırından
              "d3dtest" ile açılır (bkz. MainWindow.Commands.cs/MainWindow.Engineering.cs).
    */
    public partial class Direct3DTestWindow : Window
    {
        public Direct3DTestWindow(CadDatabase? database = null)
        {
            InitializeComponent();
            if (database != null)
            {
                Viewport3D.LoadFromDatabase(database);
            }
            else
            {
                InfoText.Text = "Açık bir çizim bulunamadı — B-Rep kernel'inden üretilen test küpü gösteriliyor. " +
                                 "Sağ fare: yörünge (orbit) · Orta fare: kaydır (pan) · Tekerlek: yakınlaştır.";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(System.EventArgs e)
        {
            Viewport3D.Dispose();
            base.OnClosed(e);
        }
    }
}
