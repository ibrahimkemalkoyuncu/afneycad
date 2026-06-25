using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public class HatchPatternItem
{
    public HatchPatternType Type { get; set; }
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public SolidColorBrush ColorBrush { get; set; } = Brushes.Gray;
}

public partial class HatchDialog : Window
{
    public HatchPatternType SelectedPattern { get; private set; } = HatchPatternType.Solid;
    public double PatternScale { get; private set; } = 1.0;

    public HatchDialog()
    {
        InitializeComponent();
        LoadPatterns();
    }

    private void LoadPatterns()
    {
        var items = new List<HatchPatternItem>
        {
            new() { Type = HatchPatternType.Solid,      DisplayName = "Solid (Düz Dolgu)",    Description = "Tam renk dolgulama",               ColorBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)) },
            new() { Type = HatchPatternType.Concrete,   DisplayName = "Beton",                Description = "45° çapraz çizgi — beton kesit",    ColorBrush = new SolidColorBrush(Color.FromRgb(136, 136, 136)) },
            new() { Type = HatchPatternType.Earth,      DisplayName = "Toprak",               Description = "Yatay çizgi — toprak dolgu",        ColorBrush = new SolidColorBrush(Color.FromRgb(139, 105, 20))  },
            new() { Type = HatchPatternType.Water,      DisplayName = "Su",                   Description = "Yatay çizgi — su alanı",            ColorBrush = new SolidColorBrush(Color.FromRgb(0, 136, 255))   },
            new() { Type = HatchPatternType.Brick,      DisplayName = "Tuğla",                Description = "Yatay çizgi — tuğla kesit",         ColorBrush = new SolidColorBrush(Color.FromRgb(204, 68, 0))    },
            new() { Type = HatchPatternType.Insulation, DisplayName = "Yalıtım",             Description = "45° çapraz — yalıtım malzemesi",    ColorBrush = new SolidColorBrush(Color.FromRgb(255, 105, 180)) },
            new() { Type = HatchPatternType.Steel,      DisplayName = "Çelik",                Description = "45° sık çizgi — çelik kesit",       ColorBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192)) },
            new() { Type = HatchPatternType.Sand,       DisplayName = "Kum",                  Description = "Yatay seyrek — kum dolgu",          ColorBrush = new SolidColorBrush(Color.FromRgb(222, 184, 135)) },
            new() { Type = HatchPatternType.CrossHatch, DisplayName = "Çapraz Çizgi",         Description = "0° + 90° çift yön — genel dolgu",   ColorBrush = new SolidColorBrush(Color.FromRgb(102, 102, 102)) },
            new() { Type = HatchPatternType.Diagonal,   DisplayName = "Diyagonal",            Description = "45° tek yön — genel amaçlı",        ColorBrush = new SolidColorBrush(Color.FromRgb(153, 153, 153)) },
        };
        PatternList.ItemsSource = items;
        PatternList.SelectedIndex = 0;
    }

    private void PatternList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PatternList.SelectedItem is HatchPatternItem item)
        {
            SelectedPattern = item.Type;
            TxtInfo.Text = item.DisplayName;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TxtScale.Text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double scale))
            PatternScale = scale;

        DialogResult = true;
    }
}
