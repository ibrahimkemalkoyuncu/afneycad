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
        #region -- MUHENDISLIK - MAHAL/ODA (ENGINEERING.ROOMS) --

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

        private void OnEdgeCaptureMahalDefine(object sender, RoutedEventArgs e)
        {
            if (_database == null)
            {
                MessageBox.Show("Önce bir çizim açın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new EdgeCaptureMahalCommand((mahal) =>
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
                            StatusText.Text = $"MAHAL KAYDEDİLDİ (Uç-Yakala): {mahal.MahalName} ({mahal.MahalType}) — {mahal.Area:F2} m²";
                        }
                        else
                        {
                            StatusText.Text = "Uç-yakala mahal tanımlama iptal edildi.";
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "[Uç-Yakala Mahal] Dialog açılırken hata");
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

        #endregion
    }
}
