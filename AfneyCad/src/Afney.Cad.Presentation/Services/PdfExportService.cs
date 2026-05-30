using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using SkiaSharp;

namespace Afney.Cad.Presentation.Services;

/*
   NE: PDF Rapor Servisi (PdfExportService)
   NEDEN: Proje metraj, kolon özeti ve sistem bilgilerini TS standartlarına uygun tek sayfalık PDF raporuna dökmek için.
   NASIL: SkiaSharp SKDocument.CreatePdf → her sayfa A4 (595 × 842 pt).
*/
public class PdfExportService
{
    private readonly CadDatabase _database;
    private const float PageW = 595f;
    private const float PageH = 842f;
    private const float Margin = 40f;

    public PdfExportService(CadDatabase database) { _database = database; }

    // ── Antet Bilgileri ──────────────────────────────────────────────────────────

    public class TitleBlockInfo
    {
        public string FirmaAdi       { get; set; } = "";
        public string MuhendisAdi    { get; set; } = "";
        public string MuhendisUnvan  { get; set; } = "İnş. Müh.";
        public string ProjeNo        { get; set; } = "";
        public string ProjeAdi       { get; set; } = "";
        public string Revizyon       { get; set; } = "Rev.0";
        public string Tarih          { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
        public string Adres          { get; set; } = "";
        public string OnayCizdiren   { get; set; } = "";
        public string OnayKontrolEden { get; set; } = "";
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public string ExportReport(string projectName = "AfneyCAD Projesi", TitleBlockInfo? titleBlock = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"AfneyCAD_Rapor_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        using var stream = new SKFileWStream(path);
        using var doc    = SKDocument.CreatePdf(stream);

        DrawReportPage(doc, projectName, titleBlock);
        DrawBoqPage(doc, titleBlock);

        doc.Close();
        return path;
    }

    // ── Sayfa 1: Sistem Özeti ────────────────────────────────────────────────────

    private void DrawReportPage(SKDocument doc, string projectName, TitleBlockInfo? tb)
    {
        using var canvas = doc.BeginPage(PageW, PageH);

        float y = Margin;
        y = DrawHeader(canvas, projectName, y);
        if (tb != null) y = DrawTitleBlock(canvas, tb, y);
        y += 14;

        // Sistem Özet Tablosu
        var pipes    = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
        var valves   = _database.GetAllEntities().OfType<ValveEntity>().ToList();

        double totalPipeLen = pipes.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0;

        y = DrawSectionTitle(canvas, "SİSTEM ÖZETİ", y);
        y = DrawKV(canvas, "Toplam Boru Uzunluğu", $"{totalPipeLen:F2} m", y);
        y = DrawKV(canvas, "Toplam Vitrifiye / Cihaz", $"{fixtures.Count} adet", y);
        y = DrawKV(canvas, "Toplam Vana", $"{valves.Count} adet", y);
        y += 10;

        // Sistem Tipi Dağılımı
        y = DrawSectionTitle(canvas, "SİSTEM TİPİ DAĞILIMI (Boru)", y);
        foreach (var grp in pipes.GroupBy(p => p.SystemType).OrderBy(g => g.Key))
        {
            double len = grp.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0;
            y = DrawKV(canvas, SystemTypeName(grp.Key), $"{len:F2} m", y);
        }
        y += 10;

        // Çap Dağılımı
        y = DrawSectionTitle(canvas, "ÇAP DAĞILIMI (DN)", y);
        foreach (var grp in pipes.GroupBy(p => (int)p.InnerDiameter).OrderBy(g => g.Key))
        {
            double len = grp.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0;
            y = DrawKV(canvas, $"DN {grp.Key}", $"{len:F2} m · {grp.Count()} segment", y);
        }
        y += 10;

        // Vitrifiye Dağılımı
        if (fixtures.Count > 0)
        {
            y = DrawSectionTitle(canvas, "VİTRİFİYE / CİHAZ DAĞILIMI", y);
            foreach (var grp in fixtures.GroupBy(f => f.FixtureType).OrderBy(g => g.Key.ToString()))
                y = DrawKV(canvas, grp.Key.ToString(), $"{grp.Count()} adet", y);
        }

        DrawFooter(canvas, 1);
        doc.EndPage();
    }

    // ── Sayfa 2: Metraj Cetveli ──────────────────────────────────────────────────

    private void DrawBoqPage(SKDocument doc, TitleBlockInfo? tb = null)
    {
        using var canvas = doc.BeginPage(PageW, PageH);

        float y = Margin;
        y = DrawHeader(canvas, "Metraj Cetveli", y);
        if (tb != null) y = DrawTitleBlock(canvas, tb, y);
        y += 10;

        // Tablo Başlıkları
        float[] cols = [Margin, 200, 370, 460, PageW - Margin];
        y = DrawTableHeader(canvas, y, cols, ["Poz No", "Tarif", "Birim", "Miktar"]);

        var pipes = _database.GetAllEntities().OfType<PipeEntity>()
            .GroupBy(p => new { p.SystemType, DN = (int)p.InnerDiameter })
            .OrderBy(g => g.Key.SystemType).ThenBy(g => g.Key.DN);

        int poz = 1;
        bool alt = false;
        foreach (var grp in pipes)
        {
            double len = grp.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0;
            string tarif = $"{SystemTypeName(grp.Key.SystemType)} Boru DN{grp.Key.DN}";
            y = DrawTableRow(canvas, y, cols,
                [$"{poz++:D2}", tarif, "m", $"{len:F2}"], alt);
            alt = !alt;
        }

        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType).OrderBy(g => g.Key.ToString());

        foreach (var grp in fixtures)
        {
            y = DrawTableRow(canvas, y, cols,
                [$"{poz++:D2}", grp.Key.ToString(), "adet", $"{grp.Count()}"], alt);
            alt = !alt;
        }

        DrawFooter(canvas, 2);
        doc.EndPage();
    }

    // ── Çizim Yardımcıları ───────────────────────────────────────────────────────

    private static float DrawHeader(SKCanvas c, string title, float y)
    {
        using var bgPaint = new SKPaint { Color = new SKColor(13, 48, 96), IsAntialias = true };
        c.DrawRect(0, y, PageW, 50, bgPaint);

        using var titlePaint = new SKPaint
        {
            Color = new SKColor(144, 202, 249),
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        using var titleFont = new SKFont(titlePaint.Typeface, 16);
        c.DrawText("AfneyCAD — " + title, Margin, y + 22, titleFont, titlePaint);

        using var subPaint = new SKPaint { Color = new SKColor(180, 180, 180), IsAntialias = true };
        using var subFont  = new SKFont(subPaint.Typeface, 9);
        c.DrawText($"TS EN 806 / TS 1258 · {DateTime.Now:dd.MM.yyyy HH:mm}", Margin, y + 38, subFont, subPaint);

        return y + 58;
    }

    private static float DrawSectionTitle(SKCanvas c, string text, float y)
    {
        using var bg = new SKPaint { Color = new SKColor(37, 37, 53), IsAntialias = true };
        c.DrawRect(Margin, y, PageW - 2 * Margin, 16, bg);
        using var paint = new SKPaint { Color = new SKColor(144, 202, 249), IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        using var font = new SKFont(paint.Typeface, 9);
        c.DrawText(text, Margin + 4, y + 12, font, paint);
        return y + 20;
    }

    private static float DrawKV(SKCanvas c, string key, string val, float y)
    {
        using var kp = new SKPaint { Color = new SKColor(180, 180, 180), IsAntialias = true };
        using var vp = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var kf = new SKFont(kp.Typeface, 9);
        using var vf = new SKFont(vp.Typeface, 9);
        c.DrawText(key, Margin + 4, y + 9, kf, kp);
        c.DrawText(val, Margin + 200, y + 9, vf, vp);
        return y + 14;
    }

    private static float DrawTableHeader(SKCanvas c, float y, float[] cols, string[] labels)
    {
        using var bg = new SKPaint { Color = new SKColor(21, 101, 192), IsAntialias = true };
        c.DrawRect(cols[0], y, cols[^1] - cols[0], 18, bg);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        using var font = new SKFont(paint.Typeface, 8);
        for (int i = 0; i < labels.Length; i++)
            c.DrawText(labels[i], cols[i] + 3, y + 13, font, paint);
        return y + 20;
    }

    private static float DrawTableRow(SKCanvas c, float y, float[] cols, string[] cells, bool alt)
    {
        if (alt)
        {
            using var bg = new SKPaint { Color = new SKColor(42, 42, 62), IsAntialias = true };
            c.DrawRect(cols[0], y, cols[^1] - cols[0], 16, bg);
        }
        using var paint = new SKPaint { Color = new SKColor(220, 220, 220), IsAntialias = true };
        using var font  = new SKFont(paint.Typeface, 8);
        for (int i = 0; i < cells.Length; i++)
            c.DrawText(cells[i], cols[i] + 3, y + 12, font, paint);
        return y + 16;
    }

    private static float DrawTitleBlock(SKCanvas c, TitleBlockInfo tb, float y)
    {
        // İnce kenarlıklı antet kutusu
        float bx = Margin, bw = PageW - 2 * Margin, bh = 56f;
        using var border = new SKPaint { Color = new SKColor(80, 120, 180), StrokeWidth = 0.8f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        using var bg     = new SKPaint { Color = new SKColor(18, 30, 50), IsAntialias = true };
        c.DrawRect(bx, y, bw, bh, bg);
        c.DrawRect(bx, y, bw, bh, border);

        // Sol sütun: Firma + Mühendis
        using var boldPaint = new SKPaint { Color = new SKColor(144, 202, 249), IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        using var normPaint = new SKPaint { Color = new SKColor(200, 210, 220), IsAntialias = true };
        using var bf = new SKFont(boldPaint.Typeface, 9);
        using var nf = new SKFont(normPaint.Typeface, 8);

        c.DrawText(tb.FirmaAdi,    bx + 6, y + 14, bf, boldPaint);
        c.DrawText($"Müh: {tb.MuhendisAdi} ({tb.MuhendisUnvan})", bx + 6, y + 26, nf, normPaint);
        c.DrawText($"Adres: {tb.Adres}",    bx + 6, y + 37, nf, normPaint);

        // Orta sütun: Proje
        float mx = bx + bw * 0.42f;
        c.DrawText(tb.ProjeAdi,   mx, y + 14, bf, boldPaint);
        c.DrawText($"Proje No: {tb.ProjeNo}", mx, y + 26, nf, normPaint);

        // Sağ sütun: Rev + Tarih + İmza
        float rx = bx + bw * 0.72f;
        using var divPaint = new SKPaint { Color = new SKColor(80, 120, 180), StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };
        c.DrawLine(rx - 4, y, rx - 4, y + bh, divPaint);
        c.DrawText($"Rev: {tb.Revizyon}",        rx, y + 12, nf, normPaint);
        c.DrawText($"Tarih: {tb.Tarih}",         rx, y + 23, nf, normPaint);
        c.DrawText($"Çizen: {tb.OnayCizdiren}",  rx, y + 34, nf, normPaint);
        c.DrawText($"Kont.: {tb.OnayKontrolEden}", rx, y + 45, nf, normPaint);

        return y + bh + 8;
    }

    private static void DrawFooter(SKCanvas c, int page)
    {
        using var line = new SKPaint { Color = new SKColor(80, 80, 100), StrokeWidth = 0.5f, IsAntialias = true };
        c.DrawLine(Margin, PageH - 25, PageW - Margin, PageH - 25, line);
        using var paint = new SKPaint { Color = new SKColor(120, 120, 140), IsAntialias = true };
        using var font  = new SKFont(paint.Typeface, 7);
        c.DrawText("AfneyCAD — Mekanik Tesisat CAD Yazılımı", Margin, PageH - 12, font, paint);
        c.DrawText($"Sayfa {page}", PageW - Margin - 30, PageH - 12, font, paint);
    }

    private static string SystemTypeName(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater  => "Sıcak Su",
        MechanicalSystemType.WasteWater        => "Pis Su",
        MechanicalSystemType.RainWater         => "Yağmur Suyu",
        MechanicalSystemType.FireProtection    => "Yangın",
        MechanicalSystemType.Gas               => "Doğalgaz",
        MechanicalSystemType.Ventilation       => "Havalandırma",
        _                                      => t.ToString()
    };
}
