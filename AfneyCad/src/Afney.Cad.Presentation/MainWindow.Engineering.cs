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
        #region -- MÜHENDİSLİK KOMUTLARI (ENGINEERING) --

        private async void OnRecalculateSystem(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Hidrolik analiz yapılıyor (TS 1258)...";
                MainProgressBar.Visibility = Visibility.Visible;
                TabCalculation.IsEnabled = false;

                var entities = _database.GetAllEntities().ToList();

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _mechanicalKernel.RecalculateProject(entities);
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
                TabCalculation.IsEnabled = true;
            }
        }

        #endregion

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

        private void OnAutoDetectSpacesCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new AutoDetectSpacesCommand(_database);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () =>
            {
                Viewport.SetActiveCommand(null);
                Viewport.InvalidateViewport();
            };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSelectRoom(object sender, RoutedEventArgs e)
        {
            var cmd = new MahalDefineCommand(_database, (Afney.Cad.Mechanical.Entities.RoomEntity mahal) =>
            {
                try
                {
                    Serilog.Log.Information("MAHAL BULUNDU: {Name}, Alan: {Area:F2}, Fixture: {FixCount}",
                        mahal.RoomName, mahal.Area, mahal.Fixtures.Count);

                    Viewport.ShowHighlight(mahal.BoundaryPoints);

                    var dialog = new RoomTagDialog(mahal);
                    dialog.Owner = this;
                    var dialogResult = dialog.ShowDialog();

                    if (dialogResult == true)
                    {
                        _database.TransactionManager.Submit(new AddEntityOperation(_database, mahal));

                        if (_mechanicalKernel != null)
                            _mechanicalKernel.TopologyGraph.AddRoom(mahal);

                        if (MessageBox.Show("Odaya uygun vitrifiyeler otomatik yerleştirilsin mi? (Lavabo, Klozet, Duş)", "Akıllı Tesisat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                var layoutService = new AutoLayoutService(
                                    _mechanicalKernel.TopologyGraph,
                                    _mechanicalKernel.ArchitecturalObstacles);

                                var fixtures = layoutService.AutoFurnishRoom(mahal);

                                if (fixtures.Count > 0)
                                {
                                    foreach (var fix in fixtures)
                                    {
                                        _database.TransactionManager.Submit(new AddEntityOperation(_database, fix));
                                        _mechanicalKernel.TopologyGraph.AddEntity(fix);
                                    }
                                    StatusText.Text = $"{fixtures.Count} parça vitrifiye yerleştirildi.";
                                    Log.Information("{Count} vitrifiye odaya yerleştirildi: {Room}", fixtures.Count, mahal.RoomName);
                                }
                                else
                                {
                                    MessageBox.Show("Bu odaya sığacak vitrifiye bulunamadı veya yerleşim başarısız.", "Bilgi");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Otomatik yerleşim hatası: {ex.Message}", "Hata");
                                Log.Error(ex, "Otomatik yerleşim hatası");
                            }
                        }

                        StatusText.Text = $"MAHAL TANIMLANDI: {mahal.RoomName} ({mahal.TotalLoadUnits:F2} LU)";
                    }
                    else
                    {
                        StatusText.Text = "Mahal tanımlama iptal edildi.";
                    }

                    Viewport.ClearHighlight();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "KRITIK HATA: Dialog açılırken exception!");
                    MessageBox.Show($"Mahal tanımlama sırasında hata:\n{ex.Message}\n\n{ex.StackTrace}", "HATA", MessageBoxButton.OK, MessageBoxImage.Error);
                    Viewport.ClearHighlight();
                }
            });

            cmd.OnFeedback += (msg) => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);

            Viewport.SetActiveCommand(cmd);
            cmd.Start();
            StatusText.Text = "MAHAL ANALİZİ: Odanın ortasına boş bir noktaya tıklayın...";
        }

        private void OnShowBOMReport(object sender, RoutedEventArgs e)
        {
            try
            {
                var res = MessageBox.Show("Metraj raporunu çizimin içine TABLO olarak eklemek ister misiniz?",
                                        "Mühendislik Raporu", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    var cmd = new InsertBOMTableCommand(_database);
                    Viewport.SetActiveCommand(cmd);
                    cmd.Start();
                    StatusText.Text = "Tablo yerleşimi için bir noktaya tıklayın...";
                }
                else if (res == MessageBoxResult.No)
                {
                    var bomService = new BillOfMaterialsService(_database);
                    var table = bomService.GenerateTable(Vector3D.Zero);
                    MessageBox.Show("Tablo modunu seçerek çizime ekleyebilirsiniz.", "Bilgi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor hatası: {ex.Message}", "Hata");
            }
        }

        private void OnManualMahalDefine(object sender, RoutedEventArgs e)
        {
            if (_database == null)
            {
                MessageBox.Show("Önce bir çizim açın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new ManualMahalCommand(_database, (mahal) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var dialog = new MahalDetailsDialog(mahal);
                        dialog.Owner = this;
                        if (dialog.ShowDialog() == true)
                        {
                            _database.TransactionManager.Submit(new AddEntityOperation(_database, mahal));
                            if (_mechanicalKernel != null)
                                _mechanicalKernel.TopologyGraph.AddRoom(mahal);
                            Viewport.InvalidateVisual();
                            StatusText.Text = $"MAHAL KAYDEDİLDİ: {mahal.MahalName} ({mahal.MahalType}) — {mahal.Area:F2} m²";
                        }
                        else
                        {
                            StatusText.Text = "Manuel mahal tanımlama iptal edildi.";
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "[Manuel Mahal] Dialog açılırken hata");
                        MessageBox.Show($"Mahal dialog hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });

            cmd.OnFeedback  += (msg) => Dispatcher.Invoke(() => StatusText.Text = msg);
            cmd.OnCompleted += ()    => Dispatcher.Invoke(() => Viewport.SetActiveCommand(null));

            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnRectMahalDefine(object sender, RoutedEventArgs e)
        {
            if (_database == null)
            {
                MessageBox.Show("Önce bir çizim açın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new RectMahalCommand((mahal) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var dialog = new MahalDetailsDialog(mahal);
                        dialog.Owner = this;
                        if (dialog.ShowDialog() == true)
                        {
                            _database.TransactionManager.Submit(new AddEntityOperation(_database, mahal));
                            if (_mechanicalKernel != null)
                                _mechanicalKernel.TopologyGraph.AddRoom(mahal);
                            Viewport.InvalidateVisual();
                            StatusText.Text = $"DİKDÖRTGEN MAHAL KAYDEDİLDİ: {mahal.MahalName} ({mahal.MahalType}) — {mahal.Area:F2} m²";
                        }
                        else
                        {
                            StatusText.Text = "Dikdörtgen mahal tanımlama iptal edildi.";
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "[Rect Mahal] Dialog açılırken hata");
                        MessageBox.Show($"Mahal dialog hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });

            cmd.OnFeedback  += (msg) => Dispatcher.Invoke(() => StatusText.Text = msg);
            cmd.OnCompleted += ()    => Dispatcher.Invoke(() => Viewport.SetActiveCommand(null));

            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSmartDetectRoomClick(object sender, RoutedEventArgs e)
        {
            var cmd = new DetectRoomCommand(_database, (room) =>
            {
                var dialog = new RoomTagDialog(room);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    _database.TransactionManager.Submit(new AddEntityOperation(_database, room));
                    _mechanicalKernel.TopologyGraph.AddRoom(room);

                    var roomDefService = new RoomDefinitionService(_database);
                    var foundFixtures = roomDefService.IdentifyFixturesInRoom(room.BoundaryPoints);

                    if (foundFixtures.Any())
                    {
                        double totalFU = foundFixtures.Sum(f => f.LoadUnits);
                        var tagPos = room.GetBoundingBox().Center;
                        string tagText = $"{room.RoomName}\nA: {room.Area:F2} m²\nFU: {totalFU:F2}";
                        var roomTag = new Afney.Cad.Domain.Entities.Basic.TextEntity(tagText, tagPos, 200)
                        {
                            Color = 0xFF00FF00,
                            Layer = "Space_Tags"
                        };
                        _database.TransactionManager.Submit(new AddEntityOperation(_database, roomTag));

                        string msg = $"Mimari planda {foundFixtures.Count} adet vitrifiye (blok) tespit edildi.\n\n" +
                                     $"Toplam Yük Birimi (FU): {totalFU:F2}\n" +
                                     string.Join("\n", foundFixtures.GroupBy(f => f.FixtureType).Select(g => $"- {g.Count()}x {g.Key}")) +
                                     "\n\nBu cihazlar akıllı tesisat nesnelerine (MEP) dönüştürülsün mü?";

                        if (MessageBox.Show(msg, "Mahal Analizi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            var ops = new CompositeOperation("Convert Fixtures");
                            foreach (var fix in foundFixtures)
                            {
                                ops.Add(new AddEntityOperation(_database, fix));
                                _mechanicalKernel.TopologyGraph.AddEntity(fix);
                            }
                            _history.TransactionManager.Submit(ops);
                            StatusText.Text = $"{foundFixtures.Count} cihaz tanıtıldı.";

                            if (MessageBox.Show("Atık su tesisat hatlarını (Kolektör) otomatik oluşturmak ister misiniz?",
                                "Otomatik Tesisat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                var autoPipeService = new AutoBranchingService(_database, _mechanicalKernel);
                                var pipes = autoPipeService.CreateSmartCollector(foundFixtures, room.GetBoundingBox().Center, MechanicalSystemType.WasteWater);

                                if (pipes.Any())
                                {
                                    var pipeOps = new CompositeOperation("Auto Piping");
                                    foreach (var p in pipes)
                                    {
                                        pipeOps.Add(new AddEntityOperation(_database, p));
                                        if (p is MechanicalEntity mechEntity)
                                        {
                                            _mechanicalKernel.TopologyGraph.AddEntity(mechEntity);
                                        }
                                    }
                                    _history.TransactionManager.Submit(pipeOps);
                                    StatusText.Text += " + Tesisat boruları çizildi.";

                                    if (MessageBox.Show("Borular çizildi. Hidrolik hesap yapılıp boru çapları optimize edilsin mi?",
                                        "Mühendislik Analizi", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                    {
                                        OnRecalculateSystem(this, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (MessageBox.Show("Oda boş görünüyor. Standart vitrifiyeler otomatik yerleştirilsin mi?",
                            "Akıllı Tesisat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                var layoutService = new AutoLayoutService(
                                    _mechanicalKernel.TopologyGraph,
                                    _mechanicalKernel.ArchitecturalObstacles);

                                var fixtures = layoutService.AutoFurnishRoom(room);

                                foreach (var fix in fixtures)
                                {
                                    _database.AddEntity(fix);
                                    _mechanicalKernel.TopologyGraph.AddEntity(fix);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Otomatik yerleşim hatası: {ex.Message}", "Hata");
                            }
                        }
                    }

                    Viewport.InvalidateVisual();
                }
            });

            Viewport.SetActiveCommand(cmd);
            cmd.Start();
            StatusText.Text = "Mahal sınırlarını belirlemek için kapalı bir alan bak noktasını tıklayın...";
        }

        private void OnConnectReceptors(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new ConnectReceptorsService(_database, _mechanicalKernel);

                var selected = Viewport.GetSelectedEntities()
                    .OfType<SanitaryFixtureEntity>()
                    .ToList();

                ConnectReceptorsService.ConnectResult result;
                if (selected.Any())
                {
                    result = service.ConnectSelected(selected);
                    StatusText.Text = $"Seçili {selected.Count} armatür için bağlantı işleniyor...";
                }
                else
                {
                    result = service.ConnectAll();
                    StatusText.Text = "Tüm armatürler için bağlantı işleniyor...";
                }

                if (!result.NewEntities.Any() && result.ConnectedCount == 0)
                {
                    string msg = "Bağlanacak armatür bulunamadı veya uygun hat mevcut değil.";
                    if (result.SkipReasons.Any())
                        msg += "\n\nAtlanan durumlar:\n" + string.Join("\n", result.SkipReasons.Take(5));
                    MessageBox.Show(msg, "Connect Receptors", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var ops = new CompositeOperation("Connect Receptors");

                foreach (var ent in result.NewEntities)
                {
                    ops.Add(new AddEntityOperation(_database, ent));
                    if (ent is MechanicalEntity mEnt)
                        _mechanicalKernel.OnEntityAddedToDatabase(mEnt);
                }

                foreach (var old in result.ToRemove)
                {
                    ops.Add(new RemoveEntityOperation(_database, old));
                    _mechanicalKernel.TopologyGraph.RemoveEntity(old.Id);
                }

                _history.TransactionManager.Submit(ops);
                Viewport.InvalidateVisual();

                string summary = $"Connect Receptors tamamlandı:\n\n" +
                                 $"  Bağlanan port sayısı : {result.ConnectedCount}\n" +
                                 $"  Atlanan port sayısı  : {result.SkippedCount}\n" +
                                 $"  Oluşturulan parça    : {result.NewEntities.Count}\n";

                if (result.SkipReasons.Any())
                    summary += $"\nAtlananlar:\n" + string.Join("\n", result.SkipReasons.Take(5));

                MessageBox.Show(summary, "Connect Receptors", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = $"Connect Receptors: {result.ConnectedCount} port bağlandı.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connect Receptors hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnAutoBranchingClick(object sender, RoutedEventArgs e)
        {
            if (Viewport.GetActiveCommand() is PickEntityCommand)
                return;

            StatusText.Text = "BAĞLANACAK CİHAZLARI SEÇİN (Önce Seçim)";

            var selected = Viewport.GetSelectedEntities();
            var fixtures = selected.OfType<SanitaryFixtureEntity>().ToList();

            if (!fixtures.Any())
            {
                MessageBox.Show("Lütfen önce bağlanacak cihazları (Vitrifiye) seçip komuta tıklayın.", "Uyarı");
                return;
            }

            StatusText.Text = "ANA BORUYU SEÇİN (Tıklayın)...";

            var pickCmd = new PickEntityCommand(_database);
            pickCmd.OnEntityPicked += (ent) =>
            {
                if (ent is PipeEntity mainPipe)
                {
                    try
                    {
                        var service = new AutoBranchingService(_database, _mechanicalKernel);
                        var newEntities = service.ConnectFixturesToPipe(fixtures, mainPipe);

                        if (newEntities.Any())
                        {
                            var ops = new CompositeOperation("Auto Branching");

                            foreach (var item in newEntities)
                            {
                                ops.Add(new AddEntityOperation(_database, item));
                                if (item is MechanicalEntity mechItem)
                                    _mechanicalKernel.TopologyGraph.AddEntity(mechItem);
                            }

                            ops.Add(new RemoveEntityOperation(_database, mainPipe));
                            _mechanicalKernel.TopologyGraph.RemoveEntity(mainPipe.Id);

                            _history.TransactionManager.Submit(ops);

                            StatusText.Text = $"{newEntities.Count} parça (boru/fittings) ile bağlantı sağlandı.";
                            Viewport.InvalidateVisual();
                        }
                        else
                        {
                            MessageBox.Show("Uygun bağlantı noktası veya sistem tipi eşleşmesi bulunamadı.", "Bilgi");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Otomatik bağlantı hatası: {ex.Message}");
                    }
                    finally
                    {
                        Viewport.SetActiveCommand(null);
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen bir BORU seçin.");
                    Viewport.SetActiveCommand(null);
                }
            };

            Viewport.SetActiveCommand(pickCmd);
            pickCmd.Start();
        }

        private void OnRiserAutoPosition(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new RiserAutoPositionService(_database);
                var suggestions = service.SuggestRiserPositions();

                if (suggestions.Count == 0)
                {
                    MessageBox.Show("Kolon konumlandırması için çizimde vitrifiye bulunamadı.\nÖnce vitrifiye tanımlayın.", "Uyarı");
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("OTOMATİK KOLON KONUMLANDIRMA ÖNERİLERİ (TS 1258)");
                sb.AppendLine(new string('─', 52));
                foreach (var s in suggestions)
                {
                    string nearby = s.HasNearbyRiser ? " ⚠ Yakında mevcut riser var" : "";
                    sb.AppendLine($"● {s.Label,-14} → X:{s.Position.X / 1000:F2} m  Y:{s.Position.Y / 1000:F2} m" +
                                  $"  ({s.FixtureCount} cihaz · ∑LU={s.WeightedLU:F1}){nearby}");
                }
                sb.AppendLine();
                sb.AppendLine("Bu konumları baz alarak 'Kolon Oluştur' komutunu kullanın.");

                MessageBox.Show(sb.ToString(), "Kolon Konumlandırma Önerisi",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Son öneriyi status bar'a yaz
                var first = suggestions.First();
                StatusText.Text = $"Kolon önerisi: {first.Label} → ({first.Position.X / 1000:F2}, {first.Position.Y / 1000:F2}) m";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kolon konumlandırma hatası: {ex.Message}", "Hata");
            }
        }

        private void OnRiserConnection(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "KOLONA BAĞLANACAK YATAY BORUYU SEÇİN...";

            var pickCmd1 = new PickEntityCommand(_database);
            pickCmd1.OnEntityPicked += (horizontalEnt) =>
            {
                if (horizontalEnt is PipeEntity horizontalPipe)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StatusText.Text = "ŞİMDİ DİKEY KOLONU (RISER) SEÇİN...";
                        var pickCmd2 = new PickEntityCommand(_database);

                        pickCmd2.OnEntityPicked += (riserEnt) =>
                        {
                            if (riserEnt is PipeEntity riserPipe)
                            {
                                try
                                {
                                    var service = new AutoBranchingService(_database);
                                    var result = service.ConnectToRiser(horizontalPipe, riserPipe);

                                    if (result.NewEntities.Any())
                                    {
                                        var ops = new CompositeOperation("Riser Connection");

                                        foreach (var item in result.NewEntities)
                                        {
                                            ops.Add(new AddEntityOperation(_database, item));
                                            if (item is MechanicalEntity mechItem)
                                                _mechanicalKernel.TopologyGraph.AddEntity(mechItem);
                                        }

                                        foreach (var oldItem in result.RemovedEntities)
                                        {
                                            ops.Add(new RemoveEntityOperation(_database, oldItem));
                                            _mechanicalKernel.TopologyGraph.RemoveEntity(oldItem.Id);
                                        }

                                        _history.TransactionManager.Submit(ops);
                                        StatusText.Text = "Kolon bağlantısı başarıyla yapıldı.";
                                        Viewport.InvalidateVisual();
                                    }
                                    else
                                    {
                                        StatusText.Text = "Uyarı: Bağlantı yapılamadı (Sistem tipi veya mesafe sorunu).";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Hata: {ex.Message}");
                                }
                                finally
                                {
                                    Viewport.SetActiveCommand(null);
                                }
                            }
                        };

                        Viewport.SetActiveCommand(pickCmd2);
                        pickCmd2.Start();
                    }));
                }
            };

            Viewport.SetActiveCommand(pickCmd1);
            pickCmd1.Start();
        }

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

        private void OnGenerateBOM(object sender, RoutedEventArgs e)
        {
            try
            {
                var bomService = new BillOfMaterialsService(_database);
                var tablePos = Viewport.GetCameraCenter();
                var table = bomService.GenerateTable(tablePos);

                _history.TransactionManager.Submit(new AddEntityOperation(_database, table));

                StatusText.Text = "Metraj ve Malzeme Listesi (BOM) ekranın ortasına tablo olarak eklendi.";
                Viewport.InvalidateVisual();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BOM hatası: {ex.Message}");
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

        private void OnGenerateHydraulicReport(object sender, RoutedEventArgs e)
        {
            try
            {
                var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
                if (!pipes.Any())
                {
                    MessageBox.Show("Raporlanacak boru bulunamadı. Lütfen önce tesisatı çizin.", "Uyarı");
                    return;
                }

                // Önce tam hidrolik hesap çalıştır (FlowCalc + AutoSize)
                var allEntities = _database.GetAllEntities().ToList();
                _mechanicalKernel.RecalculateProject(allEntities);

                var pressureService = new PressureDropService(
                    _mechanicalKernel.TopologyGraph, _mechanicalKernel.ProjectSettings, _database);

                // Aktif dosya adını proje adı olarak kullan
                string? fp = _activeContext?.FilePath;
                string projectName = !string.IsNullOrEmpty(fp)
                    ? System.IO.Path.GetFileNameWithoutExtension(fp)
                    : "AfneyCAD Projesi";

                var reportService = new HydraulicReportService(pressureService);
                // Hesaplanmış borular (FlowRate > 0 olanlar önce, sonra diğerleri)
                var orderedPipes = pipes.OrderBy(p => p.SystemType)
                                        .ThenByDescending(p => p.FlowRate)
                                        .ToList();
                var catchments = _database.GetAllEntities().OfType<RainfallCatchmentEntity>().ToList();
                string htmlContent = reportService.GenerateHtmlReport(orderedPipes, projectName, catchments);

                string tempPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"HydraulicReport_{Guid.NewGuid():N}.html");
                System.IO.File.WriteAllText(tempPath, htmlContent, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath, UseShellExecute = true
                });

                Viewport.InvalidateVisual();
                StatusText.Text = $"Hidrolik rapor: {orderedPipes.Count} boru analiz edildi, tarayıcıda açıldı.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hidrolik Rapor Hatası: {ex.Message}", "Hata");
            }
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

        private void OnAnalyzeSpecClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var specService = new AutoSpecService(_database.GetAllEntities());
                var bomItems = specService.GenerateBoMReport();
                var specText = specService.GenerateSpecificationText();

                string report = "KEŞİF ÖZETİ (PROJE METRAJI)\n---------------------------------\n";
                foreach (var item in bomItems)
                    report += $"[{item.Code}] {item.Description}: {item.Quantity:F2} {item.Unit}\n";

                report += "\n\nTEKNİK ŞARTNAME TASLAĞI (ÖN İZLEME)\n---------------------------------\n";
                report += specText.Substring(0, Math.Min(300, specText.Length)) + "...";

                MessageBox.Show(report, "AfneyCAD - Mühendislik Raporu (Auto-Spec)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor üretilirken hata oluştu: {ex.Message}");
            }
        }

        private void OnShowIsometricScheme(object sender, RoutedEventArgs e)
        {
            try
            {
                var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
                var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();

                if (!pipes.Any() && !fixtures.Any())
                {
                    MessageBox.Show("İzometrik şema için çizimde boru veya vitrifiye bulunamadı.", "Uyarı");
                    return;
                }

                // IsoSyncService ile 30-30 izometrik dönüşüm
                var isoEntities = _mechanicalKernel.IsoSync.GenerateIsometricScheme();

                // SVG + HTML izometrik şema üret ve tarayıcıda aç
                string html = GenerateIsometricHtml(pipes, fixtures, _mechanicalKernel.IsoSync);
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"IsometricScheme_{Guid.NewGuid():N}.html");
                System.IO.File.WriteAllText(tempPath, html, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = tempPath, UseShellExecute = true });

                StatusText.Text = $"İzometrik şema: {pipes.Count} boru · {fixtures.Count} vitrifiye → tarayıcıda açıldı.";
            }
            catch (Exception ex) { MessageBox.Show($"İzometrik şema hatası: {ex.Message}"); }
        }

        // ── Kolon Şeması (Riser Diagram) ─────────────────────────────────────────
        private string GenerateIsometricHtml(
            IEnumerable<PipeEntity> pipes,
            IEnumerable<SanitaryFixtureEntity> fixtures,
            Afney.Cad.Mechanical.Services.IsoSyncService _)
        {
            var pipeList = pipes.ToList();
            var fixList  = fixtures.ToList();
            if (!pipeList.Any() && !fixList.Any()) return "<html><body>Veri yok</body></html>";

            // ── Sistem renk ve label tablosu ──────────────────────────────────
            var systemMeta = new Dictionary<Afney.Cad.Mechanical.Enums.MechanicalSystemType, (string Color, string Label, string Short)>
            {
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticColdWater] = ("#2196F3", "Soğuk Su",    "SK"),
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticHotWater]  = ("#F44336", "Sıcak Su",   "SH"),
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.WasteWater]        = ("#795548", "Pis Su",      "PS"),
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.RainWater]         = ("#00BCD4", "Yağmur",      "YS"),
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.FireProtection]    = ("#FF9800", "Yangın",      "YG"),
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.Gas]               = ("#FFEB3B", "Gaz",         "GZ"),
                [Afney.Cad.Mechanical.Enums.MechanicalSystemType.Ventilation]       = ("#9C27B0", "Havalandırma","HV"),
            };

            var activeSystems = pipeList.Select(p => p.SystemType).Distinct()
                .OrderBy(s => (int)s).ToList();
            if (!activeSystems.Any()) activeSystems.Add(Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticColdWater);

            // ── Kat seviyeleri (Z kümeleme, 500 mm tolerans) ──────────────────
            var allZ = pipeList.SelectMany(p => new[] { p.StartPoint.Z, p.EndPoint.Z })
                               .Concat(fixList.Select(f => f.Position.Z))
                               .OrderBy(z => z).ToList();
            var floorZs = new List<double>();
            foreach (double z in allZ)
            {
                if (!floorZs.Any() || z - floorZs.Last() > 500)
                    floorZs.Add(z);
            }
            if (floorZs.Count < 2) { floorZs.Clear(); floorZs.Add(0); floorZs.Add(3000); }

            double zMin = floorZs.First(), zMax = floorZs.Last();
            double zRange = Math.Max(zMax - zMin, 3000);

            // ── SVG boyutları ─────────────────────────────────────────────────
            const int svgW = 1100;
            const int svgH = 720;
            const int marginLeft = 80;   // kat etiketi alanı
            const int marginBottom = 60; // legend alanı
            const int marginTop = 40;
            int drawH = svgH - marginTop - marginBottom;
            int drawW = svgW - marginLeft - 20;
            int colW   = Math.Max(60, drawW / Math.Max(activeSystems.Count, 1));

            double zToY(double z) => marginTop + drawH - (z - zMin) / zRange * drawH;
            double colX(int ci)   => marginLeft + ci * colW + colW / 2.0;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'>");
            sb.AppendLine("<title>AfneyCAD — Kolon Şeması</title>");
            sb.AppendLine($"<style>body{{background:#131320;margin:0;padding:12px;font-family:'Segoe UI',sans-serif;}}");
            sb.AppendLine("h2{color:#7FC3FF;margin:6px 0 2px;font-size:16px;}");
            sb.AppendLine(".info{color:#777;font-size:11px;margin:0 0 8px;}");
            sb.AppendLine("svg{background:#0e0e1c;border:1px solid #2a2a3c;display:block;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h2>Tesisat Kolon Şeması</h2>");
            sb.AppendLine($"<p class='info'>{pipeList.Count} boru · {fixList.Count} armatür · " +
                          $"{activeSystems.Count} sistem · TS 1258 / AfneyCAD v4.0</p>");
            sb.AppendLine($"<svg width='{svgW}' height='{svgH}' xmlns='http://www.w3.org/2000/svg'>");

            // ── Kat çizgileri ─────────────────────────────────────────────────
            for (int fi = 0; fi < floorZs.Count; fi++)
            {
                double y = zToY(floorZs[fi]);
                string floorLabel = fi == 0 ? "Zemin" : $"{fi}. Kat";
                sb.AppendLine($"<line x1='{marginLeft}' y1='{y:F0}' x2='{svgW - 10}' y2='{y:F0}' " +
                              "stroke='#2a3a4a' stroke-width='1' stroke-dasharray='6,4'/>");
                sb.AppendLine($"<text x='{marginLeft - 6}' y='{y + 4:F0}' fill='#557799' font-size='10' " +
                              $"text-anchor='end'>{floorLabel}</text>");
                // Z seviyesi (mm)
                sb.AppendLine($"<text x='4' y='{y + 4:F0}' fill='#334455' font-size='9'>{floorZs[fi]:F0}</text>");
            }

            // ── Her sistem için kolon ─────────────────────────────────────────
            for (int ci = 0; ci < activeSystems.Count; ci++)
            {
                var sys = activeSystems[ci];
                if (!systemMeta.TryGetValue(sys, out var meta))
                    meta = ("#AAAAAA", sys.ToString(), sys.ToString()[..2].ToUpper());

                double cx = colX(ci);
                var sysPipes = pipeList.Where(p => p.SystemType == sys).ToList();

                // Sistem başlığı
                double headerY = marginTop - 14;
                sb.AppendLine($"<text x='{cx:F0}' y='{headerY:F0}' fill='{meta.Color}' " +
                              $"font-size='11' font-weight='bold' text-anchor='middle'>{meta.Short}</text>");
                sb.AppendLine($"<text x='{cx:F0}' y='{headerY + 12:F0}' fill='{meta.Color}' " +
                              $"font-size='8' text-anchor='middle' opacity='0.7'>{meta.Label}</text>");

                // Ana kolon (riser) — tüm Z aralığında kalın dikey çizgi
                if (sysPipes.Any())
                {
                    double riserZ1 = sysPipes.Min(p => Math.Min(p.StartPoint.Z, p.EndPoint.Z));
                    double riserZ2 = sysPipes.Max(p => Math.Max(p.StartPoint.Z, p.EndPoint.Z));
                    double ry1 = zToY(riserZ2), ry2 = zToY(riserZ1);
                    sb.AppendLine($"<line x1='{cx:F0}' y1='{ry1:F0}' x2='{cx:F0}' y2='{ry2:F0}' " +
                                  $"stroke='{meta.Color}' stroke-width='3' stroke-linecap='round' opacity='0.9'/>");
                }

                // Her boru — dal olarak çiz
                foreach (var pipe in sysPipes)
                {
                    double z1 = pipe.StartPoint.Z, z2 = pipe.EndPoint.Z;
                    double dz = Math.Abs(z2 - z1);
                    double dxy = Math.Sqrt(Math.Pow(pipe.EndPoint.X - pipe.StartPoint.X, 2) +
                                           Math.Pow(pipe.EndPoint.Y - pipe.StartPoint.Y, 2));

                    double strokeW = Math.Max(1.5, Math.Min(pipe.InnerDiameter / 25.0, 6.0));
                    string label = $"DN{pipe.InnerDiameter:F0}";

                    if (dz > 200 && dz >= dxy * 0.5)
                    {
                        // Dikey (riser) segment — kolon üzerinde ince kaplama
                        double py1 = zToY(Math.Max(z1, z2)), py2 = zToY(Math.Min(z1, z2));
                        sb.AppendLine($"<line x1='{cx:F0}' y1='{py1:F0}' x2='{cx:F0}' y2='{py2:F0}' " +
                                      $"stroke='{meta.Color}' stroke-width='{strokeW:F1}' opacity='0.8'/>");
                    }
                    else
                    {
                        // Yatay (dal) — Z ortasında yatay kol
                        double branchZ = (z1 + z2) / 2.0;
                        double by = zToY(branchZ);
                        double branchLen = Math.Max(dxy / 1000.0 * 0.3 * colW, 18); // mm→pixel, en az 18px
                        double bx1 = cx, bx2 = cx + branchLen;
                        sb.AppendLine($"<line x1='{bx1:F0}' y1='{by:F0}' x2='{bx2:F0}' y2='{by:F0}' " +
                                      $"stroke='{meta.Color}' stroke-width='{strokeW:F1}' stroke-linecap='round'/>");
                        // DN etiketi
                        sb.AppendLine($"<text x='{(bx1 + bx2) / 2:F0}' y='{by - 3:F0}' fill='{meta.Color}' " +
                                      $"font-size='8' text-anchor='middle' opacity='0.85'>{label}</text>");

                        // Bitiş noktası ok/sembol
                        sb.AppendLine($"<polygon points='{bx2:F0},{by:F0} {bx2 - 5:F0},{by - 4:F0} {bx2 - 5:F0},{by + 4:F0}' " +
                                      $"fill='{meta.Color}' opacity='0.7'/>");
                    }
                }

                // Toplam uzunluk bilgisi
                if (sysPipes.Any())
                {
                    double totalM = sysPipes.Sum(p => p.GetLength()) / 1000.0;
                    double footerY = svgH - marginBottom + 14;
                    sb.AppendLine($"<text x='{cx:F0}' y='{footerY:F0}' fill='{meta.Color}' " +
                                  $"font-size='9' text-anchor='middle'>{totalM:F1} m</text>");
                }
            }

            // ── Armatürler ────────────────────────────────────────────────────
            foreach (var fix in fixList)
            {
                double fz = fix.Position.Z;
                double fy = zToY(fz);
                string fcol = $"#{fix.Color & 0xFFFFFF:X6}";

                // En yakın sisteme yasla
                int nearestCol = 0;
                if (pipeList.Any())
                {
                    nearestCol = activeSystems
                        .Select((s, i) => (i, dist: pipeList.Where(p => p.SystemType == s)
                            .DefaultIfEmpty()
                            .Min(p => p is null ? double.MaxValue :
                                Math.Sqrt(Math.Pow(p.StartPoint.X - fix.Position.X, 2) +
                                          Math.Pow(p.StartPoint.Y - fix.Position.Y, 2)))))
                        .OrderBy(t => t.dist).First().i;
                }
                double fx = colX(nearestCol) + 22;

                // Armatür sembolü (dikdörtgen)
                sb.AppendLine($"<rect x='{fx - 7:F0}' y='{fy - 5:F0}' width='14' height='10' " +
                              $"fill='{fcol}' rx='2' opacity='0.85'/>");
                string shortName = fix.FixtureType.Length > 4 ? fix.FixtureType[..4] : fix.FixtureType;
                sb.AppendLine($"<text x='{fx:F0}' y='{fy - 7:F0}' fill='#CCC' font-size='7' text-anchor='middle'>{shortName}</text>");
                // Armatürden kolona bağlantı çizgisi
                sb.AppendLine($"<line x1='{colX(nearestCol):F0}' y1='{fy:F0}' x2='{fx - 7:F0}' y2='{fy:F0}' " +
                              "stroke='#445566' stroke-width='0.8' stroke-dasharray='3,2'/>");
            }

            // ── Çerçeve ───────────────────────────────────────────────────────
            sb.AppendLine($"<rect x='{marginLeft}' y='{marginTop}' width='{svgW - marginLeft - 20}' height='{drawH}' " +
                          "fill='none' stroke='#1e2e3e' stroke-width='1'/>");

            // ── Legend ────────────────────────────────────────────────────────
            int lx = marginLeft, ly = svgH - marginBottom + 28;
            foreach (var (sys, idx) in activeSystems.Select((s, i) => (s, i)))
            {
                if (!systemMeta.TryGetValue(sys, out var m)) continue;
                int lxi = lx + idx * 130;
                sb.AppendLine($"<rect x='{lxi}' y='{ly - 8}' width='12' height='8' fill='{m.Color}' rx='1'/>");
                sb.AppendLine($"<text x='{lxi + 15}' y='{ly}' fill='#AABBCC' font-size='10'>{m.Label}</text>");
            }

            sb.AppendLine("</svg>");
            sb.AppendLine($"<p class='info' style='margin-top:6px'>AfneyCAD v4.0 · Kolon Şeması · {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private void OnAutoAnnotate(object sender, RoutedEventArgs e)
        {
            try
            {
                var annotationService = new AutoAnnotationService(_database);
                var annotations = annotationService.AnnotateAllPipes();
                foreach (var ann in annotations) _database.AddEntity(ann);
                Viewport.InvalidateVisual();
                MessageBox.Show($"{annotations.Count} adet etiket başarıyla yerleştirildi.", "Otomatik Etiketleme", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Etiketleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnClearAnnotations(object sender, RoutedEventArgs e)
        {
            try
            {
                var annotationService = new AutoAnnotationService(_database);
                int count = annotationService.ClearAnnotations();
                Viewport.InvalidateVisual();
                MessageBox.Show($"{count} adet etiket silindi.", "Etiketler Temizlendi");
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); }
        }

        private void OnCalculationTable(object sender, RoutedEventArgs e)
        {
            try
            {
                var pressureService = new PressureDropService(_mechanicalKernel.TopologyGraph, _mechanicalKernel.ProjectSettings, _database);
                var dialog = new CalculationTableWindow(_database, pressureService);
                dialog.Owner = this;

                var syncService = new DrawingSyncService(_database);
                dialog.PipeDN_Changed += (pipeId, newDN) =>
                {
                    try
                    {
                        syncService.SyncPipeLabel(pipeId, newDN);
                        _activeContext?.Viewport.InvalidateViewport();
                        StatusText.Text = $"Çizim güncellendi: {pipeId} — DN{newDN:F0}";
                    }
                    catch (Exception labelEx)
                    {
                        Serilog.Log.Warning(labelEx, "DrawingSyncService DN güncellemesi sırasında hata.");
                    }
                };

                dialog.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show($"Hesaplama tablosu hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnPipeWizard(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new PipeWizardDialog(_database) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    Viewport.InvalidateVisual();
                    MessageBox.Show("Tesisat şablonu başarıyla yerleştirildi.", "Boru Sihirbazı");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Boru Sihirbazı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnFixtureLibrary(object sender, RoutedEventArgs e)
        {
            try { new FixtureLibraryDialog(_database) { Owner = this }.ShowDialog(); Viewport.InvalidateViewport(); }
            catch (Exception ex) { MessageBox.Show($"Reseptör kütüphanesi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnValveLibrary(object sender, RoutedEventArgs e)
        {
            try { new ValveLibraryDialog(_database) { Owner = this }.ShowDialog(); Viewport.InvalidateViewport(); }
            catch (Exception ex) { MessageBox.Show($"Vana kütüphanesi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnManageCatalog(object sender, RoutedEventArgs e)
        {
            try { new UserFixtureCatalogDialog(new FixtureLibraryService()) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Katalog yönetimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnImportPozCsv(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Birim Fiyat CSV Seç",
                Filter = "CSV Dosyası|*.csv|Tüm Dosyalar|*.*",
                DefaultExt = ".csv"
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var svc = new Afney.Cad.Mechanical.Services.PozKatalogService();
                var (imported, skipped, error) = svc.LoadFromCsv(dlg.FileName);

                if (error is not null)
                {
                    MessageBox.Show($"CSV import hatası:\n{error}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Proje klasörüne override JSON olarak kaydet — tüm servisler bu JSON'u kullanır
                string projDir = System.IO.Path.GetDirectoryName(
                    _activeContext?.FilePath ?? System.IO.Path.GetTempPath()) ?? System.IO.Path.GetTempPath();
                string jsonOut = System.IO.Path.Combine(projDir, "poz_katalog_override.json");
                svc.SaveToJson(jsonOut);

                StatusText.Text = $"✅ Poz CSV içe aktarıldı: {imported} kayıt, {skipped} atlandı → {System.IO.Path.GetFileName(jsonOut)}";
                MessageBox.Show(
                    $"CSV başarıyla içe aktarıldı.\n\n" +
                    $"İçe aktarılan: {imported} kalem\n" +
                    $"Atlanan (hatalı satır): {skipped}\n\n" +
                    $"Override JSON: {jsonOut}\n\n" +
                    "WasteWaterCalcSheetDialog ve BillOfMaterialsService bu fiyatları kullanacak.",
                    "Poz CSV İçe Aktarma", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İçe aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnWasteWaterDesign(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new WasteWaterDesignDialog(_database) { Owner = this };

                // Boşaltma noktası yerleştirme (rögar veya yağmur)
                dlg.PlaceOutletRequested += isRain =>
                {
                    var tm = new TransactionManager();
                    var cmd = new PlaceDrainageOutletCommand(_database, tm, isRain);
                    cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); dlg.Show(); };
                    Viewport.SetActiveCommand(cmd);
                };

                // Bölünmüş kolon yaratma — kullanıcı XY'yi tıklar, iki PipeEntity oluşturulur
                dlg.CreateSplitColumnRequested += (lBot, lTop, uBot, uTop) =>
                {
                    var sys = MechanicalSystemType.WasteWater;
                    var tm0 = new TransactionManager();
                    // Tek tıklamayla XY alan geçici komut
                    var pickCmd = new PickPointCommand();
                    pickCmd.OnPointPicked += xy =>
                    {
                        Viewport.SetActiveCommand(null);
                        double x = xy.X, y = xy.Y;
                        // Alt kolon
                        var lower = new PipeEntity(
                            new Vector3D(x, y, lBot * 1000),
                            new Vector3D(x, y, lTop * 1000), 100)
                        { SystemType = sys, Layer = "MEK_PIS_SU" };
                        lower.ApplySystemColor();
                        // Üst kolon
                        var upper = new PipeEntity(
                            new Vector3D(x, y, uBot * 1000),
                            new Vector3D(x, y, uTop * 1000), 100)
                        { SystemType = sys, Layer = "MEK_PIS_SU" };
                        upper.ApplySystemColor();
                        tm0.Submit(new AddEntityOperation(_database, lower));
                        tm0.Submit(new AddEntityOperation(_database, upper));
                        Viewport.InvalidateViewport();
                        dlg.Show();
                        dlg.AppendValidationMessage($"  ✓ Bölünmüş kolon oluşturuldu: Alt {lBot}→{lTop} m, Üst {uBot}→{uTop} m", false);
                    };
                    Viewport.SetActiveCommand(pickCmd);
                    pickCmd.Start();
                };

                // Yağmur düşme alanı çizimi
                dlg.DrawCatchmentAreaRequested += () =>
                {
                    var tm = new TransactionManager();
                    var cmd = new DrawCatchmentAreaCommand(_database, tm);
                    cmd.SurfaceTypeRequested += (entity, callback) =>
                    {
                        var surfDlg = new CatchmentSurfaceDialog(entity.AreaM2) { Owner = this };
                        if (surfDlg.ShowDialog() == true && surfDlg.ChosenSurface.HasValue)
                        {
                            entity.AreaName = surfDlg.AreaName;
                            callback(surfDlg.ChosenSurface.Value);
                        }
                        else
                        {
                            callback(null); // iptal
                        }
                        dlg.Show();
                    };
                    cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
                    dlg.Hide();
                    Viewport.SetActiveCommand(cmd);
                    cmd.Start();
                };

                // Kat kopyalama — kolon hariç (ExcludeRisers)
                dlg.FilterAndCopyRequested += () =>
                {
                    var selection = Viewport.GetSelectedEntities().ToList();
                    if (selection.Count == 0) { MessageBox.Show("Önce kopyalanacak tesisat entity'lerini seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    const double zOffset = 3000;
                    int copied = 0;
                    var tm2 = new TransactionManager();
                    foreach (var entity in selection)
                    {
                        if (entity is PipeEntity pipe)
                        {
                            var dir = (pipe.EndPoint - pipe.StartPoint).Normalize();
                            if ((double)Math.Abs(dir.Z) > 0.8) continue; // kolon boru → atla
                        }
                        var clone = entity.Clone();
                        clone.Id = Guid.NewGuid();
                        clone.Transform(Matrix4x4.TranslationMatrix(0, 0, zOffset));
                        tm2.Submit(new AddEntityOperation(_database, clone));
                        copied++;
                    }
                    Viewport.InvalidateViewport();
                    MessageBox.Show($"{copied} entity kopyalandı (kolonlar hariç).", "Kopyalama Tamam", MessageBoxButton.OK, MessageBoxImage.Information);
                };

                // Seçimdeki kolon boru sayısını say (ValidateCopySelection)
                dlg.ValidateCopySelectionRequested += () =>
                {
                    var selection = Viewport.GetSelectedEntities().ToList();
                    int riserCount = selection.OfType<PipeEntity>()
                        .Count(p => { var d = (p.EndPoint - p.StartPoint).Normalize(); return (double)Math.Abs(d.Z) > 0.8; });
                    dlg.SetCopyValidationResult(new CopyValidationResult { IsValid = riserCount == 0, RiserPipeCount = riserCount });
                };

                // Tesisatı Kabul Et — DomainGuardService ile tam validasyon (hata + uyarı)
                dlg.AcceptSystemRequested += () =>
                {
                    var guard = new DomainGuardService(_database, _mechanicalKernel.TopologyGraph);
                    var vr = guard.ValidateSystem();

                    // Hataları yaz
                    foreach (string err in vr.Errors)
                        dlg.AppendValidationMessage($"  ✗ {err}", true);

                    // Uyarıları yaz (eğim yetersizliği, topoloji vb.)
                    foreach (string w in vr.Warnings)
                        dlg.AppendValidationMessage($"  ⚠ {w}", true);

                    // Hatalı borulara kırmızı flag
                    foreach (var pipe in _database.GetAllEntities().OfType<PipeEntity>())
                        pipe.HasHydraulicViolation = false;
                    foreach (var id in vr.ProblematicEntityIds)
                    {
                        if (_database.GetAllEntities().FirstOrDefault(x => x.Id == id) is PipeEntity p)
                            p.HasHydraulicViolation = true;
                    }

                    if (vr.IsValid && vr.Warnings.Count == 0)
                        dlg.AppendValidationMessage("  ✓ DomainGuard: Tüm kontroller geçti.", false);

                    Viewport.InvalidateViewport();
                };

                dlg.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show($"Pis su hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnWasteWaterCalcSheet(object sender, RoutedEventArgs e)
        {
            try
            {
                // Aktif proje kaydedilmişse klasörünü çıkar — override poz kataloğu için.
                string? projectDir = null;
                string? filePath = _activeContext?.FilePath;
                if (!string.IsNullOrWhiteSpace(filePath))
                    projectDir = System.IO.Path.GetDirectoryName(filePath);

                new WasteWaterCalcSheetDialog(_database, projectDir) { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show($"Hesap Föyü hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnEntityDoubleClicked(Afney.Cad.Domain.Abstractions.CadEntity entity)
        {
            try
            {
                var dialog = new EntityPropertiesDialog(_database, entity) { Owner = this };
                dialog.EntityChanged += (_, _) => Viewport.InvalidateViewport();
                dialog.ShowDialog();
                Viewport.InvalidateViewport();
            }
            catch (Exception ex) { MessageBox.Show($"Özellik paneli hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnRainWaterCalc(object sender, RoutedEventArgs e)
        {
            try { new RainWaterCalcDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Yağmur Suyu Hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnHeatingDesign(object sender, RoutedEventArgs e)
        {
            try { new HeatingDesignDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnHvacDesign(object sender, RoutedEventArgs e)
        {
            try { new HvacDesignDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnAdvancedTools(object sender, RoutedEventArgs e)
        {
            try { new AdvancedToolsDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnGasCalc(object sender, RoutedEventArgs e)
        {
            try { new GasCalcDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Doğalgaz Hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnPipe3DView(object sender, RoutedEventArgs e)
        {
            try { new Pipe3DViewWindow(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"3D görünüm hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnMultiStoryManager(object sender, RoutedEventArgs e)
        {
            try { new MultiStoryManagerDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Çok katlı bina hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnStandardSelection(object sender, RoutedEventArgs e)
        {
            try { new StandardSelectionDialog(_mechanicalKernel) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Standart seçimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
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

        private void OnSepticTankDesign(object sender, RoutedEventArgs e)
        {
            try { new SepticTankDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Fosseptik hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnFireFightingDesign(object sender, RoutedEventArgs e)
        {
            try { new FireFightingDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Yangın söndürme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnReportExport(object sender, RoutedEventArgs e)
        {
            try { new ReportExportDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Rapor hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnSpecificationExport(object sender, RoutedEventArgs e)
        {
            try { new SpecificationExportDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Şartname hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
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

        private void OnPrintViewport(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new Services.PrintViewportService();
                var options = new Services.PrintViewportService.PrintOptions
                {
                    ProjectName  = "AfneyCAD Projesi",
                    DrawingTitle = "Tesisat Planı",
                    DrawnBy      = ""
                };

                if (!svc.PrintViewport(Viewport, options))
                    StatusText.Text = "Yazdırma iptal edildi.";
                else
                    StatusText.Text = "Çizim yazıcıya gönderildi.";
            }
            catch (Exception ex) { MessageBox.Show($"Yazdırma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnFlowAnimationToggle(object sender, RoutedEventArgs e)
        {
            if (_flowAnimService.IsRunning)
            {
                _flowAnimService.Stop();
                Viewport.OverlayRenderer = null;
                if (BtnFlowAnim != null) BtnFlowAnim.Background = System.Windows.Media.Brushes.SteelBlue;
                StatusText.Text = "Akış animasyonu durduruldu.";
            }
            else
            {
                Viewport.OverlayRenderer = (canvas, w, h) =>
                    _flowAnimService.DrawOverlay(canvas, w, h,
                        (wx, wy) => { var p = Viewport.WorldToScreen(new Vector3D(wx, wy, 0)); return ((float)p.X, (float)p.Y); },
                        _activeContext?.Viewport != null ? 100.0 : 100.0);

                _flowAnimService.Start(_database, () => Dispatcher.InvokeAsync(() => Viewport.InvalidateViewport()));
                if (BtnFlowAnim != null) BtnFlowAnim.Background = System.Windows.Media.Brushes.DarkGreen;
                StatusText.Text = "Akış animasyonu aktif — borulardaki akış yönü ve hız görselleştiriliyor.";
            }
        }

        private void OnCloudBackup(object sender, RoutedEventArgs e)
        {
            string projName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD_Proje";
            string sourceFile = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AfneyCAD", "AutoSave", "autosave.afney.bak");

            var dialog = new CloudBackupDialog(_cloudBackupService, projName, sourceFile) { Owner = this };
            dialog.ShowDialog();
        }

        private void OnCoolingDesign(object sender, RoutedEventArgs e)
        {
            try { new CoolingDesignDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnNewProjectWizard(object sender, RoutedEventArgs e)
        {
            try { new NewProjectWizardDialog() { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnRevisionTracking(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new RevisionTrackingService();
                svc.TitleBlock.ProjectName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD Projesi";
                new RevisionTrackingDialog(svc) { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
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

        private void OnPrintLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                var tb = Services.PrintLayoutService.CreateFromProject(
                    _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD",
                    "Tesisat Paftası", drawingNo: "M-001");
                string path = Services.PrintLayoutService.ExportToFile(tb);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
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

        private void OnViewportCapture(object sender, RoutedEventArgs e)
        {
            try { new ViewportCaptureDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Ekran Çizimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnXrefManager(object sender, RoutedEventArgs e)
        {
            try { new XrefManagerDialog(_database) { Owner = this }.ShowDialog(); Viewport.InvalidateVisual(); }
            catch (Exception ex) { MessageBox.Show($"Xref Yöneticisi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnLayoutSheet(object sender, RoutedEventArgs e)
        {
            try { new LayoutSheetDialog(_database) { Owner = this }.ShowDialog(); Viewport.InvalidateVisual(); }
            catch (Exception ex) { MessageBox.Show($"Pafta Düzeni hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnTitleBlock(object sender, RoutedEventArgs e)
        {
            try { new TitleBlockDialog(_database) { Owner = this }.ShowDialog(); Viewport.InvalidateVisual(); }
            catch (Exception ex) { MessageBox.Show($"Antet hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnAuditSystem(object sender, RoutedEventArgs e)
        {
            try
            {
                var guard = new DomainGuardService(_database, _mechanicalKernel.TopologyGraph);
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
    }
}
