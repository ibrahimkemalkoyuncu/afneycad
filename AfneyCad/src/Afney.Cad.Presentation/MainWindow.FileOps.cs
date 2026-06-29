using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
using Afney.Cad.Presentation.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow
    {
        private void OnNewProject(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                CreateNewDocument(dialog.ProjectName);

                string archPath = dialog.ArchitectPath;
                if (!string.IsNullOrEmpty(archPath))
                {
                    string targetPath = Path.Combine(dialog.FinalProjectFolder, Path.GetFileName(archPath));
                    LoadDwgInternal(targetPath);
                }

                StatusText.Text = $"Yeni Proje Sekmesi: {dialog.ProjectName}";
                Log.Information("Yeni proje sekmesi oluşturuldu: {ProjectName}", dialog.ProjectName);
            }
        }

        private void OnNewFile(object sender, RoutedEventArgs e)
        {
            string name = $"Drawing{_documents.Count + 1}";
            CreateNewDocument(name);
        }

        private void OnNewWindow(object sender, RoutedEventArgs e)
        {
            Process.Start(Environment.ProcessPath!);
        }

        private void OnOpenFile(object sender, RoutedEventArgs e)
        {
            Log.Information("Dosya açma diyaloğu açılıyor...");
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Autocad DWG (*.dwg)|*.dwg|Autocad DXF (*.dxf)|*.dxf|Tüm Dosyalar (*.*)|*.*",
                Title = "AfneyCAD - Proje Aç"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var info = new FileInfo(openFileDialog.FileName);
                string name = Path.GetFileNameWithoutExtension(info.Name);

                CreateNewDocument(name, info.FullName);
                LoadDwgInternal(openFileDialog.FileName);
                _recentFiles.AddFile(info.FullName);
            }
        }

        private async void LoadDwgInternal(string filePath)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Information("[MAIN] Dosya yükleniyor: {Path}", filePath);
                StatusText.Text = "Dosya yükleniyor... Lütfen bekleyin.";

                // UI donmasını önlemek için ağır işlemi arka plana at
                var importer = new CadImporter();
                var entities = await System.Threading.Tasks.Task.Run(() => importer.Import(filePath));

                // Geri kalan işlemler UI thread'inde devam edecek (aşağıda)
                LoadDwgEntities(filePath, entities, stopwatch);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Dosya yükleme hatası");
                MessageBox.Show($"Dosya yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDwgEntities(string filePath, System.Collections.Generic.List<Afney.Cad.Domain.Abstractions.CadEntity> entities, Stopwatch stopwatch)
        {
            try
            {

                stopwatch.Stop();
                Log.Information("[MAIN] Dosya yüklendi. Nesne: {Count}, Süre: {Duration}ms", entities.Count, stopwatch.ElapsedMilliseconds);

                if (entities.Count == 0)
                {
                    Log.Warning("[MAIN] Dosya boş veya nesne okunamadı: {Path}", filePath);
                    MessageBox.Show("Dosyada okunabilir nesne bulunamadı.");
                    return;
                }

                _database.Clear();
                Log.Information("Veritabanı temizlendi (önceki çizim silindi).");

                var distinctLayers = entities.Select(e => e.Layer).Distinct().ToList();
                foreach (var layerName in distinctLayers)
                {
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        _database.AddLayer(new Afney.Cad.Domain.Tables.CadLayer(layerName));
                    }
                }
                Log.Information("{Count} adet layer çıkarıldı ve eklendi.", distinctLayers.Count);

                var activeEntities = entities;

                var centers = entities.AsParallel().Select(e => e.GetBoundingBox().Center).ToList();
                if (centers.Count > 0)
                {
                    double avgX = centers.AsParallel().Average(c => c.X);
                    double avgY = centers.AsParallel().Average(c => c.Y);

                    var filtered = new System.Collections.Concurrent.ConcurrentBag<Afney.Cad.Domain.Abstractions.CadEntity>();
                    int removedCount = 0;
                    double thresholdSq = 500000.0 * 500000.0;

                    System.Threading.Tasks.Parallel.ForEach(entities, ent =>
                    {
                        var c = ent.GetBoundingBox().Center;
                        double distSq = Math.Pow(c.X - avgX, 2) + Math.Pow(c.Y - avgY, 2);
                        if (distSq < thresholdSq)
                        {
                            filtered.Add(ent);
                        }
                        else
                        {
                            System.Threading.Interlocked.Increment(ref removedCount);
                        }
                    });

                    if (removedCount > 0)
                    {
                        activeEntities = filtered.ToList();
                        Log.Information("Otomatik Temizleme: {Count} adet uzak nesne silindi.", removedCount);
                    }
                }

                var finalEntities = new System.Collections.Concurrent.ConcurrentBag<Afney.Cad.Domain.Abstractions.CadEntity>();
                double totalLen = 0;
                int lineCount = 0;

                object lenLock = new object();

                System.Threading.Tasks.Parallel.ForEach(activeEntities, ent =>
                {
                    if (ent is Afney.Cad.Domain.Entities.Basic.LineEntity l)
                    {
                        l.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(l.StartPoint.X, l.StartPoint.Y, 0);
                        l.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(l.EndPoint.X, l.EndPoint.Y, 0);

                        double dx = l.EndPoint.X - l.StartPoint.X;
                        double dy = l.EndPoint.Y - l.StartPoint.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);

                        if (len > 0.01)
                        {
                            lock (lenLock)
                            {
                                totalLen += len;
                            }
                            System.Threading.Interlocked.Increment(ref lineCount);
                            finalEntities.Add(l);
                        }
                    }
                    else
                    {
                        finalEntities.Add(ent);
                    }
                });

                activeEntities = finalEntities.ToList();
                double avgLen = lineCount > 0 ? totalLen / lineCount : 0;
                Log.Information("Yüklenen temiz nesne sayısı: {Count}. Ort çizgi uz.: {AvgLen:F2}", activeEntities.Count, avgLen);

                foreach (var ent in activeEntities)
                {
                    _database.AddEntity(ent);
                }

                var mahalService = new Afney.Cad.Application.Services.MahalExportService(_database);
                string mahalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mahal.txt");
                try { mahalService.ExportMahalDataToJson(mahalPath); } catch (Exception ex) { Log.Warning("Otomatik Mahal Analizi tamamlanamadı: " + ex.Message); }

                Viewport.ZoomExtents();

                LoadLayerState(filePath);

                Dispatcher.Invoke(() =>
                {
                    if (_activeContext != null)
                    {
                        RefreshActiveLayerCombo(_activeContext.Database);
                        LayerPanel.RefreshLayers(_activeContext.Database);
                        LayerPanel.SyncHiddenLayers(_activeContext.Viewport.HiddenLayers);
                        LeftPanelBorder.Visibility = Visibility.Visible;
                        LayerPanel.Visibility = Visibility.Visible;
                        ProjectNavigatorPanel.Visibility = Visibility.Collapsed;
                    }
                });

                string statusMsg = $"Proje yüklendi: {activeEntities.Count} nesne.";
                StatusText.Text = statusMsg;
                MessageBox.Show($"{statusMsg}\nOrtalama çizgi: {avgLen:F2} birim.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Dosya yükleme hatası");
                MessageBox.Show($"Dosya yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
        }

        private void OnExportMahalData(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;
            try
            {
                var service = new Afney.Cad.Application.Services.MahalExportService(_database);
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mahal.txt");
                string result = service.ExportMahalDataToJson(path);
                MessageBox.Show(result, "Mahal Analizi (JSON)", MessageBoxButton.OK, MessageBoxImage.Information);

                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Mahal analizi çıkarılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (_activeContext == null || _database == null) return;

            string filePath = _activeContext.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                OnSaveAs(sender, e);
                return;
            }

            try
            {
                SaveToFile(filePath);
                _activeContext.IsModified = false;
                StatusText.Text = $"Kaydedildi: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatasi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Farkli Kaydet",
                Filter = "AutoCAD DWG (*.dwg)|*.dwg|DXF Dosyasi (*.dxf)|*.dxf|AfneyCAD Projesi (*.afney)|*.afney",
                FileName = !string.IsNullOrEmpty(_activeContext?.FilePath)
                    ? Path.GetFileNameWithoutExtension(_activeContext.FilePath)
                    : _activeContext?.ProjectName ?? "Proje",
                DefaultExt = ".dwg"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    SaveToFile(dlg.FileName);
                    if (_activeContext != null)
                    {
                        _activeContext.FilePath = dlg.FileName;
                        _activeContext.IsModified = false;
                    }
                    Title = $"AfneyCAD - {_activeContext?.ProjectName} [{dlg.FileName}]";
                    StatusText.Text = $"Kaydedildi: {Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kaydetme hatasi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveToFile(string filePath)
        {
            if (_database == null) return;

            if (filePath.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
            {
                var dxf = new DxfWriterService(_database);
                dxf.WriteToFile(filePath);
            }
            else
            {
                var dwg = new DwgExportService(_database);
                dwg.WriteToFile(filePath);
            }

            SaveLayerState(filePath);
        }

        private void SaveLayerState(string filePath)
        {
            try
            {
                string stateFile = filePath + ".layerstate";
                var hiddenLayers = _activeContext?.Viewport?.HiddenLayers;
                if (hiddenLayers != null && hiddenLayers.Any())
                {
                    File.WriteAllLines(stateFile, hiddenLayers);
                }
                else if (File.Exists(stateFile))
                {
                    File.Delete(stateFile);
                }
            }
            catch { }
        }

        private void LoadLayerState(string filePath)
        {
            try
            {
                string stateFile = filePath + ".layerstate";
                if (File.Exists(stateFile) && _activeContext?.Viewport != null)
                {
                    var hidden = File.ReadAllLines(stateFile);
                    foreach (var layer in hidden)
                    {
                        if (!string.IsNullOrWhiteSpace(layer))
                            _activeContext.Viewport.HiddenLayers.Add(layer.Trim());
                    }
                }
            }
            catch { }
        }

        private void OnExportDwgCommand(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "DWG Olarak Kaydet",
                    Filter     = "AutoCAD DWG (*.dwg)|*.dwg|DXF Dosyası (*.dxf)|*.dxf",
                    FileName   = $"AfneyCAD_{DateTime.Now:yyyyMMdd_HHmm}",
                    DefaultExt = ".dwg"
                };
                if (dlg.ShowDialog() != true) return;

                if (dlg.FileName.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                {
                    var dxf = new DxfWriterService(_database);
                    dxf.WriteToFile(dlg.FileName);
                }
                else
                {
                    var dwg = new DwgExportService(_database);
                    dwg.WriteToFile(dlg.FileName);
                }

                StatusText.Text = $"Kaydedildi: {Path.GetFileName(dlg.FileName)}";
                MessageBox.Show($"Dosya başarıyla kaydedildi:\n{dlg.FileName}", "Kaydet",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnAutoSave(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                StatusText.Text = item.IsChecked ? "Otomatik Kaydet: AÇIK" : "Otomatik Kaydet: KAPALI";
            }
        }

        private void OnCloseEditor(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Editör kapatıldı (Simülasyon).", "Bilgi");
        }

        private void OnCloseFolder(object sender, RoutedEventArgs e)
        {
        }

        private void OnCloseWindow(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void OnIfcImportCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new IfcImportDialog(_database)
                {
                    Owner = this
                };
                if (dialog.ShowDialog() == true)
                {
                    _activeContext?.Viewport.InvalidateViewport();
                    StatusText.Text = "IFC aktarımı tamamlandı — mimari altlık görüntüleniyor.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"IFC aktarım hatası: {ex.Message}");
            }
        }

        private void OnExportDxfCommand(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title       = "DXF Olarak Kaydet",
                    Filter      = "DXF Dosyası (*.dxf)|*.dxf",
                    FileName    = $"AfneyCAD_{DateTime.Now:yyyyMMdd_HHmm}.dxf",
                    DefaultExt  = ".dxf"
                };

                if (dlg.ShowDialog() == true)
                {
                    var writer = new DxfWriterService(_database);
                    writer.WriteToFile(dlg.FileName);
                    StatusText.Text = $"DXF kaydedildi: {Path.GetFileName(dlg.FileName)}";
                    MessageBox.Show($"DXF başarıyla kaydedildi:\n{dlg.FileName}", "DXF Export",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DXF export hatası: {ex.Message}");
            }
        }

        private void OnExportExcel(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "Excel Çıktısı",
                    Filter     = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx",
                    FileName   = $"AfneyCAD_{DateTime.Now:yyyyMMdd_HHmm}",
                    DefaultExt = ".xlsx"
                };
                if (dlg.ShowDialog() != true) return;

                var svc = new ExcelExportService(_database);
                svc.WriteToFile(dlg.FileName, projectName: "AfneyCAD Projesi");

                StatusText.Text = $"Excel kaydedildi: {Path.GetFileName(dlg.FileName)}";
                var ans = MessageBox.Show($"Excel başarıyla kaydedildi.\nDosyayı açmak ister misiniz?",
                    "Excel Çıktısı", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ans == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel export hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPdfExport(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new PdfExportDialog(_database) { Owner = this };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF çıktı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRiserDiagramExport(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new RiserDiagramExportDialog(_database, _mechanicalKernel);
                dialog.Owner = this;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kolon şeması hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExportPng(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "PNG Olarak Kaydet",
                    Filter     = "PNG Dosyası (*.png)|*.png",
                    FileName   = $"AfneyCAD_{DateTime.Now:yyyyMMdd_HHmm}.png",
                    DefaultExt = ".png"
                };

                if (dlg.ShowDialog() == true)
                {
                    var svc = new Services.PrintViewportService();
                    var options = new Services.PrintViewportService.PrintOptions
                    {
                        Format       = Services.PrintViewportService.PageFormat.A3_Landscape,
                        DpiResolution = 200
                    };
                    svc.ExportToPng(Viewport, dlg.FileName, options);
                    StatusText.Text = $"PNG kaydedildi: {Path.GetFileName(dlg.FileName)}";
                    MessageBox.Show($"PNG başarıyla kaydedildi:\n{dlg.FileName}", "PNG Export",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PNG export hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExportHtmlViewer(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "Mobil HTML Görüntüleyici Olarak Kaydet",
                    Filter     = "HTML Dosyası (*.html)|*.html",
                    FileName   = $"AfneyCAD_{(_mechanicalKernel?.Metadata?.ProjectName ?? "proje")}_{DateTime.Now:yyyyMMdd}.html",
                    DefaultExt = ".html"
                };

                if (dlg.ShowDialog() == true)
                {
                    string projName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD Proje";
                    string html = _htmlViewerService.Export(_database, projName);
                    File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
                    StatusText.Text = $"HTML Viewer kaydedildi: {Path.GetFileName(dlg.FileName)}";

                    if (MessageBox.Show($"HTML başarıyla oluşturuldu.\n{dlg.FileName}\n\nTarayıcıda açılsın mı?",
                        "Mobil HTML Viewer", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"HTML export hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnAxonometricExport(object sender, RoutedEventArgs e)
        {
            try
            {
                string projName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD";
                new AxonometricExportDialog(_database, projName) { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnManufacturerCatalog(object sender, RoutedEventArgs e)
        {
            try { new ManufacturerCatalogDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}
