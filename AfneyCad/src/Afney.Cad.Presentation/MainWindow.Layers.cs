using Afney.Cad.Presentation.Dialogs;
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

        /*
           NE: Sekme Kapatma (OnCloseTab_Click) — Kaydedilmemiş Değişiklik Onayı
           NEDEN: Önceden ctx.IsModified hiç kontrol edilmiyordu — kaydedilmemiş bir çizim
                  tek tıkla, hiçbir uyarı olmadan geri dönüşsüz şekilde kapanıyordu. Artık
                  IsModified true ise AutoCAD tarzı Kaydet/Kaydetme/İptal seçimi sunuluyor.
        */
        private void OnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TabItem tab)
            {
                var ctx = tab.Tag as CadDocumentContext;

                if (ctx != null && ctx.IsModified)
                {
                    var result = MessageBox.Show(
                        $"\"{ctx.ProjectName}\" sekmesinde kaydedilmemiş değişiklikler var.\n\nKapatmadan önce kaydetmek ister misiniz?",
                        "Kaydedilmemiş Değişiklikler",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Cancel) return;

                    if (result == MessageBoxResult.Yes)
                    {
                        if (!TrySaveDocumentContext(ctx)) return; // Kaydetme başarısız/iptal edildi → sekmeyi kapatma
                    }
                }

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

        /*
           NE: Belirli Bir Doküman Bağlamını Kaydet (TrySaveDocumentContext)
           NEDEN: SaveToFile/SaveAs akışları ambient _activeContext/_database üzerinden
                  çalışıyor — kapatılan sekme aktif sekme olmayabileceğinden, kaydetme
                  süresince bağlamı geçici olarak değiştirip sonra eski hâline döndürüyoruz.
        */
        private bool TrySaveDocumentContext(CadDocumentContext ctx)
        {
            var previousActive = _activeContext;
            _activeContext = ctx;
            try
            {
                string filePath = ctx.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "Farklı Kaydet",
                        Filter = "AutoCAD DWG (*.dwg)|*.dwg|DXF Dosyası (*.dxf)|*.dxf",
                        FileName = ctx.ProjectName,
                        DefaultExt = ".dwg"
                    };
                    if (dlg.ShowDialog() != true) return false; // Kullanıcı iptal etti
                    filePath = dlg.FileName;
                    ctx.FilePath = filePath;
                }

                SaveToFile(filePath);
                ctx.IsModified = false;
                return true;
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Error(ex, "Sekme kapatılırken kaydetme hatası");
                MessageBox.Show($"Kaydetme hatası: {ex.Message}\nSekme kapatılmadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                _activeContext = previousActive;
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

        /*
           NE: Katman Durumu Yöneticisi Diyaloğunu Aç (OnLayerStateManager)
           NEDEN (Session #75): Denetim raporundaki "Layer State Manager: isimlendirilmiş
                  çoklu-state yönetimi yok" bulgusunu kapatan LayerStateManagerService'in
                  arayüz girişi.
        */
        private void OnLayerStateManager(object sender, RoutedEventArgs e)
        {
            if (_activeContext == null) return;

            var dlg = new LayerStateManagerDialog(
                _activeContext.Database, _activeContext.LayerStates, _activeContext.Viewport.HiddenLayers,
                onApplied: () =>
                {
                    RefreshActiveLayerCombo(_activeContext.Database);
                    LayerPanel.RefreshLayers(_activeContext.Database);
                    LayerPanel.SyncHiddenLayers(_activeContext.Viewport.HiddenLayers);
                    Viewport.InvalidateVisual();
                })
            { Owner = this };
            dlg.ShowDialog();
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
