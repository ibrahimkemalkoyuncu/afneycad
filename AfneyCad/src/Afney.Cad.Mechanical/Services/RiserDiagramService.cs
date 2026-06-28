using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

public class RiserDiagramService
{
    private readonly CadDatabase _database;
    private readonly LevelManager _levelManager;

    public RiserDiagramService(CadDatabase database, LevelManager levelManager)
    {
        _database = database;
        _levelManager = levelManager;
    }

    public List<Afney.Cad.Domain.Abstractions.CadEntity> GenerateRiserDiagram(Vector3D insertPoint, double scale = 1.0)
    {
        var entities = new List<Afney.Cad.Domain.Abstractions.CadEntity>();
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        if (levels.Count == 0) return entities;

        var risers = DetectRiserSystems();
        if (risers.Count == 0) return entities;

        double floorSpacing = 4000 * scale;
        double riserSpacing = 3000 * scale;
        double textHeight = 200 * scale;

        double x0 = insertPoint.X;
        double y0 = insertPoint.Y;

        for (int i = 0; i < levels.Count; i++)
        {
            double y = y0 + i * floorSpacing;
            double lineWidth = risers.Count * riserSpacing + 2000 * scale;

            entities.Add(new LineEntity(new Vector3D(x0 - 500 * scale, y, 0), new Vector3D(x0 + lineWidth, y, 0))
            {
                Color = 0xFF888888,
                Layer = "RISER_DIAGRAM"
            });

            entities.Add(new TextEntity(levels[i].Name, new Vector3D(x0 - 2500 * scale, y + 100 * scale, 0), textHeight)
            {
                Color = 0xFFFFFFFF,
                Layer = "RISER_DIAGRAM"
            });

            string elevText = $"+{levels[i].Elevation / 1000.0:F2} m";
            entities.Add(new TextEntity(elevText, new Vector3D(x0 - 2500 * scale, y - 300 * scale, 0), textHeight * 0.8)
            {
                Color = 0xFF888888,
                Layer = "RISER_DIAGRAM"
            });
        }

        int riserIdx = 0;
        foreach (var riser in risers)
        {
            double xRiser = x0 + riserIdx * riserSpacing;
            uint riserColor = GetSystemColor(riser.SystemType);

            double yBottom = y0;
            double yTop = y0 + (levels.Count - 1) * floorSpacing;

            entities.Add(new LineEntity(new Vector3D(xRiser, yBottom, 0), new Vector3D(xRiser, yTop, 0))
            {
                Color = riserColor,
                Layer = "RISER_DIAGRAM"
            });

            entities.Add(new TextEntity(riser.Label, new Vector3D(xRiser - 200 * scale, yTop + 400 * scale, 0), textHeight)
            {
                Color = riserColor,
                Layer = "RISER_DIAGRAM"
            });

            foreach (var conn in riser.FloorConnections)
            {
                int levelIdx = levels.FindIndex(l => l.Name == conn.LevelName);
                if (levelIdx < 0) continue;

                double yConn = y0 + levelIdx * floorSpacing;
                double branchLen = 1500 * scale;

                entities.Add(new LineEntity(new Vector3D(xRiser, yConn, 0), new Vector3D(xRiser + branchLen, yConn, 0))
                {
                    Color = riserColor,
                    Layer = "RISER_DIAGRAM"
                });

                string pipeLabel = $"DN{conn.DiameterMM:F0} Q={conn.FlowRate:F2}";
                entities.Add(new TextEntity(pipeLabel, new Vector3D(xRiser + branchLen + 100 * scale, yConn + 50 * scale, 0), textHeight * 0.7)
                {
                    Color = riserColor,
                    Layer = "RISER_DIAGRAM"
                });

                foreach (var fixture in conn.Fixtures)
                {
                    double xFix = xRiser + branchLen + 800 * scale + fixture.Index * 600 * scale;
                    entities.Add(new CircleEntity(new Vector3D(xFix, yConn, 0), 150 * scale)
                    {
                        Color = riserColor,
                        Layer = "RISER_DIAGRAM"
                    });
                    entities.Add(new TextEntity(fixture.Symbol, new Vector3D(xFix - 80 * scale, yConn - 80 * scale, 0), textHeight * 0.6)
                    {
                        Color = 0xFFFFFFFF,
                        Layer = "RISER_DIAGRAM"
                    });
                }
            }

            riserIdx++;
        }

        double titleY = y0 + levels.Count * floorSpacing + 500 * scale;
        entities.Add(new TextEntity("KOLON ŞEMASI (RISER DIAGRAM)", new Vector3D(x0, titleY, 0), textHeight * 1.5)
        {
            Color = 0xFFFFFFFF,
            Layer = "RISER_DIAGRAM"
        });

        return entities;
    }

    private List<RiserSystem> DetectRiserSystems()
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var verticals = pipes.Where(p =>
        {
            var dir = (p.EndPoint - p.StartPoint).Normalize();
            return Math.Abs(dir.Z) > 0.9;
        }).ToList();

        var groups = new Dictionary<string, RiserSystem>();

        foreach (var pipe in verticals)
        {
            string key = $"{pipe.SystemType}_{(int)(pipe.StartPoint.X / 500)}_{(int)(pipe.StartPoint.Y / 500)}";

            if (!groups.TryGetValue(key, out var riser))
            {
                riser = new RiserSystem
                {
                    SystemType = pipe.SystemType,
                    Label = $"{pipe.SystemType} R{groups.Count + 1}",
                    Position = new Vector3D(pipe.StartPoint.X, pipe.StartPoint.Y, 0)
                };
                groups[key] = riser;
            }

            var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
            foreach (var level in levels)
            {
                double pipeZ = Math.Min(pipe.StartPoint.Z, pipe.EndPoint.Z);
                if (pipeZ >= level.Elevation && pipeZ < level.Elevation + level.Height)
                {
                    if (!riser.FloorConnections.Any(c => c.LevelName == level.Name))
                    {
                        var fixtures = FindFixturesNearRiser(pipe, level);
                        riser.FloorConnections.Add(new RiserFloorConnection
                        {
                            LevelName = level.Name,
                            DiameterMM = pipe.InnerDiameter,
                            FlowRate = pipe.FlowRate,
                            Fixtures = fixtures
                        });
                    }
                }
            }
        }

        return groups.Values.ToList();
    }

    private List<RiserFixtureSymbol> FindFixturesNearRiser(PipeEntity riserPipe, MepLevel level)
    {
        return _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .Where(f =>
            {
                double fz = f.GetBoundingBox().Center.Z;
                if (fz < level.Elevation || fz >= level.Elevation + level.Height) return false;
                double dist = Math.Sqrt(Math.Pow(f.Position.X - riserPipe.StartPoint.X, 2) + Math.Pow(f.Position.Y - riserPipe.StartPoint.Y, 2));
                return dist < 5000;
            })
            .Select((f, idx) => new RiserFixtureSymbol
            {
                Symbol = GetFixtureSymbol(f.FixtureType),
                Index = idx
            })
            .ToList();
    }

    private string GetFixtureSymbol(string fixtureType)
    {
        if (string.IsNullOrEmpty(fixtureType)) return "?";
        if (fixtureType.Contains("WC") || fixtureType.Contains("Klozet") || fixtureType.Contains("Toilet")) return "WC";
        if (fixtureType.Contains("Lavabo") || fixtureType.Contains("Washbasin")) return "LV";
        if (fixtureType.Contains("Duş") || fixtureType.Contains("Shower")) return "DŞ";
        if (fixtureType.Contains("Küvet") || fixtureType.Contains("Bathtub")) return "KV";
        if (fixtureType.Contains("Eviye") || fixtureType.Contains("Sink")) return "EV";
        if (fixtureType.Contains("Çamaşır") || fixtureType.Contains("Washing")) return "ÇM";
        if (fixtureType.Contains("Bulaşık") || fixtureType.Contains("Dish")) return "BM";
        return "?";
    }

    private uint GetSystemColor(Enums.MechanicalSystemType type) => type switch
    {
        Enums.MechanicalSystemType.DomesticColdWater => 0xFF4488FF,
        Enums.MechanicalSystemType.DomesticHotWater => 0xFFFF4444,
        Enums.MechanicalSystemType.WasteWater => 0xFF888844,
        Enums.MechanicalSystemType.Ventilation => 0xFF44FF44,
        Enums.MechanicalSystemType.FireProtection => 0xFFFF0000,
        Enums.MechanicalSystemType.Gas => 0xFFFFFF00,
        _ => 0xFFCCCCCC
    };
}

public class RiserSystem
{
    public Enums.MechanicalSystemType SystemType { get; set; }
    public string Label { get; set; } = "";
    public Vector3D Position { get; set; }
    public List<RiserFloorConnection> FloorConnections { get; set; } = new();
}

public class RiserFloorConnection
{
    public string LevelName { get; set; } = "";
    public double DiameterMM { get; set; }
    public double FlowRate { get; set; }
    public List<RiserFixtureSymbol> Fixtures { get; set; } = new();
}

public class RiserFixtureSymbol
{
    public string Symbol { get; set; } = "";
    public int Index { get; set; }
}
