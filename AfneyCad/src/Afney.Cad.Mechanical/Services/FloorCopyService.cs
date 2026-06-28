using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

public class FloorCopyService
{
    private readonly CadDatabase _database;

    public FloorCopyService(CadDatabase database)
    {
        _database = database;
    }

    public FloorCopyResult CopyFloor(MepLevel sourceLevel, MepLevel targetLevel, FloorCopyOptions options)
    {
        var result = new FloorCopyResult { SourceLevel = sourceLevel.Name, TargetLevel = targetLevel.Name };

        double elevationDelta = targetLevel.Elevation - sourceLevel.Elevation;
        var offset = new Vector3D(0, 0, elevationDelta);

        var sourceEntities = _database.GetAllEntities()
            .Where(e => IsOnLevel(e, sourceLevel))
            .ToList();

        foreach (var entity in sourceEntities)
        {
            if (!options.CopyArchitectural && entity.Layer?.StartsWith("ARCH") == true)
                continue;
            if (!options.CopyMEP && entity is MechanicalEntity)
                continue;

            var clone = entity.Clone();
            clone.Id = Guid.NewGuid();

            clone.Transform(Matrix4x4.TranslationMatrix(offset.X, offset.Y, offset.Z));

            if (options.RenameLayer && !string.IsNullOrEmpty(targetLevel.Name))
            {
                string levelTag = targetLevel.Name.Replace(" ", "_");
                if (!string.IsNullOrEmpty(clone.Layer))
                    clone.Layer = $"{clone.Layer}_{levelTag}";
            }

            _database.AddEntity(clone);
            result.CopiedCount++;
        }

        result.ElevationOffset = elevationDelta;
        return result;
    }

    public FloorCopyResult CopyFloorToMultiple(MepLevel sourceLevel, IEnumerable<MepLevel> targetLevels, FloorCopyOptions options)
    {
        var combined = new FloorCopyResult { SourceLevel = sourceLevel.Name };
        foreach (var target in targetLevels)
        {
            var r = CopyFloor(sourceLevel, target, options);
            combined.CopiedCount += r.CopiedCount;
        }
        return combined;
    }

    private bool IsOnLevel(Afney.Cad.Domain.Abstractions.CadEntity entity, MepLevel level)
    {
        var bbox = entity.GetBoundingBox();
        double z = bbox.Center.Z;
        return z >= level.Elevation && z < level.Elevation + level.Height;
    }

    // Statik basınç hesabı (kat bazlı)
    // H_static = rho * g * h (mSS)
    public static double CalculateStaticPressure(double elevationDifferenceM, double waterTempC = 10.0)
    {
        double rho = WaterPropertiesService.GetDensity(waterTempC);
        return rho * 9.81 * elevationDifferenceM / (rho * 9.81); // = elevationDifferenceM mSS
    }

    public static double CalculateBuildingStaticHead(IEnumerable<MepLevel> levels)
    {
        if (!levels.Any()) return 0;
        double minElev = levels.Min(l => l.Elevation);
        double maxElev = levels.Max(l => l.Elevation + l.Height);
        return (maxElev - minElev) / 1000.0; // mm → m (mSS)
    }

    public StaticPressureReport GenerateStaticPressureReport(IEnumerable<MepLevel> levels, double waterTempC = 10.0)
    {
        var sorted = levels.OrderBy(l => l.Elevation).ToList();
        var report = new StaticPressureReport();

        if (sorted.Count == 0) return report;

        double baseElevation = sorted.First().Elevation;

        foreach (var level in sorted)
        {
            double heightM = (level.Elevation - baseElevation) / 1000.0;
            double staticHead = heightM; // mSS (basitleştirilmiş: rho*g*h / rho*g = h)
            double pressureBar = staticHead * 0.0981; // mSS → bar

            report.Levels.Add(new LevelPressureEntry
            {
                LevelName = level.Name,
                ElevationM = level.Elevation / 1000.0,
                HeightFromBaseM = heightM,
                StaticHeadMSS = staticHead,
                PressureBar = pressureBar
            });
        }

        report.TotalBuildingHeightM = (sorted.Last().Elevation + sorted.Last().Height - baseElevation) / 1000.0;
        report.TotalStaticHeadMSS = report.TotalBuildingHeightM;
        report.RequiredPumpHeadMSS = report.TotalStaticHeadMSS + 5.0; // +5 mSS uç basınç

        return report;
    }
}

public class FloorCopyOptions
{
    public bool CopyArchitectural { get; set; } = true;
    public bool CopyMEP { get; set; } = true;
    public bool RenameLayer { get; set; } = false;
}

public class FloorCopyResult
{
    public string SourceLevel { get; set; } = "";
    public string TargetLevel { get; set; } = "";
    public int CopiedCount { get; set; }
    public double ElevationOffset { get; set; }
}

public class StaticPressureReport
{
    public double TotalBuildingHeightM { get; set; }
    public double TotalStaticHeadMSS { get; set; }
    public double RequiredPumpHeadMSS { get; set; }
    public List<LevelPressureEntry> Levels { get; set; } = new();
}

public class LevelPressureEntry
{
    public string LevelName { get; set; } = "";
    public double ElevationM { get; set; }
    public double HeightFromBaseM { get; set; }
    public double StaticHeadMSS { get; set; }
    public double PressureBar { get; set; }
}
