using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AutoRouteDialog : Window
{
    private readonly CadDatabase _database;
    private readonly Afney.Cad.Database.Transactions.TransactionManager _tm;
    private RouteResult? _lastResult;
    private RouteOptions? _lastOptions;

    public AutoRouteDialog(CadDatabase database, Afney.Cad.Database.Transactions.TransactionManager tm)
    {
        InitializeComponent();
        _database = database;
        _tm = tm;
    }

    public void SetRouteResult(Vector3D start, Vector3D end)
    {
        var options = BuildOptions();
        var svc = new AutoRouteService(_database);
        _lastResult = svc.FindRoute(start, end, options);
        _lastOptions = options;

        if (_lastResult.Success)
        {
            TxtResult.Text = _lastResult.Message;
            TxtLength.Text = $"{_lastResult.TotalLength / 1000.0:F2} m";
            TxtBends.Text = $"{_lastResult.BendCount} dirsek";
            TxtCost.Text = $"{_lastResult.EstimatedCost:N0} TRY";
            ResultDetails.Visibility = Visibility.Visible;
            BtnDraw.IsEnabled = true;
        }
        else
        {
            TxtResult.Text = "Rota bulunamadı. Parametreleri değiştirip tekrar deneyin.";
            ResultDetails.Visibility = Visibility.Collapsed;
            BtnDraw.IsEnabled = false;
        }
    }

    private RouteOptions BuildOptions()
    {
        var sys = MechanicalSystemType.DomesticColdWater;
        if (RbHot.IsChecked == true) sys = MechanicalSystemType.DomesticHotWater;
        else if (RbWaste.IsChecked == true) sys = MechanicalSystemType.WasteWater;
        else if (RbFire.IsChecked == true) sys = MechanicalSystemType.FireProtection;

        double[] dns = { 15, 20, 25, 32, 40, 50 };
        double dn = dns[CbDiameter.SelectedIndex >= 0 ? CbDiameter.SelectedIndex : 1];

        double.TryParse(TxtGridStep.Text, out double grid);
        double.TryParse(TxtWallOffset.Text, out double wall);

        return new RouteOptions
        {
            SystemType = sys,
            Diameter = dn,
            GridStep = grid > 0 ? grid : 100,
            WallOffset = wall > 0 ? wall : 50,
            PreferOrthogonal = ChkOrtho.IsChecked == true,
            AvoidObstacles = true
        };
    }

    private void OnDrawRoute_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _lastOptions == null || !_lastResult.Success) return;

        var svc = new AutoRouteService(_database);
        var pipes = svc.CreatePipesFromRoute(_lastResult, _lastOptions);

        foreach (var pipe in pipes)
            _tm.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, pipe));

        MessageBox.Show($"Rota çizildi!\n\n{pipes.Count} boru segmenti eklendi.\nToplam: {_lastResult.TotalLength / 1000.0:F2} m\nDirsek: {_lastResult.BendCount}\nTahmini Maliyet: {_lastResult.EstimatedCost:N0} TRY",
            "Akıllı Rota", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }
}
