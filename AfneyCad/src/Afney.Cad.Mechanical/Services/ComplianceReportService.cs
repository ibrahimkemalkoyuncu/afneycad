using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

// TS 1258 / TS EN 806 / DIN 1988 mevzuat uyum raporu
public class ComplianceReportService
{
    private readonly CadDatabase _database;
    private readonly MechanicalProjectSettings _settings;

    public ComplianceReportService(CadDatabase database, MechanicalProjectSettings settings)
    {
        _database = database;
        _settings = settings;
    }

    public ComplianceReport GenerateReport()
    {
        var report = new ComplianceReport();
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();

        report.TotalPipeCount = pipes.Count;
        report.TotalFixtureCount = fixtures.Count;

        // TS 1258 Madde 5.1 — Hız limitleri
        CheckVelocityLimits(pipes, report);

        // TS 1258 Madde 5.2 — Minimum boru çapları
        CheckMinimumDiameters(pipes, report);

        // TS 1258 Madde 5.3 — Pis su eğim kontrolleri
        CheckWasteWaterSlopes(pipes, report);

        // TS EN 806-3 — Doluluk oranı (%70 max)
        CheckFillingRatio(pipes, report);

        // TS 1258 Madde 6 — Basınç kontrolleri
        CheckPressureLimits(pipes, report);

        // DIN 1988-300 — Bağlantı uyumluluğu
        CheckFixtureConnections(fixtures, pipes, report);

        // TS EN 12056-2 — Atık su sistemi
        CheckWasteWaterSystem(pipes, report);

        // Genel skor hesapla
        report.CalculateScore();

        return report;
    }

    private void CheckVelocityLimits(List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("TS 1258 §5.1", "Akış hızı limitleri");

        foreach (var pipe in pipes.Where(p => p.Velocity > 0))
        {
            double maxV = pipe.SystemType switch
            {
                MechanicalSystemType.DomesticColdWater => 2.0,
                MechanicalSystemType.DomesticHotWater => 1.5,
                MechanicalSystemType.WasteWater => 3.0,
                MechanicalSystemType.FireProtection => 3.0,
                _ => 2.0
            };

            double minV = pipe.SystemType == MechanicalSystemType.WasteWater ? 0.7 : 0.3;

            if (pipe.Velocity > maxV)
                rule.AddViolation(pipe.Id, $"Hız aşımı: {pipe.Velocity:F2} m/s > max {maxV} m/s (DN{pipe.InnerDiameter:F0})", ComplianceSeverity.Error);
            else if (pipe.Velocity < minV && pipe.FlowRate > 0)
                rule.AddViolation(pipe.Id, $"Düşük hız: {pipe.Velocity:F2} m/s < min {minV} m/s (çökelme riski)", ComplianceSeverity.Warning);
            else
                rule.PassCount++;
        }

        report.Rules.Add(rule);
    }

    private void CheckMinimumDiameters(List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("TS 1258 §5.2", "Minimum boru çapları");

        foreach (var pipe in pipes)
        {
            double minDN = pipe.SystemType switch
            {
                MechanicalSystemType.DomesticColdWater => 15,
                MechanicalSystemType.DomesticHotWater => 15,
                MechanicalSystemType.WasteWater => 30,
                MechanicalSystemType.Gas => 15,
                _ => 15
            };

            if (pipe.InnerDiameter < minDN)
                rule.AddViolation(pipe.Id, $"Çap yetersiz: DN{pipe.InnerDiameter:F0} < min DN{minDN}", ComplianceSeverity.Error);
            else
                rule.PassCount++;
        }

        report.Rules.Add(rule);
    }

    private void CheckWasteWaterSlopes(List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("TS EN 12056-2", "Pis su eğim kontrolleri");

        foreach (var pipe in pipes.Where(p => p.SystemType == MechanicalSystemType.WasteWater))
        {
            var dir = (pipe.EndPoint - pipe.StartPoint).Normalize();
            if (Math.Abs(dir.Z) > 0.9) continue; // Dikey boru, eğim kontrolü gereksiz

            double minSlope = pipe.InnerDiameter >= 100 ? 0.01 : 0.02; // DN100+ = %1, DN<100 = %2

            if (pipe.Slope < minSlope && pipe.Slope >= 0)
                rule.AddViolation(pipe.Id, $"Eğim yetersiz: %{pipe.Slope * 100:F1} < min %{minSlope * 100:F0} (DN{pipe.InnerDiameter:F0})", ComplianceSeverity.Warning);
            else if (pipe.Slope > 0.05)
                rule.AddViolation(pipe.Id, $"Aşırı eğim: %{pipe.Slope * 100:F1} > %5 (sifon bozulma riski)", ComplianceSeverity.Warning);
            else
                rule.PassCount++;
        }

        report.Rules.Add(rule);
    }

    private void CheckFillingRatio(List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("TS EN 806-3", "Doluluk oranı kontrolü");

        foreach (var pipe in pipes.Where(p => p.HasHydraulicViolation))
        {
            rule.AddViolation(pipe.Id, $"Doluluk oranı %70 aşıldı (DN{pipe.InnerDiameter:F0})", ComplianceSeverity.Error);
        }
        rule.PassCount = pipes.Count(p => !p.HasHydraulicViolation);

        report.Rules.Add(rule);
    }

    private void CheckPressureLimits(List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("TS 1258 §6", "Basınç limitleri");

        foreach (var pipe in pipes.Where(p => p.PressureDrop > 0))
        {
            double maxDropPerM = 0.04; // 40 Pa/m = 4 mbar/m (TS 1258 önerisi)
            double pipeLength = pipe.GetLength() / 1000.0;
            double dropPerM = pipeLength > 0 ? pipe.PressureDrop / pipeLength : 0;

            if (dropPerM > maxDropPerM)
                rule.AddViolation(pipe.Id, $"Birim basınç kaybı yüksek: {dropPerM * 1000:F1} mbar/m > {maxDropPerM * 1000:F0} mbar/m", ComplianceSeverity.Warning);
            else
                rule.PassCount++;
        }

        report.Rules.Add(rule);
    }

    private void CheckFixtureConnections(List<SanitaryFixtureEntity> fixtures, List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("DIN 1988-300", "Cihaz bağlantı uyumu");

        foreach (var fix in fixtures)
        {
            bool connected = pipes.Any(p =>
            {
                double dist = Math.Min(
                    (p.StartPoint - fix.Position).Length(),
                    (p.EndPoint - fix.Position).Length());
                return dist < 500;
            });

            if (!connected)
                rule.AddViolation(fix.Id, $"{fix.FixtureType} bağlantısız (500mm içinde boru yok)", ComplianceSeverity.Error);
            else
                rule.PassCount++;
        }

        report.Rules.Add(rule);
    }

    private void CheckWasteWaterSystem(List<PipeEntity> pipes, ComplianceReport report)
    {
        var rule = new ComplianceRule("TS EN 12056-2", "Atık su boru boyutları");

        foreach (var pipe in pipes.Where(p => p.SystemType == MechanicalSystemType.WasteWater))
        {
            if (pipe.TotalFixtureUnits > 12 && pipe.InnerDiameter < 100)
                rule.AddViolation(pipe.Id, $"FU={pipe.TotalFixtureUnits:F0} için DN{pipe.InnerDiameter:F0} yetersiz (min DN100)", ComplianceSeverity.Error);
            else if (pipe.TotalFixtureUnits > 4 && pipe.InnerDiameter < 75)
                rule.AddViolation(pipe.Id, $"FU={pipe.TotalFixtureUnits:F0} için DN{pipe.InnerDiameter:F0} yetersiz (min DN75)", ComplianceSeverity.Warning);
            else
                rule.PassCount++;
        }

        report.Rules.Add(rule);
    }

    public string ExportToHtml(ComplianceReport report, string projectName = "AfneyCAD Projesi")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>Mevzuat Uyum Raporu</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;background:#1a1a2e;color:#e0e0e0;padding:20px}");
        sb.AppendLine("h1{color:#00ddff}h2{color:#ffa500}table{border-collapse:collapse;width:100%;margin:10px 0}");
        sb.AppendLine("th,td{border:1px solid #333;padding:8px;text-align:left}th{background:#16213e}");
        sb.AppendLine(".pass{color:#4caf50}.warn{color:#ff9800}.fail{color:#f44336}");
        sb.AppendLine(".score{font-size:48px;font-weight:bold;text-align:center;padding:20px}</style></head><body>");

        sb.AppendLine($"<h1>MEVZUAT UYUM RAPORU — {projectName}</h1>");
        sb.AppendLine($"<p>Tarih: {DateTime.Now:dd.MM.yyyy HH:mm} | Boru: {report.TotalPipeCount} | Cihaz: {report.TotalFixtureCount}</p>");

        string scoreClass = report.ScorePercent >= 80 ? "pass" : report.ScorePercent >= 50 ? "warn" : "fail";
        sb.AppendLine($"<div class='score {scoreClass}'>{report.ScorePercent:F0}% UYUMLU</div>");

        sb.AppendLine("<h2>Kural Detayları</h2><table><tr><th>Standart</th><th>Kural</th><th>Geçen</th><th>Hata</th><th>Uyarı</th><th>Durum</th></tr>");

        foreach (var rule in report.Rules)
        {
            string status = rule.Violations.Count == 0 ? "<span class='pass'>GEÇER</span>" :
                rule.Violations.Any(v => v.Severity == ComplianceSeverity.Error) ? "<span class='fail'>KALMAZ</span>" : "<span class='warn'>DİKKAT</span>";
            int errors = rule.Violations.Count(v => v.Severity == ComplianceSeverity.Error);
            int warnings = rule.Violations.Count(v => v.Severity == ComplianceSeverity.Warning);
            sb.AppendLine($"<tr><td>{rule.StandardRef}</td><td>{rule.Description}</td><td>{rule.PassCount}</td><td>{errors}</td><td>{warnings}</td><td>{status}</td></tr>");
        }
        sb.AppendLine("</table>");

        var allViolations = report.Rules.SelectMany(r => r.Violations).ToList();
        if (allViolations.Any())
        {
            sb.AppendLine("<h2>İhlal Detayları</h2><table><tr><th>ID</th><th>Açıklama</th><th>Seviye</th></tr>");
            foreach (var v in allViolations.Take(50))
            {
                string sev = v.Severity == ComplianceSeverity.Error ? "<span class='fail'>HATA</span>" : "<span class='warn'>UYARI</span>";
                sb.AppendLine($"<tr><td>{v.EntityId.ToString()[..8]}</td><td>{v.Message}</td><td>{sev}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

public class ComplianceReport
{
    public int TotalPipeCount { get; set; }
    public int TotalFixtureCount { get; set; }
    public List<ComplianceRule> Rules { get; set; } = new();
    public double ScorePercent { get; set; }

    public void CalculateScore()
    {
        int totalChecks = Rules.Sum(r => r.PassCount + r.Violations.Count);
        int passed = Rules.Sum(r => r.PassCount);
        ScorePercent = totalChecks > 0 ? (double)passed / totalChecks * 100.0 : 100.0;
    }
}

public class ComplianceRule
{
    public string StandardRef { get; }
    public string Description { get; }
    public int PassCount { get; set; }
    public List<ComplianceViolation> Violations { get; } = new();

    public ComplianceRule(string standardRef, string description)
    {
        StandardRef = standardRef;
        Description = description;
    }

    public void AddViolation(Guid entityId, string message, ComplianceSeverity severity)
    {
        Violations.Add(new ComplianceViolation(entityId, message, severity));
    }
}

public record ComplianceViolation(Guid EntityId, string Message, ComplianceSeverity Severity);

public enum ComplianceSeverity { Warning, Error }
