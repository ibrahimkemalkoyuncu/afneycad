using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

public class HydraulicReportService
{
    private readonly PressureDropService _pressureDropService;

    public HydraulicReportService(PressureDropService pressureDropService)
    {
        _pressureDropService = pressureDropService;
    }

    public string GenerateHtmlReport(
        IEnumerable<PipeEntity> pipes,
        string projectName,
        IEnumerable<RainfallCatchmentEntity>? catchments = null)
    {
        var pipeList = pipes.ToList();
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'>");
        sb.AppendLine($"<title>Hidrolik Hesap Raporu - {projectName}</title>");
        sb.AppendLine(@"<style>
body { font-family:'Segoe UI',sans-serif; margin:24px; color:#222; background:#f5f5f5; }
h1 { color:#005A9C; border-bottom:3px solid #005A9C; padding-bottom:8px; }
h2 { color:#fff; padding:8px 14px; border-radius:4px; margin-top:32px; }
h2.cw  { background:#0077CC; }
h2.hw  { background:#CC2200; }
h2.ww  { background:#886633; }
h2.rw  { background:#00BBDD; }
table { width:100%; border-collapse:collapse; margin-top:12px; font-size:13px; background:#fff; }
th,td { border:1px solid #ccc; padding:8px 10px; text-align:center; }
th { background:#eee; font-weight:700; }
tr:nth-child(even) { background:#f9f9f9; }
tr:hover { background:#eef5ff; }
.violation { background:#ffe0e0 !important; color:#b00; font-weight:700; }
.warn { background:#fff8cc !important; }
.ok { color:#006600; }
.summary { margin-top:18px; padding:14px 18px; background:#fff; border-left:4px solid #005A9C; border-radius:3px; }
.footer { margin-top:30px; font-size:11px; color:#888; text-align:center; }
</style></head><body>");

        // ── Başlık ──────────────────────────────────────────────────────────
        sb.AppendLine($@"<div style='display:flex;justify-content:space-between;align-items:center;margin-bottom:16px'>
<div><h1>PROJe HİDROLİK HESAP TABLOSU</h1><p>Proje: <strong>{projectName}</strong></p></div>
<div style='text-align:right'><strong>AfneyCAD Engine</strong><br/>TS 1258 · TS EN 12056 · DIN 1988<br/>{DateTime.Now:dd/MM/yyyy HH:mm}</div>
</div>");

        // ── 1. Temiz Su (Basınçlı) ──────────────────────────────────────────
        var cleanPipes = pipeList
            .Where(p => p.SystemType is MechanicalSystemType.DomesticColdWater
                                     or MechanicalSystemType.DomesticHotWater)
            .OrderBy(p => p.SystemType).ThenByDescending(p => p.FlowRate)
            .ToList();

        if (cleanPipes.Count > 0)
        {
            sb.AppendLine("<h2 class='cw'>1. Temiz Su Tesisatı — TS 1258 / DIN 1988</h2>");
            AppendCleanWaterTable(sb, cleanPipes);
        }

        // ── 2. Pis Su ───────────────────────────────────────────────────────
        var wastePipes = pipeList
            .Where(p => p.SystemType == MechanicalSystemType.WasteWater)
            .OrderByDescending(p => p.TotalFixtureUnits)
            .ToList();

        if (wastePipes.Count > 0)
        {
            sb.AppendLine("<h2 class='ww'>2. Pis Su Tesisatı — TS EN 12056-2</h2>");
            AppendWasteWaterTable(sb, wastePipes);
        }

        // ── 3. Yağmur Suyu ─────────────────────────────────────────────────
        var rainPipes = pipeList
            .Where(p => p.SystemType == MechanicalSystemType.RainWater)
            .OrderByDescending(p => p.TotalFixtureUnits)
            .ToList();

        var catchmentList = catchments?.ToList() ?? [];

        if (rainPipes.Count > 0 || catchmentList.Count > 0)
        {
            sb.AppendLine("<h2 class='rw'>3. Yağmur Suyu Tesisatı — TS EN 12056-3</h2>");

            if (catchmentList.Count > 0)
                AppendCatchmentTable(sb, catchmentList);

            if (rainPipes.Count > 0)
                AppendRainWaterTable(sb, rainPipes);
        }

        // ── 4. Diğer Sistemler (Yangın, Gaz vb.) ──────────────────────────
        var otherPipes = pipeList
            .Where(p => p.SystemType is not MechanicalSystemType.DomesticColdWater
                                     and not MechanicalSystemType.DomesticHotWater
                                     and not MechanicalSystemType.WasteWater
                                     and not MechanicalSystemType.RainWater)
            .ToList();

        if (otherPipes.Count > 0)
        {
            sb.AppendLine("<h2 style='background:#555'>4. Diğer Sistemler</h2>");
            AppendGenericTable(sb, otherPipes);
        }

        // ── Basınç Kaybı Grafiği ────────────────────────────────────────────
        // NE/NEDEN: SvgChartService önceden yalnızca kullanılmayan (hiç çağrılmayan) bir
        // "tam rapor" servisinden (PdfReportService) referans alınıyordu; o servis silindi,
        // grafik burada — canlı, gerçekten açılan hidrolik rapora — taşındı.
        var chartPipes = pipeList.Where(p => p.PressureDrop > 0).OrderByDescending(p => p.PressureDrop).Take(15).ToList();
        if (chartPipes.Count > 0)
        {
            var chartData = chartPipes
                .Select((p, i) => ($"DN{p.InnerDiameter:F0}-{i + 1}", p.PressureDrop))
                .ToList();
            sb.AppendLine("<div class='summary'>");
            sb.AppendLine(SvgChartService.BarChart("Boru Basınç Kaybı Dağılımı (en yüksek 15)", chartData, " mSS", "#FF6600"));
            sb.AppendLine("</div>");
        }

        // ── Özet ────────────────────────────────────────────────────────────
        int violationCount = pipeList.Count(p => p.HasHydraulicViolation);
        double totalLength = pipeList.Sum(p => p.GetLength()) / 1000.0;

        sb.AppendLine($@"<div class='summary'>
<h3>Proje Özeti</h3>
<p>Toplam boru sayısı: <strong>{pipeList.Count}</strong> | Toplam uzunluk: <strong>{totalLength:F1} m</strong></p>
<p>Pis su boru adedi: <strong>{wastePipes.Count}</strong> | Yağmur suyu: <strong>{rainPipes.Count}</strong> | Temiz su: <strong>{cleanPipes.Count}</strong></p>
<p>Hydraulic violation: <strong style='color:{(violationCount > 0 ? "#b00" : "green")}'>{violationCount} boru</strong> {(violationCount > 0 ? "(kırmızı satırlar)" : "✓ hata yok")}</p>
</div>");

        sb.AppendLine($"<div class='footer'>AfneyCAD — Otomatik oluşturuldu: {DateTime.Now:dd/MM/yyyy HH:mm}</div>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ── Temiz Su Tablosu ─────────────────────────────────────────────────
    private void AppendCleanWaterTable(StringBuilder sb, List<PipeEntity> pipes)
    {
        sb.AppendLine("<table><thead><tr>");
        foreach (var h in new[] { "Sistem", "Uzunluk (m)", "DN (mm)", "LU", "Q (l/s)", "Hız (m/s)", "ΔP (mbar/m)", "Hat Kaybı (mSS)" })
            sb.AppendLine($"<th>{h}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var pipe in pipes)
        {
            if (pipe.PressureDrop <= 0 && pipe.FlowRate > 0)
                pipe.PressureDrop = _pressureDropService.CalculatePipePressureDrop(pipe);

            double len = pipe.GetLength() / 1000.0;
            double q   = pipe.FlowRate / 3.6;
            double v   = pipe.GetVelocity();
            double dp  = len > 0 ? pipe.PressureDrop * 98.0665 / len : 0;
            string cls = pipe.HasHydraulicViolation ? "violation" : (v > 1.5 ? "warn" : "");

            sb.AppendLine($"<tr class='{cls}'>");
            sb.AppendLine($"<td>{SystemLabel(pipe.SystemType)}</td><td>{len:F2}</td><td>{pipe.InnerDiameter:F0}</td>");
            sb.AppendLine($"<td>{(pipe.LoadUnits > 0 ? pipe.LoadUnits.ToString("F1") : "-")}</td>");
            sb.AppendLine($"<td>{q:F3}</td><td>{v:F2}</td><td>{dp:F2}</td><td>{pipe.PressureDrop:F3}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");
    }

    // ── Pis Su Tablosu (TS EN 12056-2) ──────────────────────────────────
    private static void AppendWasteWaterTable(StringBuilder sb, List<PipeEntity> pipes)
    {
        sb.AppendLine("<table><thead><tr>");
        foreach (var h in new[] { "Uzunluk (m)", "DN (mm)", "Σ DU", "Q_ww (l/s)", "Eğim (%)", "Doluluk h/D", "Hız (m/s)", "WC Kolonu", "Durum" })
            sb.AppendLine($"<th>{h}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var pipe in pipes)
        {
            double len   = pipe.GetLength() / 1000.0;
            double qWw   = pipe.FlowRate / 3.6;
            double slope = pipe.Slope * 100;
            double vel   = pipe.GetVelocity();
            // h/D doluluk oranı — Manning tam dolu debiden kestirme
            double diam  = pipe.InnerDiameter / 1000.0;
            double qFull = diam > 0 && pipe.Slope > 0
                ? (1.0 / 0.013) * Math.PI * diam * diam / 4.0
                  * Math.Pow(diam / 4.0, 2.0 / 3.0)
                  * Math.Pow(pipe.Slope, 0.5)
                : 0;
            double fill  = qFull > 0 ? Math.Min(qWw / qFull, 1.0) : 0;

            bool slopeOk = pipe.Slope >= 0.02 || IsVertical(pipe);
            string status = pipe.HasHydraulicViolation ? "✗ VIOLATİON"
                          : !slopeOk ? "⚠ Eğim < %2"
                          : "✓ OK";
            string cls = pipe.HasHydraulicViolation ? "violation"
                       : !slopeOk ? "warn" : "";

            sb.AppendLine($"<tr class='{cls}'>");
            sb.AppendLine($"<td>{len:F2}</td><td>{pipe.InnerDiameter:F0}</td>");
            sb.AppendLine($"<td>{(pipe.TotalFixtureUnits > 0 ? pipe.TotalFixtureUnits.ToString("F1") : "-")}</td>");
            sb.AppendLine($"<td>{qWw:F3}</td><td>{slope:F1}</td><td>{fill:F2}</td><td>{vel:F2}</td>");
            sb.AppendLine($"<td>{(pipe.IsCarryingWCLoad ? "Evet" : "-")}</td>");
            sb.AppendLine($"<td>{status}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");

        int slopeViol = pipes.Count(p => p.Slope < 0.02 && !IsVertical(p));
        if (slopeViol > 0)
            sb.AppendLine($"<p class='warn' style='padding:6px'>⚠ {slopeViol} boru TS EN 12056-2 min eğim şartını (%2) sağlamıyor.</p>");
    }

    // ── Yağmur Düşme Alanları Tablosu ───────────────────────────────────
    private static void AppendCatchmentTable(StringBuilder sb, List<RainfallCatchmentEntity> catchments)
    {
        sb.AppendLine("<p><strong>Yağmur Düşme Alanları (TS EN 12056-3):</strong></p>");
        sb.AppendLine("<table><thead><tr>");
        foreach (var h in new[] { "Alan Adı", "Yüzey Tipi", "Alan (m²)", "Akış Katsayısı C", "Efektif Alan (m²)" })
            sb.AppendLine($"<th>{h}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        double totalEff = 0;
        foreach (var c in catchments)
        {
            double eff = c.AreaM2 * c.RunoffCoefficient;
            totalEff += eff;
            sb.AppendLine($"<tr><td>{c.AreaName}</td><td>{c.Surface}</td>");
            sb.AppendLine($"<td>{c.AreaM2:F1}</td><td>{c.RunoffCoefficient:F1}</td><td>{eff:F1}</td></tr>");
        }

        sb.AppendLine($"<tr style='font-weight:700;background:#e0f4ff'><td colspan='4'>Toplam Efektif Alan</td><td>{totalEff:F1} m²</td></tr>");
        sb.AppendLine("</tbody></table>");
    }

    // ── Yağmur Suyu Boru Tablosu ─────────────────────────────────────────
    private static void AppendRainWaterTable(StringBuilder sb, List<PipeEntity> pipes)
    {
        sb.AppendLine("<table style='margin-top:10px'><thead><tr>");
        foreach (var h in new[] { "Uzunluk (m)", "DN (mm)", "Q (l/s)", "Eğim (%)", "Hız (m/s)", "Durum" })
            sb.AppendLine($"<th>{h}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var pipe in pipes)
        {
            double len   = pipe.GetLength() / 1000.0;
            double q     = pipe.FlowRate / 3.6;
            double slope = pipe.Slope * 100;
            double vel   = pipe.GetVelocity();
            bool slopeOk = pipe.Slope >= 0.02 || IsVertical(pipe);
            string cls   = pipe.HasHydraulicViolation ? "violation" : (!slopeOk ? "warn" : "");
            string st    = pipe.HasHydraulicViolation ? "✗" : (!slopeOk ? "⚠ Eğim" : "✓");

            sb.AppendLine($"<tr class='{cls}'><td>{len:F2}</td><td>{pipe.InnerDiameter:F0}</td>");
            sb.AppendLine($"<td>{q:F3}</td><td>{slope:F1}</td><td>{vel:F2}</td><td>{st}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
    }

    // ── Genel Tablo (Yangın, Gaz vb.) ────────────────────────────────────
    private void AppendGenericTable(StringBuilder sb, List<PipeEntity> pipes)
    {
        sb.AppendLine("<table><thead><tr>");
        foreach (var h in new[] { "Sistem", "Uzunluk (m)", "DN (mm)", "Q (l/s)", "Hız (m/s)", "Hat Kaybı (mSS)" })
            sb.AppendLine($"<th>{h}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var pipe in pipes)
        {
            if (pipe.PressureDrop <= 0 && pipe.FlowRate > 0)
                pipe.PressureDrop = _pressureDropService.CalculatePipePressureDrop(pipe);
            double len = pipe.GetLength() / 1000.0;
            double q   = pipe.FlowRate / 3.6;
            double v   = pipe.GetVelocity();
            string cls = pipe.HasHydraulicViolation ? "violation" : "";
            sb.AppendLine($"<tr class='{cls}'><td>{SystemLabel(pipe.SystemType)}</td><td>{len:F2}</td>");
            sb.AppendLine($"<td>{pipe.InnerDiameter:F0}</td><td>{q:F3}</td><td>{v:F2}</td><td>{pipe.PressureDrop:F3}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
    }

    private static bool IsVertical(PipeEntity p)
    {
        var d = p.EndPoint - p.StartPoint;
        double len = d.Length();
        return len > 0 && Math.Abs(d.Z) / len > 0.8;
    }

    private static string SystemLabel(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater  => "Sıcak Su",
        MechanicalSystemType.WasteWater        => "Pis Su",
        MechanicalSystemType.RainWater         => "Yağmur",
        MechanicalSystemType.FireProtection    => "Yangın",
        MechanicalSystemType.Gas               => "Gaz",
        _                                      => t.ToString()
    };
}
