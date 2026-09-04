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
        #region -- TEMEL CIZIM & BOYUTLANDIRMA KOMUTLARI --

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

        // NE: Katı Cisim (Solid/CSG) Komutları — BOX/UNION/SUBTRACT/INTERSECT
        // NEDEN: Denetim raporu bulgusu — CSG Boolean kernel'i (GeneralSolidUnion/Subtractor/
        //        Intersector) arayüze hiç bağlı değildi. BOX, kullanıcının denemesi için test
        //        edilecek bir SolidEntity üretir; UNION/SUBTRACT/INTERSECT ise seçilen iki
        //        SolidEntity üzerinde kernel'i çalıştırıp sonucu (Undo'lu, tek işlemde) veritabanına yazar.
        private void OnSolidBoxCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnSolidBoxCommand(this, new RoutedEventArgs());
            var cmd = new SolidBoxCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        private void OnSolidUnionCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnSolidUnionCommand(this, new RoutedEventArgs());
            var cmd = new SolidUnionCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSolidSubtractCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnSolidSubtractCommand(this, new RoutedEventArgs());
            var cmd = new SolidSubtractCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        private void OnSolidIntersectCommand(object sender, RoutedEventArgs e)
        {
            _lastRepeatableCommand = () => OnSolidIntersectCommand(this, new RoutedEventArgs());
            var cmd = new SolidIntersectCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
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

        /*
           NE: Ölçek Doğrula (OnScaleVerifyCommand)
           NEDEN: Kullanıcı isteği — mimardan gelen bir DWG'nin birimi ($INSUNITS) yanlış
                  veya eksik olabilir. `DwgImportService` artık dosyadaki INSUNITS'i doğru
                  okuyup uyguluyor (bkz. bugünkü unitScale düzeltmesi) ama dosyanın KENDİSİ
                  bu bilgiyi hiç taşımıyorsa veya yanlış taşıyorsa otomatik algılama işe
                  yaramaz. Bu komut kullanıcının çizimde bildiği GERÇEK bir ölçüyle (ör.
                  bir kapı genişliği) iki nokta seçip karşılaştırmasını, gerekirse TÜM
                  çizimi seçilen 1. nokta etrafında (AutoCAD SCALE komutundaki "base point"
                  mantığıyla aynı) tek bir Undo'lu adımda düzeltmesini sağlar.
        */
        private void OnScaleVerifyCommand(object sender, RoutedEventArgs e)
        {
            if (_database == null)
            {
                MessageBox.Show("Önce bir çizim açın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new ScaleVerifyCommand((p1, p2) =>
            {
                Dispatcher.Invoke(() =>
                {
                    double measuredMm = (p2 - p1).Length();
                    if (measuredMm < 1.0)
                    {
                        StatusText.Text = "Ölçek Doğrula: iki nokta çok yakın, tekrar deneyin.";
                        return;
                    }

                    var dialog = new ScaleVerifyDialog(measuredMm) { Owner = this };
                    if (dialog.ShowDialog() != true) return;

                    double factor = dialog.CorrectionFactor;
                    if (Math.Abs(factor - 1.0) < 1e-9) return;

                    var toOrigin     = Afney.Cad.Geometry.Primitives.Matrix4x4.TranslationMatrix(-p1.X, -p1.Y, -p1.Z);
                    var scale        = Afney.Cad.Geometry.Primitives.Matrix4x4.Scaling(factor, factor, factor);
                    var backToAnchor = Afney.Cad.Geometry.Primitives.Matrix4x4.TranslationMatrix(p1.X, p1.Y, p1.Z);
                    var transform    = backToAnchor * scale * toOrigin;

                    var toOriginInv     = Afney.Cad.Geometry.Primitives.Matrix4x4.TranslationMatrix(p1.X, p1.Y, p1.Z);
                    var scaleInv        = Afney.Cad.Geometry.Primitives.Matrix4x4.Scaling(1.0 / factor, 1.0 / factor, 1.0 / factor);
                    var backToAnchorInv = Afney.Cad.Geometry.Primitives.Matrix4x4.TranslationMatrix(-p1.X, -p1.Y, -p1.Z);
                    var inverseTransform = toOriginInv * scaleInv * backToAnchorInv;

                    var entities = _database.GetAllEntities().ToList();
                    var composite = new Afney.Cad.Database.Transactions.CompositeOperation($"Ölçek Düzeltme (×{factor:F4})");
                    foreach (var ent in entities)
                        composite.Add(new TransformEntityOperation(ent, transform, inverseTransform, _database));

                    _database.TransactionManager.Submit(composite);
                    Viewport.InvalidateVisual();
                    StatusText.Text = $"Ölçek düzeltildi: {entities.Count} nesne ×{factor:F4} oranında ölçeklendi (Ctrl+Z ile geri alınabilir).";
                });
            });

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

        /*
           NE: Ölçü Stili Yöneticisini Aç (OnDimensionStyleManager)
           NEDEN: DimensionStyleService'in stil oluşturma/düzenleme/kaydetme için hiçbir UI'ı
                  yoktu (sadece 3 hazır stil arası Small/Medium/Large geçişi vardı). Bu dialog
                  kullanıcının kendi stillerini tanımlamasını sağlıyor.
        */
        private void OnDimensionStyleManager(object sender, RoutedEventArgs e)
        {
            var dialog = new DimensionStyleManagerDialog(_dimStyleService) { Owner = this };
            dialog.ShowDialog();
            _dimTextHeight = _dimStyleService.ActiveStyle.TextHeight;
            StatusText.Text = $"Ölçü stili: {_dimStyleService.ActiveStyleName}";
        }

        private void OnOffsetCommand(object sender, RoutedEventArgs e)
        {
            if (ActiveContext?.Viewport == null) return;
            _lastRepeatableCommand = () => OnOffsetCommand(this, new RoutedEventArgs());
            ExecuteCommand("OFFSET");
        }

        #endregion
    }
}
