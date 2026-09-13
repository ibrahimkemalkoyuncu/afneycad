using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Presentation.Views;
using SkiaSharp;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Toplu Pafta Baskı Servisi (BatchPlotService)
   NEDEN — Session #75 iş akışı denetiminde bulunan gerçek boşluk: `SheetSetManagerDialog`
          paftaları listeliyordu ama "toplu baskı/export" hiç yoktu — her pafta gerçekte
          sadece bir isim/numara kaydıydı, ayrı bir çizim dosyası veya görünüm DEĞİLDİ. Bu
          servis, her paftayı `LayerStateManagerService`'in isimlendirilmiş bir katman
          görünürlük durumuna (`SheetEntry.LayerStateName`) bağlayarak GERÇEK bir çözüm sunuyor:
          her pafta için o katman durumunu uygulayıp viewport'u yeniden çizdiriyor, ekran
          görüntüsünü yakalayıp tek bir çok sayfalı PDF'in bir sayfası yapıyor. İşlem bitince
          orijinal katman görünürlüğü/dondurma/kilit durumu TAM olarak geri yükleniyor.

   DÜRÜSTLÜK NOTU: `LayerStateName` atanmamış paftalar, o anki (mevcut) görünümle çizilir —
          yani birden fazla böyle pafta varsa aynı görünecektir. Gerçek çok-sayfalı bir baskı
          için her paftaya bir katman durumu atanması gerekir (bkz. SheetSetManagerDialog).
*/
public static class BatchPlotService
{
    public class BatchPlotResult
    {
        public int PagesWritten { get; set; }
        public List<string> SkippedSheets { get; set; } = new();
    }

    public static BatchPlotResult ExportSheetsToPdf(
        CadViewport viewport,
        CadDatabase database,
        LayerStateManagerService layerStates,
        IReadOnlyList<SheetIndexService.SheetEntry> sheets,
        string outputPath,
        PrintViewportService.PrintOptions? options = null)
    {
        options ??= new PrintViewportService.PrintOptions();
        var result = new BatchPlotResult();

        // Geri yükleme için orijinal durumu yedekle: hiddenLayers seti + her katmanın kendi Frozen/Locked bayrağı.
        var originalHidden = new HashSet<string>(viewport.HiddenLayers, StringComparer.OrdinalIgnoreCase);
        var originalFlags = database.GetLayers().ToDictionary(l => l.Name, l => (l.IsFrozen, l.IsLocked), StringComparer.OrdinalIgnoreCase);

        try
        {
            var (wMm, hMm) = PrintViewportService.GetPageSize(options.Format);
            int pxW = (int)(wMm / 25.4 * options.DpiResolution);
            int pxH = (int)(hMm / 25.4 * options.DpiResolution);

            using var stream = new SKFileWStream(outputPath);
            using var doc = SKDocument.CreatePdf(stream);

            foreach (var sheet in sheets)
            {
                if (!string.IsNullOrWhiteSpace(sheet.LayerStateName))
                {
                    var snapshot = layerStates.Find(sheet.LayerStateName);
                    if (snapshot == null)
                    {
                        result.SkippedSheets.Add($"{sheet.Number} — '{sheet.LayerStateName}' katman durumu bulunamadı");
                        continue;
                    }
                    layerStates.ApplyState(snapshot, database, viewport.HiddenLayers);
                }

                ForceRender(viewport);

                var bmp = PrintViewportService.RenderToBitmap(viewport, pxW, pxH, new PrintViewportService.PrintOptions
                {
                    Format = options.Format,
                    FitToPage = options.FitToPage,
                    PrintTitleBlock = options.PrintTitleBlock,
                    ProjectName = options.ProjectName,
                    DrawingTitle = sheet.Name,
                    DrawingNumber = sheet.Number,
                    Scale = options.Scale,
                    DrawnBy = options.DrawnBy,
                    Date = options.Date,
                    BackgroundColor = options.BackgroundColor,
                    ForegroundColor = options.ForegroundColor,
                    DpiResolution = options.DpiResolution
                });

                using (bmp)
                using (var canvas = doc.BeginPage(pxW * 72f / options.DpiResolution, pxH * 72f / options.DpiResolution))
                {
                    canvas.Scale(72f / options.DpiResolution);
                    canvas.DrawBitmap(bmp, 0, 0);
                    doc.EndPage();
                }

                result.PagesWritten++;
            }

            doc.Close();
        }
        finally
        {
            // Orijinal katman görünürlüğü/dondurma/kilit durumunu TAM olarak geri yükle.
            viewport.HiddenLayers.Clear();
            foreach (var h in originalHidden) viewport.HiddenLayers.Add(h);

            foreach (var layer in database.GetLayers())
            {
                if (originalFlags.TryGetValue(layer.Name, out var flags))
                {
                    layer.IsFrozen = flags.IsFrozen;
                    layer.IsLocked = flags.IsLocked;
                }
            }
            ForceRender(viewport);
        }

        return result;
    }

    // CadViewport.InvalidateViewport() sonrası gerçek bir render geçişi yapılmasını garantiler —
    // aksi halde VisualBrush/RenderTargetBitmap yakalaması eski (bir önceki) görüntüyü alabilir.
    private static void ForceRender(CadViewport viewport)
    {
        viewport.InvalidateViewport();
        viewport.UpdateLayout();
        viewport.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }
}
