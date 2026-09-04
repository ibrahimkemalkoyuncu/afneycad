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
        #region -- KOMUT SATIRI & BOM/METRAJ --

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

                    // Dinamik girdi: "=1200" → aktif ölçü komutunun otomatik hesaplanan
                    // metnini "1200" ile geçersiz kılar. "=" tek başına (boş değer) override'ı temizler.
                    if (rawText.StartsWith("=") &&
                        Viewport.GetActiveCommand() is Afney.Cad.Commands.Abstractions.IDimensionOverridable overridable)
                    {
                        string overrideValue = rawText.Substring(1).Trim();
                        overridable.SetTextOverride(string.IsNullOrEmpty(overrideValue) ? null : overrideValue);
                        e.Handled = true;
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
                    case "dimstyle": case "ddim": case "olcustili": OnDimensionStyleManager(this, new RoutedEventArgs()); break;
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
                    case "d3dtest": case "d3d11": OnD3D11EngineTest(this, new RoutedEventArgs()); break;
                    case "box": case "kutu": OnSolidBoxCommand(this, new RoutedEventArgs()); break;
                    case "union": case "birlestir": OnSolidUnionCommand(this, new RoutedEventArgs()); break;
                    case "subtract": case "cikar": OnSolidSubtractCommand(this, new RoutedEventArgs()); break;
                    case "intersect": case "kesistir": OnSolidIntersectCommand(this, new RoutedEventArgs()); break;
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
