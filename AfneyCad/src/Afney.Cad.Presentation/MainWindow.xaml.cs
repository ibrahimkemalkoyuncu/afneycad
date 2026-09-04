using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow : Window
    {
        private System.Collections.ObjectModel.ObservableCollection<CadDocumentContext> _documents = new System.Collections.ObjectModel.ObservableCollection<CadDocumentContext>();
        private CadDocumentContext? _activeContext;
        private Afney.Cad.Presentation.Services.AutoSaveService? _autoSaveService;
        private Action? _lastRepeatableCommand;
        private readonly Afney.Cad.Mechanical.Services.PressureMapService _pressureMapService = new();
        private readonly Afney.Cad.Mechanical.Services.ClashHighlightService _clashHighlightService = new();
        private readonly Afney.Cad.Presentation.Services.PipeFlowAnimationService _flowAnimService = new();
        private readonly Afney.Cad.Presentation.Services.CloudBackupService _cloudBackupService = new();
        private readonly Afney.Cad.Presentation.Services.HtmlViewerExportService _htmlViewerService = new();
        private readonly Afney.Cad.Presentation.Services.RecentFilesService _recentFiles = new();
        private readonly Afney.Cad.Presentation.Services.ClipboardService _clipboard = new();
        private readonly Afney.Cad.Presentation.Services.UserSettingsService _userSettings = new();
        private readonly Afney.Cad.Mechanical.Services.DimensionStyleService _dimStyleService = new();
        private double _dimTextHeight = 100.0;

        public CadDocumentContext ActiveContext
        {
            get
            {
                if (_activeContext == null && _documents.Count > 0) _activeContext = _documents[0];
                if (_activeContext == null) throw new InvalidOperationException("Aktif doküman yok. Lütfen yeni bir sekme açın.");
                return _activeContext;
            }
        }

        private CadDatabase _database => ActiveContext.Database;
        private Afney.Cad.Commands.History.CommandHistory _history => ActiveContext.History;
        private MechanicalKernel _mechanicalKernel => ActiveContext.MechanicalKernel;
        private Afney.Cad.Application.Services.SnapEngine _snapEngine => ActiveContext.SnapEngine;
        public Afney.Cad.Presentation.Views.CadViewport Viewport => ActiveContext.Viewport;

        public MainWindow()
        {
            InitializeComponent();

            RightPanel.EntityModified += OnEntityModifiedFromRightPanel;
            EntityPropertiesPanel.EntityModified += OnEntityModifiedFromRightPanel;
            ProjectNavigatorPanel.LayerVisibilityChanged += OnLayerVisibilityChanged;

            LayerPanel.LayerVisibilityChanged += (layerName, isVisible) =>
            {
                OnLayerVisibilityChanged(layerName, isVisible);
            };

            LayerPanel.LayerFreezeChanged += (layerName, isFrozen) =>
            {
                OnLayerVisibilityChanged(layerName, !isFrozen);
            };

            LayerPanel.LayerLockChanged += (layerName, isLocked) =>
            {
                Log.Information("[MainWindow] Katman kilidi: {Layer} = {IsLocked}", layerName, isLocked);
            };

            CreateNewDocument("Boş Proje");

            Viewport.EntityDoubleClicked += OnEntityDoubleClicked;

            CheckCrashRecovery();
            MarkSessionActive();

            _autoSaveService = new Afney.Cad.Presentation.Services.AutoSaveService(ActiveContext.Database, TimeSpan.FromMinutes(5));
            _autoSaveService.OnAutoSaveCompleted += (path) =>
            {
                Dispatcher.Invoke(() => StatusText.Text = $"Otomatik Kayıt: {DateTime.Now:HH:mm} ({System.IO.Path.GetFileName(path)})");
            };
            _autoSaveService.OnAutoSaveFailed += (ex) =>
            {
                Dispatcher.Invoke(() => StatusText.Text = $"Otomatik Kayıt Hatası: {ex.Message}");
            };
            _autoSaveService.Start();

            this.Closing += MainWindow_Closing;
            ApplyUserSettings();
        }

        private void CreateNewDocument(string title, string? filePath = null)
        {
            var ctx = new CadDocumentContext
            {
                Database = new CadDatabase(),
                ProjectName = title,
                FilePath = filePath ?? string.Empty,
                MechanicalKernel = new MechanicalKernel()
            };

            ctx.History = new Afney.Cad.Commands.History.CommandHistory(ctx.Database.TransactionManager);
            ctx.SnapEngine = new Afney.Cad.Application.Services.SnapEngine(ctx.Database);
            ctx.SelectionManager = new Afney.Cad.Application.Services.SelectionManager(ctx.Database);

            ctx.MechanicalKernel.SetDatabase(ctx.Database);

            ctx.Database.EntityAdded += ctx.MechanicalKernel.OnEntityAddedToDatabase;
            ctx.Database.EntityRemoved += ctx.MechanicalKernel.OnEntityRemovedFromDatabase;
            ctx.Database.EntityUpdated += ctx.MechanicalKernel.OnEntityUpdatedInDatabase;

            ctx.MechanicalKernel.OnRequestAddEntity += (entity) => ctx.History.TransactionManager.Submit(new AddEntityOperation(ctx.Database, entity));
            ctx.MechanicalKernel.OnRequestDeleteEntity += (entity) => ctx.History.TransactionManager.Submit(new RemoveEntityOperation(ctx.Database, entity));

            var viewport = new Afney.Cad.Presentation.Views.CadViewport();
            viewport.Initialize(ctx.Database, ctx.SnapEngine, ctx.SelectionManager);
            viewport.MechanicalKernel = ctx.MechanicalKernel;
            viewport.OnFeedback += (msg) => StatusText.Text = msg;
            viewport.OnUndoRequested += () =>
            {
                if (!ctx.History.CanUndo) return;
                string? opName = ctx.History.TransactionManager.PeekUndoName();
                ctx.History.Undo();
                viewport.InvalidateViewport();
                StatusText.Text = $"Geri alındı: {opName ?? "işlem"}";
            };
            viewport.OnRedoRequested += () =>
            {
                if (!ctx.History.CanRedo) return;
                string? opName = ctx.History.TransactionManager.PeekRedoName();
                ctx.History.Redo();
                viewport.InvalidateViewport();
                StatusText.Text = $"Yinelendi: {opName ?? "işlem"}";
            };
            viewport.SelectionChanged += (items) =>
            {
                RightPanel.UpdateEntityInfo(items.FirstOrDefault());
                EntityPropertiesPanel.UpdateSelection(items?.ToList());
                var first = items?.FirstOrDefault();
                if (first != null)
                {
                    LayerPanel.HighlightLayer(first.Layer);
                    UpdateToolbarLayerIndicator(first.Layer);
                }
            };
            viewport.OrthoToggled += (isOrtho) =>
            {
                Dispatcher.Invoke(() =>
                {
                    BtnOrthoMode.IsChecked = isOrtho;
                    BtnOrthoMode.Foreground = isOrtho
                        ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00DDFF"))
                        : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AAAAAA"));
                });
            };
            viewport.PolarTrackingToggled += (isOn) =>
            {
                Dispatcher.Invoke(() => { if (BtnPolarMode != null) BtnPolarMode.IsChecked = isOn; });
            };
            viewport.ObjectSnapTrackingToggled += (isOn) =>
            {
                Dispatcher.Invoke(() => { if (BtnOTrackMode != null) BtnOTrackMode.IsChecked = isOn; });
            };

            // Kaydedilmiş kullanıcı ayarlarını yeni sekmenin viewport'una uygula (bkz. UserSettingsService).
            viewport.PolarAngleIncrement = _userSettings.Settings.PolarAngleIncrement;
            viewport.IsPolarTrackingEnabled = _userSettings.Settings.PolarTracking;
            viewport.IsObjectSnapTrackingEnabled = _userSettings.Settings.ObjectSnapTracking;

            ctx.Viewport = viewport;

            var tab = new TabItem
            {
                Header = title,
                Content = CreateTabContent(viewport),
                Tag = ctx
            };

            _documents.Add(ctx);
            DocumentTabs.Items.Add(tab);
            DocumentTabs.SelectedItem = tab;
        }

        private FrameworkElement CreateTabContent(Control viewport)
        {
            return viewport;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _autoSaveService?.Stop();
            _userSettings.Settings.WindowMaximized = WindowState == WindowState.Maximized;
            _userSettings.Settings.LeftPanelVisible = LeftPanelBorder.Visibility == Visibility.Visible;
            _userSettings.Settings.DimTextHeight = _dimTextHeight;
            _userSettings.Settings.ActiveDimStyle = _dimStyleService.ActiveStyleName;
            _userSettings.Save();
            ClearSessionActive();
        }

        private static string AutoSaveDirectory => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AfneyCAD", "AutoSave");

        private static string SessionLockPath => System.IO.Path.Combine(AutoSaveDirectory, ".session.lock");

        /*
           NE: Çökme Sonrası Kurtarma Kontrolü (CheckCrashRecovery)
           NEDEN: Uygulama önceki oturumda düzgün kapatılmadıysa (crash/elektrik kesintisi) kullanıcının
                  saatler süren çalışmasını kaybetmemesi için en son otomatik kaydı geri yükleme seçeneği sunar.
                  Önceden AutoSaveService periyodik olarak diske yazıyordu ama başlangıçta hiç kontrol edilmiyordu —
                  kullanıcı crash sonrası hiçbir kurtarma penceresi görmüyordu.
        */
        private void CheckCrashRecovery()
        {
            try
            {
                if (!System.IO.File.Exists(SessionLockPath)) return; // Önceki oturum temiz kapanmış

                string autoSaveFile = System.IO.Path.Combine(AutoSaveDirectory, "autosave.afney.bak");
                if (!System.IO.File.Exists(autoSaveFile)) return;

                var lastWrite = System.IO.File.GetLastWriteTime(autoSaveFile);
                var answer = MessageBox.Show(
                    $"AfneyCAD önceki oturumda düzgün kapatılmamış olabilir.\n\n" +
                    $"En son otomatik kayıt: {lastWrite:dd.MM.yyyy HH:mm}\n\n" +
                    "Bu otomatik kaydı geri yüklemek ister misiniz?",
                    "Kurtarma Kontrolü", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes) return;

                string json = System.IO.File.ReadAllText(autoSaveFile);
                var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
                var data = serializer.Deserialize(json);
                if (data?.Entities == null) return;

                _database.Clear();
                foreach (var layer in data.Layers ?? new())
                    if (_database.GetLayer(layer.Name) == null) _database.AddLayer(layer);
                foreach (var ent in data.Entities)
                    _database.AddEntity(ent);

                Viewport.InvalidateViewport();
                StatusText.Text = $"Otomatik kayıttan kurtarıldı: {data.Entities.Count} nesne ({lastWrite:HH:mm}).";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Kurtarma] Otomatik kayıt geri yüklenemedi.");
            }
        }

        private void MarkSessionActive()
        {
            try
            {
                System.IO.Directory.CreateDirectory(AutoSaveDirectory);
                System.IO.File.WriteAllText(SessionLockPath, DateTime.Now.ToString("O"));
            }
            catch (Exception ex)
            {
                Log.Debug("[Kurtarma] Oturum kilidi yazılamadı: {Error}", ex.Message);
            }
        }

        private void ClearSessionActive()
        {
            try
            {
                if (System.IO.File.Exists(SessionLockPath)) System.IO.File.Delete(SessionLockPath);
            }
            catch (Exception ex)
            {
                Log.Debug("[Kurtarma] Oturum kilidi silinemedi: {Error}", ex.Message);
            }
        }

        private void ApplyUserSettings()
        {
            var s = _userSettings.Settings;
            if (s.WindowMaximized) WindowState = WindowState.Maximized;
            LeftPanelBorder.Visibility = s.LeftPanelVisible ? Visibility.Visible : Visibility.Collapsed;
            _dimTextHeight = s.DimTextHeight;

            // NE: ActiveDimStyle alanı önceden hiç okunmuyordu (dead field) — Stil Yöneticisi
            // dialogunda oluşturulan özel stiller de dahil, gerçek stil adı artık burada geri yükleniyor.
            if (_dimStyleService.StyleNames.Contains(s.ActiveDimStyle))
                _dimStyleService.SetActiveStyle(s.ActiveDimStyle);
            else
                _dimStyleService.SetActiveStyle(
                    _dimTextHeight <= 125.0 ? "Compact" :
                    _dimTextHeight >= 500.0 ? "Large" : "Standard");
        }

        private void OnEntityModifiedFromRightPanel(object? sender, Afney.Cad.Domain.Abstractions.CadEntity e)
        {
            try
            {
                if (_activeContext == null) return;

                _database.UpdateEntity(e);
                OnCalculateFlowCommand(sender, new RoutedEventArgs());
                RightPanel.UpdateEntityInfo(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Değişiklik uygulanırken hata: {ex.Message}");
            }
        }

        // Komut aktifken canvas'ta yazılan her karakter otomatik CommandInput'a yönlenir (AutoCAD davranışı)
        private void Window_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (_activeContext?.Viewport?.HasActiveCommand != true) return;
            if (CommandInput.IsFocused) return;
            if (string.IsNullOrEmpty(e.Text)) return;

            CommandInput.Text += e.Text;
            CommandInput.Focus();
            CommandInput.CaretIndex = CommandInput.Text.Length;
            e.Handled = true;
        }

        private void OnLayerVisibilityChanged(string layerName, bool isVisible)
        {
            if (_activeContext?.Viewport == null) return;

            if (isVisible)
                _activeContext.Viewport.HiddenLayers.Remove(layerName);
            else
                _activeContext.Viewport.HiddenLayers.Add(layerName);

            _activeContext.Viewport.InvalidateViewport();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_activeContext == null) return;

            bool isCtrlDown = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control);

            if (isCtrlDown && e.Key == System.Windows.Input.Key.Z)
            {
                if (_history.CanUndo)
                {
                    string? opName = _history.TransactionManager.PeekUndoName();
                    _history.Undo();
                    _activeContext.Viewport.InvalidateViewport();
                    StatusText.Text = $"Geri alındı: {opName ?? "işlem"}";
                }
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.Y)
            {
                if (_history.CanRedo)
                {
                    string? opName = _history.TransactionManager.PeekRedoName();
                    _history.Redo();
                    _activeContext.Viewport.InvalidateViewport();
                    StatusText.Text = $"Yinelendi: {opName ?? "işlem"}";
                }
                e.Handled = true;
            }
            else if (isCtrlDown && System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) && e.Key == System.Windows.Input.Key.Z)
            {
                if (_history.CanRedo)
                {
                    string? opName = _history.TransactionManager.PeekRedoName();
                    _history.Redo();
                    _activeContext.Viewport.InvalidateViewport();
                    StatusText.Text = $"Yinelendi: {opName ?? "işlem"}";
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F3)
            {
                if (_activeContext?.SnapEngine != null)
                {
                    bool isOn = _activeContext.SnapEngine.IsOsnapEnabled = !_activeContext.SnapEngine.IsOsnapEnabled;
                    if (BtnOsnapMode != null) BtnOsnapMode.IsChecked = isOn;
                    StatusText.Text = isOn ? "OSNAP: AÇIK (F3)" : "OSNAP: KAPALI (F3)";
                    _activeContext.Viewport.InvalidateViewport();
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F8)
            {
                _activeContext.Viewport.ToggleOrtho();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F10 || e.SystemKey == System.Windows.Input.Key.F10)
            {
                if (_activeContext?.Viewport != null)
                {
                    _activeContext.Viewport.PolarAngleIncrement = _userSettings.Settings.PolarAngleIncrement;
                    _activeContext.Viewport.TogglePolarTracking();
                    bool isOn = _activeContext.Viewport.IsPolarTrackingEnabled;
                    if (BtnPolarMode != null) BtnPolarMode.IsChecked = isOn;
                    _userSettings.Settings.PolarTracking = isOn;
                    _userSettings.Save();
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F11 || e.SystemKey == System.Windows.Input.Key.F11)
            {
                if (_activeContext?.Viewport != null)
                {
                    _activeContext.Viewport.ToggleObjectSnapTracking();
                    bool isOn = _activeContext.Viewport.IsObjectSnapTrackingEnabled;
                    if (BtnOTrackMode != null) BtnOTrackMode.IsChecked = isOn;
                    _userSettings.Settings.ObjectSnapTracking = isOn;
                    _userSettings.Save();
                }
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.S)
            {
                OnSave(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.C)
            {
                var selected = _activeContext?.SelectionManager?.GetSelectedEntities();
                if (selected != null && selected.Any())
                {
                    var center = selected.First().GetBoundingBox().Center;
                    _clipboard.Copy(selected, center);
                    StatusText.Text = $"Kopyalandi: {selected.Count()} nesne";
                }
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.X)
            {
                var selected = _activeContext?.SelectionManager?.GetSelectedEntities();
                if (selected != null && selected.Any())
                {
                    var toCut = selected.ToList();
                    var center = toCut.First().GetBoundingBox().Center;
                    _clipboard.Cut(toCut, center);
                    Viewport.DeleteEntities(toCut);
                    StatusText.Text = $"Kesildi: {_clipboard.Count} nesne (Ctrl+Z ile geri alınabilir)";
                }
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.V)
            {
                if (_clipboard.HasContent)
                {
                    var basePoint = new Vector3D(0, 0, 0);
                    var entities = _clipboard.Paste(basePoint);
                    var cmd = new Afney.Cad.Commands.BasicCommands.PasteCommand(_database, _history.TransactionManager, entities, basePoint);
                    cmd.OnFeedback  += msg => StatusText.Text = msg;
                    cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
                    Viewport.SetActiveCommand(cmd);
                    cmd.Start();
                }
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.L)
            {
                ToggleLeftPanel();
                e.Handled = true;
            }
            else if (isCtrlDown && e.Key == System.Windows.Input.Key.F)
            {
                _activeContext?.Viewport?.ZoomToSelection();
                e.Handled = true;
            }
            else if (!isCtrlDown && e.Key == System.Windows.Input.Key.Space && _activeContext?.Viewport?.HasActiveCommand != true)
            {
                // AutoCAD standardı: hiçbir komut aktif değilken Space = son komutu tekrarla.
                if (_lastRepeatableCommand != null)
                {
                    _lastRepeatableCommand.Invoke();
                    e.Handled = true;
                }
            }
            else if (!isCtrlDown && e.Key == System.Windows.Input.Key.F2 && _activeContext?.Viewport?.HasActiveCommand != true)
            {
                // AutoCAD/Revit standardı: F2 = seçili tek nesnenin özelliklerini düzenle.
                var selected = _activeContext?.SelectionManager?.GetSelectedEntities()?.ToList();
                if (selected != null && selected.Count == 1)
                {
                    OnEntityDoubleClicked(selected[0]);
                    e.Handled = true;
                }
            }
        }

        private void OnTabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtTabCount != null)
                TxtTabCount.Text = _documents.Count > 1 ? $"{DocumentTabs.SelectedIndex + 1}/{_documents.Count}" : "";

            if (DocumentTabs.SelectedItem is TabItem tab && tab.Tag is CadDocumentContext ctx)
            {
                _activeContext = ctx;

                Title = $"AfneyCAD - {ctx.ProjectName} [{(string.IsNullOrEmpty(ctx.FilePath) ? "Kaydedilmemiş" : ctx.FilePath)}]";

                UpdateUndoLabels();

                LeftPanelBorder.Visibility = Visibility.Visible;
                LayerPanel.RefreshLayers(ctx.Database);
                LayerPanel.SyncHiddenLayers(ctx.Viewport.HiddenLayers);
                RefreshActiveLayerCombo(ctx.Database);
            }
        }

        private void OnLeftTab_Navigator(object sender, RoutedEventArgs e)
        {
            ProjectNavigatorPanel.Visibility = Visibility.Visible;
            LayerPanel.Visibility = Visibility.Collapsed;
            TabNavBtn.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F3A5F"));
            TabNavBtn.Foreground = System.Windows.Media.Brushes.White;
            TabLayerBtn.Background = System.Windows.Media.Brushes.Transparent;
            TabLayerBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AAA"));
        }

        private void OnLeftTab_Layers(object sender, RoutedEventArgs e)
        {
            ProjectNavigatorPanel.Visibility = Visibility.Collapsed;
            LayerPanel.Visibility = Visibility.Visible;
            EntityPropertiesPanel.Visibility = Visibility.Collapsed;
            TabLayerBtn.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F3A5F"));
            TabLayerBtn.Foreground = System.Windows.Media.Brushes.White;
            TabNavBtn.Background = System.Windows.Media.Brushes.Transparent;
            TabNavBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AAA"));

            if (_activeContext != null)
            {
                LayerPanel.RefreshLayers(_activeContext.Database);
                LayerPanel.SyncHiddenLayers(_activeContext.Viewport.HiddenLayers);
            }
        }

        private void ToggleLeftPanel()
        {
            if (LeftPanelBorder.Visibility == Visibility.Collapsed)
            {
                LeftPanelBorder.Visibility = Visibility.Visible;
                OnLeftTab_Layers(this, new RoutedEventArgs());
            }
            else
            {
                LeftPanelBorder.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                foreach (var ctx in _documents)
                {
                    ctx.Dispose();
                }
                _documents.Clear();
                Log.Information("Uygulama kapanıyor, tüm sekmeler temizlendi.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Uygulama kapanırken hata oluştu.");
            }
            base.OnClosed(e);
        }
    }
}
