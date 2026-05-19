using System;
using System.Collections.Generic;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Viewport Baskı ve PDF Dışa Aktarma Servisi
   NEDEN: Mühendislik çizimlerini A3/A4 kağıda yazdırmak veya PDF olarak kaydetmek için.
          SkiaSharp bitmap → WPF PrintDialog akışı kullanır.
*/
public class PrintViewportService
{
    public enum PageFormat { A4_Portrait, A4_Landscape, A3_Portrait, A3_Landscape }

    public class PrintOptions
    {
        public PageFormat Format         { get; set; } = PageFormat.A3_Landscape;
        public bool   FitToPage          { get; set; } = true;
        public bool   PrintTitleBlock    { get; set; } = true;
        public string ProjectName        { get; set; } = "AfneyCAD Projesi";
        public string DrawingTitle       { get; set; } = "Plan";
        public string DrawingNumber      { get; set; } = "001";
        public string Scale              { get; set; } = "1:100";
        public string DrawnBy            { get; set; } = "";
        public string Date               { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
        public uint   BackgroundColor    { get; set; } = 0xFF1E1E2E;  // ARGB dark
        public uint   ForegroundColor    { get; set; } = 0xFFFFFFFF;
        public int    DpiResolution      { get; set; } = 150;
    }

    private static (double widthMm, double heightMm) GetPageSize(PageFormat fmt) => fmt switch
    {
        PageFormat.A4_Portrait  => (210, 297),
        PageFormat.A4_Landscape => (297, 210),
        PageFormat.A3_Portrait  => (297, 420),
        PageFormat.A3_Landscape => (420, 297),
        _                       => (297, 210)
    };

    /*
       NE: Ekrana çizili viewport'u WPF PrintDialog üzerinden yazdır
       NEDEN: Kullanıcı "Yazdır" dediğinde seçili yazıcıya A3/A4 gönder
    */
    public bool PrintViewport(Visual viewportElement, PrintOptions options)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return false;

        var (wMm, hMm) = GetPageSize(options.Format);

        dlg.PrintTicket.PageMediaSize = new PageMediaSize(
            options.Format is PageFormat.A3_Landscape or PageFormat.A4_Landscape
                ? PageMediaSizeName.ISOA3 : PageMediaSizeName.ISOA4);
        dlg.PrintTicket.PageOrientation = options.Format is PageFormat.A3_Landscape or PageFormat.A4_Landscape
            ? PageOrientation.Landscape : PageOrientation.Portrait;

        double printW = dlg.PrintableAreaWidth;
        double printH = dlg.PrintableAreaHeight;

        var canvas = BuildPrintCanvas(viewportElement, printW, printH, options);
        dlg.PrintVisual(canvas, options.DrawingTitle);
        return true;
    }

    /*
       NE: SkiaSharp bitmap olarak PNG/PDF oluştur
       NEDEN: Yazıcı olmadan doğrudan dosya dışa aktarımı için
    */
    public void ExportToPng(Visual viewportElement, string outputPath, PrintOptions options)
    {
        var (wMm, hMm) = GetPageSize(options.Format);
        int pxW = (int)(wMm / 25.4 * options.DpiResolution);
        int pxH = (int)(hMm / 25.4 * options.DpiResolution);

        var bmp = RenderToBitmap(viewportElement, pxW, pxH, options);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    // ── YARDIMCI ──────────────────────────────────────────────────────────────

    private static Canvas BuildPrintCanvas(Visual source, double printW, double printH, PrintOptions options)
    {
        var canvas = new Canvas { Width = printW, Height = printH, Background = Brushes.White };

        double titleH = options.PrintTitleBlock ? 50 : 0;
        double drawH  = printH - titleH - 20;
        double drawW  = printW - 20;

        // Viewport içeriğini ölçeklendir
        var vb = new Viewbox
        {
            Width       = drawW,
            Height      = drawH,
            Stretch     = Stretch.Uniform,
            StretchDirection = StretchDirection.Both
        };

        // WPF visual'i VisualBrush ile kopyala
        var sourceCopy = new System.Windows.Shapes.Rectangle
        {
            Width  = drawW,
            Height = drawH,
            Fill   = new VisualBrush(source) { Stretch = Stretch.Uniform }
        };
        Canvas.SetLeft(sourceCopy, 10);
        Canvas.SetTop(sourceCopy, 10);
        canvas.Children.Add(sourceCopy);

        if (options.PrintTitleBlock)
        {
            var title = BuildTitleBlock(printW, printH, options);
            canvas.Children.Add(title);
        }

        return canvas;
    }

    private static UIElement BuildTitleBlock(double pageW, double pageH, PrintOptions options)
    {
        double blockH = 48;
        var border = new Border
        {
            Width           = pageW - 20,
            Height          = blockH,
            BorderBrush     = Brushes.Black,
            BorderThickness = new Thickness(1),
            Background      = new SolidColorBrush(Color.FromRgb(240, 248, 255))
        };

        var grid = new Grid();
        for (int i = 0; i < 4; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddTitleCell(grid, 0, "PROJE", options.ProjectName);
        AddTitleCell(grid, 1, "ÇİZİM", options.DrawingTitle);
        AddTitleCell(grid, 2, $"No: {options.DrawingNumber}  |  Ölçek: {options.Scale}", options.Date);
        AddTitleCell(grid, 3, "AfneyCAD", options.DrawnBy);

        border.Child = grid;
        Canvas.SetLeft(border, 10);
        Canvas.SetTop(border, pageH - blockH - 10);
        return border;
    }

    private static void AddTitleCell(Grid grid, int col, string label, string value)
    {
        var sp = new StackPanel { Margin = new Thickness(4, 2, 4, 2) };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 7, Foreground = Brushes.Gray });
        sp.Children.Add(new TextBlock { Text = value, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black });
        Grid.SetColumn(sp, col);
        grid.Children.Add(sp);
    }

    private static SKBitmap RenderToBitmap(Visual source, int pxW, int pxH, PrintOptions options)
    {
        // WPF Visual → BitmapSource
        var rtb = new RenderTargetBitmap(pxW, pxH, options.DpiResolution, options.DpiResolution, PixelFormats.Pbgra32);
        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            var brush = new VisualBrush(source);
            dc.DrawRectangle(brush, null, new Rect(0, 0, pxW, pxH));
        }
        rtb.Render(drawingVisual);

        // BitmapSource → SKBitmap
        var stride = pxW * 4;
        var pixels = new byte[pxH * stride];
        rtb.CopyPixels(pixels, stride, 0);

        var skBmp = new SKBitmap(pxW, pxH, SKColorType.Bgra8888, SKAlphaType.Premul);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, skBmp.GetPixels(), pixels.Length);
        return skBmp;
    }
}
