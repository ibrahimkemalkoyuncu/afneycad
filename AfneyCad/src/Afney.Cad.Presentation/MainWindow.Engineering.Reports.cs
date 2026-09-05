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
        #region -- MUHENDISLIK - RAPORLAMA/ETIKETLEME (ENGINEERING.REPORTS) --

        private void OnShowBOMReport(object sender, RoutedEventArgs e)
        {
            try
            {
                var res = MessageBox.Show("Metraj raporunu çizimin içine TABLO olarak eklemek ister misiniz?",
                                        "Mühendislik Raporu", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    var cmd = new InsertBOMTableCommand(_database, _history.TransactionManager);
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

        private async void OnGenerateHydraulicReport(object sender, RoutedEventArgs e)
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
                StatusText.Text = "Hidrolik rapor için hesaplama yapılıyor...";
                MainProgressBar.IsIndeterminate = false;
                MainProgressBar.Minimum = 0;
                MainProgressBar.Maximum = 100;
                MainProgressBar.Value = 0;
                MainProgressBar.Visibility = Visibility.Visible;

                var allEntities = _database.GetAllEntities().ToList();
                var progress = new Progress<(int Percent, string Stage)>(p =>
                {
                    MainProgressBar.Value = p.Percent;
                    StatusText.Text = $"Hidrolik rapor hesaplaması: %{p.Percent} — {p.Stage}";
                });

                await System.Threading.Tasks.Task.Run(() =>
                {
                    _mechanicalKernel.RecalculateProject(allEntities, progress);
                });

                MainProgressBar.Visibility = Visibility.Collapsed;
                MainProgressBar.IsIndeterminate = true;

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
            finally
            {
                MainProgressBar.Visibility = Visibility.Collapsed;
                MainProgressBar.IsIndeterminate = true;
            }
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

        private async void OnShowIsometricScheme(object sender, RoutedEventArgs e)
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

                // Gerçek kat sayısı (çok katlı projeler) — UI thread'de tespit et.
                int detectedFloors = new Afney.Cad.Mechanical.Services.FloorSnapshotService()
                    .DetectFloors(_database).Count;

                // ── Çıktı biçimi seçimi ───────────────────────────────────────
                var choice = MessageBox.Show(
                    "İzometrik kolon şeması çıktı biçimini seçin:\n\n" +
                    "EVET  → 🌐 Tarayıcıda Aç (HTML)\n" +
                    "HAYIR → 📐 DXF Çıktısı (CAD)\n" +
                    "İPTAL → 🖼 PNG Çıktısı (A4 300dpi)",
                    "İzometrik Şema Çıktısı",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (choice == MessageBoxResult.Yes)
                {
                    StatusText.Text = "İzometrik şema üretiliyor…";
                    // Büyük projelerde UI donmasın — HTML üretimi arka planda.
                    string html = await System.Threading.Tasks.Task.Run(
                        () => GenerateIsometricHtml(pipes, fixtures, detectedFloors));

                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"IsometricScheme_{Guid.NewGuid():N}.html");
                    System.IO.File.WriteAllText(tempPath, html, System.Text.Encoding.UTF8);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = tempPath, UseShellExecute = true });

                    StatusText.Text = $"İzometrik şema: {pipes.Count} boru · {fixtures.Count} vitrifiye → tarayıcıda açıldı.";
                }
                else if (choice == MessageBoxResult.No)
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "İzometrik Şema — DXF/DWG Çıktısı",
                        Filter = "DXF (AutoCAD R12)|*.dxf|DWG (AutoCAD R2004+)|*.dwg",
                        FileName = $"AfneyCAD_KolonSemasi_{DateTime.Now:yyyyMMdd}",
                        DefaultExt = ".dxf"
                    };
                    if (dlg.ShowDialog(this) != true) return;
                    bool asDwg = dlg.FileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase);
                    await System.Threading.Tasks.Task.Run(
                        () => ExportRiserDxf(pipes, fixtures, detectedFloors, dlg.FileName, asDwg));
                    string fmt = asDwg ? "DWG" : "DXF";
                    StatusText.Text = $"İzometrik şema {fmt} olarak kaydedildi: {dlg.FileName}";
                    MessageBox.Show($"{fmt} kaydedildi:\n{dlg.FileName}", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (choice == MessageBoxResult.Cancel)
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "İzometrik Şema — PNG Çıktısı",
                        Filter = "PNG Görüntü|*.png",
                        FileName = $"AfneyCAD_KolonSemasi_{DateTime.Now:yyyyMMdd}",
                        DefaultExt = ".png"
                    };
                    if (dlg.ShowDialog(this) != true) return;
                    await System.Threading.Tasks.Task.Run(
                        () => ExportRiserPng(pipes, fixtures, detectedFloors, dlg.FileName));
                    StatusText.Text = $"İzometrik şema PNG olarak kaydedildi: {dlg.FileName}";
                    MessageBox.Show($"PNG kaydedildi:\n{dlg.FileName}", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show($"İzometrik şema hatası: {ex.Message}"); }
        }

        // ── Kolon Şeması (Riser Diagram) ─────────────────────────────────────────
        // detectedFloorCount: FloorSnapshotService.DetectFloors ile bulunan gerçek
        //                     kat sayısı (çok katlı projelerde etiket için).

        private string GenerateIsometricHtml(
            IEnumerable<PipeEntity> pipes,
            IEnumerable<SanitaryFixtureEntity> fixtures,
            int detectedFloorCount)
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

            // ── Efektif kat Z çözümü ──────────────────────────────────────────
            // 2D plan (tüm borular Z=0) projelerde bile anlamlı riser diyagramı
            // üretmek için: gerçek Z → layer bazlı kat → sanal index sırasıyla.
            var (effZ, effFZ, floorMode) = ResolveFloorLevels(pipeList, fixList, detectedFloorCount);
            double PZ1(PipeEntity p) => effZ.TryGetValue(p, out var v) ? v.z1 : p.StartPoint.Z;
            double PZ2(PipeEntity p) => effZ.TryGetValue(p, out var v) ? v.z2 : p.EndPoint.Z;
            double FZ(SanitaryFixtureEntity f) => effFZ.TryGetValue(f, out var v) ? v : f.Position.Z;

            // ── Kat seviyeleri (efektif Z kümeleme, 500 mm tolerans) ──────────
            var allZ = pipeList.SelectMany(p => new[] { PZ1(p), PZ2(p) })
                               .Concat(fixList.Select(FZ))
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

            // ── Kat bazlı özet (boru sayısı + DN dağılımı) ────────────────────
            var floorSummaries = new List<(string Label, int Count, string DnBreak, double TotalM)>();
            for (int fi = 0; fi < floorZs.Count; fi++)
            {
                double fz = floorZs[fi];
                double lo = fi == 0 ? double.MinValue : (floorZs[fi - 1] + fz) / 2.0;
                double hi = fi == floorZs.Count - 1 ? double.MaxValue : (fz + floorZs[fi + 1]) / 2.0;
                var onFloor = pipeList.Where(p =>
                {
                    double pz = (PZ1(p) + PZ2(p)) / 2.0;
                    return pz >= lo && pz < hi;
                }).ToList();
                if (onFloor.Count == 0) continue;
                string dnBreak = string.Join(" · ", onFloor
                    .GroupBy(p => Math.Round(p.InnerDiameter))
                    .OrderBy(g => g.Key)
                    .Select(g => $"DN{g.Key:F0}×{g.Count()}"));
                double totalM = onFloor.Sum(p => p.GetLength()) / 1000.0;
                string label = fi == 0 ? "Zemin" : $"{fi}. Kat";
                floorSummaries.Add((label, onFloor.Count, dnBreak, totalM));
            }

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
                          $"{activeSystems.Count} sistem · {floorZs.Count} kat · Kat modeli: {floorMode} · TS 1258 / AfneyCAD v4.0</p>");
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
                    double riserZ1 = sysPipes.Min(p => Math.Min(PZ1(p), PZ2(p)));
                    double riserZ2 = sysPipes.Max(p => Math.Max(PZ1(p), PZ2(p)));
                    double ry1 = zToY(riserZ2), ry2 = zToY(riserZ1);
                    sb.AppendLine($"<line x1='{cx:F0}' y1='{ry1:F0}' x2='{cx:F0}' y2='{ry2:F0}' " +
                                  $"stroke='{meta.Color}' stroke-width='3' stroke-linecap='round' opacity='0.9'/>");
                }

                // Her boru — dal olarak çiz
                foreach (var pipe in sysPipes)
                {
                    double z1 = PZ1(pipe), z2 = PZ2(pipe);
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
                double fz = FZ(fix);
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

            // ── Kat bazlı özet tablosu ────────────────────────────────────────
            if (floorSummaries.Count > 0)
            {
                sb.AppendLine("<h2 style='margin-top:14px'>Kat Bazlı Özet</h2>");
                sb.AppendLine("<table style='border-collapse:collapse;font-size:12px;color:#c8d4e0;margin-top:4px'>");
                sb.AppendLine("<tr style='background:#182030;color:#7FC3FF'>" +
                              "<th style='border:1px solid #2a3a4a;padding:4px 10px;text-align:left'>Kat</th>" +
                              "<th style='border:1px solid #2a3a4a;padding:4px 10px'>Boru Sayısı</th>" +
                              "<th style='border:1px solid #2a3a4a;padding:4px 10px'>Toplam Uzunluk</th>" +
                              "<th style='border:1px solid #2a3a4a;padding:4px 10px;text-align:left'>DN Dağılımı</th></tr>");
                foreach (var fs in Enumerable.Reverse(floorSummaries).ToList())
                {
                    sb.AppendLine("<tr>" +
                        $"<td style='border:1px solid #2a3a4a;padding:4px 10px'>{fs.Label}</td>" +
                        $"<td style='border:1px solid #2a3a4a;padding:4px 10px;text-align:center'>{fs.Count}</td>" +
                        $"<td style='border:1px solid #2a3a4a;padding:4px 10px;text-align:right'>{fs.TotalM:F1} m</td>" +
                        $"<td style='border:1px solid #2a3a4a;padding:4px 10px'>{fs.DnBreak}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            sb.AppendLine($"<p class='info' style='margin-top:6px'>AfneyCAD v4.0 · Kolon Şeması · Kat modeli: {floorMode} · {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── Efektif kat Z çözücü ─────────────────────────────────────────────────
        // NE: Riser diyagramı için her boru/armatüre bir "kat Z" değeri atar.
        // NEDEN: 2D planlarda (tüm entity Z=0) borular tek yatay çizgiye çökerek
        //        anlamsız bir şema oluşturur. Sırayla üç strateji uygulanır:
        //        (1) Gerçek Z farkı ≥ 500 mm  → koordinatları aynen kullan (çok katlı)
        //        (2) Layer'da kat bilgisi var → "KAT_1", "GROUND" vb. → kat×3000 mm
        //        (3) Bilgi yok               → sistem içi index'e göre 3000 mm artan sanal Z

        private static (Dictionary<PipeEntity, (double z1, double z2)> pipeZ,
                        Dictionary<SanitaryFixtureEntity, double> fixZ,
                        string mode)
            ResolveFloorLevels(List<PipeEntity> pipes, List<SanitaryFixtureEntity> fixtures, int detectedFloorCount)
        {
            const double FloorHeight = 3000.0; // mm — tipik kat yüksekliği
            var pipeZ = new Dictionary<PipeEntity, (double, double)>();
            var fixZ  = new Dictionary<SanitaryFixtureEntity, double>();

            var zVals = pipes.SelectMany(p => new[] { p.StartPoint.Z, p.EndPoint.Z })
                             .Concat(fixtures.Select(f => f.Position.Z)).ToList();
            double span = zVals.Count > 0 ? zVals.Max() - zVals.Min() : 0;

            // (1) Çok katlı — gerçek Z değerleri anlamlı
            if (span >= 500.0)
            {
                foreach (var p in pipes)    pipeZ[p] = (p.StartPoint.Z, p.EndPoint.Z);
                foreach (var f in fixtures) fixZ[f]  = f.Position.Z;
                int floors = Math.Max(detectedFloorCount, 1);
                return (pipeZ, fixZ, $"Gerçek Z (çok katlı · {floors} kat)");
            }

            // (2) Düz plan — layer bazlı kat tespiti
            var layerFloors = pipes.ToDictionary(p => p, p => ParseFloorFromLayer(p.Layer));
            if (layerFloors.Values.Any(v => v.HasValue))
            {
                foreach (var p in pipes)
                {
                    double z = (layerFloors[p] ?? 0) * FloorHeight;
                    pipeZ[p] = (z, z);
                }
                foreach (var f in fixtures) fixZ[f] = NearestPipeZ(f, pipes, pipeZ);
                return (pipeZ, fixZ, "Layer bazlı kat tespiti");
            }

            // (3) Katman bilgisi yok — sistem içi sıraya göre sanal kat
            foreach (var grp in pipes.GroupBy(p => p.SystemType))
            {
                int idx = 0;
                foreach (var p in grp.OrderBy(p => p.StartPoint.X).ThenBy(p => p.StartPoint.Y))
                {
                    double z = idx * FloorHeight;
                    pipeZ[p] = (z, z);
                    idx++;
                }
            }
            foreach (var f in fixtures) fixZ[f] = NearestPipeZ(f, pipes, pipeZ);
            return (pipeZ, fixZ, "Sanal kat (index bazlı)");
        }

        // Layer adından kat numarası çıkar: "KAT_2", "FLOOR 3", "GROUND", "ZEMIN", "BODRUM".

        private static int? ParseFloorFromLayer(string? layer)
        {
            if (string.IsNullOrWhiteSpace(layer)) return null;
            string u = layer.ToUpperInvariant();
            if (u.Contains("BODRUM") || u.Contains("BASEMENT")) return -1;
            if (u.Contains("ZEMIN") || u.Contains("ZEMİN") || u.Contains("GROUND") || u.Contains("GRND")) return 0;
            var m = System.Text.RegularExpressions.Regex.Match(u, @"(?:KAT|FLOOR|LEVEL|STOREY|K)[_\-\s]?(\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n)) return n;
            return null;
        }

        // Armatürü, XY düzleminde en yakın borunun kat Z'sine yasla.

        private static double NearestPipeZ(SanitaryFixtureEntity f, List<PipeEntity> pipes,
                                           Dictionary<PipeEntity, (double z1, double z2)> pipeZ)
        {
            if (pipes.Count == 0) return 0;
            PipeEntity best = pipes[0];
            double bestD = double.MaxValue;
            foreach (var p in pipes)
            {
                double d = Math.Pow(p.StartPoint.X - f.Position.X, 2) +
                           Math.Pow(p.StartPoint.Y - f.Position.Y, 2);
                if (d < bestD) { bestD = d; best = p; }
            }
            return pipeZ.TryGetValue(best, out var v) ? (v.z1 + v.z2) / 2.0 : 0;
        }

        // ── Kolon şeması ilkel geometrisi (SVG/DXF/PNG ortak kaynağı) ────────────
        // Piksel uzayında (Y aşağı, tuval 1100×720) çizim ilkelleri üretir; HTML,
        // DXF ve PNG çıktı üreticileri bu tek modeli kullanır.
        // anchor: 0=sol, 1=orta, 2=sağ (metin hizası)

        private (List<(double x1, double y1, double x2, double y2, uint color, double w, bool dashed)> lines,
                 List<(double x, double y, string t, uint color, double size, int anchor)> texts,
                 List<(double cx, double cy, double r, uint color)> circles,
                 int width, int height)
            BuildRiserPrimitives(List<PipeEntity> pipeList, List<SanitaryFixtureEntity> fixList, int detectedFloorCount)
        {
            var lines   = new List<(double, double, double, double, uint, double, bool)>();
            var texts   = new List<(double, double, string, uint, double, int)>();
            var circles = new List<(double, double, double, uint)>();

            var systemMeta = new Dictionary<MechanicalSystemType, (uint Color, string Label, string Short)>
            {
                [MechanicalSystemType.DomesticColdWater] = (0xFF2196F3, "Soğuk Su",     "SK"),
                [MechanicalSystemType.DomesticHotWater]  = (0xFFF44336, "Sıcak Su",     "SH"),
                [MechanicalSystemType.WasteWater]        = (0xFF795548, "Pis Su",       "PS"),
                [MechanicalSystemType.RainWater]         = (0xFF00BCD4, "Yağmur",       "YS"),
                [MechanicalSystemType.FireProtection]    = (0xFFFF9800, "Yangın",       "YG"),
                [MechanicalSystemType.Gas]               = (0xFFFFEB3B, "Gaz",          "GZ"),
                [MechanicalSystemType.Ventilation]       = (0xFF9C27B0, "Havalandırma", "HV"),
            };
            const uint gridColor = 0xFF2A3A4A, labelColor = 0xFF557799, frameColor = 0xFF1E2E3E;

            var activeSystems = pipeList.Select(p => p.SystemType).Distinct().OrderBy(s => (int)s).ToList();
            if (!activeSystems.Any()) activeSystems.Add(MechanicalSystemType.DomesticColdWater);

            var (effZ, effFZ, _) = ResolveFloorLevels(pipeList, fixList, detectedFloorCount);
            double PZ1(PipeEntity p) => effZ.TryGetValue(p, out var v) ? v.z1 : p.StartPoint.Z;
            double PZ2(PipeEntity p) => effZ.TryGetValue(p, out var v) ? v.z2 : p.EndPoint.Z;
            double FZ(SanitaryFixtureEntity f) => effFZ.TryGetValue(f, out var v) ? v : f.Position.Z;

            var allZ = pipeList.SelectMany(p => new[] { PZ1(p), PZ2(p) }).Concat(fixList.Select(FZ)).OrderBy(z => z).ToList();
            var floorZs = new List<double>();
            foreach (double z in allZ) { if (!floorZs.Any() || z - floorZs.Last() > 500) floorZs.Add(z); }
            if (floorZs.Count < 2) { floorZs.Clear(); floorZs.Add(0); floorZs.Add(3000); }
            double zMin = floorZs.First(), zMax = floorZs.Last();
            double zRange = Math.Max(zMax - zMin, 3000);

            const int svgW = 1100, svgH = 720, marginLeft = 80, marginBottom = 60, marginTop = 40;
            int drawH = svgH - marginTop - marginBottom;
            int drawW = svgW - marginLeft - 20;
            int colW = Math.Max(60, drawW / Math.Max(activeSystems.Count, 1));
            double zToY(double z) => marginTop + drawH - (z - zMin) / zRange * drawH;
            double colX(int ci) => marginLeft + ci * colW + colW / 2.0;

            texts.Add((marginLeft, 22, "Tesisat Kolon Şeması", 0xFF7FC3FF, 15, 0));

            // Kat çizgileri
            for (int fi = 0; fi < floorZs.Count; fi++)
            {
                double y = zToY(floorZs[fi]);
                string fl = fi == 0 ? "Zemin" : $"{fi}. Kat";
                lines.Add((marginLeft, y, svgW - 10, y, gridColor, 1, true));
                texts.Add((marginLeft - 6, y + 4, fl, labelColor, 10, 2));
            }

            // Sistem kolonları
            for (int ci = 0; ci < activeSystems.Count; ci++)
            {
                var sys = activeSystems[ci];
                if (!systemMeta.TryGetValue(sys, out var meta))
                    meta = (0xFFAAAAAA, sys.ToString(), sys.ToString()[..2].ToUpper());
                double cx = colX(ci);
                var sysPipes = pipeList.Where(p => p.SystemType == sys).ToList();

                double headerY = marginTop - 14;
                texts.Add((cx, headerY, meta.Short, meta.Color, 11, 1));
                texts.Add((cx, headerY + 12, meta.Label, meta.Color, 8, 1));

                if (sysPipes.Any())
                {
                    double rz1 = sysPipes.Min(p => Math.Min(PZ1(p), PZ2(p)));
                    double rz2 = sysPipes.Max(p => Math.Max(PZ1(p), PZ2(p)));
                    lines.Add((cx, zToY(rz2), cx, zToY(rz1), meta.Color, 3, false));
                }

                foreach (var pipe in sysPipes)
                {
                    double z1 = PZ1(pipe), z2 = PZ2(pipe);
                    double dz = Math.Abs(z2 - z1);
                    double dxy = Math.Sqrt(Math.Pow(pipe.EndPoint.X - pipe.StartPoint.X, 2) +
                                           Math.Pow(pipe.EndPoint.Y - pipe.StartPoint.Y, 2));
                    double strokeW = Math.Max(1.5, Math.Min(pipe.InnerDiameter / 25.0, 6.0));
                    string label = $"DN{pipe.InnerDiameter:F0}";
                    if (dz > 200 && dz >= dxy * 0.5)
                    {
                        double py1 = zToY(Math.Max(z1, z2)), py2 = zToY(Math.Min(z1, z2));
                        lines.Add((cx, py1, cx, py2, meta.Color, strokeW, false));
                    }
                    else
                    {
                        double by = zToY((z1 + z2) / 2.0);
                        double branchLen = Math.Max(dxy / 1000.0 * 0.3 * colW, 18);
                        double bx2 = cx + branchLen;
                        lines.Add((cx, by, bx2, by, meta.Color, strokeW, false));
                        texts.Add(((cx + bx2) / 2, by - 3, label, meta.Color, 8, 1));
                    }
                }

                if (sysPipes.Any())
                {
                    double totalM = sysPipes.Sum(p => p.GetLength()) / 1000.0;
                    texts.Add((cx, svgH - marginBottom + 14, $"{totalM:F1} m", meta.Color, 9, 1));
                }
            }

            // Armatürler
            foreach (var fix in fixList)
            {
                double fy = zToY(FZ(fix));
                uint fcol = 0xFF000000 | (fix.Color & 0xFFFFFF);
                int nearestCol = 0;
                if (pipeList.Any())
                {
                    nearestCol = activeSystems
                        .Select((s, i) => (i, dist: pipeList.Where(p => p.SystemType == s).DefaultIfEmpty()
                            .Min(p => p is null ? double.MaxValue :
                                Math.Sqrt(Math.Pow(p.StartPoint.X - fix.Position.X, 2) +
                                          Math.Pow(p.StartPoint.Y - fix.Position.Y, 2)))))
                        .OrderBy(t => t.dist).First().i;
                }
                double fx = colX(nearestCol) + 22;
                circles.Add((fx, fy, 6, fcol));
                string shortName = fix.FixtureType.Length > 4 ? fix.FixtureType[..4] : fix.FixtureType;
                texts.Add((fx, fy - 9, shortName, 0xFFCCCCCC, 7, 1));
                lines.Add((colX(nearestCol), fy, fx - 7, fy, 0xFF445566, 0.8, true));
            }

            // Çerçeve
            lines.Add((marginLeft, marginTop, svgW - 20, marginTop, frameColor, 1, false));
            lines.Add((marginLeft, marginTop + drawH, svgW - 20, marginTop + drawH, frameColor, 1, false));
            lines.Add((marginLeft, marginTop, marginLeft, marginTop + drawH, frameColor, 1, false));
            lines.Add((svgW - 20, marginTop, svgW - 20, marginTop + drawH, frameColor, 1, false));

            // Legend
            int lx = marginLeft, ly = svgH - marginBottom + 28;
            for (int idx = 0; idx < activeSystems.Count; idx++)
            {
                if (!systemMeta.TryGetValue(activeSystems[idx], out var m)) continue;
                int lxi = lx + idx * 130;
                lines.Add((lxi, ly - 4, lxi + 12, ly - 4, m.Color, 6, false));
                texts.Add((lxi + 16, ly, m.Label, 0xFFAABBCC, 10, 0));
            }

            return (lines, texts, circles, svgW, svgH);
        }

        // ── Kolon şeması → DXF (AutoCAD R12) veya DWG (AutoCAD R2004+) ───────────

        private void ExportRiserDxf(List<PipeEntity> pipes, List<SanitaryFixtureEntity> fixtures,
                                    int detectedFloors, string path, bool asDwg = false)
        {
            var (lines, texts, circles, _, h) = BuildRiserPrimitives(pipes, fixtures, detectedFloors);
            var db = new Afney.Cad.Database.Core.CadDatabase();

            // Piksel (Y aşağı) → CAD (Y yukarı): worldY = h - pixelY
            foreach (var l in lines)
            {
                db.AddEntity(new Afney.Cad.Domain.Entities.Basic.LineEntity(
                    new Vector3D(l.x1, h - l.y1, 0), new Vector3D(l.x2, h - l.y2, 0))
                { Color = l.color, Layer = "ISO_KOLON" });
            }
            foreach (var c in circles)
            {
                db.AddEntity(new Afney.Cad.Domain.Entities.Basic.CircleEntity(
                    new Vector3D(c.cx, h - c.cy, 0), c.r)
                { Color = c.color, Layer = "ISO_ARMATUR" });
            }
            foreach (var t in texts)
            {
                // DXF TEXT sol-alt referanslı; orta/sağ hizayı yaklaşık ofsetle
                double tx = t.x - t.anchor switch { 1 => t.t.Length * t.size * 0.28, 2 => t.t.Length * t.size * 0.55, _ => 0 };
                db.AddEntity(new Afney.Cad.Domain.Entities.Basic.TextEntity(
                    t.t, new Vector3D(tx, h - t.y, 0), t.size)
                { Color = t.color, Layer = "ISO_YAZI" });
            }

            if (asDwg)
                new Afney.Cad.Infrastructure.Export.DwgExportService(db).WriteToFile(path);
            else
                new Afney.Cad.Infrastructure.Export.DxfWriterService(db).WriteToFile(path);
        }

        // ── Kolon şeması → PNG (A4 300 dpi) ──────────────────────────────────────

        private void ExportRiserPng(List<PipeEntity> pipes, List<SanitaryFixtureEntity> fixtures,
                                    int detectedFloors, string path)
        {
            var (lines, texts, circles, w, h) = BuildRiserPrimitives(pipes, fixtures, detectedFloors);
            const int PW = 2480, PH = 3508, pad = 120; // A4 300 dpi portrait
            double scale = Math.Min((PW - 2.0 * pad) / w, (PH - 2.0 * pad) / h);
            double ox = (PW - w * scale) / 2.0, oy = (PH - h * scale) / 2.0;
            float SX(double x) => (float)(ox + x * scale);
            float SY(double y) => (float)(oy + y * scale);

            using var bmp = new SkiaSharp.SKBitmap(PW, PH);
            using var canvas = new SkiaSharp.SKCanvas(bmp);
            canvas.Clear(new SkiaSharp.SKColor(0x0E, 0x0E, 0x1C));

            foreach (var l in lines)
            {
                using var paint = new SkiaSharp.SKPaint
                {
                    Color = ArgbToSk(l.color),
                    StrokeWidth = (float)Math.Max(1.0, l.w * scale),
                    IsAntialias = true,
                    Style = SkiaSharp.SKPaintStyle.Stroke
                };
                if (l.dashed) paint.PathEffect = SkiaSharp.SKPathEffect.CreateDash(new float[] { 8f, 5f }, 0);
                canvas.DrawLine(SX(l.x1), SY(l.y1), SX(l.x2), SY(l.y2), paint);
            }
            foreach (var c in circles)
            {
                using var paint = new SkiaSharp.SKPaint
                { Color = ArgbToSk(c.color), IsAntialias = true, Style = SkiaSharp.SKPaintStyle.Fill };
                canvas.DrawCircle(SX(c.cx), SY(c.cy), (float)(c.r * scale), paint);
            }
            foreach (var t in texts)
            {
                using var paint = new SkiaSharp.SKPaint
                {
                    Color = ArgbToSk(t.color),
                    IsAntialias = true,
                    TextSize = (float)(t.size * scale),
                    TextAlign = t.anchor == 1 ? SkiaSharp.SKTextAlign.Center
                              : t.anchor == 2 ? SkiaSharp.SKTextAlign.Right
                              : SkiaSharp.SKTextAlign.Left
                };
                canvas.DrawText(t.t, SX(t.x), SY(t.y), paint);
            }

            using var img = SkiaSharp.SKImage.FromBitmap(bmp);
            using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var fs = System.IO.File.OpenWrite(path);
            data.SaveTo(fs);
        }

        private static SkiaSharp.SKColor ArgbToSk(uint argb) =>
            new SkiaSharp.SKColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

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

        private void OnRevisionTracking(object sender, RoutedEventArgs e)
        {
            try
            {
                // Session #74: artık sekme/proje bazlı kalıcı örnek (CadDocumentContext.Revisions)
                // kullanılıyor — önceden burada her tıklamada "new RevisionTrackingService()" ile
                // sıfır bir örnek oluşturuluyordu ve girilen revizyonlar diyalog kapanınca kayboluyordu.
                if (_activeContext == null) return;
                var svc = _activeContext.Revisions;
                if (string.IsNullOrWhiteSpace(svc.TitleBlock.ProjectName))
                    svc.TitleBlock.ProjectName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD Projesi";
                new RevisionTrackingDialog(svc) { Owner = this }.ShowDialog();
            }
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

        private void OnViewportCapture(object sender, RoutedEventArgs e)
        {
            try { new ViewportCaptureDialog(_database) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show($"Ekran Çizimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnLayoutSheet(object sender, RoutedEventArgs e)
        {
            try { new LayoutSheetDialog(_database, _activeContext?.SheetIndex) { Owner = this }.ShowDialog(); Viewport.InvalidateVisual(); }
            catch (Exception ex) { MessageBox.Show($"Pafta Düzeni hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnTitleBlock(object sender, RoutedEventArgs e)
        {
            try { new TitleBlockDialog(_database, _activeContext?.SheetIndex) { Owner = this }.ShowDialog(); Viewport.InvalidateVisual(); }
            catch (Exception ex) { MessageBox.Show($"Antet hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnSheetSetManager(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activeContext == null) return;
                string projectName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD Projesi";
                new SheetSetManagerDialog(_activeContext.SheetIndex, projectName) { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show($"Pafta Seti Yöneticisi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnSheetIndex(object sender, RoutedEventArgs e)
        {
            try
            {
                string projectName = _mechanicalKernel?.Metadata?.ProjectName ?? "AfneyCAD Projesi";
                var sheetIndex = _activeContext?.SheetIndex ?? SheetIndexService.Instance;
                string html = sheetIndex.BuildIndexHtml(projectName);

                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"AfneyCAD_PaftaIndeksi_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                System.IO.File.WriteAllText(tempPath, html, System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { MessageBox.Show($"Pafta İndeksi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        /*
           NE: Halkalı Şebeke Analizi (OnHardyCrossAnalysis)
           NEDEN: HardyCrossSolver/HydraulicNetwork yazılmıştı ama hiçbir komut onları
                  çizimdeki gerçek borulardan bir ağ kurup çağırmıyordu — kapalı halka
                  (ring main) tespiti hiç tetiklenmiyordu (yangın söndürme ring hattı, halkalı
                  doğalgaz şebekesi gibi durumlarda debi dağılımı hep basit ağaç varsayımıyla
                  hesaplanıyordu, yanlış sonuç veriyordu). Artık her sistem tipi (Soğuk Su,
                  Sıcak Su, Yangın, Gaz...) için ayrı bir HydraulicNetwork kurulup
                  HardyCrossSolver.Solve çağrılıyor; sonuçlar (FlowRate, PressureDrop) ilgili
                  borulara geri yazılıyor.
        */

        #endregion
    }
}
