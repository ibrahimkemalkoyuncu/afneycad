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
        #region -- MUHENDISLIK - HIDROLIK HESAPLAMA (ENGINEERING.HYDRAULICS) --

        private async void OnRecalculateSystem(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Hidrolik analiz yapılıyor (TS 1258)...";
                MainProgressBar.IsIndeterminate = false;
                MainProgressBar.Minimum = 0;
                MainProgressBar.Maximum = 100;
                MainProgressBar.Value = 0;
                MainProgressBar.Visibility = Visibility.Visible;
                TabCalculation.IsEnabled = false;

                var entities = _database.GetAllEntities().ToList();

                var progress = new Progress<(int Percent, string Stage)>(p =>
                {
                    MainProgressBar.Value = p.Percent;
                    StatusText.Text = $"Hidrolik analiz: %{p.Percent} — {p.Stage}";
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _mechanicalKernel.RecalculateProject(entities, progress);
                });

                Viewport.InvalidateVisual();

                StatusText.Text = "Analiz Tamamlandı.";
                MessageBox.Show("Tüm sistem analizi asenkron olarak tamamlandı.\n" +
                                "- Akış yükleri (FU) hesaplandı.\n" +
                                "- Boru çapları otomatik optimize edildi.\n" +
                                "- Kritik hat basınç kayıpları güncellendi.",
                                "Mühendislik Analizi");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Analiz hatası: {ex.Message}");
                StatusText.Text = "Analiz Başarısız.";
            }
            finally
            {
                MainProgressBar.Visibility = Visibility.Collapsed;
                MainProgressBar.IsIndeterminate = true;
                TabCalculation.IsEnabled = true;
            }
        }

        private void OnPressureDropCalc(object sender, RoutedEventArgs e)
        {
            try
            {
                OnCalculateFlowCommand(sender, e);

                var entities = _database.GetAllEntities().ToList();
                var pipes = entities.OfType<PipeEntity>().ToList();
                var sinks = pipes.Where(p =>
                    Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) > 0.8
                ).ToList();

                if (!sinks.Any())
                {
                    MessageBox.Show("Rapor üretilecek bir kolon (Riser) veya giriş noktası bulunamadı.", "Hata");
                    return;
                }

                var pressureService = new PressureDropService(
                    _mechanicalKernel.TopologyGraph,
                    _mechanicalKernel.ProjectSettings,
                    _database);

                var report = pressureService.GenerateReport(sinks.First().Id);

                if (report != null && report.Segments.Any())
                {
                    var reportWindow = new PressureDropReportWindow(report);
                    reportWindow.Owner = this;
                    reportWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Bu kolon için kritik hat tespit edilemedi. Bağlantıların tam olduğundan emin olun.", "Uyarı");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Kritik hat raporu oluşturulurken hata");
                MessageBox.Show($"Rapor oluşturulamadı: {ex.Message}", "Hata");
            }
        }

        private void OnAutoPipeSizing(object sender, RoutedEventArgs e)
        {
            try
            {
                var flowService = new FlowCalculationService(_mechanicalKernel.TopologyGraph);
                var mechEntities = _database.GetAllEntities().OfType<MechanicalEntity>().ToList();

                flowService.CalculateSystemFlow(mechEntities);
                flowService.AutoSizePipes(mechEntities);

                Viewport.InvalidateVisual();
                StatusText.Text = "AKILLI ÇAPLANDIRMA TAMAMLANDI: Borular standartlara göre güncellendi.";
                MessageBox.Show("Tesisat ağındaki tüm borular TS 1258 standartlarına göre otomatik olarak çaplandırıldı.", "Mühendislik Modu");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Çaplandırma hatası: {ex.Message}");
            }
        }

        private void OnGenerateLegend(object sender, RoutedEventArgs e)
        {
            try
            {
                var legendService = new LegendService(_database.GetAllEntities());
                var pos = Viewport.GetCameraCenter();
                var legendTable = legendService.GenerateLegend(pos);

                foreach (var ent in legendTable)
                    _database.AddEntity(ent);
                Viewport.InvalidateVisual();
                StatusText.Text = "LEJANT OLUŞTURULDU: Kullanılan semboller tabloya eklendi.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lejant oluşturulurken hata: {ex.Message}");
            }
        }

        private void OnCalculateFlowCommand(object sender, RoutedEventArgs e)
        {
            OnAutoPipeSizing(sender, e);
        }

        private void OnLevelManager(object sender, RoutedEventArgs e)
        {
            var dialog = new LevelManagerDialog(_mechanicalKernel.LevelManager);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void OnBuildingProperties(object sender, RoutedEventArgs e)
        {
            var dialog = new BuildingPropertiesDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void OnPumpSelection(object sender, RoutedEventArgs e)
        {
            try
            {
                double maxFlow    = 5.0;
                double reqHead    = 25.0;

                var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
                if (pipes.Any())
                {
                    OnCalculateFlowCommand(sender, e);
                    maxFlow = pipes.Max(p => p.FlowRate);

                    var pressureService = new PressureDropService(
                        _mechanicalKernel.TopologyGraph, _mechanicalKernel.ProjectSettings, _database);
                    var sink = pipes.OrderByDescending(p => p.FlowRate).FirstOrDefault();
                    if (sink != null)
                    {
                        var report = pressureService.GenerateReport(sink.Id);
                        if (report != null)
                            reqHead = report.TotalPressureRequired;
                    }
                }

                var dialog = new PumpSelectionDialog(_database, maxFlow, reqHead)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Seçim modülü hatası: {ex.Message}");
            }
        }

        private void OnNetworkTopology(object sender, RoutedEventArgs e)
        {
            try { var d = new NetworkTopologyDialog(_database) { Owner = this }; if (d.ShowDialog() == true) Viewport.InvalidateViewport(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnDepoHidrofor(object sender, RoutedEventArgs e)
        {
            try { new DepoHidroforDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnWaterMeter(object sender, RoutedEventArgs e)
        {
            try { new WaterMeterDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnExpansionTank(object sender, RoutedEventArgs e)
        {
            try { new ExpansionTankDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnBackflowPreventer(object sender, RoutedEventArgs e)
        {
            try { new BackflowPreventerDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnConfirmSystemSettings(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Bina ve Sistem ayarları onaylandı.\nUç Noktalar sekmesinin kilidi açıldı.", "Süreç Onayı", MessageBoxButton.OK, MessageBoxImage.Information);
            if (TabTerminals != null) { TabTerminals.IsEnabled = true; TabTerminals.IsSelected = true; }
        }

        private void OnConfirmTerminals(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Mahal yükleri tanımlamaları onaylandı.\nTesisat sekmesinin kilidi açıldı.", "Süreç Onayı", MessageBoxButton.OK, MessageBoxImage.Information);
            if (TabRouting != null) { TabRouting.IsEnabled = true; TabRouting.IsSelected = true; }
        }

        private void OnConfirmRouting(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rotalama onaylandı.\nHesap sekmesinin kilidi açıldı.", "Süreç Onayı", MessageBoxButton.OK, MessageBoxImage.Information);
            if (TabCalculation != null) { TabCalculation.IsEnabled = true; TabCalculation.IsSelected = true; }
        }

        private void OnHotWaterCirculation(object sender, RoutedEventArgs e)
        {
            try { new HotWaterCirculationDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Resirkülasyon hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnPressureZoneDesign(object sender, RoutedEventArgs e)
        {
            try { new PressureZoneDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Basınç bölgesi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnPipeCostAnalysis(object sender, RoutedEventArgs e)
        {
            try { new PipeCostDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Maliyet analizi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnFanSelection(object sender, RoutedEventArgs e)
        {
            try { new FanSelectionDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnHeatPump(object sender, RoutedEventArgs e)
        {
            try { new HeatPumpDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnFloorHeating(object sender, RoutedEventArgs e)
        {
            try { new FloorHeatingDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnSprinklerDesign(object sender, RoutedEventArgs e)
        {
            try { new SprinklerDesignDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnEnergyPerformance(object sender, RoutedEventArgs e)
        {
            try { new EnergyPerformanceDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnAHUDesign(object sender, RoutedEventArgs e)
        {
            try { new AHUDesignDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnAutoSizing(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc    = new AutoSizingService();
                var result = svc.SizeAll(_database);

                string warnings = result.Warnings.Count > 0
                    ? "\n\nUyarılar:\n" + string.Join("\n", result.Warnings.Take(10))
                    : "";

                MessageBox.Show(result.Summary + warnings, "Otomatik Boyutlandırma Tamamlandı",
                    MessageBoxButton.OK, result.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                if (result.ResizedPipes > 0)
                {
                    Viewport?.InvalidateVisual();
                    StatusText.Text = $"Oto boyutlandırma: {result.ResizedPipes} boru güncellendi.";
                }
            }
            catch (Exception ex) { MessageBox.Show($"Otomatik boyutlandırma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnPumpGroup(object sender, RoutedEventArgs e)
        {
            try { new PumpGroupDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Pompaj Grubu hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnHardyCrossAnalysis(object sender, RoutedEventArgs e)
        {
            try
            {
                var allPipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
                if (allPipes.Count == 0)
                {
                    MessageBox.Show("Analiz edilecek boru bulunamadı.", "Halkalı Şebeke Analizi", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var solver = new Afney.Cad.Mechanical.Engine.Hydraulics.HardyCrossSolver();
                int totalLoops = 0;
                int systemsWithLoops = 0;
                var sb = new System.Text.StringBuilder();

                foreach (var group in allPipes.GroupBy(p => p.SystemType))
                {
                    var buildResult = Afney.Cad.Mechanical.Engine.Hydraulics.HydraulicNetworkBuilder.Build(group.ToList());
                    int pipeCountBefore = buildResult.Network.Pipes.Count;
                    int nodeCount = buildResult.Network.Nodes.Count;
                    int loopCount = pipeCountBefore - nodeCount + CountConnectedComponents(buildResult.Network);

                    if (loopCount <= 0) continue;

                    solver.Solve(buildResult.Network);

                    foreach (var (pipe, netPipe) in buildResult.PipeMap)
                    {
                        pipe.FlowRate = System.Math.Abs(netPipe.FlowRate);
                        pipe.PressureDrop = netPipe.HeadLoss;
                    }

                    totalLoops += loopCount;
                    systemsWithLoops++;
                    sb.AppendLine($"• {group.Key}: {loopCount} bağımsız halka, {group.Count()} boru — debi dağılımı Hardy-Cross ile düzeltildi.");
                }

                Viewport.InvalidateVisual();

                if (systemsWithLoops == 0)
                {
                    MessageBox.Show(
                        "Çizimde kapalı halka (ring) oluşturan bir boru şebekesi bulunamadı.\nTüm sistemler ağaç (dallanan) topolojisinde — Hardy-Cross düzeltmesine gerek yok.",
                        "Halkalı Şebeke Analizi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Hardy-Cross analizi tamamlandı.\n\nToplam {totalLoops} bağımsız halka bulundu ve düzeltildi:\n\n{sb}",
                        "Halkalı Şebeke Analizi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Hardy-Cross analizi hatası");
                MessageBox.Show($"Halkalı şebeke analizi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*
           NE: Bağlantılı Bileşen Sayısı (CountConnectedComponents)
           NEDEN: Bağımsız halka sayısı = Kenar - Düğüm + Bileşen (graf teorisi). Şebeke birden
                  fazla ayrık parçadan oluşabileceği için (örn. iki farklı bina hattı) bileşen
                  sayısı doğru halka sayısını bulmak için gereklidir.
        */

        private static int CountConnectedComponents(Afney.Cad.Mechanical.Engine.Hydraulics.HydraulicNetwork network)
        {
            var adjacency = network.Nodes.ToDictionary(
                n => n,
                _ => new List<Afney.Cad.Mechanical.Engine.Hydraulics.NetworkNode>());
            foreach (var pipe in network.Pipes)
            {
                adjacency[pipe.StartNode].Add(pipe.EndNode);
                adjacency[pipe.EndNode].Add(pipe.StartNode);
            }

            var visited = new HashSet<Afney.Cad.Mechanical.Engine.Hydraulics.NetworkNode>();
            int components = 0;
            foreach (var node in network.Nodes)
            {
                if (visited.Contains(node)) continue;
                components++;
                var queue = new Queue<Afney.Cad.Mechanical.Engine.Hydraulics.NetworkNode>();
                queue.Enqueue(node);
                visited.Add(node);
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    foreach (var next in adjacency[cur])
                    {
                        if (visited.Contains(next)) continue;
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            return components;
        }

        private void OnAuditSystem(object sender, RoutedEventArgs e)
        {
            try
            {
                var guard = new DomainGuardService(_database, _mechanicalKernel.TopologyGraph, _mechanicalKernel.ArchitecturalObstacles);
                var result = guard.ValidateSystem();

                // Tüm borularda önceki violation işaretini temizle
                foreach (var pipe in _database.GetAllEntities().OfType<PipeEntity>())
                    pipe.HasHydraulicViolation = false;
                foreach (var ent in _database.GetAllEntities())
                    ent.IsSelected = false;

                if (result.ProblematicEntityIds.Count > 0)
                {
                    foreach (var id in result.ProblematicEntityIds)
                    {
                        var problemEnt = _database.GetAllEntities().FirstOrDefault(x => x.Id == id);
                        if (problemEnt == null) continue;
                        problemEnt.IsSelected = true;
                        if (problemEnt is PipeEntity pipeErr) pipeErr.HasHydraulicViolation = true;
                    }
                }

                Viewport.InvalidateVisual();

                if (result.IsValid && result.Warnings.Count == 0)
                {
                    MessageBox.Show("Sistem topolojisi doğrulandı.\nHata ve uyarı yok.", "Mühendislik Validasyonu", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (TabOutputs != null) TabOutputs.IsEnabled = true;
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    if (result.Errors.Count > 0)
                    {
                        sb.AppendLine($"■ {result.Errors.Count} HATA (kırmızı vurgulanan borular):");
                        foreach (var err in result.Errors) sb.AppendLine($"  ✗ {err}");
                    }
                    if (result.Warnings.Count > 0)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.AppendLine($"▲ {result.Warnings.Count} UYARI:");
                        foreach (var w in result.Warnings) sb.AppendLine($"  ⚠ {w}");
                    }
                    var icon = result.IsValid ? MessageBoxImage.Warning : MessageBoxImage.Error;
                    var title = result.IsValid ? "Uyarılar" : "Validasyon Hatası";
                    MessageBox.Show(sb.ToString(), title, MessageBoxButton.OK, icon);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Sistem kontrol hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion
    }
}
