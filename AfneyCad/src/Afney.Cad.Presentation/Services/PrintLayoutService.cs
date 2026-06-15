using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Pafta Yerleşim Servisi (PrintLayoutService)
   NEDEN: FINE MEP'te A3/A2/A1 pafta formatı ve antet (title block) otomatik
          yerleşim vardır. AfneyCAD'de yoktu. Bu servis SVG tabanlı çizim
          paftası üretir — antet, ölçek çubuğu, kuzey oku, revizyon tablosu.
*/
public class PrintLayoutService
{
    // ── Pafta Formatı ─────────────────────────────────────────────────────────────

    public enum SheetFormat { A4, A3, A2, A1, A0 }

    private static (int wMm, int hMm) SheetSize(SheetFormat f) => f switch
    {
        SheetFormat.A4 => (297, 210),
        SheetFormat.A3 => (420, 297),
        SheetFormat.A2 => (594, 420),
        SheetFormat.A1 => (841, 594),
        SheetFormat.A0 => (1189, 841),
        _              => (420, 297)
    };

    // ── Antet Veri ────────────────────────────────────────────────────────────────

    public class TitleBlockData
    {
        public string ProjectName    { get; set; } = "";
        public string ProjectNumber  { get; set; } = "";
        public string DrawingTitle   { get; set; } = "";
        public string DrawingNumber  { get; set; } = "";
        public string Scale          { get; set; } = "1:50";
        public string Date           { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
        public string Engineer       { get; set; } = "";
        public string Checker        { get; set; } = "";
        public string CompanyName    { get; set; } = "AfneyCAD";
        public string Phase          { get; set; } = "Uygulama Projesi";
        public string Client         { get; set; } = "";
        public string RevCode        { get; set; } = "A";
        public string Discipline     { get; set; } = "Mekanik Tesisat";
        public SheetFormat Format    { get; set; } = SheetFormat.A3;
    }

    // ── SVG Pafta Oluşturucu ─────────────────────────────────────────────────────

    public static string GenerateLayoutSvg(TitleBlockData tb, string drawingContent = "")
    {
        var (wMm, hMm) = SheetSize(tb.Format);
        double scale = 2.83465;  // mm → px (96dpi → A3 boyutu)
        int W = (int)(wMm * scale);
        int H = (int)(hMm * scale);

        // Antet yüksekliği: A4=30mm, A3=35mm, A2+=40mm
        int tbHmm = tb.Format <= SheetFormat.A3 ? 35 : 40;
        int tbH   = (int)(tbHmm * scale);
        int border = (int)(10 * scale);  // 10mm iç çerçeve

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{W}' height='{H}' ");
        sb.Append($"style='background:white;font-family:Arial' viewBox='0 0 {W} {H}'>");

        // Dış çerçeve (kenar boşluğu)
        sb.Append($"<rect x='{border}' y='{border}' width='{W-2*border}' height='{H-2*border}' fill='none' stroke='#000' stroke-width='2'/>");

        // Çizim alanı (antet üstü)
        int drawAreaY = border;
        int drawAreaH = H - tbH - border - 5;
        sb.Append($"<rect x='{border}' y='{drawAreaY}' width='{W-2*border}' height='{drawAreaH}' fill='none' stroke='#999' stroke-width='0.5' stroke-dasharray='4,4'/>");

        if (!string.IsNullOrEmpty(drawingContent))
            sb.Append($"<g transform='translate({border},{drawAreaY})'>{drawingContent}</g>");
        else
        {
            // Çizim alanı placeholder
            int cx = W / 2, cy = drawAreaY + drawAreaH / 2;
            sb.Append($"<text x='{cx}' y='{cy}' text-anchor='middle' font-size='14' fill='#CCC'>Çizim Alanı — {(W-2*border)/scale:F0} × {drawAreaH/scale:F0} mm</text>");
        }

        // ── ANTET ────────────────────────────────────────────────────────────────

        int anthY = H - tbH - 5;
        int anthW = W - 2 * border;

        // Antet arkaplan
        sb.Append($"<rect x='{border}' y='{anthY}' width='{anthW}' height='{tbH}' fill='#0D1117' stroke='#000' stroke-width='1'/>");

        // Antet bölümleri
        int col1 = (int)(anthW * 0.35);  // Firma bilgileri
        int col2 = (int)(anthW * 0.65);  // Proje bilgileri
        int lx = border;

        // Firm bilgisi solu
        double y1 = anthY + 12 * scale / 2.83465;
        sb.Append(TbText(lx + 10, anthY + 18, 16, "#FFD740", tb.CompanyName, "bold"));
        sb.Append(TbText(lx + 10, anthY + 33, 11, "#90CAF9", "MEP CAD · TS EN · ASHRAE"));
        sb.Append(TbText(lx + 10, anthY + 46, 10, "#888", tb.Discipline));

        // Orta bölüm dikey çizgi
        sb.Append($"<line x1='{lx+col1}' y1='{anthY}' x2='{lx+col1}' y2='{anthY+tbH}' stroke='#333' stroke-width='1'/>");

        // Proje adı ve pafta bilgisi
        int px = lx + col1 + 10;
        sb.Append(TbText(px, anthY + 14, 13, "#FFD740", tb.ProjectName, "bold"));
        sb.Append(TbText(px, anthY + 26, 10, "#ddd", $"Proje No: {tb.ProjectNumber}  |  İşveren: {tb.Client}"));
        sb.Append(TbText(px, anthY + 38, 11, "#90CAF9", tb.DrawingTitle, "bold"));

        // Sağ sütun
        int rx = lx + col2 + 10;
        sb.Append($"<line x1='{lx+col2}' y1='{anthY}' x2='{lx+col2}' y2='{anthY+tbH}' stroke='#333' stroke-width='1'/>");
        sb.Append(TbText(rx, anthY + 10, 10, "#888", "Pafta No:"));
        sb.Append(TbText(rx, anthY + 22, 12, "#FFD740", tb.DrawingNumber, "bold"));
        sb.Append(TbText(rx, anthY + 34, 10, "#888", $"Ölçek: {tb.Scale}  |  Rev: {tb.RevCode}"));
        sb.Append(TbText(rx, anthY + 46, 10, "#888", $"Tarih: {tb.Date}  |  Aşama: {tb.Phase}"));

        // Yatay çizgi alt bölüm
        int hrY = anthY + (int)(tbH * 0.55);
        sb.Append($"<line x1='{lx+col1}' y1='{hrY}' x2='{W-border}' y2='{hrY}' stroke='#333' stroke-width='1'/>");
        sb.Append(TbText(px, hrY + 12, 9, "#888", $"Hazırlayan: {tb.Engineer}  |  Kontrol: {tb.Checker}"));

        // ── Ölçek Çubuğu ─────────────────────────────────────────────────────────
        AddScaleBar(sb, border + 20, anthY - 20, tb.Scale);

        // ── Kuzey Oku ─────────────────────────────────────────────────────────────
        AddNorthArrow(sb, W - border - 40, anthY - 40);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string TbText(int x, int y, int size, string fill, string text, string weight = "normal") =>
        $"<text x='{x}' y='{y}' font-size='{size}' fill='{fill}' font-weight='{weight}'>{System.Security.SecurityElement.Escape(text)}</text>";

    private static void AddScaleBar(StringBuilder sb, int x, int y, string scale)
    {
        // Basit ölçek çubuğu: 50mm = 1:50 ölçekte 1m
        int bw = 80;
        sb.Append($"<rect x='{x}' y='{y}' width='{bw/2}' height='6' fill='#000'/>");
        sb.Append($"<rect x='{x+bw/2}' y='{y}' width='{bw/2}' height='6' fill='white' stroke='#000' stroke-width='0.5'/>");
        sb.Append($"<line x1='{x}' y1='{y}' x2='{x}' y2='{y-5}' stroke='#000' stroke-width='1'/>");
        sb.Append($"<line x1='{x+bw}' y1='{y}' x2='{x+bw}' y2='{y-5}' stroke='#000' stroke-width='1'/>");
        sb.Append($"<text x='{x}' y='{y-7}' font-size='8' fill='#555'>0</text>");
        sb.Append($"<text x='{x+bw-10}' y='{y-7}' font-size='8' fill='#555'>1m ({scale})</text>");
    }

    private static void AddNorthArrow(StringBuilder sb, int cx, int cy)
    {
        sb.Append($"<polygon points='{cx},{cy-18} {cx-8},{cy+8} {cx},{cy+2} {cx+8},{cy+8}' fill='#0D1117' stroke='#555' stroke-width='1'/>");
        sb.Append($"<text x='{cx}' y='{cy-22}' text-anchor='middle' font-size='10' fill='#555' font-weight='bold'>N</text>");
    }

    // ── HTML Pafta ────────────────────────────────────────────────────────────────

    public static string GenerateHtml(TitleBlockData tb, string drawingContent = "")
    {
        string svg = GenerateLayoutSvg(tb, drawingContent);
        var (wMm, hMm) = SheetSize(tb.Format);
        return $"<!DOCTYPE html><html><head><meta charset='utf-8'><title>Pafta — {tb.DrawingTitle}</title>" +
               $"<style>@page{{size:{wMm}mm {hMm}mm;margin:0}}body{{margin:0;background:#666}}</style>" +
               $"</head><body>{svg}</body></html>";
    }

    // ── Dışa Aktar ────────────────────────────────────────────────────────────────

    public static string ExportToFile(TitleBlockData tb, string drawingContent = "")
    {
        string html = GenerateHtml(tb, drawingContent);
        string path = Path.Combine(Path.GetTempPath(),
            $"Pafta_{tb.DrawingNumber.Replace("/","_")}_{DateTime.Now:yyyyMMdd}.html");
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    // ── Antet Şablonları ─────────────────────────────────────────────────────────

    public static TitleBlockData CreateFromProject(string projectName, string drawingTitle,
        string engineer = "", string drawingNo = "M-001")
    {
        return new TitleBlockData
        {
            ProjectName  = projectName,
            DrawingTitle = drawingTitle,
            DrawingNumber = drawingNo,
            Engineer     = engineer,
            Date         = DateTime.Now.ToString("dd.MM.yyyy"),
            Format       = SheetFormat.A3
        };
    }
}
