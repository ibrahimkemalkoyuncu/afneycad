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
        #region -- MEKANIK CIZIM, BLOK & MIMARI KOMUTLARI --

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
                var guard = new DomainGuardService(_database, _mechanicalKernel.TopologyGraph, _mechanicalKernel.ArchitecturalObstacles);
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

        private async void OnRecalculatePlumbing(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Tesisat hesaplamaları ve otomatik çakışma giderme yapılıyor...";
                MainProgressBar.IsIndeterminate = false;
                MainProgressBar.Minimum = 0;
                MainProgressBar.Maximum = 100;
                MainProgressBar.Value = 0;
                MainProgressBar.Visibility = Visibility.Visible;

                var entities = _database.GetAllEntities().ToList();

                var progress = new Progress<(int Percent, string Stage)>(p =>
                {
                    MainProgressBar.Value = p.Percent;
                    StatusText.Text = $"Tesisat hesaplaması: %{p.Percent} — {p.Stage}";
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _mechanicalKernel.RecalculateProject(entities, progress);
                    _mechanicalKernel.ResolveAllClashes(entities);
                });

                Viewport.InvalidateViewport();
                StatusText.Text = "Tesisat hesaplamaları ve otomatik çakışma giderme tamamlandı.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tesisat hesaplama hatası: {ex.Message}");
                StatusText.Text = "Tesisat hesaplaması başarısız.";
            }
            finally
            {
                MainProgressBar.Visibility = Visibility.Collapsed;
                MainProgressBar.IsIndeterminate = true;
            }
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

            dialog.OnShow3D += async (levels) =>
            {
                try
                {
                    StatusText.Text = "Bina montajı yapılıyor (BIM Alignment)...";
                    MainProgressBar.IsIndeterminate = false;
                    MainProgressBar.Minimum = 0;
                    MainProgressBar.Maximum = 100;
                    MainProgressBar.Value = 0;
                    MainProgressBar.Visibility = Visibility.Visible;

                    var regs = levels.Select(l => new LevelFileRegistration
                    {
                        FilePath = l.FilePath,
                        Elevation = l.Elevation,
                        LevelName = l.LevelName
                    }).ToList();

                    var progress = new Progress<(int Percent, string Stage)>(p =>
                    {
                        MainProgressBar.Value = p.Percent;
                        StatusText.Text = $"Bina montajı: %{p.Percent} — {p.Stage}";
                    });

                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var assemblyService = new BuildingAssemblyService(_database, _mechanicalKernel);
                        assemblyService.AssembleBuilding(regs);
                        _mechanicalKernel.RecalculateProject(_database.GetAllEntities(), progress);
                    });

                    Viewport.SetViewMode(true);
                    Viewport.InvalidateViewport();
                    Viewport.ZoomExtents();

                    StatusText.Text = "3D Bina Modeli ve Tesisat Ağı Oluşturuldu.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"3D Model oluşturulurken hata: {ex.Message}");
                }
                finally
                {
                    MainProgressBar.Visibility = Visibility.Collapsed;
                    MainProgressBar.IsIndeterminate = true;
                }
            };

            dialog.ShowDialog();
        }

        #endregion
    }
}
