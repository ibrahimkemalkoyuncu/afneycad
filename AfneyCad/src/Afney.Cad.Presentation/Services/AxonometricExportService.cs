using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Presentation.Services;

/*
   NE: 3D Axonometrik (İzometrik) Boru Şeması Export Servisi (AxonometricExportService)
   NEDEN: FINE MEP'in "AxoModel" özelliğinin karşılığı — mühendis projeyi şantiye için
          ölçülü 3D izometrik şema olarak teslim edebilsin diye.

   PROJEKSIYON:
   - Kabinetik axonometri: X ekseni 30° sağa, Y ekseni 150° sola, Z ekseni dikey.
   - Formül:
       screen_x = (world_x - world_y) * cos30  (cos30 = 0.866)
       screen_y = -(world_x + world_y) * sin30 - world_z  (sin30 = 0.5)
   - Her kat arasında Z_FLOOR_HEIGHT_M metre yükseklik eklenir.
   - Boru kalınlığı DN ile orantılı.
   - Sistem tipi renk şeması (FINE MEP uyumlu).

   ÇIKTI:
   - Tek dosya, bağımsız HTML — SVG axonometrik çizim + etiketler.
   - Pan + zoom JS.
   - DN / sistem etiketi her boru üstünde.
   - Kat kesit çizgileri.
*/
public class AxonometricExportService
{
    private const double COS30           = 0.866025;
    private const double SIN30           = 0.5;
    private const double Z_FLOOR_HEIGHT  = 3.0;    // m — kat yüksekliği varsayılan
    private const double SCALE           = 80.0;   // piksel/m
    private const double SVG_W           = 1400;
    private const double SVG_H           = 900;
    private const double LABEL_THRESHOLD = 0.5;    // m — bu uzunluktan kısa borular etiketlenmez

    // ── Axonometrik projeksiyon ──────────────────────────────────────────────────

    private static (double sx, double sy) Project(double wx, double wy, double wz)
    {
        double sx =  (wx - wy) * COS30 * SCALE;
        double sy = -((wx + wy) * SIN30 + wz) * SCALE;
        return (sx, sy);
    }

    // ── Ana Export Metodu ────────────────────────────────────────────────────────

    public string Export(CadDatabase database, string projectName, int totalFloors = 1,
                          double floorHeightM = Z_FLOOR_HEIGHT)
    {
        var entities = database.GetAllEntities().ToList();
        var pipes    = entities.OfType<PipeEntity>().ToList();
        var valves   = entities.OfType<ValveEntity>().ToList();
        var fixtures = entities.OfType<SanitaryFixtureEntity>().ToList();

        if (pipes.Count == 0 && fixtures.Count == 0)
            return BuildEmptyHtml(projectName);

        // SVG içeriği
        var svg = new StringBuilder();

        // ── Kat kesit çizgileri ────────────────────────────────────────────────
        svg.AppendLine($"<!-- Kat Çizgileri -->");
        for (int fl = 0; fl <= totalFloors; fl++)
        {
            double wz = fl * floorHeightM;
            // Kat düzlemi: Z sabit, X ve Y taranır
            double x1w = -10, y1w = -10, x2w = 30, y2w = 30;
            var (lx1, ly1) = Project(x1w, y1w, wz);
            var (lx2, ly2) = Project(x2w, y2w, wz);
            double ox = SVG_W / 2; double oy = SVG_H * 0.6;
            svg.AppendLine($"<line x1=\"{lx1 + ox:F1}\" y1=\"{ly1 + oy:F1}\" " +
                           $"x2=\"{lx2 + ox:F1}\" y2=\"{ly2 + oy:F1}\" " +
                           $"stroke=\"#3A3A5A\" stroke-width=\"0.5\" stroke-dasharray=\"6,4\"/>");
            svg.AppendLine($"<text x=\"{lx1 + ox - 50:F1}\" y=\"{ly1 + oy:F1}\" " +
                           $"fill=\"#5566AA\" font-size=\"10\" font-family=\"monospace\">{fl}. KAT ({wz:F1} m)</text>");
        }

        // ── Borular ───────────────────────────────────────────────────────────
        svg.AppendLine($"<!-- Borular ({pipes.Count} adet) -->");
        foreach (var pipe in pipes)
        {
            string color    = SystemColor(pipe.SystemType);
            double strokeW  = Math.Clamp(pipe.InnerDiameter / 25.0 * 3.0, 1.5, 8.0);
            double ox       = SVG_W / 2;
            double oy       = SVG_H * 0.6;

            var (sx1, sy1) = Project(pipe.StartPoint.X / 1000.0,
                                      pipe.StartPoint.Y / 1000.0,
                                      pipe.StartPoint.Z / 1000.0);
            var (sx2, sy2) = Project(pipe.EndPoint.X / 1000.0,
                                      pipe.EndPoint.Y / 1000.0,
                                      pipe.EndPoint.Z / 1000.0);

            svg.AppendLine(
                $"<line x1=\"{sx1 + ox:F1}\" y1=\"{sy1 + oy:F1}\" " +
                $"x2=\"{sx2 + ox:F1}\" y2=\"{sy2 + oy:F1}\" " +
                $"stroke=\"{color}\" stroke-width=\"{strokeW:F1}\" stroke-linecap=\"round\">" +
                $"<title>DN{pipe.InnerDiameter:F0} · {pipe.SystemType} · L={pipe.Length / 1000:F2} m</title>" +
                $"</line>");

            // DN etiketi (yeterince uzun boruya orta nokta)
            if (pipe.Length / 1000.0 > LABEL_THRESHOLD)
            {
                double mx = (sx1 + sx2) / 2 + ox;
                double my = (sy1 + sy2) / 2 + oy - 4;
                svg.AppendLine(
                    $"<text x=\"{mx:F1}\" y=\"{my:F1}\" fill=\"{color}\" " +
                    $"font-size=\"8\" font-family=\"monospace\" text-anchor=\"middle\" " +
                    $"opacity=\"0.9\">DN{pipe.InnerDiameter:F0}</text>");
            }
        }

        // ── Vanalar ───────────────────────────────────────────────────────────
        svg.AppendLine($"<!-- Vanalar ({valves.Count} adet) -->");
        foreach (var valve in valves)
        {
            var bb   = valve.GetBoundingBox();
            double cx = (bb.Min.X + bb.Max.X) / 2000.0;
            double cy = (bb.Min.Y + bb.Max.Y) / 2000.0;
            double cz = (bb.Min.Z + bb.Max.Z) / 2000.0;
            var (sx, sy) = Project(cx, cy, cz);
            double ox = SVG_W / 2;
            double oy = SVG_H * 0.6;

            // Vana: küçük kare sembol
            double r = 4;
            svg.AppendLine(
                $"<rect x=\"{sx + ox - r:F1}\" y=\"{sy + oy - r:F1}\" " +
                $"width=\"{r * 2:F0}\" height=\"{r * 2:F0}\" " +
                $"fill=\"#E91E63\" stroke=\"#111\" stroke-width=\"1\" rx=\"1\">" +
                $"<title>Vana — {valve.EntityType}</title></rect>");
        }

        // ── Armatürler ────────────────────────────────────────────────────────
        svg.AppendLine($"<!-- Armatürler ({fixtures.Count} adet) -->");
        foreach (var fix in fixtures)
        {
            var bb   = fix.GetBoundingBox();
            double cx = (bb.Min.X + bb.Max.X) / 2000.0;
            double cy = (bb.Min.Y + bb.Max.Y) / 2000.0;
            double cz = (bb.Min.Z + bb.Max.Z) / 2000.0;
            var (sx, sy) = Project(cx, cy, cz);
            double ox = SVG_W / 2;
            double oy = SVG_H * 0.6;

            svg.AppendLine(
                $"<circle cx=\"{sx + ox:F1}\" cy=\"{sy + oy:F1}\" r=\"5\" " +
                $"fill=\"#FFC107\" stroke=\"#333\" stroke-width=\"1\">" +
                $"<title>{fix.EntityType} — {fix.SystemType}</title></circle>");
        }

        // ── Eksen Gösterge (UCS oku) ──────────────────────────────────────────
        double axO = 80; double ayO = 80;
        double axLen = 40;
        // X ekseni
        svg.AppendLine($"<line x1=\"{axO}\" y1=\"{ayO}\" " +
                       $"x2=\"{axO + axLen * COS30:F1}\" y2=\"{ayO - axLen * SIN30:F1}\" " +
                       "stroke=\"#FF5252\" stroke-width=\"2\"/>");
        svg.AppendLine($"<text x=\"{axO + axLen * COS30 + 4:F1}\" y=\"{ayO - axLen * SIN30:F1}\" " +
                       "fill=\"#FF5252\" font-size=\"11\" font-family=\"monospace\">X</text>");
        // Y ekseni
        svg.AppendLine($"<line x1=\"{axO}\" y1=\"{ayO}\" " +
                       $"x2=\"{axO - axLen * COS30:F1}\" y2=\"{ayO - axLen * SIN30:F1}\" " +
                       "stroke=\"#69F0AE\" stroke-width=\"2\"/>");
        svg.AppendLine($"<text x=\"{axO - axLen * COS30 - 14:F1}\" y=\"{ayO - axLen * SIN30:F1}\" " +
                       "fill=\"#69F0AE\" font-size=\"11\" font-family=\"monospace\">Y</text>");
        // Z ekseni
        svg.AppendLine($"<line x1=\"{axO}\" y1=\"{ayO}\" " +
                       $"x2=\"{axO}\" y2=\"{ayO - axLen:F1}\" " +
                       "stroke=\"#40C4FF\" stroke-width=\"2\"/>");
        svg.AppendLine($"<text x=\"{axO + 4}\" y=\"{ayO - axLen - 2:F1}\" " +
                       "fill=\"#40C4FF\" font-size=\"11\" font-family=\"monospace\">Z</text>");

        // ── Özet istatistik ───────────────────────────────────────────────────
        double totalLenM = pipes.Sum(p => p.Length) / 1000.0;
        var sysCounts = pipes.GroupBy(p => p.SystemType)
                             .Select(g => $"{g.Key}: {g.Count()} boru ({g.Sum(p => p.Length) / 1000:F1} m)")
                             .ToList();
        string legend = string.Join("", SystemColorMap.Select(kv =>
            $"<span style='display:inline-block;width:14px;height:14px;background:{kv.Value};" +
            $"border-radius:2px;margin-right:5px;vertical-align:middle'></span>{kv.Key} &nbsp; "));

        return $@"<!DOCTYPE html>
<html lang=""tr"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=6.0"">
<title>AfneyCAD — {projectName} — Axonometrik</title>
<style>
  body {{margin:0;background:#0D1117;color:#eee;font-family:system-ui,sans-serif}}
  header {{background:#0A2040;padding:10px 16px;display:flex;align-items:center;justify-content:space-between}}
  header h1 {{margin:0;font-size:1.1rem;color:#90CAF9}}
  header small {{color:#5577AA;font-size:.8rem}}
  #svgwrap {{overflow:auto;max-height:calc(100vh - 120px);background:#0D1117;cursor:grab}}
  #svgwrap:active {{cursor:grabbing}}
  svg {{display:block}}
  .stats {{padding:10px 16px;background:#131820;font-size:.82rem;line-height:1.8}}
  .legend {{padding:8px 16px;background:#0A1020;font-size:.78rem}}
  .badge {{display:inline-block;background:#1B3A1B;color:#A5D6A7;padding:2px 8px;border-radius:10px;
           margin:2px;font-size:.75rem}}
</style>
</head>
<body>
<header>
  <h1>📐 AfneyCAD — {projectName} — Axonometrik Boru Şeması</h1>
  <small>Projeksiyon: Kabinetik Axonometri · {pipes.Count} boru · {valves.Count} vana · {fixtures.Count} armatür · {totalFloors} kat</small>
</header>
<div id=""svgwrap"">
  <svg width=""{SVG_W:F0}"" height=""{SVG_H:F0}"" xmlns=""http://www.w3.org/2000/svg"">
    <rect width=""100%"" height=""100%"" fill=""#0D1117""/>
    {svg}
  </svg>
</div>
<div class=""stats"">
  <b>Özet:</b> Toplam boru: {totalLenM:F1} m &nbsp;|&nbsp;
  {string.Join(" &nbsp;|&nbsp; ", sysCounts)}
</div>
<div class=""legend"">
  <b>Renk Şeması:</b> &nbsp; {legend}
  &nbsp;&nbsp;
  <span class=""badge"">🔴 Vana</span>
  <span class=""badge"" style=""background:#2A2A00;color:#FFC107"">● Armatür</span>
  <span class=""badge"" style=""background:#1A2A3A;color:#7799BB"">- - Kat Kesit</span>
</div>
<script>
(function(){{
  var el=document.getElementById('svgwrap'),sx=0,sy=0,isPan=false;
  el.addEventListener('mousedown',function(e){{isPan=true;sx=e.clientX+el.scrollLeft;sy=e.clientY+el.scrollTop}});
  el.addEventListener('mousemove',function(e){{if(!isPan)return;el.scrollLeft=sx-e.clientX;el.scrollTop=sy-e.clientY}});
  ['mouseup','mouseleave'].forEach(function(ev){{el.addEventListener(ev,function(){{isPan=false}})}});
}})();
</script>
</body>
</html>";
    }

    // ── Sistem Renk Haritası ──────────────────────────────────────────────────────

    private static readonly Dictionary<MechanicalSystemType, string> SystemColorMap = new()
    {
        [MechanicalSystemType.DomesticColdWater] = "#2196F3",
        [MechanicalSystemType.DomesticHotWater]  = "#F44336",
        [MechanicalSystemType.WasteWater]          = "#795548",
        [MechanicalSystemType.RainWater]           = "#00BCD4",
        [MechanicalSystemType.Gas]                 = "#FF9800",
        [MechanicalSystemType.FireProtection]      = "#D32F2F",
        [MechanicalSystemType.Ventilation]         = "#4CAF50",
    };

    private static string SystemColor(MechanicalSystemType t)
        => SystemColorMap.TryGetValue(t, out var c) ? c : "#9E9E9E";

    private static string BuildEmptyHtml(string projectName) =>
        $"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>{projectName} — Axo</title></head>" +
        "<body style='background:#0D1117;color:#eee;font-family:sans-serif;padding:40px'>" +
        "<h2>📐 AfneyCAD — Boş Proje</h2><p>Axonometrik çizim için boru elemanı bulunamadı.</p>" +
        "</body></html>";
}
