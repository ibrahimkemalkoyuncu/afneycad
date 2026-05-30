using System;
using System.Linq;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class NetworkTopologyDialog
{
    private readonly CadDatabase _database;
    private NetworkTopologyAnalysisService.AnalysisResult? _lastResult;

    public NetworkTopologyDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        Loaded += (_, _) => Analyze_Click(this, new RoutedEventArgs());
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new NetworkTopologyAnalysisService(_database);
            _lastResult = svc.Analyze();

            TxtLoops.Text      = _lastResult.LoopCount.ToString();
            TxtOpenEnds.Text   = _lastResult.OpenEndCount.ToString();
            TxtComponents.Text = _lastResult.ComponentCount.ToString();
            TxtCritical.Text   = $"{_lastResult.CriticalPathM:F1}";

            TxtLoops.Foreground      = _lastResult.HasLoops       ? System.Windows.Media.Brushes.Orange    : System.Windows.Media.Brushes.LightGreen;
            TxtOpenEnds.Foreground   = _lastResult.HasOpenEnds    ? System.Windows.Media.Brushes.Orange    : System.Windows.Media.Brushes.LightGreen;
            TxtComponents.Foreground = _lastResult.HasDisconnected ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.LightGreen;

            SummaryList.ItemsSource = _lastResult.Summary;
            StatusText.Text = "✓ Analiz tamamlandı.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    private void SelectCriticalPath_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _lastResult.CriticalPathPipes.Count == 0)
        {
            StatusText.Text = "Önce analiz yapın.";
            return;
        }

        int selected = 0;
        foreach (var entity in _database.GetAllEntities())
        {
            bool onPath = _lastResult.CriticalPathPipes.Contains(entity.Id);
            entity.IsSelected = onPath;
            if (onPath) selected++;
        }

        StatusText.Text = $"✓ {selected} kritik yol borusu seçildi.";
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
