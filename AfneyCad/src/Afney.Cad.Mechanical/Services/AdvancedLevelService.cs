using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// Gelişmiş Kat Yönetim Servisi — aktif/pasif, filtreleme, 3D montaj v2
public class AdvancedLevelService
{
    private readonly LevelManager _levelManager;
    private readonly CadDatabase _database;
    private MepLevel? _activeLevel;
    private readonly HashSet<string> _hiddenLevels = new();

    public MepLevel? ActiveLevel => _activeLevel;

    public AdvancedLevelService(LevelManager levelManager, CadDatabase database)
    {
        _levelManager = levelManager;
        _database = database;
    }

    public void SetActiveLevel(string levelName)
    {
        _activeLevel = _levelManager.GetLevels().FirstOrDefault(l => l.Name == levelName);
    }

    public void ShowLevel(string levelName) => _hiddenLevels.Remove(levelName);
    public void HideLevel(string levelName) => _hiddenLevels.Add(levelName);
    public bool IsLevelVisible(string levelName) => !_hiddenLevels.Contains(levelName);

    // Aktif kattaki entity'leri getir
    public IEnumerable<CadEntity> GetEntitiesOnLevel(MepLevel level)
    {
        return _database.GetAllEntities().Where(e =>
        {
            var z = e.GetBoundingBox().Center.Z;
            return z >= level.Elevation && z < level.Elevation + level.Height;
        });
    }

    // Tüm katların entity sayısı raporu
    public List<LevelEntityCount> GetLevelSummary()
    {
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        return levels.Select(l => new LevelEntityCount
        {
            LevelName = l.Name,
            Elevation = l.Elevation,
            Height = l.Height,
            EntityCount = GetEntitiesOnLevel(l).Count(),
            IsActive = _activeLevel?.Name == l.Name,
            IsVisible = IsLevelVisible(l.Name)
        }).ToList();
    }

    // Standart bina şablonu oluştur
    public void CreateBuildingTemplate(BuildingTemplate template)
    {
        double currentElevation = template.BasementDepth > 0 ? -template.BasementDepth : 0;

        if (template.BasementDepth > 0)
        {
            _levelManager.AddLevel(new MepLevel("Bodrum Kat", currentElevation, template.BasementHeight));
            currentElevation += template.BasementHeight;
        }

        _levelManager.AddLevel(new MepLevel("Zemin Kat", currentElevation, template.GroundFloorHeight));
        currentElevation += template.GroundFloorHeight;

        for (int i = 1; i <= template.TypicalFloorCount; i++)
        {
            _levelManager.AddLevel(new MepLevel($"{i}. Kat", currentElevation, template.TypicalFloorHeight));
            currentElevation += template.TypicalFloorHeight;
        }

        if (template.HasRoof)
        {
            _levelManager.AddLevel(new MepLevel("Çatı Katı", currentElevation, template.RoofHeight));
        }
    }

    // 3D Bina Montajı v2 — tüm katları dikey hizala
    public int AssembleBuilding3D()
    {
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        int totalMoved = 0;

        foreach (var level in levels)
        {
            var entities = GetEntitiesOnLevel(level).ToList();
            foreach (var ent in entities)
            {
                var bbox = ent.GetBoundingBox();
                double currentZ = bbox.Center.Z;
                double targetZ = level.Elevation + (bbox.Center.Z - level.Elevation);

                if (Math.Abs(currentZ - targetZ) > 1.0)
                {
                    ent.Transform(Matrix4x4.TranslationMatrix(0, 0, targetZ - currentZ));
                    totalMoved++;
                }
            }
        }

        return totalMoved;
    }
}

public class LevelEntityCount
{
    public string LevelName { get; set; } = "";
    public double Elevation { get; set; }
    public double Height { get; set; }
    public int EntityCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
}

public class BuildingTemplate
{
    public double BasementDepth { get; set; } = 3000;
    public double BasementHeight { get; set; } = 3000;
    public double GroundFloorHeight { get; set; } = 4000;
    public int TypicalFloorCount { get; set; } = 5;
    public double TypicalFloorHeight { get; set; } = 3000;
    public bool HasRoof { get; set; } = true;
    public double RoofHeight { get; set; } = 1500;
}
