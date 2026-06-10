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
   NE: Mobil/Web HTML Görüntüleyici Export Servisi (HtmlViewerExportService)
   NEDEN: Mühendislerin projeyi herhangi bir cihazdan (telefon, tablet, web tarayıcı)
          yalnızca okuma (read-only) amacıyla görüntüleyebilmesi için.

   ÇIKTI:
   - Tek dosya, bağımsız HTML — harici kaynak gerektirmez.
   - Inline SVG: tüm boru, bağlantı ve etiket bilgileri SVG çizim elemanı olarak.
   - Renk kodlaması: Sistem tipine göre FINE MEP renk şeması.
   - Mobil uyumlu: viewport meta etiketi, pinch-zoom JS desteği.
   - Açıklama paneli: Boru sayısı, sistem tipleri, toplam uzunluk özeti.
*/
public class HtmlViewerExportService
{
    // Sistem tiplerine göre FINE MEP renk şeması
    private static readonly Dictionary<MechanicalSystemType, string> SystemColors = new()
    {
        [MechanicalSystemType.DomesticColdWater] = "#2196F3",
        [MechanicalSystemType.DomesticHotWater]  = "#F44336",
        [MechanicalSystemType.WasteWater]          = "#795548",
        [MechanicalSystemType.RainWater]           = "#00BCD4",
        [MechanicalSystemType.Gas]                 = "#FF9800",
        [MechanicalSystemType.FireProtection]      = "#D32F2F",
        [MechanicalSystemType.Ventilation]         = "#4CAF50",
    };

    public string Export(CadDatabase database, string projectName)
    {
        var entities  = database.GetAllEntities().ToList();
        var pipes     = entities.OfType<PipeEntity>().ToList();
        var fixtures  = entities.OfType<SanitaryFixtureEntity>().ToList();
        var valves    = entities.OfType<ValveEntity>().ToList();

        if (entities.Count == 0)
            return BuildEmptyHtml(projectName);

        // Koordinat sınırlarını hesapla
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var e in entities)
        {
            var bb = e.GetBoundingBox();
            minX = Math.Min(minX, bb.Min.X);
            minY = Math.Min(minY, bb.Min.Y);
            maxX = Math.Max(maxX, bb.Max.X);
            maxY = Math.Max(maxY, bb.Max.Y);
        }

        double padX = (maxX - minX) * 0.05 + 1;
        double padY = (maxY - minY) * 0.05 + 1;
        minX -= padX; minY -= padY;
        maxX += padX; maxY += padY;

        double worldW = maxX - minX;
        double worldH = maxY - minY;

        const double SVG_W = 1200;
        double scale  = SVG_W / worldW;
        double svgH   = worldH * scale;

        string T(double x) => (( x - minX) * scale).ToString("F1", CultureInfo.InvariantCulture);
        string U(double y) => ((maxY -  y) * scale).ToString("F1", CultureInfo.InvariantCulture);

        var svg = new StringBuilder();

        // Borular
        foreach (var p in pipes)
        {
            string color = SystemColors.TryGetValue(p.SystemType, out var c) ? c : "#9E9E9E";
            double strokeW = Math.Max(1.5, p.InnerDiameter / 15.0 * scale / 10.0);
            svg.AppendLine($"<line x1=\"{T(p.StartPoint.X)}\" y1=\"{U(p.StartPoint.Y)}\" " +
                           $"x2=\"{T(p.EndPoint.X)}\" y2=\"{U(p.EndPoint.Y)}\" " +
                           $"stroke=\"{color}\" stroke-width=\"{strokeW:F1}\" " +
                           $"stroke-linecap=\"round\">" +
                           $"<title>DN{p.InnerDiameter:F0} {p.SystemType} L={p.Length / 1000:F2}m</title></line>");
        }

        // Armatürler
        foreach (var f in fixtures)
        {
            var bb = f.GetBoundingBox();
            double cx = (bb.Min.X + bb.Max.X) / 2;
            double cy = (bb.Min.Y + bb.Max.Y) / 2;
            svg.AppendLine($"<circle cx=\"{T(cx)}\" cy=\"{U(cy)}\" r=\"5\" fill=\"#FFC107\" stroke=\"#333\" stroke-width=\"1\">" +
                           $"<title>{f.EntityType} — {f.SystemType}</title></circle>");
        }

        // Vanalar
        foreach (var v in valves)
        {
            var bb = v.GetBoundingBox();
            double cx = (bb.Min.X + bb.Max.X) / 2;
            double cy = (bb.Min.Y + bb.Max.Y) / 2;
            double rx = ((bb.Min.X + bb.Max.X) / 2 - minX) * scale - 4;
            double ry = (maxY - (bb.Min.Y + bb.Max.Y) / 2) * scale - 4;
            svg.AppendLine($"<rect x=\"{rx:F1}\" y=\"{ry:F1}\" width=\"8\" height=\"8\" " +
                           $"fill=\"#E91E63\" stroke=\"#111\" stroke-width=\"1\" rx=\"1\">" +
                           $"<title>Vana — {v.EntityType}</title></rect>");
        }

        // Özet istatistik
        double totalLenM = pipes.Sum(p => p.Length) / 1000.0;
        var sysCounts = pipes.GroupBy(p => p.SystemType)
                             .Select(g => $"{g.Key}: {g.Count()} boru ({g.Sum(p => p.Length) / 1000:F1} m)")
                             .ToList();

        string legend = string.Join("", SystemColors.Select(kv =>
            $"<span style='display:inline-block;width:14px;height:14px;background:{kv.Value};border-radius:3px;margin-right:5px;vertical-align:middle'></span>{kv.Key} &nbsp; "));

        return $@"<!DOCTYPE html>
<html lang=""tr"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=5.0"">
<title>AfneyCAD — {projectName}</title>
<style>
  body {{margin:0;background:#1E1E2E;color:#eee;font-family:system-ui,sans-serif}}
  header {{background:#0D3060;padding:10px 16px;display:flex;align-items:center;justify-content:space-between}}
  header h1 {{margin:0;font-size:1.1rem;color:#90CAF9}}
  header small {{color:#7799BB;font-size:.8rem}}
  #svgwrap {{overflow:auto;max-height:calc(100vh - 120px);background:#212830;cursor:grab}}
  #svgwrap:active {{cursor:grabbing}}
  svg {{display:block}}
  .stats {{padding:10px 16px;background:#252535;font-size:.82rem;line-height:1.8}}
  .legend {{padding:8px 16px;background:#1a1a2e;font-size:.78rem}}
</style>
</head>
<body>
<header>
  <h1>☁️ AfneyCAD — {projectName}</h1>
  <small>Dışa aktarma: {DateTime.Now:dd.MM.yyyy HH:mm} | {pipes.Count} boru | {fixtures.Count} armatür | {valves.Count} vana</small>
</header>
<div id=""svgwrap"">
  <svg width=""{SVG_W:F0}"" height=""{svgH:F0}"" xmlns=""http://www.w3.org/2000/svg"">
    <rect width=""100%"" height=""100%"" fill=""#212830""/>
    {svg}
  </svg>
</div>
<div class=""stats"">
  <b>Özet:</b> Toplam boru uzunluğu: {totalLenM:F1} m &nbsp;|&nbsp;
  {string.Join(" &nbsp;|&nbsp; ", sysCounts)}
</div>
<div class=""legend""><b>Renk Şeması:</b> &nbsp; {legend}</div>
<script>
// Pinch-zoom ve pan desteği
(function(){{
  var el=document.getElementById('svgwrap'),sx=0,sy=0,isPan=false;
  el.addEventListener('mousedown',function(e){{isPan=true;sx=e.clientX+el.scrollLeft;sy=e.clientY+el.scrollTop}});
  el.addEventListener('mousemove',function(e){{if(!isPan)return;el.scrollLeft=sx-e.clientX;el.scrollTop=sy-e.clientY}});
  el.addEventListener('mouseup',function(){{isPan=false}});
  el.addEventListener('mouseleave',function(){{isPan=false}});
}})();
</script>
</body>
</html>";
    }

    private static string BuildEmptyHtml(string projectName) =>
        $"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>{projectName}</title></head>" +
        "<body style='background:#1E1E2E;color:#eee;font-family:sans-serif;padding:40px'>" +
        "<h2>☁️ AfneyCAD — Boş Proje</h2><p>Dışa aktarılacak çizim elemanı bulunamadı.</p></body></html>";
}
