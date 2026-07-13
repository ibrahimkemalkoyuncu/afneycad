using Afney.Cad.Presentation.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow
    {
        private void RefreshActiveLayerCombo(Afney.Cad.Database.Core.CadDatabase db)
        {
            if (LayerPickerList == null) return;

            var allLayers = db.GetLayers().ToList();
            var sorted = allLayers
                .Where(l => l.Name == "0")
                .Concat(allLayers.Where(l => l.Name != "0").OrderBy(l => l.Name))
                .ToList();

            var viewModels = sorted.Select(l => new LayerItemViewModel
            {
                Name        = l.Name,
                ColorBrush  = l.ColorBrush,
                IsVisible   = !(_activeContext?.Viewport?.HiddenLayers.Contains(l.Name) ?? false),
                IsFrozen    = l.IsFrozen,
                IsLocked    = l.IsLocked
            }).ToList();

            LayerPickerList.ItemsSource = viewModels;

            string current = db.ActiveLayerName ?? "0";
            var active = sorted.FirstOrDefault(l => l.Name == current) ?? sorted.FirstOrDefault();
            if (active != null) SetActiveLayerUI(active.Name, active.ColorBrush);
        }

        private void SetActiveLayerUI(string name, string colorBrush)
        {
            if (ActiveLayerLabel != null)
                ActiveLayerLabel.Text = name;

            if (ActiveLayerColorDot != null)
            {
                try
                {
                    ActiveLayerColorDot.Background =
                        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                            .ConvertFromString(colorBrush)!;
                }
                catch { }
            }
        }

        private void UpdateToolbarLayerIndicator(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return;
            var layer = _database?.GetLayer(layerName);
            if (layer != null)
            {
                byte r = (byte)((layer.Color >> 16) & 0xFF);
                byte g = (byte)((layer.Color >> 8) & 0xFF);
                byte b = (byte)(layer.Color & 0xFF);
                SetActiveLayerUI(layerName, $"#{r:X2}{g:X2}{b:X2}");
            }
            else
            {
                SetActiveLayerUI(layerName, "#CCCCCC");
            }
        }

        private void OnLayerPickerBtnClick(object sender, RoutedEventArgs e)
        {
            LayerPickerPopup.IsOpen = !LayerPickerPopup.IsOpen;
        }

        private void OnLayerNameClick(object sender, MouseButtonEventArgs e)
        {
            if (_activeContext == null) return;
            if (sender is System.Windows.Controls.TextBlock tb && tb.DataContext is LayerItemViewModel vm)
            {
                _activeContext.Database.ActiveLayerName = vm.Name;
                SetActiveLayerUI(vm.Name, vm.ColorBrush);
                StatusText.Text = $"Aktif Katman: {vm.Name}";
                Serilog.Log.Information("[Layer] Aktif katman: {Layer}", vm.Name);
                LayerPickerPopup.IsOpen = false;
            }
        }

        /*
           NE: Katman Değişikliklerini Undo Yığınına Ekle (SubmitLayerToggle)
           NEDEN: Görünürlük/dondurma/kilit değişiklikleri önceden Ctrl+Z ile geri alınamıyordu —
                  katman panelindeki her işlem sessizce ve kalıcı gibi davranıyordu. AutoCAD'de LAYER
                  komutuyla yapılan değişiklikler de undo yığınına girer; burada aynı tutarlılık sağlanır.
        */
        private void SubmitLayerToggle(string opName, System.Action doAction, System.Action undoAction)
        {
            var op = new Afney.Cad.Database.Transactions.Operations.ModifyEntityPropertyOperation(opName, doAction, undoAction);
            _activeContext?.Database.TransactionManager.Submit(op); // Submit, operation.Do()'yu kendi çağırır
        }

        private void OnLayerVisibilityToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_activeContext == null) return;
            if (sender is Button btn && btn.DataContext is LayerItemViewModel vm)
            {
                bool newValue = !vm.IsVisible;
                SubmitLayerToggle($"Katman görünürlüğü: {vm.Name}",
                    () => { vm.IsVisible = newValue; OnLayerVisibilityChanged(vm.Name, newValue); },
                    () => { vm.IsVisible = !newValue; OnLayerVisibilityChanged(vm.Name, !newValue); });
            }
        }

        private void OnLayerFreezeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_activeContext == null) return;
            if (sender is Button btn && btn.DataContext is LayerItemViewModel vm)
            {
                bool newFrozen = !vm.IsFrozen;
                var layer = _activeContext.Database.GetLayer(vm.Name);
                SubmitLayerToggle($"Katman dondurma: {vm.Name}",
                    () => { vm.IsFrozen = newFrozen; if (layer != null) layer.IsFrozen = newFrozen; OnLayerVisibilityChanged(vm.Name, !newFrozen); Serilog.Log.Information("[Layer] Dondurma: {Layer} = {Frozen}", vm.Name, newFrozen); },
                    () => { vm.IsFrozen = !newFrozen; if (layer != null) layer.IsFrozen = !newFrozen; OnLayerVisibilityChanged(vm.Name, newFrozen); });
            }
        }

        private void OnLayerLockToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_activeContext == null) return;
            if (sender is Button btn && btn.DataContext is LayerItemViewModel vm)
            {
                bool newLocked = !vm.IsLocked;
                var layer = _activeContext.Database.GetLayer(vm.Name);
                SubmitLayerToggle($"Katman kilidi: {vm.Name}",
                    () => { vm.IsLocked = newLocked; if (layer != null) layer.IsLocked = newLocked; Serilog.Log.Information("[Layer] Kilit: {Layer} = {Locked}", vm.Name, newLocked); },
                    () => { vm.IsLocked = !newLocked; if (layer != null) layer.IsLocked = !newLocked; });
            }
        }

        private void OnToggleLayerPanel(object sender, RoutedEventArgs e)
        {
            if (LeftPanelBorder.Visibility == Visibility.Collapsed)
            {
                LeftPanelBorder.Visibility = Visibility.Visible;
                OnLeftTab_Layers(sender, e);
            }
            else
            {
                if (LayerPanel.Visibility == Visibility.Visible)
                    LeftPanelBorder.Visibility = Visibility.Collapsed;
                else
                    OnLeftTab_Layers(sender, e);
            }
        }

        private void OnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TabItem tab)
            {
                var ctx = tab.Tag as CadDocumentContext;

                DocumentTabs.Items.Remove(tab);
                if (ctx != null)
                {
                    _documents.Remove(ctx);
                    ctx.Dispose();
                }

                if (DocumentTabs.Items.Count == 0)
                {
                    CreateNewDocument("Boş Proje");
                }
            }
        }

        // ── Sistem Katman Toggle'ları ─────────────────────────────────────────

        private void OnSyncSystemLayers(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new Afney.Cad.Mechanical.Services.SystemLayerService();
                int updated = svc.SyncEntityLayers(_database);
                Viewport.InvalidateVisual();
                MessageBox.Show(
                    $"Katman senkronizasyonu tamamlandı.\n{updated} entity güncellendi.",
                    "Kat Senkron", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Katman senkron hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnToggleColdWater(object sender, RoutedEventArgs e)  => ToggleSystemLayer("MEP_TEMIZ_SU",     BtnToggleColdWater);
        private void OnToggleHotWater(object sender, RoutedEventArgs e)   => ToggleSystemLayer("MEP_SICAK_SU",     BtnToggleHotWater);
        private void OnToggleWasteWater(object sender, RoutedEventArgs e) => ToggleSystemLayer("MEP_PIS_SU",       BtnToggleWasteWater);
        private void OnToggleFire(object sender, RoutedEventArgs e)       => ToggleSystemLayer("MEP_YANGIN",       BtnToggleFire);
        private void OnToggleGas(object sender, RoutedEventArgs e)        => ToggleSystemLayer("MEP_GAZ",          BtnToggleGas);
        private void OnToggleVent(object sender, RoutedEventArgs e)       => ToggleSystemLayer("MEP_HAVALANDIRMA", BtnToggleVent);

        private void OnShowAllSystems(object sender, RoutedEventArgs e)
        {
            Viewport.HiddenLayers.Clear();
            foreach (var btn in new[] { BtnToggleColdWater, BtnToggleHotWater, BtnToggleWasteWater,
                                         BtnToggleFire, BtnToggleGas, BtnToggleVent })
            {
                btn.Opacity = 1.0;
            }
            Viewport.InvalidateVisual();
        }

        private void ToggleSystemLayer(string layerName, System.Windows.Controls.Button btn)
        {
            if (Viewport.HiddenLayers.Contains(layerName))
            {
                Viewport.HiddenLayers.Remove(layerName);
                btn.Opacity = 1.0;
            }
            else
            {
                Viewport.HiddenLayers.Add(layerName);
                btn.Opacity = 0.4;
            }
            Viewport.InvalidateVisual();
        }
    }
}
