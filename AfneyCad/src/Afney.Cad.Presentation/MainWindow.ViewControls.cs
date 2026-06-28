using System.Windows;
using System.Windows.Controls;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow
    {
        #region -- GÖRÜNÜM KONTROLLERİ (VIEW) --

        private void OnZoomExtents(object sender, RoutedEventArgs e) => Viewport.ZoomExtents();

        private void OnToggleProjectNavigator(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && ProjectNavigatorPanel != null)
            {
                ProjectNavigatorPanel.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnToggleIntelligencePanel(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && RightPanel != null)
            {
                RightPanel.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnToggle2DView(object sender, RoutedEventArgs e)
        {
            Viewport.SetViewMode(false);

            var view2DBtn = this.FindName("View2DBtn") as Control;
            var view3DBtn = this.FindName("View3DBtn") as Control;

            if (view2DBtn != null)
            {
                view2DBtn.Background = System.Windows.Media.Brushes.Cyan;
                view2DBtn.Foreground = System.Windows.Media.Brushes.Black;
            }
            if (view3DBtn != null)
            {
                view3DBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
                view3DBtn.Foreground = System.Windows.Media.Brushes.White;
            }
            StatusText.Text = "Çizim Modu: 2D Plan";
        }

        private void OnToggle3DView(object sender, RoutedEventArgs e)
        {
            Viewport.SetViewMode(true);

            var view2DBtn = this.FindName("View2DBtn") as Control;
            var view3DBtn = this.FindName("View3DBtn") as Control;

            if (view3DBtn != null)
            {
                view3DBtn.Background = System.Windows.Media.Brushes.Orange;
                view3DBtn.Foreground = System.Windows.Media.Brushes.Black;
            }
            if (view2DBtn != null)
            {
                view2DBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
                view2DBtn.Foreground = System.Windows.Media.Brushes.White;
            }
            StatusText.Text = "Çizim Modu: 3D İzometrik";
        }

        #endregion

        #region -- OSNAP (YAKALAMA) KONTROLLERİ --

        private void OnOsnapMasterToggle(object sender, RoutedEventArgs e)
        {
            if (_activeContext?.SnapEngine == null) return;

            if (sender is System.Windows.Controls.Primitives.ToggleButton masterBtn)
            {
                bool isEnabled = masterBtn.IsChecked == true;
                _activeContext.SnapEngine.IsOsnapEnabled = isEnabled;

                masterBtn.Foreground = isEnabled ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 221, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150));
                masterBtn.Content = isEnabled ? "Açık" : "Kapalı";

                Serilog.Log.Information($"OSNAP Ana Şalter: {(isEnabled ? "Açık" : "Kapalı")}");
                _activeContext.Viewport.InvalidateViewport();
            }
        }

        private void OnOsnapFlagToggle(object sender, RoutedEventArgs e)
        {
            if (_activeContext?.SnapEngine == null) return;

            if (sender is System.Windows.Controls.Primitives.ToggleButton btn)
            {
                bool isEnabled = btn.IsChecked == true;
                btn.Foreground = isEnabled ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150));

                switch (btn.Name)
                {
                    case "BtnOsnapEnd": _activeContext.SnapEngine.EnableEndpoint = isEnabled; break;
                    case "BtnOsnapMid": _activeContext.SnapEngine.EnableMidpoint = isEnabled; break;
                    case "BtnOsnapCen": _activeContext.SnapEngine.EnableCenter = isEnabled; break;
                    case "BtnOsnapPerp": _activeContext.SnapEngine.EnablePerpendicular = isEnabled; break;
                }

                Serilog.Log.Information($"OSNAP Bayrağı ({btn.Name}): {(isEnabled ? "Açık" : "Kapalı")}");
            }
        }

        #endregion

        #region -- ORTHO MODE --

        private void OnOrthoModeToggle(object sender, RoutedEventArgs e)
        {
            if (_activeContext?.Viewport == null) return;

            if (sender is System.Windows.Controls.Primitives.ToggleButton btn)
            {
                bool isEnabled = btn.IsChecked == true;
                _activeContext.Viewport.ToggleOrthoMode(isEnabled);

                btn.Foreground = isEnabled ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 221, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150));

                Serilog.Log.Information($"Ortho Modu: {(isEnabled ? "Açık" : "Kapalı")}");
            }
        }

        #endregion

        private void OnUndo(object sender, RoutedEventArgs e) => _history.Undo();
        private void OnRedo(object sender, RoutedEventArgs e) => _history.Redo();
        private void UpdateUndoLabels() { }
    }
}
