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
        public event System.EventHandler<Afney.Cad.Domain.Abstractions.CadEntity>? EntityModified;

        public void UpdateEntityInfo(Afney.Cad.Domain.Abstractions.CadEntity? entity)
        {
            if (entity == null)
            {
                EntityTitle.Text = "None Selected";
                PropertiesList.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            PropertiesList.Visibility = System.Windows.Visibility.Visible;
            EntityTitle.Text = entity.GetType().Name.Replace("Entity", "").ToUpper();

            if (entity is Afney.Cad.Mechanical.Entities.PipeEntity pipe)
            {
                PropertiesList.Children.Clear();
                
                // Sisteme (System Type) Göre Düzenleme
                var systemTypes = System.Enum.GetNames(typeof(Afney.Cad.Mechanical.Enums.MechanicalSystemType));
                AddComboProperty("System Type", systemTypes, pipe.SystemType.ToString(), val => 
                {
                    if (System.Enum.TryParse(val, out Afney.Cad.Mechanical.Enums.MechanicalSystemType newSys)) {
                        pipe.SystemType = newSys;
                        // Renk gibi özellikleri varsayılanlara çekmek için:
                        // pipe.ColorIndex = MechanicalColorService.GetColorForSystem(newSys); (İleride eklenebilir)
                        EntityModified?.Invoke(this, pipe);
                    }
                });

                // Malzeme (Material)
                var materials = System.Enum.GetNames(typeof(Afney.Cad.Mechanical.Enums.PipeMaterial));
                AddComboProperty("Material", materials, pipe.PipeMaterialType.ToString(), val => 
                {
                    if (System.Enum.TryParse(val, out Afney.Cad.Mechanical.Enums.PipeMaterial newMat)) {
                        pipe.PipeMaterialType = newMat;
                        EntityModified?.Invoke(this, pipe);
                    }
                });
                
                // Çap (Düzenlenebilir Kombobox)
                AddComboProperty("Diameter (DN)", new[]{"15", "20", "25", "32", "40", "50", "65", "80", "100", "125", "150", "200"}, pipe.InnerDiameter.ToString("F0"), val => 
                {
                    if(double.TryParse(val, out double newD)) {
                        pipe.InnerDiameter = newD;
                        EntityModified?.Invoke(this, pipe);
                    }
                });
                
                AddProperty("Flow Rate (Q)", $"{pipe.FlowRate:F2} m³/h");
                AddProperty("Velocity (v)", $"{pipe.Velocity:F2} m/s");
                
                if (pipe.Velocity > 2.0)
                    AddProperty("WARNING", "High Velocity!", System.Windows.Media.Brushes.Red);
            }
            else if (entity is Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity fix)
            {
                PropertiesList.Children.Clear();
                AddProperty("Type", fix.FixtureType.ToString());
                AddEditableProperty("Load Units (LU)", fix.LoadUnits.ToString("F2"), val => 
                {
                    if (double.TryParse(val, out double newLU)) {
                        fix.LoadUnits = newLU;
                        EntityModified?.Invoke(this, fix);
                    }
                });
                AddProperty("Elevation", $"{fix.Position.Z:F2} m");
            }
            else if (entity is Afney.Cad.Mechanical.Entities.MahalEntity mahal)
            {
                PropertiesList.Children.Clear();
                AddEditableProperty("Mahal Name", mahal.Name, val => 
                {
                    mahal.Name = val;
                    EntityModified?.Invoke(this, mahal);
                });
                AddProperty("Area", $"{mahal.Area:F2} m²");
                AddProperty("Fixtures", mahal.Fixtures.Count.ToString());
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

        private void AddEditableProperty(string label, string currentValue, System.Action<string> onValueChanged)
        {
            var border = new Border 
            { 
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(62, 62, 66)), 
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 5),
                CornerRadius = new CornerRadius(4)
            };
            
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 10, Margin = new Thickness(0,0,0,2) });
            
            var txt = new TextBox 
            { 
                Text = currentValue, 
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)), 
                Foreground = System.Windows.Media.Brushes.White,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85))
            };
            
            txt.LostFocus += (s, e) => onValueChanged(txt.Text);
            txt.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) onValueChanged(txt.Text); };

            stack.Children.Add(txt);
            border.Child = stack;
            PropertiesList.Children.Add(border);
        }

        private void AddComboProperty(string label, System.Collections.Generic.IEnumerable<string> items, string selectedItem, System.Action<string> onSelectionChanged)
        {
            var border = new Border 
            { 
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(62, 62, 66)), 
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 5),
                CornerRadius = new CornerRadius(4)
            };
            
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 10, Margin = new Thickness(0,0,0,2) });

            var combo = new ComboBox
            {
                ItemsSource = items,
                SelectedItem = selectedItem,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)),
                Foreground = System.Windows.Media.Brushes.Black, // Let WPF theme handle mostly
                Padding = new Thickness(2)
            };

            combo.SelectionChanged += (s, e) => 
            {
                if (combo.SelectedItem != null && combo.SelectedItem.ToString() != selectedItem)
                {
                    onSelectionChanged(combo.SelectedItem.ToString()!);
                }
            };

            stack.Children.Add(combo);
            border.Child = stack;
            PropertiesList.Children.Add(border);
        }
    }
}
