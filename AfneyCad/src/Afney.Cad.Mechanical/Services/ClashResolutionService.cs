using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// Çakışma Çözüm Servisi — otomatik rota düzeltme ve yükseklik ayarı
public class ClashResolutionService
{
    private readonly CadDatabase _database;

    public ClashResolutionService(CadDatabase database) => _database = database;

    // Tüm çakışmaları tespit et ve çözüm önerileri üret
    public List<ClashResolution> AnalyzeAndResolve(double minClearanceMm = 50)
    {
        var resolutions = new List<ClashResolution>();
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();

        for (int i = 0; i < pipes.Count; i++)
        {
            for (int j = i + 1; j < pipes.Count; j++)
            {
                double dist = MinPipeDistance(pipes[i], pipes[j]);
                double required = minClearanceMm + (pipes[i].InnerDiameter + pipes[j].InnerDiameter) / 2.0;

                if (dist < required)
                {
                    var resolution = CreateResolution(pipes[i], pipes[j], dist, required);
                    resolutions.Add(resolution);
                }
            }
        }

        return resolutions;
    }

    // Otomatik çözüm uygula (Z offset yöntemi)
    public int AutoResolve(List<ClashResolution> resolutions)
    {
        int resolved = 0;
        foreach (var res in resolutions.Where(r => r.CanAutoResolve))
        {
            var entity = _database.GetAllEntities().FirstOrDefault(e => e.Id == res.MovedEntityId);
            if (entity is PipeEntity pipe)
            {
                var offset = new Vector3D(res.OffsetX, res.OffsetY, res.OffsetZ);
                pipe.StartPoint = pipe.StartPoint + offset;
                pipe.EndPoint = pipe.EndPoint + offset;
                _database.UpdateEntity(pipe);
                res.IsResolved = true;
                resolved++;
            }
        }
        return resolved;
    }

    private ClashResolution CreateResolution(PipeEntity a, PipeEntity b, double actualDist, double requiredDist)
    {
        double gap = requiredDist - actualDist;
        var res = new ClashResolution
        {
            Entity1Id = a.Id,
            Entity2Id = b.Id,
            Entity1Label = $"DN{a.InnerDiameter:F0} ({a.SystemType})",
            Entity2Label = $"DN{b.InnerDiameter:F0} ({b.SystemType})",
            ActualDistanceMm = actualDist,
            RequiredDistanceMm = requiredDist,
            GapMm = gap
        };

        // Çözüm stratejisi seç
        // 1. Küçük boruyu kaydır (daha az etki)
        var smaller = a.InnerDiameter <= b.InnerDiameter ? a : b;
        res.MovedEntityId = smaller.Id;

        // Yatay mı dikey mi?
        var dirA = (a.EndPoint - a.StartPoint).Normalize();
        var dirB = (b.EndPoint - b.StartPoint).Normalize();
        bool parallel = Math.Abs(dirA.X * dirB.X + dirA.Y * dirB.Y + dirA.Z * dirB.Z) > 0.9;

        if (parallel)
        {
            // Paralel borular — Z yönünde ayır
            res.Strategy = ClashStrategy.VerticalOffset;
            res.OffsetZ = gap + 50; // +50mm güvenlik payı
            res.Description = $"Küçük boruyu (DN{smaller.InnerDiameter:F0}) {res.OffsetZ:F0}mm yukarı kaydırın";
            res.CanAutoResolve = true;
        }
        else
        {
            // Kesişen borular — biri üstten geçmeli
            res.Strategy = ClashStrategy.VerticalCrossing;
            res.OffsetZ = requiredDist + smaller.InnerDiameter;
            res.Description = $"Kesişim noktasında DN{smaller.InnerDiameter:F0} boruyu {res.OffsetZ:F0}mm yukarı kaldırın (U-bend)";
            res.CanAutoResolve = true;
        }

        return res;
    }

    private double MinPipeDistance(PipeEntity a, PipeEntity b)
    {
        // Segment-segment minimum mesafe (basitleştirilmiş — merkez çizgileri arası)
        var closestA = ClosestPointOnSegment(a.StartPoint, a.EndPoint, (b.StartPoint + b.EndPoint) * 0.5);
        var closestB = ClosestPointOnSegment(b.StartPoint, b.EndPoint, closestA);
        return (closestA - closestB).Length();
    }

    private Vector3D ClosestPointOnSegment(Vector3D segStart, Vector3D segEnd, Vector3D point)
    {
        var ab = segEnd - segStart;
        double lenSq = ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z;
        if (lenSq < 1e-10) return segStart;

        double t = ((point.X - segStart.X) * ab.X + (point.Y - segStart.Y) * ab.Y + (point.Z - segStart.Z) * ab.Z) / lenSq;
        t = Math.Clamp(t, 0, 1);
        return new Vector3D(segStart.X + t * ab.X, segStart.Y + t * ab.Y, segStart.Z + t * ab.Z);
    }
}

public class ClashResolution
{
    public Guid Entity1Id { get; set; }
    public Guid Entity2Id { get; set; }
    public string Entity1Label { get; set; } = "";
    public string Entity2Label { get; set; } = "";
    public Guid MovedEntityId { get; set; }
    public double ActualDistanceMm { get; set; }
    public double RequiredDistanceMm { get; set; }
    public double GapMm { get; set; }
    public ClashStrategy Strategy { get; set; }
    public string Description { get; set; } = "";
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double OffsetZ { get; set; }
    public bool CanAutoResolve { get; set; }
    public bool IsResolved { get; set; }
}

public enum ClashStrategy { VerticalOffset, HorizontalOffset, VerticalCrossing, Reroute, Manual }
