using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Blocks;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Presentation.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow
    {
        #region -- KOMUTLAR (COMMANDS) --

        private void OnLineCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnLineCommand(this, new RoutedEventArgs());
            var cmd = new LineCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        private void OnCircleCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnCircleCommand(this, new RoutedEventArgs());
            var cmd = new CircleCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        private void OnTrimCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnTrimCommand(this, new RoutedEventArgs());
            var cmd = new TrimCommand(_database, _history.TransactionManager, 1.0);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnExtendCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnExtendCommand(this, new RoutedEventArgs());
            var cmd = new ExtendCommand(_database, _history.TransactionManager, 1.0);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnMirrorCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnMirrorCommand(this, new RoutedEventArgs());
            var selectedEntities = _activeContext?.SelectionManager?.GetSelectedEntities() ?? new List<Afney.Cad.Domain.Abstractions.CadEntity>();

            var cmd = new MirrorCommand(_database, _history.TransactionManager, selectedEntities);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnExplodeCommand(object sender, RoutedEventArgs e)
        {
            var selectedEntities = _activeContext?.SelectionManager?.GetSelectedEntities() ?? new List<Afney.Cad.Domain.Abstractions.CadEntity>();

            var cmd = new ExplodeCommand(_database, _history.TransactionManager, selectedEntities);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnMoveCommand(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Taşı: Nesne seçin ve hedef noktayı tıklayın.";
            MessageBox.Show("Taşı komutu için önce nesne seçin, sonra 'M' kısayoluna basın.", "Taşı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnCopyCommand(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Kopyala: Nesne seçin ve hedef noktayı tıklayın.";
            MessageBox.Show("Kopyala komutu için önce nesne seçin, sonra 'CO' kısayoluna basın.", "Kopyala", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── BOYUTLANDIRMA KOMUTLARI ──────────────────────────────────────────────

        private void OnLinearDimCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new LinearDimCommand(_database, _history.TransactionManager, _dimStyleService.ActiveStyle);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnAlignedDimCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new AlignedDimCommand(_database, _history.TransactionManager, _dimStyleService.ActiveStyle);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnRadiusDimCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new RadiusDimCommand(_database, _history.TransactionManager, _dimStyleService.ActiveStyle);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnAngularDimCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new AngularDimCommand(_database, _history.TransactionManager, _dimStyleService.ActiveStyle);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnMTextCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new MTextCommand(_database, _history.TransactionManager, () =>
            {
                var dlg = new TextInputDialog("Metin Girin", "Çizime eklenecek metin:");
                dlg.Owner = this;
                return dlg.ShowDialog() == true ? dlg.InputText : null;
            });
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnPolylineCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnPolylineCommand(this, new RoutedEventArgs());
            var cmd = new PolylineCommand(_database, _history.TransactionManager);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        private void OnRectangleCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnRectangleCommand(this, new RoutedEventArgs());
            var cmd = new RectangleCommand(_database, _history.TransactionManager);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        private void OnProjectInfoCommand(object sender, RoutedEventArgs e)
        {
            var dlg = new ProjectInfoDialog(_database, null) { Owner = this };
            dlg.ShowDialog();
        }

        private void OnNorthArrowCommand(object sender, RoutedEventArgs e)
        {
            var svc = new NorthArrowService();
            var entities = svc.Generate(new Afney.Cad.Geometry.Primitives.Vector3D(0, 0, 0));
            foreach (var ent in entities)
                _history.TransactionManager.Submit(new AddEntityOperation(_database, ent));
            Viewport.InvalidateVisual();
            StatusText.Text = $"Kuzey işareti eklendi ({entities.Count} nesne).";
        }

        private void OnConnectFixtureCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new ConnectFixtureCommand(_database, _history.TransactionManager);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnRiserPipeCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new RiserPipeCommand(_database, _history.TransactionManager, GetActiveSystemType());
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSourcePointCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new SourcePointCommand(_database, _history.TransactionManager, GetActiveSystemType());
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnAcceptPlumbingCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var guard = new DomainGuardService(_database, _mechanicalKernel.TopologyGraph);
                var result = guard.ValidateSystem();
                if (result.IsValid)
                {
                    StatusText.Text = "Tesisat kabul edildi — sistem doğrulandı, hesaba hazır.";
                    MessageBox.Show("Tesisat doğrulaması başarılı!\n\nSistem hesaplamaya hazır.", "Tesisatı Kabul Et", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string errors = string.Join("\n", result.Errors);
                    string warnings = string.Join("\n", result.Warnings);
                    string msg = "";
                    if (result.Errors.Count > 0) msg += $"HATALAR:\n{errors}\n\n";
                    if (result.Warnings.Count > 0) msg += $"UYARILAR:\n{warnings}";
                    StatusText.Text = $"Tesisat doğrulaması: {result.Errors.Count} hata, {result.Warnings.Count} uyarı.";
                    MessageBox.Show(msg, "Tesisatı Kabul Et — Sorunlar Tespit Edildi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Doğrulama hatası: {ex.Message}";
            }
        }

        private void OnArchDetectCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new ArchEntityConverterService(_database);
                var result = svc.ConvertFromLayers();

                if (result.Total == 0)
                {
                    MessageBox.Show("Mimari element algilanamadi.\nDWG layer isimlerinde DUVAR/KOLON/KAPI/PENCERE/KIRIS gibi anahtar kelimeler aranir.", "Mimari Algilama", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Viewport.InvalidateViewport();
                    StatusText.Text = $"Mimari Algilama: {result.WallsCreated} duvar, {result.ColumnsCreated} kolon, {result.DoorsCreated} kapi, {result.WindowsCreated} pencere, {result.BeamsCreated} kiris";
                    MessageBox.Show($"Mimari element algilama tamamlandi:\n\nDuvar: {result.WallsCreated}\nKolon: {result.ColumnsCreated}\nKapi: {result.DoorsCreated}\nPencere: {result.WindowsCreated}\nKiris: {result.BeamsCreated}\n\nToplam: {result.Total} element", "Mimari Algilama", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Algilama hatasi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRecentFilesClick(object sender, RoutedEventArgs e)
        {
            RecentFilesList.Children.Clear();
            var files = _recentFiles.Files;

            if (files.Count == 0)
            {
                RecentFilesList.Children.Add(new TextBlock
                {
                    Text = "Henüz dosya açılmadı.",
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B6F88")),
                    Margin = new Thickness(8, 6, 8, 6),
                    FontSize = 12
                });
            }
            else
            {
                foreach (var (file, idx) in files.Select((f, i) => (f, i)))
                {
                    bool exists = System.IO.File.Exists(file);
                    string fileName  = System.IO.Path.GetFileNameWithoutExtension(file);
                    string folder    = System.IO.Path.GetDirectoryName(file) ?? "";
                    string shortDir  = folder.Length > 40 ? "…" + folder[^38..] : folder;
                    string modDate   = exists ? System.IO.File.GetLastWriteTime(file).ToString("dd.MM.yyyy") : "—";

                    // Her dosya için içerik paneli
                    var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0) };
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"{idx + 1}.  📄  {fileName}",
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                                exists ? "#D4D6E8" : "#666880")),
                        FontSize = 12
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"      {shortDir}   ·   {modDate}",
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555770")),
                        FontSize = 10
                    });

                    var btn = new Button
                    {
                        Content = panel,
                        ToolTip = file,
                        Height = 42,
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(8, 4, 8, 4),
                        Cursor = Cursors.Hand,
                        Tag = file,
                        Opacity = exists ? 1.0 : 0.5
                    };
                    btn.Click += (s, args) =>
                    {
                        RecentFilesPopup.IsOpen = false;
                        string path = (string)((Button)s!).Tag;
                        if (System.IO.File.Exists(path))
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(path);
                            CreateNewDocument(name, path);
                            LoadDwgInternal(path);
                            _recentFiles.AddFile(path);
                        }
                        else
                        {
                            MessageBox.Show($"Dosya bulunamadı:\n{path}\n\nListeden kaldırılıyor.",
                                "Dosya Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                            _recentFiles.RemoveFile(path);
                        }
                    };
                    RecentFilesList.Children.Add(btn);

                    // İnce ayraç
                    if (idx < files.Count - 1)
                        RecentFilesList.Children.Add(new Separator
                        {
                            Background = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252535")),
                            Margin = new Thickness(8, 0, 8, 0), Height = 1
                        });
                }

                // "Tümünü Temizle" butonu
                RecentFilesList.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 2) });
                var clearBtn = new Button
                {
                    Content = "🗑  Tümünü Temizle",
                    Height = 28,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B04040")),
                    BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 0, 8, 0),
                    FontSize = 11,
                    Cursor = Cursors.Hand
                };
                clearBtn.Click += (_, _) =>
                {
                    _recentFiles.Clear();
                    RecentFilesPopup.IsOpen = false;
                };
                RecentFilesList.Children.Add(clearBtn);
            }

            RecentFilesPopup.IsOpen = !RecentFilesPopup.IsOpen;
        }

        private void OnArchBomCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new ArchitecturalBomService(_database);
                var bom = svc.Generate();

                if (bom.Items.Count == 0)
                {
                    MessageBox.Show("Projede mimari element bulunamadi.\nDuvar/Kolon/Kiris/Kapi/Pencere/Mahal ekleyin.", "Mimari Metraj", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Mimari Metraj Kaydet",
                    Filter = "HTML (*.html)|*.html",
                    FileName = $"Mimari_Metraj_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".html"
                };

                if (dlg.ShowDialog() == true)
                {
                    string html = svc.ExportToHtml(bom, _activeContext?.ProjectName);
                    System.IO.File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
                    StatusText.Text = $"Mimari Metraj: {bom.WallCount} duvar, {bom.ColumnCount} kolon, {bom.DoorCount} kapi, {bom.WindowCount} pencere, {bom.RoomCount} mahal";
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
                    catch (Exception exOpen) { Serilog.Log.Warning("[Rapor] Dosya kaydedildi ama açılamadı: {File} — {Error}", dlg.FileName, exOpen.Message); }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mimari Metraj hatasi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRouteDuctCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new RouteDuctCommand(_database, _history.TransactionManager);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnConnectDuctCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new ConnectDuctCommand(_database, _history.TransactionManager);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSelectAreaCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new SelectAreaCommand(_database, _history.TransactionManager);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSelectionBomCommand(object sender, RoutedEventArgs e)
        {
            var selected = _activeContext?.SelectionManager?.GetSelectedEntities();
            if (selected == null || !selected.Any())
            {
                MessageBox.Show("Metraj icin once nesne secin.", "Secim Metraj", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var svc = new SelectionBomService();
            var result = svc.Calculate(selected);
            StatusText.Text = result.Summary;

            var answer = MessageBox.Show($"{result.Summary}\n\nHTML rapor olusturulsun mu?", "Secim Metraj",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Secim Metraj Kaydet",
                    Filter = "HTML (*.html)|*.html",
                    FileName = $"Secim_Metraj_{DateTime.Now:yyyyMMdd_HHmm}",
                    DefaultExt = ".html"
                };
                if (dlg.ShowDialog() == true)
                {
                    string html = svc.ExportToHtml(result);
                    System.IO.File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
                    catch (Exception exOpen) { Serilog.Log.Warning("[Rapor] Dosya kaydedildi ama açılamadı: {File} — {Error}", dlg.FileName, exOpen.Message); }
                }
            }
        }

        private void OnHvacBomCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new HvacBomService(_database);
                var bom = svc.Generate();

                if (bom.DuctCount == 0)
                {
                    MessageBox.Show("Projede HVAC kanal entity'si bulunamadı.\nÖnce kanal çizimi yapın.", "HVAC Metraj", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "HVAC Metraj Kaydet",
                    Filter = "HTML Dosyası (*.html)|*.html|CSV Dosyası (*.csv)|*.csv",
                    FileName = $"HVAC_Metraj_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".html"
                };

                if (dlg.ShowDialog() == true)
                {
                    string content = dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                        ? svc.ExportToCsv(bom)
                        : svc.ExportToHtml(bom, _activeContext?.ProjectName);

                    System.IO.File.WriteAllText(dlg.FileName, content, System.Text.Encoding.UTF8);
                    StatusText.Text = $"HVAC Metraj: {bom.DuctCount} kanal, {bom.TotalDuctLength:F1} m, {bom.TotalCost:N0} TRY";

                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
                    catch (Exception exOpen) { Serilog.Log.Warning("[Rapor] Dosya kaydedildi ama açılamadı: {File} — {Error}", dlg.FileName, exOpen.Message); }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"HVAC Metraj hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDwgImportDialog(object sender, RoutedEventArgs e)
        {
            var dlg = new DwgImportDialog() { Owner = this };
            if (dlg.ShowDialog() == true && dlg.ImportedEntities != null)
            {
                foreach (var ent in dlg.ImportedEntities)
                    _database.AddEntity(ent);

                var layerGroups = dlg.ImportedEntities.Where(e2 => e2.Layer != null).GroupBy(e2 => e2.Layer);
                foreach (var group in layerGroups)
                {
                    if (_database.GetLayer(group.Key!) == null)
                    {
                        uint layerColor = group.GroupBy(e2 => e2.Color).OrderByDescending(g => g.Count()).First().Key;
                        _database.AddLayer(new Afney.Cad.Domain.Tables.CadLayer(group.Key!) { Color = layerColor });
                    }
                }

                Viewport.InvalidateViewport();
                Viewport.ZoomExtents();
                StatusText.Text = $"Import tamamlandı: {dlg.ImportedEntities.Count:N0} nesne yüklendi.";
            }
        }

        private void OnAutoRouteCommand(object sender, RoutedEventArgs e)
        {
            var dlg = new AutoRouteDialog(_database, _history.TransactionManager) { Owner = this };
            dlg.Show();
            StatusText.Text = "AUTO-ROUTE: Dialog açıldı. Başlangıç ve bitiş noktalarını belirleyin.";
        }

        private void OnTechnicalSpecCommand(object sender, RoutedEventArgs e)
        {
            var dlg = new TechnicalSpecDialog(_database) { Owner = this };
            dlg.ShowDialog();
        }

        private void OnHatchCommand(object sender, RoutedEventArgs e)
        {
            var dlg = new HatchDialog() { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                StatusText.Text = $"HATCH: {dlg.SelectedPattern} seçildi (Ölçek: {dlg.PatternScale}). Kapalı alan noktalarını tıklayın.";
            }
        }

        private void OnViewportPrintCommand(object sender, RoutedEventArgs e)
        {
            var dlg = new ViewportPrintDialog(Viewport) { Owner = this };
            dlg.ShowDialog();
        }

        private void OnPrintPreviewCommand(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintPreviewDialog(_database) { Owner = this };
            dlg.ShowDialog();
        }

        private void OnToggleGridMode(object sender, RoutedEventArgs e)
        {
            Viewport.GridDotMode = !Viewport.GridDotMode;
            StatusText.Text = Viewport.GridDotMode ? "Grid: Nokta modu" : "Grid: Çizgi modu";
        }

        private void OnContinueDimCommand(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "DIMCONTINUE: Başlangıç noktasını tıklayın, ardından zincir ölçüler ekleyin.";
            var cmd = new LinearDimCommand(_database, _history.TransactionManager, _dimStyleService.ActiveStyle);
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () =>
            {
                var lastDims = _database.GetAllEntities()
                    .OfType<Afney.Cad.Domain.Entities.Annotation.DimensionEntity>()
                    .LastOrDefault();
                if (lastDims != null)
                {
                    // Zincir, başlangıç ölçüsünün yönünde (yatay → Y sabit, dikey → X sabit) devam etmeli.
                    double dimLineCoord = lastDims.IsHorizontal ? lastDims.DimLinePoint.Y : lastDims.DimLinePoint.X;
                    var cont = new ContinueDimCommand(
                        _database, _history.TransactionManager, lastDims.SecondPoint, dimLineCoord,
                        _dimStyleService.ActiveStyle, lastDims.IsHorizontal);
                    cont.OnFeedback  += msg2 => StatusText.Text = msg2;
                    cont.OnCompleted += () => Viewport.SetActiveCommand(null);
                    Viewport.SetActiveCommand(cont);
                    cont.Start();
                }
                else
                {
                    Viewport.SetActiveCommand(null);
                }
            };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnDistCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new DistCommand();
            cmd.OnFeedback  += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnDimTextHeightSmall(object sender, RoutedEventArgs e)
        {
            _dimStyleService.SetActiveStyle("Compact");
            _dimTextHeight = _dimStyleService.ActiveStyle.TextHeight;
            StatusText.Text = "Ölçü metin boyutu: Küçük (Compact stili)";
        }

        private void OnDimTextHeightMedium(object sender, RoutedEventArgs e)
        {
            _dimStyleService.SetActiveStyle("Standard");
            _dimTextHeight = _dimStyleService.ActiveStyle.TextHeight;
            StatusText.Text = "Ölçü metin boyutu: Normal (Standard stili)";
        }

        private void OnDimTextHeightLarge(object sender, RoutedEventArgs e)
        {
            _dimStyleService.SetActiveStyle("Large");
            _dimTextHeight = _dimStyleService.ActiveStyle.TextHeight;
            StatusText.Text = "Ölçü metin boyutu: Büyük (Large stili)";
        }

        private void OnDrawPipeCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new RoutePipeCommand(_database, _mechanicalKernel);
            SyncMechanicalSettings(cmd);

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnEntityPlaced += entity => _history.TransactionManager.Submit(new AddEntityOperation(_database, entity));
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); StatusText.Text = "Ready"; };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnPlaceFixtureOnWall(object sender, RoutedEventArgs e)
        {
            bool hasRecognizedWalls = _mechanicalKernel.ArchitecturalObstacles
                .Any(o => o.Type == Afney.Cad.Mechanical.Models.ObstacleType.Wall);
            if (!hasRecognizedWalls)
            {
                MessageBox.Show(
                    "Bu çizimde henüz tanınmış (mimari olarak algılanmış) duvar yok.\n\n" +
                    "Cihazı duvara yerleştirebilmek için önce:\n" +
                    "AutoBLD sekmesi → \"Eleman Tanı\" ile DWG çizgilerini duvar/kapı olarak tanıtın.",
                    "Duvar Tanınmadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new PlaceFixtureOnWallCommand(_database, _mechanicalKernel);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); StatusText.Text = "Ready"; };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void SyncMechanicalSettings(RoutePipeCommand? cmd = null)
        {
            if (Viewport == null) return;

            var target = cmd ?? (Viewport.GetType()
                .GetField("_activeCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(Viewport) as Afney.Cad.Commands.Abstractions.ICadCommand);

            if (target is not RoutePipeCommand pipeCmd) return;

            // Sistem tipi
            MechanicalSystemType sys = MechanicalSystemType.DomesticColdWater;
            string material = "PPRC";
            if (PipeSystemCombo?.SelectedItem is ComboBoxItem sysItem)
            {
                sys = (sysItem.Content?.ToString() ?? "") switch
                {
                    "Soğuk Su"  => MechanicalSystemType.DomesticColdWater,
                    "Sıcak Su"  => MechanicalSystemType.DomesticHotWater,
                    "Pis Su"    => MechanicalSystemType.WasteWater,
                    "Yangın"    => MechanicalSystemType.FireProtection,
                    "Gaz"       => MechanicalSystemType.Gas,
                    "Yağmur"    => MechanicalSystemType.RainWater,
                    _           => MechanicalSystemType.DomesticColdWater
                };
                material = sys switch
                {
                    MechanicalSystemType.WasteWater    => "PVC",
                    MechanicalSystemType.RainWater     => "PVC",
                    MechanicalSystemType.FireProtection => "Steel",
                    MechanicalSystemType.Gas           => "Steel",
                    _                                  => "PPRC"
                };
            }

            // Çap (DN)
            double size = 50.0;
            if (PipeDiameterCombo?.SelectedItem is ComboBoxItem dnItem)
            {
                string dnStr = dnItem.Content?.ToString() ?? "50";
                if (double.TryParse(dnStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                    size = parsed;
            }

            // Eğim
            double slope = 0.0;
            if (SlopeComboBox?.SelectedItem is ComboBoxItem slopeItem)
            {
                string slopeStr = (slopeItem.Content?.ToString() ?? "0").Replace("%", "").Trim();
                double.TryParse(slopeStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out slope);
            }

            pipeCmd.SetSettings(size, sys, material, slope);
        }

        private void OnMechanicalSettingsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_activeContext == null) return;
            SyncMechanicalSettings();
        }

        // PipeSystemCombo'dan aktif sistem tipini okur — RiserPipe/SourcePoint komutları bunu kullanır
        private MechanicalSystemType GetActiveSystemType()
        {
            if (PipeSystemCombo?.SelectedItem is ComboBoxItem item)
            {
                return (item.Content?.ToString() ?? "") switch
                {
                    "Soğuk Su" => MechanicalSystemType.DomesticColdWater,
                    "Sıcak Su" => MechanicalSystemType.DomesticHotWater,
                    "Pis Su"   => MechanicalSystemType.WasteWater,
                    "Yangın"   => MechanicalSystemType.FireProtection,
                    "Gaz"      => MechanicalSystemType.Gas,
                    "Yağmur"   => MechanicalSystemType.RainWater,
                    _          => MechanicalSystemType.DomesticColdWater
                };
            }
            return MechanicalSystemType.DomesticColdWater;
        }

        private void OnWBlockCommand(object sender, RoutedEventArgs e)
        {
            string projectPath = _mechanicalKernel.Metadata.ProjectName != null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CALC", $"{_mechanicalKernel.Metadata.ProjectName}.bld")
                : AppDomain.CurrentDomain.BaseDirectory;

            if (!System.IO.Directory.Exists(projectPath)) System.IO.Directory.CreateDirectory(projectPath);

            var tempDefPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "YeniPlan.dwg");
            var wizard = new WBlockWizard(Viewport, tempDefPath);
            var cmd = new ArchitecturalBlockCommand(_database, null!);
            wizard.Owner = this;

            wizard.RequestPickPoint += () =>
            {
                Viewport.SetActiveCommand(cmd);
                Viewport.Focus();
                Serilog.Log.Information("MAINWINDOW: RequestPickPoint. Viewport focused.");
                cmd.StartPickPoint();
            };

            wizard.RequestSelectObjects += () =>
            {
                Viewport.SetActiveCommand(cmd);
                cmd.StartSelection();
            };

            cmd.OnPointPicked += () =>
            {
                Serilog.Log.Information("MAINWINDOW: OnPointPicked received from WBlockCommand.");
                Dispatcher.Invoke(() =>
                {
                    wizard.SetBasePoint(cmd.BasePoint);
                    wizard.Show();
                    Serilog.Log.Information("MAINWINDOW: Wizard shown after point pick.");
                });
            };

            cmd.OnEntitiesSelected += () =>
            {
                cmd.SetSelectedEntities(Viewport.GetSelectedEntities());
                wizard.SetEntities(cmd.SelectedEntities);
                wizard.Show();
            };

            Viewport.SelectionChanged += (selection) =>
            {
                if (Viewport.ActiveCommand == cmd)
                {
                    cmd.SetSelectedEntities(selection);
                }
            };

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () =>
            {
                Viewport.SetActiveCommand(null);
                StatusText.Text = "WBlock Hazır.";
            };

            cmd.SetSelectedEntities(Viewport.GetSelectedEntities());
            wizard.SetEntities(cmd.SelectedEntities);

            wizard.OnExportConfirmed += (finalPath, floorName, entitiesToSave, basePoint) =>
            {
                var cloned = entitiesToSave.Select(x => x.Clone()).ToList();
                var scaleService = new ArchitecturalScaleService();
                var (_, factor) = scaleService.DetectScale(cloned);

                var moveMat = Afney.Cad.Geometry.Primitives.Matrix4x4.TranslationMatrix(-basePoint.X, -basePoint.Y, -basePoint.Z);
                var scaleMat = Afney.Cad.Geometry.Primitives.Matrix4x4.Scaling(factor, factor, factor);
                var final = scaleMat * moveMat;

                foreach (var ent in cloned) ent.Transform(final);

                var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
                var json = serializer.Serialize(new Afney.Cad.Database.Persistence.ProjectData
                {
                    Entities = cloned,
                    Layers = _database.GetLayers().ToList()
                });

                cmd.FinalizeExport(floorName, System.IO.Path.GetDirectoryName(finalPath) ?? projectPath, json);
            };

            wizard.Closed += (s, ev) =>
            {
                if (Viewport.ActiveCommand == cmd)
                {
                    Viewport.SetActiveCommand(null);
                }
            };

            wizard.Show();
        }

        private void OnBlockCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new BlockCommand(_database, (bc) =>
            {
                var dialog = new BMakeDialog(bc, _database);
                dialog.Owner = this;

                dialog.Show();
                return true;
            });

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnInsertCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new InsertCommand(_database, (ic) =>
            {
                var dialog = new BlockSelectionDialog(_database.GetBlocks());
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    ic.SetBlock(dialog.SelectedBlockName);
                }
                else
                {
                    ic.Cancel();
                }
            });

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnDefineMahalCommand(object sender, RoutedEventArgs e)
        {
            OnSelectRoom(sender, e);
        }

        private void OnInspectMahalCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new MahalInspectCommand(_database, _mechanicalKernel, (mahal, fixtures) =>
            {
                var dialog = new MahalDetailsDialog(mahal);
                dialog.Owner = this;
                dialog.ShowDialog();
            });

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnRecalculatePlumbing(object sender, RoutedEventArgs e)
        {
            _mechanicalKernel.RecalculateProject(_database.GetAllEntities());
            _mechanicalKernel.ResolveAllClashes(_database.GetAllEntities());
            Viewport.InvalidateViewport();
            StatusText.Text = "Tesisat hesaplamaları ve otomatik çakışma giderme tamamlandı.";
        }

        private void OnRiserGenerateCommand(object sender, RoutedEventArgs e)
        {
            if (!_mechanicalKernel.LevelManager.GetLevels().Any())
            {
                _mechanicalKernel.LevelManager.AddLevel(new Afney.Cad.Mechanical.Models.MepLevel("Zemin Kat", 0, 3000));
                _mechanicalKernel.LevelManager.AddLevel(new Afney.Cad.Mechanical.Models.MepLevel("1. Kat", 3000, 3000));
            }

            var cmd = new RiserGenerateCommand(_database, _mechanicalKernel);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSmartLabelCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new SmartLabelCommand(_database);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnGenerateBOQ_Click(object sender, RoutedEventArgs e)
        {
            Serilog.Log.Information("UI Komut: Metraj (BOM) Raporu Oluştur.");
            try
            {
                var bomReportWin = new BomReportWindow(_database);
                bomReportWin.Owner = this;
                bomReportWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Metraj raporu oluşturulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnIfcExportCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new Afney.Cad.Infrastructure.Export.IfcExportService();
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Project_{DateTime.Now:yyyyMMdd_HHmm}.ifc");

                service.ExportToIfc(_database.GetAllEntities(), path);

                MessageBox.Show($"IFC dosyası başarıyla oluşturuldu:\n{path}", "BIM Export", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "IFC dışa aktarımı tamamlandı.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
            }
        }

        private void OnLegendCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new LegendGenerateCommand(_database);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnOffsetCommand(object sender, RoutedEventArgs e)
        {
            if (ActiveContext?.Viewport == null) return;
            _lastRepeatableCommand = () => OnOffsetCommand(this, new RoutedEventArgs());
            ExecuteCommand("OFFSET");
        }

        private void OnDefineBuilding(object sender, RoutedEventArgs e)
        {
            string projectPath = _mechanicalKernel.Metadata.ProjectName != null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CALC", $"{_mechanicalKernel.Metadata.ProjectName}.bld")
                : AppDomain.CurrentDomain.BaseDirectory;

            if (!System.IO.Directory.Exists(projectPath)) System.IO.Directory.CreateDirectory(projectPath);

            var dialog = new DefineBuildingDialog(projectPath);
            dialog.Owner = this;

            dialog.OnLevelActivated += (filePath) =>
            {
                try
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        _database.Clear();

                        var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
                        string json = System.IO.File.ReadAllText(filePath);

                        var data = serializer.Deserialize(json);

                        if (data?.Entities != null)
                        {
                            var layers = data.Layers ?? new List<Afney.Cad.Domain.Tables.CadLayer>();
                            foreach (var layer in layers)
                            {
                                if (_database.GetLayer(layer.Name) == null) _database.AddLayer(layer);
                            }

                            foreach (var ent in data.Entities)
                            {
                                _database.AddEntity(ent);
                            }
                        }

                        Viewport.SetViewMode(false);
                        Viewport.InvalidateViewport();
                        Viewport.ZoomExtents();
                        StatusText.Text = $"Kat Yüklendi: {System.IO.Path.GetFileName(filePath)}";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kat yüklenirken hata: {ex.Message}");
                }
            };

            dialog.OnShow3D += (levels) =>
            {
                try
                {
                    StatusText.Text = "Bina montajı yapılıyor (BIM Alignment)...";

                    var assemblyService = new BuildingAssemblyService(_database, _mechanicalKernel);

                    var regs = levels.Select(l => new LevelFileRegistration
                    {
                        FilePath = l.FilePath,
                        Elevation = l.Elevation,
                        LevelName = l.LevelName
                    });

                    assemblyService.AssembleBuilding(regs);

                    Viewport.SetViewMode(true);
                    Viewport.InvalidateViewport();
                    Viewport.ZoomExtents();

                    _mechanicalKernel.RecalculateProject(_database.GetAllEntities());

                    StatusText.Text = "3D Bina Modeli ve Tesisat Ağı Oluşturuldu.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"3D Model oluşturulurken hata: {ex.Message}");
                }
            };

            dialog.ShowDialog();
        }

        private void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string rawText = CommandInput.Text.Trim();
                string cmdText = rawText.ToLower();
                CommandInput.Clear();

                // Aktif komut varsa: koordinat/mesafe girişi olarak dene
                if (Viewport.HasActiveCommand && !string.IsNullOrEmpty(rawText))
                {
                    // Boş Enter → komuta Enter ilet (polyline/line bitir)
                    if (string.IsNullOrEmpty(rawText))
                    {
                        Viewport.GetActiveCommand()?.OnKeyDown(Afney.Cad.Commands.Abstractions.InputKey.Enter);
                        return;
                    }

                    if (Viewport.AcceptCoordinateInput(rawText))
                    {
                        StatusText.Text = $"→ {rawText}";
                        e.Handled = true;
                        return;
                    }
                }

                switch (cmdText)
                {
                    case "wblock": OnWBlockCommand(this, new RoutedEventArgs()); break;
                    case "l": case "line": OnLineCommand(this, new RoutedEventArgs()); break;
                    case "o": case "offset": OnOffsetCommand(this, new RoutedEventArgs()); break;
                    case "c": case "circle": OnCircleCommand(this, new RoutedEventArgs()); break;
                    case "p": case "pipe": OnDrawPipeCommand(this, new RoutedEventArgs()); break;
                    case "rec": OnRecognizeArchitecture(this, new RoutedEventArgs()); break;
                    case "block": case "b": OnBlockCommand(this, new RoutedEventArgs()); break;
                    case "insert": case "i": OnInsertCommand(this, new RoutedEventArgs()); break;
                    case "mahal": case "ma": OnDefineMahalCommand(this, new RoutedEventArgs()); break;
                    case "mahalanaliz": case "man": OnInspectMahalCommand(this, new RoutedEventArgs()); break;
                    case "kolonsema": case "ks": OnRiserGenerateCommand(this, new RoutedEventArgs()); break;
                    case "etiket": case "label": OnSmartLabelCommand(this, new RoutedEventArgs()); break;
                    case "metraj": case "bom": OnGenerateBOQ_Click(this, new RoutedEventArgs()); break;
                    case "lejant": case "legana": case "legend": case "leg": OnLegendCommand(this, new RoutedEventArgs()); break;
                    case "ifc": case "ifcexport": case "export": case "bim": OnIfcExportCommand(this, new RoutedEventArgs()); break;
                    case "ifcimport": case "ifc-import": OnIfcImportCommand(this, new RoutedEventArgs()); break;
                    case "dxf": case "dxfexport": case "saveas": OnExportDxfCommand(this, new RoutedEventArgs()); break;
                    case "dimlinear": case "diml": case "dim": OnLinearDimCommand(this, new RoutedEventArgs()); break;
                    case "dimaligned": case "dima": OnAlignedDimCommand(this, new RoutedEventArgs()); break;
                    case "dimradius": case "dimr": OnRadiusDimCommand(this, new RoutedEventArgs()); break;
                    case "dimangular": case "dimang": OnAngularDimCommand(this, new RoutedEventArgs()); break;
                    case "dist": case "mesafe": case "uzaklik": OnDistCommand(this, new RoutedEventArgs()); break;
                    case "dimcontinue": case "dimcont": case "dco": OnContinueDimCommand(this, new RoutedEventArgs()); break;
                    case "hardycross": case "halka": case "ringanaliz": OnHardyCrossAnalysis(this, new RoutedEventArgs()); break;
                    case "tr": case "trim": OnTrimCommand(this, new RoutedEventArgs()); break;
                    case "ex": case "extend": OnExtendCommand(this, new RoutedEventArgs()); break;
                    case "mi": case "mirror": OnMirrorCommand(this, new RoutedEventArgs()); break;
                    case "co": case "copy": OnCopyCommand(this, new RoutedEventArgs()); break;
                    case "m": case "move": OnMoveCommand(this, new RoutedEventArgs()); break;
                    case "x": case "explode": OnExplodeCommand(this, new RoutedEventArgs()); break;
                    case "pl": case "pline": case "polyline": OnPolylineCommand(this, new RoutedEventArgs()); break;
                    case "rect": case "rectangle": OnRectangleCommand(this, new RoutedEventArgs()); break;
                    case "mtext": case "mt": case "text": OnMTextCommand(this, new RoutedEventArgs()); break;
                    case "hatch": case "h": case "bh": OnHatchCommand(this, new RoutedEventArgs()); break;
                    case "bagla": case "connect": case "cf": OnConnectFixtureCommand(this, new RoutedEventArgs()); break;
                    case "riser": case "kolon": case "kolonboru": OnRiserPipeCommand(this, new RoutedEventArgs()); break;
                    case "source": case "baslangic": case "sp": OnSourcePointCommand(this, new RoutedEventArgs()); break;
                    case "kabul": case "accept": case "validate": OnAcceptPlumbingCommand(this, new RoutedEventArgs()); break;
                    case "print": case "plot": OnViewportPrintCommand(this, new RoutedEventArgs()); break;
                    case "dwgimport": case "import": case "acimport": OnDwgImportDialog(this, new RoutedEventArgs()); break;
                    case "hvacbom": case "kanalmetraj": case "ductbom": OnHvacBomCommand(this, new RoutedEventArgs()); break;
                    case "duct": case "kanal": OnRouteDuctCommand(this, new RoutedEventArgs()); break;
                    case "ductconnect": case "kanalbagla": case "dc": OnConnectDuctCommand(this, new RoutedEventArgs()); break;
                    case "area": case "alan": OnSelectAreaCommand(this, new RoutedEventArgs()); break;
                    case "secimmetraj": case "selbom": case "sm": OnSelectionBomCommand(this, new RoutedEventArgs()); break;
                    case "mimaribom": case "archbom": case "mb": OnArchBomCommand(this, new RoutedEventArgs()); break;
                    case "archdetect": case "mimaritani": case "ad": OnArchDetectCommand(this, new RoutedEventArgs()); break;
                    case "autoroute": case "route": case "ar": OnAutoRouteCommand(this, new RoutedEventArgs()); break;
                    case "sartname": case "spec": case "techspec": OnTechnicalSpecCommand(this, new RoutedEventArgs()); break;
                    case "help": case "?": case "yardim": new CommandHelpDialog { Owner = this }.ShowDialog(); break;
                    default: StatusText.Text = $"Bilinmeyen komut: {cmdText} (komut listesi için HELP veya ? yazın)"; break;
                }
            }
        }

        private void ExecuteCommand(string commandName)
        {
            if (ActiveContext?.Viewport == null) return;

            ActiveContext.Viewport.CancelCurrentCommand();

            switch (commandName.ToUpper())
            {
                case "LINE":
                    ActiveContext.Viewport.SetCommand(new LineCommand(ActiveContext.Database, ActiveContext.History.TransactionManager));
                    break;
                case "PIPING":
                    ActiveContext.Viewport.SetCommand(new RoutePipeCommand(ActiveContext.Database, ActiveContext.MechanicalKernel));
                    break;
                case "MAHAL":
                    ActiveContext.Viewport.SetCommand(new SelectRoomCommand(ActiveContext.Database, (room) =>
                    {
                    }));
                    break;
                case "OFFSET":
                    var selectedForOffset = ActiveContext.Database.GetSelectedEntities().ToList();
                    if (selectedForOffset.Count > 0)
                        ActiveContext.Viewport.SetCommand(new OffsetCommand(ActiveContext.Database, ActiveContext.History.TransactionManager, selectedForOffset));
                    else
                        StatusText.Text = "Lütfen önce ötelenecek nesneleri seçin.";
                    break;
            }
        }

        #endregion
    }
}
