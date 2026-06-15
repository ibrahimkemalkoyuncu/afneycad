using System;
using System.Collections.Generic;
using System.Text;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Pompa Q/H Eğrisi Grafik Servisi (PumpCurveChartService)
   NEDEN: ManufacturerCatalogDialog'da pompa seçiminde Q/H eğrisini görsel
          göstermek için. SkiaSharp gereksinimsiz — SVG çıktı üretir,
          WPF WebBrowser veya doğrudan dosyaya yazılarak gösterilir.
          FINE MEP'te grafik Q/H eğrisi yoktur.
*/
public class PumpCurveChartService
{
    private const int W = 600, H = 400;
    private const int PL = 60, PR = 20, PT = 30, PB = 50;  // padding

    // ── SVG Q/H Eğrisi ──────────────────────────────────────────────────────────

    public static string GenerateSvg(
        ManufacturerCatalogService.PumpModel pump,
        double workingFlowM3h  = -1,
        double workingHeadM    = -1)
    {
        var pts = pump.CurvePoints;
        if (pts == null || pts.Count < 2) return "<svg><!-- Eğri verisi yok --></svg>";

        double maxQ = 0, maxH = 0;
        foreach (var p in pts) { if (p.Q > maxQ) maxQ = p.Q; if (p.H > maxH) maxH = p.H; }
        maxQ *= 1.15; maxH *= 1.15;
        if (maxQ <= 0) maxQ = 10; if (maxH <= 0) maxH = 10;

        int cw = W - PL - PR;
        int ch = H - PT - PB;

        double Sx(double q) => PL + q / maxQ * cw;
        double Sy(double h) => PT + ch - h / maxH * ch;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{W}' height='{H}' style='background:#0D1117;font-family:Arial'>");

        // ── Izgara ────────────────────────────────────────────────────────────
        sb.Append($"<line x1='{PL}' y1='{PT}' x2='{PL}' y2='{PT+ch}' stroke='#444' stroke-width='1'/>");
        sb.Append($"<line x1='{PL}' y1='{PT+ch}' x2='{PL+cw}' y2='{PT+ch}' stroke='#444' stroke-width='1'/>");

        int gridQ = 5, gridH = 5;
        for (int i = 1; i <= gridQ; i++)
        {
            double x = Sx(maxQ * i / gridQ);
            sb.Append($"<line x1='{x:F0}' y1='{PT}' x2='{x:F0}' y2='{PT+ch}' stroke='#222' stroke-width='1' stroke-dasharray='3,3'/>");
            sb.Append($"<text x='{x:F0}' y='{PT+ch+15}' text-anchor='middle' font-size='10' fill='#888'>{maxQ * i / gridQ:F1}</text>");
        }
        for (int i = 1; i <= gridH; i++)
        {
            double y = Sy(maxH * i / gridH);
            sb.Append($"<line x1='{PL}' y1='{y:F0}' x2='{PL+cw}' y2='{y:F0}' stroke='#222' stroke-width='1' stroke-dasharray='3,3'/>");
            sb.Append($"<text x='{PL-8}' y='{y+4:F0}' text-anchor='end' font-size='10' fill='#888'>{maxH * i / gridH:F0}</text>");
        }

        // ── Eksen etiketleri ─────────────────────────────────────────────────
        sb.Append($"<text x='{PL + cw / 2}' y='{H - 5}' text-anchor='middle' font-size='11' fill='#90CAF9'>Q (m³/sa)</text>");
        sb.Append($"<text x='14' y='{PT + ch / 2}' text-anchor='middle' font-size='11' fill='#90CAF9' transform='rotate(-90,14,{PT + ch / 2})'>H (m)</text>");

        // ── Başlık ────────────────────────────────────────────────────────────
        sb.Append($"<text x='{W / 2}' y='20' text-anchor='middle' font-size='12' font-weight='bold' fill='#FFD740'>{pump.ModelName} — Q/H Karakteristik Eğrisi</text>");

        // ── Q/H Eğrisi ───────────────────────────────────────────────────────
        var path = new StringBuilder("M");
        for (int i = 0; i < pts.Count; i++)
        {
            double x = Sx(pts[i].Q);
            double y = Sy(pts[i].H);
            path.Append(i == 0 ? $"{x:F1},{y:F1}" : $" L{x:F1},{y:F1}");
        }
        sb.Append($"<path d='{path}' fill='none' stroke='#40C4FF' stroke-width='2.5' stroke-linejoin='round'/>");

        // Eğri noktaları
        foreach (var p in pts)
        {
            double x = Sx(p.Q), y = Sy(p.H);
            sb.Append($"<circle cx='{x:F1}' cy='{y:F1}' r='4' fill='#40C4FF'/>");
        }

        // ── Çalışma Noktası ──────────────────────────────────────────────────
        if (workingFlowM3h >= 0 && workingHeadM >= 0)
        {
            double wx = Sx(workingFlowM3h), wy = Sy(workingHeadM);
            sb.Append($"<line x1='{wx:F0}' y1='{PT}' x2='{wx:F0}' y2='{PT+ch}' stroke='#FF5252' stroke-width='1' stroke-dasharray='4,3'/>");
            sb.Append($"<line x1='{PL}' y1='{wy:F0}' x2='{PL+cw}' y2='{wy:F0}' stroke='#FF5252' stroke-width='1' stroke-dasharray='4,3'/>");
            sb.Append($"<circle cx='{wx:F1}' cy='{wy:F1}' r='7' fill='#FF5252' stroke='white' stroke-width='1.5'/>");
            sb.Append($"<text x='{wx+10:F0}' y='{wy-8:F0}' font-size='10' fill='#FF5252'>Q={workingFlowM3h:F1} m³/h · H={workingHeadM:F1} m</text>");
        }

        // ── Pompa bilgileri ────────────────────────────────────────────────────
        sb.Append($"<text x='{PL+cw-5}' y='{PT+20}' text-anchor='end' font-size='10' fill='#A5D6A7'>P={pump.NomPowerKw:F2} kW · η={pump.MaxEffPct:F0}%</text>");
        sb.Append($"<text x='{PL+cw-5}' y='{PT+35}' text-anchor='end' font-size='10' fill='#A5D6A7'>{pump.Manufacturer} · DN{pump.ConnectionDN}</text>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    // ── HTML Sarmalayıcı ─────────────────────────────────────────────────────────

    public static string GenerateHtml(
        ManufacturerCatalogService.PumpModel pump,
        double workingFlowM3h = -1, double workingHeadM = -1)
    {
        string svg = GenerateSvg(pump, workingFlowM3h, workingHeadM);
        return $"<!DOCTYPE html><html><head><meta charset='utf-8'></head>" +
               $"<body style='margin:0;background:#0D1117'>{svg}</body></html>";
    }
}
