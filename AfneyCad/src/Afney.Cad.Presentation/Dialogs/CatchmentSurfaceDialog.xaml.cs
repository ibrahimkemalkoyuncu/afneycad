using System.Windows;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Presentation.Dialogs;

public partial class CatchmentSurfaceDialog : Window
{
    public RainfallCatchmentEntity.SurfaceType? ChosenSurface { get; private set; }
    public string AreaName => AreaNameBox.Text.Trim();

    public CatchmentSurfaceDialog(double areaM2)
    {
        InitializeComponent();
        AreaLabel.Text = $"Çizilen Alan: {areaM2:F1} m²";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ChosenSurface = RbFlat.IsChecked    == true ? RainfallCatchmentEntity.SurfaceType.FlatRoof
                      : RbSloped.IsChecked  == true ? RainfallCatchmentEntity.SurfaceType.SlopedRoof
                      : RbPaved.IsChecked   == true ? RainfallCatchmentEntity.SurfaceType.PavedTerrace
                      : RbGravel.IsChecked  == true ? RainfallCatchmentEntity.SurfaceType.GravelRoof
                      : RainfallCatchmentEntity.SurfaceType.GreenRoof;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ChosenSurface = null;
        DialogResult = false;
    }
}
