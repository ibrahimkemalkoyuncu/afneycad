using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Controls;

public partial class CostDashboardPanel : UserControl
{
    private CadDatabase? _database;
    private readonly RealTimeCostService _costService = new();

    public CostDashboardPanel()
    {
        InitializeComponent();
    }

    public void SetDatabase(CadDatabase database)
    {
        _database = database;
        Refresh();
    }

    public void Refresh()
    {
        if (_database == null) return;

        var cost = _costService.CalculateProjectCost(_database);

        TxtTotal.Text = cost.TotalCost.ToString("N0");
        TxtPipe.Text = cost.PipeCost.ToString("N0");
        TxtFitting.Text = cost.FittingCost.ToString("N0");
        TxtFixture.Text = cost.FixtureCost.ToString("N0");
        TxtLabor.Text = cost.LaborCost.ToString("N0");

        TxtPipeCount.Text = cost.PipeCount.ToString();
        TxtTotalLength.Text = $"{cost.TotalLength:F1} m";
        TxtFixtureCount.Text = cost.FixtureCount.ToString();
        TxtFittingCount.Text = cost.FittingCount.ToString();
    }

    private void OnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();
}
