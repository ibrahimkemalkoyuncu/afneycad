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
           NE: Özellik Değişikliği Gönderim Aracı (SubmitPropertyChange)
           NEDEN: Özellik değerindeki değişikliği Geri Al (Undo/Redo) yapısına aktarabilmek için.
        */
        private void SubmitPropertyChange(string propName, System.Action doAction, System.Action undoAction)
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var history = mainWindow.ActiveContext.History;
                var op = new Afney.Cad.Database.Transactions.Operations.ModifyEntityPropertyOperation(propName, doAction, undoAction);
                history.TransactionManager.Submit(op);
            }
            else
            {
                doAction?.Invoke();
            }
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
                var oldSys = pipe.SystemType;
                var systemTypes = System.Enum.GetNames(typeof(Afney.Cad.Mechanical.Enums.MechanicalSystemType));
                AddComboProperty("System Type", systemTypes, oldSys.ToString(), val => 
                {
                    if (System.Enum.TryParse(val, out Afney.Cad.Mechanical.Enums.MechanicalSystemType newSys) && newSys != oldSys) {
                        var capturedOldSys = oldSys;
                        SubmitPropertyChange("System Type",
                            () => { pipe.SystemType = newSys; EntityModified?.Invoke(this, pipe); },
                            () => { pipe.SystemType = capturedOldSys; EntityModified?.Invoke(this, pipe); });
                        oldSys = newSys;
                    }
                });

                // Malzeme (Material)
                var oldMat = pipe.PipeMaterialType;
                var materials = System.Enum.GetNames(typeof(Afney.Cad.Mechanical.Enums.PipeMaterial));
                AddComboProperty("Material", materials, oldMat.ToString(), val => 
                {
                    if (System.Enum.TryParse(val, out Afney.Cad.Mechanical.Enums.PipeMaterial newMat) && newMat != oldMat) {
                        var capturedOldMat = oldMat;
                        SubmitPropertyChange("Material",
                            () => { pipe.PipeMaterialType = newMat; EntityModified?.Invoke(this, pipe); },
                            () => { pipe.PipeMaterialType = capturedOldMat; EntityModified?.Invoke(this, pipe); });
                        oldMat = newMat;
                    }
                });
                
                // Çap (Düzenlenebilir Kombobox)
                var oldDiam = pipe.InnerDiameter;
                AddComboProperty("Diameter (DN)", new[]{"15", "20", "25", "32", "40", "50", "65", "80", "100", "125", "150", "200"}, oldDiam.ToString("F0"), val => 
                {
                    if(double.TryParse(val, out double newD) && newD != oldDiam) {
                        var capturedOldDiam = oldDiam;
                        SubmitPropertyChange("Diameter (DN)",
                            () => { pipe.InnerDiameter = newD; EntityModified?.Invoke(this, pipe); },
                            () => { pipe.InnerDiameter = capturedOldDiam; EntityModified?.Invoke(this, pipe); });
                        oldDiam = newD;
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
                var oldLU = fix.LoadUnits;
                AddEditableProperty("Load Units (LU)", oldLU.ToString("F2"), val => 
                {
                    if (double.TryParse(val, out double newLU) && newLU != oldLU) {
                        var capturedOldLU = oldLU;
                        SubmitPropertyChange("Load Units (LU)",
                            () => { fix.LoadUnits = newLU; EntityModified?.Invoke(this, fix); },
                            () => { fix.LoadUnits = capturedOldLU; EntityModified?.Invoke(this, fix); });
                        oldLU = newLU;
                    }
                });
                AddProperty("Elevation", $"{fix.Position.Z:F2} m");
            }
            else if (entity is Afney.Cad.Mechanical.Entities.MahalEntity mahal)
            {
                PropertiesList.Children.Clear();
                var oldName = mahal.Name;
                AddEditableProperty("Mahal Name", oldName, val => 
                {
                    if(val != oldName) {
                        var capturedOldName = oldName;
                        SubmitPropertyChange("Mahal Name",
                            () => { mahal.Name = val; EntityModified?.Invoke(this, mahal); },
                            () => { mahal.Name = capturedOldName; EntityModified?.Invoke(this, mahal); });
                        oldName = val;
                    }
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
