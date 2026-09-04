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
        #region -- MUHENDISLIK - KUTUPHANE/KATALOG/OZEL HESAPLAR/ARACLAR (ENGINEERING.LIBRARY) --

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

        private void OnSilencerSelection(object sender, RoutedEventArgs e)
        {
            try { new SilencerSelectionDialog { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Susturucu seçimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
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

        /*
           NE: Poz Katalogunu CSV Olarak Dışa Aktar (OnExportPozCsv)
           NEDEN: Önceden sadece CSV İÇE aktarma vardı — kullanıcı mevcut poz/birim fiyat
                  listesini görüp Excel'de güncelleyemiyordu (round-trip eksikti). Artık
                  projenin geçerli kataloğu (varsa override dahil) CSV'ye yazılıyor;
                  kullanıcı fiyatları güncelleyip aynı "Poz CSV İçe" ile geri yükleyebilir.
        */

        private void OnExportPozCsv(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new Afney.Cad.Mechanical.Services.PozKatalogService();

                string projDir = System.IO.Path.GetDirectoryName(
                    _activeContext?.FilePath ?? System.IO.Path.GetTempPath()) ?? System.IO.Path.GetTempPath();
                string existingOverride = System.IO.Path.Combine(projDir, "poz_katalog_override.json");
                if (System.IO.File.Exists(existingOverride))
                    svc.LoadFromJson(existingOverride);

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title      = "Poz Kataloğunu CSV Olarak Kaydet",
                    Filter     = "CSV Dosyası (*.csv)|*.csv",
                    FileName   = $"PozKatalogu_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".csv"
                };
                if (dlg.ShowDialog(this) != true) return;

                svc.SaveToCsv(dlg.FileName);
                StatusText.Text = $"✅ Poz kataloğu CSV'ye aktarıldı: {System.IO.Path.GetFileName(dlg.FileName)} ({svc.GetAll().Count} kalem)";

                var ans = MessageBox.Show(
                    $"{svc.GetAll().Count} poz kalemi CSV'ye aktarıldı.\nDosyayı Excel'de açmak ister misiniz?",
                    "Poz CSV Dışa Aktarma", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ans == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var guard = new DomainGuardService(_database, _mechanicalKernel.TopologyGraph, _mechanicalKernel.ArchitecturalObstacles);
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

        // TS 825 — Binalarda Isı Yalıtım Kuralları: U değeri, yalıtım kalınlığı,
        // ısı kaybı ve yıllık enerji hesabı.

        private void OnTS825Insulation(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new TS825InsulationDialog(_database) { Owner = this };
                dlg.DrawingChanged += (_, _) => Viewport.InvalidateViewport();
                dlg.ShowDialog();
                Viewport.InvalidateViewport();
            }
            catch (Exception ex) { MessageBox.Show($"TS 825 hesabı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
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

        /*
           NE: D3D11 Motor Test Penceresi (OnD3D11EngineTest)
           NEDEN: docs/Roadmap_3D_Render_Motoru.md Faz 1 — sıfırdan yazılan Direct3D11 render
                  motorunun (WPF Viewport3D KULLANILMIYOR) gerçekten çalıştığını görsel olarak
                  doğrulamak için. Faz 2 ile birlikte artık test küpü yerine açık projenin
                  GERÇEK verisini (_database) render ediyor. Komut satırından "d3dtest" ile açılır.
        */

        public void OnD3D11EngineTest(object? sender, RoutedEventArgs? e)
        {
            try { new Dialogs.Direct3DTestWindow(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"D3D11 motor testi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnStandardSelection(object sender, RoutedEventArgs e)
        {
            try { new StandardSelectionDialog(_mechanicalKernel) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Standart seçimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
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

        private void OnXrefManager(object sender, RoutedEventArgs e)
        {
            try { new XrefManagerDialog(_database) { Owner = this }.ShowDialog(); Viewport.InvalidateVisual(); }
            catch (Exception ex) { MessageBox.Show($"Xref Yöneticisi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion
    }
}
