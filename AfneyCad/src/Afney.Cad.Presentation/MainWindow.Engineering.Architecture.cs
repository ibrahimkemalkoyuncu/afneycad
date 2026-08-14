using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Presentation.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Afney.Cad.Presentation
{
    public partial class MainWindow
    {
        #region -- MUHENDISLIK - MIMARI ANALIZ (ENGINEERING.ARCHITECTURE) --

        private void OnRecognizeArchitecture(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("Mimari Tanıma İşlemi Başlatıldı...");
                var recognitionService = new ArchitecturalRecognitionService(_database);
                var result = recognitionService.RecognizeObstacles();

                _mechanicalKernel.ArchitecturalObstacles.Clear();
                _mechanicalKernel.ArchitecturalObstacles.AddRange(result);

                StatusText.Text = $"MİMARİ ANALİZ: {result.Count} adet engel (Duvar/Kapı/Pencere) tanımlandı.";

                int walls = result.Count(o => o.Type == ObstacleType.Wall);
                int doors = result.Count(o => o.Type == ObstacleType.Door);
                int windows = result.Count(o => o.Type == ObstacleType.Window);

                MessageBox.Show($"Mimari kat planı başarıyla analiz edildi.\n\n" +
                                $"Tespit Edilen Unsurlar:\n" +
                                $"-----------------------\n" +
                                $"- Duvarlar: {walls}\n" +
                                $"- Kapılar: {doors}\n" +
                                $"- Pencereler: {windows}\n" +
                                $"- Kolonlar: {result.Count - (walls + doors + windows)}\n\n" +
                                $"Bu veriler 'Akıllı Yerleşim' (AutoLayout) ve 'Otomatik Rotalama' sırasında engel olarak dikkate alınacaktır.",
                                "Mimari Tanıma (Sematik Analiz)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Mimari tanıma hatası");
                MessageBox.Show($"Mimari analiz hatası: {ex.Message}", "Hata");
            }
        }

        private void OnClashDetectionClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var clashService = new ClashDetectionService(_mechanicalKernel.ArchitecturalObstacles);
                var mechanicals = _mechanicalKernel.TopologyGraph.Nodes.Select(n => n.Entity).ToList();
                var clashes = clashService.DetectClashes(mechanicals);

                var reportDialog = new ClashReportDialog(clashes);
                reportDialog.Owner = this;
                reportDialog.ShowDialog();

                Viewport.InvalidateViewport();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Clash Detection Hatası");
                MessageBox.Show($"Çakışma Analizi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnBimProperties(object sender, RoutedEventArgs e)
        {
            try
            {
                var obstacle = _mechanicalKernel.ArchitecturalObstacles.FirstOrDefault();
                if (obstacle == null)
                {
                    obstacle = new ArchitecturalObstacle
                    {
                        Name = "Yeni BIM Nesnesi",
                        Type = ObstacleType.Wall,
                        Height = 3000
                    };
                    _mechanicalKernel.ArchitecturalObstacles.Add(obstacle);
                }
                var dialog = new BimPropertiesDialog(obstacle) { Owner = this };
                if (dialog.ShowDialog() == true)
                    StatusText.Text = $"BIM özellikleri kaydedildi — U={obstacle.UValue:F3} W/m²K";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnSmartBimConvert(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SmartBimConverterDialog(_database, _mechanicalKernel) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    StatusText.Text = $"DWG-BIM dönüşüm tamamlandı — {_mechanicalKernel.ArchitecturalObstacles.Count} nesne";
                    Viewport.InvalidateViewport();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnArchitecturalLibrary(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ArchitecturalLibraryDialog(_mechanicalKernel) { Owner = this };
                dialog.ShowDialog();
                StatusText.Text = $"Mimari kütüphane — {_mechanicalKernel.ArchitecturalObstacles.Count} BIM nesnesi mevcut.";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnMultiStoryManager(object sender, RoutedEventArgs e)
        {
            try { new MultiStoryManagerDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Çok katlı bina hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnWallParallelRoute(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new WallParallelRouteDialog(_database) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    Viewport.InvalidateVisual();
                    MessageBox.Show("Duvara paralel boru rotalama tamamlandı.", "AfneyCAD");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Rotalama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnDoublePipeRoute(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "ÇIFT HAT — Başlangıç noktasını tıklayın...";

            var pickStartCmd = new PickPointCommand();
            pickStartCmd.OnPointPicked += (startPt) =>
            {
                StatusText.Text = "ÇIFT HAT — Bitiş noktasını tıklayın...";

                var pickEndCmd = new PickPointCommand();
                pickEndCmd.OnPointPicked += (endPt) =>
                {
                    try
                    {
                        var service = new DoublePipeRoutingService(_database) { SeparationDistance = 150.0 };
                        var result = service.RouteDoublePipe(startPt, endPt);

                        var ops = new CompositeOperation("Double Pipe Route");
                        foreach (var p in result.HotPipes)
                        {
                            ops.Add(new AddEntityOperation(_database, p));
                            _mechanicalKernel.OnEntityAddedToDatabase(p);
                        }
                        foreach (var p in result.ColdPipes)
                        {
                            ops.Add(new AddEntityOperation(_database, p));
                            _mechanicalKernel.OnEntityAddedToDatabase(p);
                        }

                        _history.TransactionManager.Submit(ops);
                        Viewport.InvalidateVisual();

                        StatusText.Text = $"Çift Hat: {result.HotPipes.Count} sıcak + {result.ColdPipes.Count} soğuk boru oluşturuldu.";
                    }
                    catch (Exception ex) { MessageBox.Show($"Çift hat hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
                    finally { Viewport.SetActiveCommand(null); }
                };

                Viewport.SetActiveCommand(pickEndCmd);
                pickEndCmd.Start();
            };

            Viewport.SetActiveCommand(pickStartCmd);
            pickStartCmd.Start();
        }

        private void OnPressureMapToggle(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_pressureMapService.IsActive)
                {
                    _pressureMapService.Restore(_database);
                    if (BtnPressureMap != null) BtnPressureMap.Background = System.Windows.Media.Brushes.SteelBlue;
                    StatusText.Text = "Basınç haritası kapatıldı.";
                }
                else
                {
                    int count = _pressureMapService.Apply(_database);
                    if (count == 0) { StatusText.Text = "Basınç verisi yok — önce hesaplama yapın."; return; }
                    var summary = _pressureMapService.GetSummary(_database);
                    if (BtnPressureMap != null) BtnPressureMap.Background = System.Windows.Media.Brushes.OrangeRed;
                    StatusText.Text = $"Basınç Haritası: max={summary.MaxPressureDropM:F4} mSS · ort={summary.AvgPressureDropM:F4} mSS · {summary.CriticalPipeCount} kritik boru";
                }
                Viewport.InvalidateViewport();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnClashHighlightToggle(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clashHighlightService.IsActive)
                {
                    _clashHighlightService.Restore(_database);
                    if (BtnClashHighlight != null) BtnClashHighlight.Background = System.Windows.Media.Brushes.SteelBlue;
                    StatusText.Text = "Çakışma vurgusu kapatıldı.";
                }
                else
                {
                    var obstacles = _mechanicalKernel?.ArchitecturalObstacles ?? [];
                    var summary = _clashHighlightService.Apply(_database, obstacles);
                    if (summary.TotalClashes == 0)
                    {
                        StatusText.Text = "Çakışma bulunamadı — tüm tesisat elemanları temiz.";
                    }
                    else
                    {
                        if (BtnClashHighlight != null) BtnClashHighlight.Background = System.Windows.Media.Brushes.Red;
                        StatusText.Text = $"Çakışma Vurgusu: {summary.TotalClashes} çakışma · {summary.CriticalCount} kritik · {summary.WarningCount} uyarı · {summary.AffectedEntities} etkilenen eleman";
                    }
                }
                Viewport.InvalidateViewport();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion
    }
}
