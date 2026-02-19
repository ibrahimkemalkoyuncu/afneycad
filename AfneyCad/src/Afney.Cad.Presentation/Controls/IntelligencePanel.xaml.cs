using System.Windows;
using System.Windows.Controls;

namespace Afney.Cad.Presentation.Controls
{
    /*
       NE: Zeka ve Özellik Paneli (IntelligencePanel)
       NEDEN: Seçilen mühendislik nesnelerinin hidrolik verilerini ve validasyon hatalarını göstermek için.
    */
    public partial class IntelligencePanel : UserControl
    {
        /*
           NE: IntelligencePanel Yapıcı Metodu
           NEDEN: Nesne özelliklerini gösterecek olan UI bileşenlerini hazırlar.
        */
        public IntelligencePanel()
        {
            InitializeComponent();
        }

        /*
           NE: Varlık Bilgisini Güncelle (UpdateEntityInfo)
           NEDEN: Viewport'ta bir nesne seçildiğinde, o nesnenin tipine özel mühendislik verilerini (Debi, Çap, Hız vb.) sağ panelde listelemek için.
        */
        /*
           NE: Varlık Bilgisini Güncelle (UpdateEntityInfo)
           NEDEN: Viewport'ta bir nesne seçildiğinde, o nesnenin tipine özel mühendislik verilerini (Debi, Çap, Hız vb.) sağ panelde listelemek için.
        */
        public void UpdateEntityInfo(Afney.Cad.Domain.Abstractions.CadEntity? entity)
        {
            if (entity == null)
            {
                EntityTitle.Text = "None Seleted";
                PropertiesList.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            PropertiesList.Visibility = System.Windows.Visibility.Visible;
            EntityTitle.Text = entity.GetType().Name.Replace("Entity", "").ToUpper();

            if (entity is Afney.Cad.Mechanical.Entities.PipeEntity pipe)
            {
                // UI'daki textblock'ları dinamik oluşturabiliriz veya XAML'de tanımlayıp doldurabiliriz.
                // Şimdilik basitleştirelim.
                PropertiesList.Children.Clear();
                AddProperty("Diameter (DN)", $"{pipe.InnerDiameter:F0} mm");
                AddProperty("Flow Rate (Q)", $"{pipe.FlowRate:F2} m³/h");
                AddProperty("Velocity (v)", $"{pipe.Velocity:F2} m/s");
                
                if (pipe.Velocity > 2.0)
                    AddProperty("WARNING", "High Velocity!", System.Windows.Media.Brushes.Red);
            }
            else if (entity is Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity fix)
            {
                PropertiesList.Children.Clear();
                AddProperty("Type", fix.FixtureType.ToString());
                AddProperty("Load Units (FU)", $"{fix.LoadUnits:F2} LU");
                AddProperty("Elevation", $"{fix.Position.Z:F2} m");
            }
        }

        /*
           NE: Özellik Ekle (AddProperty)
           NEDEN: Panel içerisine görsel bir kutucuk içinde teknik bilgi satırı eklemek için.
        */
        private void AddProperty(string label, string value, System.Windows.Media.Brush? color = null)
        {
            var border = new Border 
            { 
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(62, 62, 66)), 
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 5),
                CornerRadius = new CornerRadius(4)
            };
            
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 10 });
            stack.Children.Add(new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, Foreground = color ?? System.Windows.Media.Brushes.White, FontSize = 13 });
            
            border.Child = stack;
            PropertiesList.Children.Add(border);
        }
    }
}
