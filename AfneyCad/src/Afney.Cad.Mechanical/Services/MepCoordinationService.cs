using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// BIM MEP Koordinasyon Servisi — Çakışma çözüm önerileri + mesafe kuralları
public class MepCoordinationService
{
    private readonly CadDatabase _database;

    // TS 8373 / ASHRAE — MEP minimum mesafe kuralları (mm)
    private static readonly Dictionary<string, double> ClearanceRules = new()
    {
        ["Pipe-Pipe"] = 50,
        ["Pipe-Wall"] = 25,
        ["Pipe-Duct"] = 100,
        ["Duct-Wall"] = 50,
        ["Duct-Duct"] = 100,
        ["Pipe-Electrical"] = 150,
        ["Duct-Electrical"] = 150,
        ["Pipe-Structural"] = 50,
        ["HotPipe-ColdPipe"] = 100,
        ["Insulation-Wall"] = 10,
    };

    public MepCoordinationService(CadDatabase database) => _database = database;

    public CoordinationReport Analyze(List<ArchitecturalObstacle> obstacles)
    {
        var report = new CoordinationReport();
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var ducts = _database.GetAllEntities().OfType<DuctEntity>().ToList();

        // 1. Boru-Boru mesafe kontrolü
        for (int i = 0; i < pipes.Count; i++)
        {
            for (int j = i + 1; j < pipes.Count; j++)
            {
                double dist = MinDistance(pipes[i], pipes[j]);
                double required = GetRequiredClearance(pipes[i], pipes[j]);

                if (dist < required)
                {
                    report.Issues.Add(new CoordinationIssue
                    {
                        Type = "Mesafe İhlali",
                        Description = $"Boru DN{pipes[i].InnerDiameter:F0} ↔ DN{pipes[j].InnerDiameter:F0}: {dist:F0}mm < min {required:F0}mm",
                        Entity1Id = pipes[i].Id,
                        Entity2Id = pipes[j].Id,
                        Severity = dist < required * 0.5 ? "Kritik" : "Uyarı",
                        Resolution = SuggestResolution(pipes[i], pipes[j], dist, required)
                    });
                }
                else report.PassCount++;
            }
        }

        // 2. Boru-Mimari mesafe kontrolü
        foreach (var pipe in pipes)
        {
            foreach (var obs in obstacles.Where(o => o.Type == ObstacleType.Wall))
            {
                double dist = DistanceToObstacle(pipe, obs);
                double required = ClearanceRules.GetValueOrDefault("Pipe-Wall", 25);

                if (dist < required)
                {
                    report.Issues.Add(new CoordinationIssue
                    {
                        Type = "Duvar Mesafesi",
                        Description = $"Boru DN{pipe.InnerDiameter:F0} ↔ {obs.Name}: {dist:F0}mm < min {required:F0}mm",
                        Entity1Id = pipe.Id,
                        Severity = "Uyarı",
                        Resolution = $"Boruyu duvardan {required - dist:F0}mm uzaklaştırın"
                    });
                }
                else report.PassCount++;
            }
        }

        // 3. Boru-Kanal mesafe kontrolü
        foreach (var pipe in pipes)
        {
            foreach (var duct in ducts)
            {
                double dist = MinDistancePipeDuct(pipe, duct);
                double required = ClearanceRules.GetValueOrDefault("Pipe-Duct", 100);

                if (dist < required)
                {
                    report.Issues.Add(new CoordinationIssue
                    {
                        Type = "Boru-Kanal Çakışma",
                        Description = $"DN{pipe.InnerDiameter:F0} ↔ Kanal: {dist:F0}mm < min {required:F0}mm",
                        Entity1Id = pipe.Id,
                        Entity2Id = duct.Id,
                        Severity = "Kritik",
                        Resolution = "Boruyu kanalın altından veya üstünden geçirin (Z offset)"
                    });
                }
                else report.PassCount++;
            }
        }

        report.TotalChecks = report.PassCount + report.Issues.Count;
        report.CompliancePercent = report.TotalChecks > 0 ? (double)report.PassCount / report.TotalChecks * 100 : 100;

        return report;
    }

    private double GetRequiredClearance(PipeEntity a, PipeEntity b)
    {
        bool isHotCold = (a.SystemType == Enums.MechanicalSystemType.DomesticHotWater && b.SystemType == Enums.MechanicalSystemType.DomesticColdWater) ||
                         (b.SystemType == Enums.MechanicalSystemType.DomesticHotWater && a.SystemType == Enums.MechanicalSystemType.DomesticColdWater);
        return isHotCold ? ClearanceRules["HotPipe-ColdPipe"] : ClearanceRules["Pipe-Pipe"];
    }

    private string SuggestResolution(PipeEntity a, PipeEntity b, double actualDist, double requiredDist)
    {
        double gap = requiredDist - actualDist;
        if (a.InnerDiameter < b.InnerDiameter)
            return $"Küçük boruyu (DN{a.InnerDiameter:F0}) {gap:F0}mm kaydırın";
        return $"Küçük boruyu (DN{b.InnerDiameter:F0}) {gap:F0}mm kaydırın";
    }

    private double MinDistance(PipeEntity a, PipeEntity b)
    {
        double d1 = (a.StartPoint - b.StartPoint).Length();
        double d2 = (a.StartPoint - b.EndPoint).Length();
        double d3 = (a.EndPoint - b.StartPoint).Length();
        double d4 = (a.EndPoint - b.EndPoint).Length();
        return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4)) - (a.InnerDiameter + b.InnerDiameter) / 2.0;
    }

    private double MinDistancePipeDuct(PipeEntity pipe, DuctEntity duct)
    {
        var pipeCenter = (pipe.StartPoint + pipe.EndPoint) * 0.5;
        var ductCenter = (duct.StartPoint + duct.EndPoint) * 0.5;
        return (pipeCenter - ductCenter).Length() - pipe.InnerDiameter / 2.0 - duct.WidthMm / 2.0;
    }

    private double DistanceToObstacle(PipeEntity pipe, ArchitecturalObstacle obs)
    {
        var pipeCenter = (pipe.StartPoint + pipe.EndPoint) * 0.5;
        if (obs.Boundary.Count < 2) return double.MaxValue;
        var obsCenter = Afney.Cad.Geometry.Primitives.Vector3D.Zero;
        foreach (var pt in obs.Boundary) obsCenter = new Afney.Cad.Geometry.Primitives.Vector3D(obsCenter.X + pt.X / obs.Boundary.Count, obsCenter.Y + pt.Y / obs.Boundary.Count, obsCenter.Z + pt.Z / obs.Boundary.Count);
        return (pipeCenter - obsCenter).Length() - pipe.InnerDiameter / 2.0;
    }

    public static IReadOnlyDictionary<string, double> GetClearanceRules() => ClearanceRules;
}

public class CoordinationReport
{
    public int TotalChecks { get; set; }
    public int PassCount { get; set; }
    public double CompliancePercent { get; set; }
    public List<CoordinationIssue> Issues { get; set; } = new();
}

public class CoordinationIssue
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid Entity1Id { get; set; }
    public Guid Entity2Id { get; set; }
    public string Severity { get; set; } = "";
    public string Resolution { get; set; } = "";
}
