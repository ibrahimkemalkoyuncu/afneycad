using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Engine;

/*
   NE: Kolon Tespit Motoru (RiserEngine)
   NEDEN: 3D MEP modelindeki dikey boruları (Kolon/Riser) algoritmik olarak ayıklayıp kolon şeması topolojisini kurmak için.
*/
public class RiserEngine
{
    private const double VerticalTolerance = 0.95; 

    // NE: Şema üretim ana metodu
    public List<RiserSchema> GenerateSchemas(IEnumerable<MechanicalEntity> entities, List<MepLevel> levels, MechanicalTopologyGraph graph)
    {
        var allEntities = entities.ToDictionary(e => e.Id);
        var pipes = entities.OfType<PipeEntity>();
        var risers = DetectRisers(pipes);
        
        var results = new List<RiserSchema>();
        int count = 1;

        foreach (var riser in risers)
        {
            riser.RecalculateHydraulics(graph, levels, allEntities);
            
            var schema = new RiserSchema
            {
                RiserName = $"K-{count++}",
                Floors = riser.AnalyzeFloorConnections(graph, levels, allEntities)
            };
            
            schema.TotalFlowRate = riser.TotalLoadUnits; 

            if (schema.Floors.Any())
                results.Add(schema);
        }

        return results;
    }

    public List<RiserSystemModel> DetectRisers(IEnumerable<PipeEntity> pipes)
    {
        var verticalPipes = pipes.Where(IsPipeVertical).ToList();
        var risers = new List<RiserSystemModel>();

        foreach (var pipe in verticalPipes)
        {
            var existingRiser = risers.FirstOrDefault(r => r.IsPointOnRiserLine(pipe.StartPoint, 50.0));
            if (existingRiser != null) existingRiser.AddSegment(pipe);
            else risers.Add(new RiserSystemModel(pipe));
        }

        return risers;
    }

    private bool IsPipeVertical(PipeEntity pipe)
    {
        var dir = (pipe.EndPoint - pipe.StartPoint).Normalize();
        return System.Math.Abs(dir.Z) > VerticalTolerance;
    }
}

/*
   NE: Kolon Matematiksel Graf Modeli (RiserSystemModel)
*/
public class RiserSystemModel
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "K-X";
    public List<Vector3D> Vertices { get; } = new();
    public List<PipeEntity> Segments { get; } = new();
    public double TotalLoadUnits { get; set; }
    public double PeakFlowRate { get; set; } 
    public double CenterX { get; private set; }
    public double CenterY { get; private set; }

    public RiserSystemModel(PipeEntity firstPipe)
    {
        Segments.Add(firstPipe);
        CenterX = (firstPipe.StartPoint.X + firstPipe.EndPoint.X) / 2.0;
        CenterY = (firstPipe.StartPoint.Y + firstPipe.EndPoint.Y) / 2.0;
        
        Vertices.Add(firstPipe.StartPoint);
        Vertices.Add(firstPipe.EndPoint);
    }

    public void AddSegment(PipeEntity pipe)
    {
        Segments.Add(pipe);
        if (!Vertices.Any(v => v.DistanceTo(pipe.StartPoint) < 1.0)) Vertices.Add(pipe.StartPoint);
        if (!Vertices.Any(v => v.DistanceTo(pipe.EndPoint) < 1.0)) Vertices.Add(pipe.EndPoint);
    }

    public bool IsPointOnRiserLine(Vector3D point, double tolerance = 50.0)
    {
        double distXY = System.Math.Sqrt(System.Math.Pow(point.X - CenterX, 2) + System.Math.Pow(point.Y - CenterY, 2));
        return distXY < tolerance;
    }

    public void RecalculateHydraulics(MechanicalTopologyGraph graph, List<MepLevel> levels, Dictionary<Guid, MechanicalEntity> allEntities)
    {
        var floorList = levels.OrderByDescending(l => l.Elevation).ToList();
        double currentAggregatedLU = 0;

        foreach (var level in floorList)
        {
            double floorLU = GetFloorBranchLoad(level, graph, allEntities);
            currentAggregatedLU += floorLU;

            var segmentsInLevel = Segments.Where(s => 
                (Math.Min(s.StartPoint.Z, s.EndPoint.Z) >= level.Elevation - 10) && 
                (Math.Min(s.StartPoint.Z, s.EndPoint.Z) < level.Elevation + level.Height + 10)).ToList();

            foreach (var segment in segmentsInLevel)
            {
                segment.TotalFixtureUnits = currentAggregatedLU;
            }
        }
        
        TotalLoadUnits = currentAggregatedLU;
    }

    private double GetFloorBranchLoad(MepLevel level, MechanicalTopologyGraph graph, Dictionary<Guid, MechanicalEntity> allEntities)
    {
        double totalLU = 0;
        var riserSegmentsInFloor = Segments.Where(s => 
            (Math.Min(s.StartPoint.Z, s.EndPoint.Z) >= level.Elevation - 10) && 
            (Math.Min(s.StartPoint.Z, s.EndPoint.Z) < level.Elevation + level.Height + 10)).ToList();

        foreach (var seg in riserSegmentsInFloor)
        {
            var neighbors = graph.GetNeighbors(seg.Id);
            foreach (var neighborNode in neighbors)
            {
                if (allEntities.TryGetValue(neighborNode.OwnerId, out var neighborEntity))
                {
                    if (neighborEntity is PipeEntity p && Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) < 0.1)
                    {
                        totalLU += p.TotalFixtureUnits;
                    }
                }
            }
        }
        return totalLU;
    }

    public List<FloorSchema> AnalyzeFloorConnections(MechanicalTopologyGraph graph, List<MepLevel> levels, Dictionary<Guid, MechanicalEntity> allEntities)
    {
        var floorSchemas = new List<FloorSchema>();
        foreach (var level in levels.OrderBy(l => l.Elevation))
        {
            var floorSchema = new FloorSchema
            {
                FloorLevel = levels.IndexOf(level),
                FloorName = level.Name,
                Elevation = level.Elevation
            };

            var riserSegmentsInFloor = Segments.Where(s => 
                (Math.Min(s.StartPoint.Z, s.EndPoint.Z) >= level.Elevation - 100 && 
                 Math.Min(s.StartPoint.Z, s.EndPoint.Z) < level.Elevation + level.Height + 100)).ToList();

            foreach (var segment in riserSegmentsInFloor)
            {
                var neighbors = graph.GetNeighbors(segment.Id);
                foreach (var neighborNode in neighbors)
                {
                    if (allEntities.TryGetValue(neighborNode.OwnerId, out var neighborEntity))
                    {
                        if (neighborEntity is PipeEntity pipe && Math.Abs((pipe.EndPoint - pipe.StartPoint).Normalize().Z) < 0.1)
                        {
                            if (floorSchema.BranchDiameter == 0) floorSchema.BranchDiameter = pipe.InnerDiameter;
                            var fixturesInBranch = FindFixturesInBranch(graph, pipe.Id, allEntities, new HashSet<Guid>());
                            foreach (var f in fixturesInBranch)
                            {
                                floorSchema.Fixtures.Add(new FixtureSchema {
                                    Type = f.FixtureType,
                                    FixtureUnit = f.LoadUnits,
                                    ConnectionDiameter = (f as MechanicalEntity)?.InnerDiameter ?? 50.0
                                });
                            }
                        }
                    }
                }
            }
            if (floorSchema.Fixtures.Any()) floorSchemas.Add(floorSchema);
        }
        return floorSchemas;
    }

    private List<SanitaryFixtureEntity> FindFixturesInBranch(MechanicalTopologyGraph graph, Guid currentId, Dictionary<Guid, MechanicalEntity> allEntities, HashSet<Guid> visited)
    {
        var found = new List<SanitaryFixtureEntity>();
        if (visited.Contains(currentId)) return found;
        visited.Add(currentId);

        if (allEntities.TryGetValue(currentId, out var entity))
        {
            if (entity is SanitaryFixtureEntity fixture) { found.Add(fixture); return found; }
            var neighbors = graph.GetNeighbors(currentId);
            foreach (var n in neighbors) found.AddRange(FindFixturesInBranch(graph, n.OwnerId, allEntities, visited));
        }
        return found;
    }
}
