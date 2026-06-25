using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

public class RouteOptions
{
    public double GridStep      { get; set; } = 100;
    public double WallOffset    { get; set; } = 50;
    public double MinBendRadius { get; set; } = 150;
    public bool   PreferOrthogonal { get; set; } = true;
    public bool   AvoidObstacles   { get; set; } = true;
    public MechanicalSystemType SystemType { get; set; } = MechanicalSystemType.DomesticColdWater;
    public double Diameter { get; set; } = 20;
}

public class RouteResult
{
    public List<Vector3D> Waypoints { get; set; } = new();
    public double TotalLength { get; set; }
    public int BendCount { get; set; }
    public double EstimatedCost { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class AutoRouteService
{
    private readonly CadDatabase _database;

    public AutoRouteService(CadDatabase database)
    {
        _database = database;
    }

    public RouteResult FindRoute(Vector3D start, Vector3D end, RouteOptions options)
    {
        var result = new RouteResult();

        if (start.DistanceTo(end) < 1e-6)
        {
            result.Message = "Başlangıç ve bitiş noktası aynı.";
            return result;
        }

        var obstacles = CollectObstacles(options);
        var path = AStarSearch(start, end, options, obstacles);

        if (path.Count == 0)
        {
            path = FallbackOrthogonalRoute(start, end);
            result.Message = "Engel bulunamadı — ortogonal rota kullanıldı.";
        }

        result.Waypoints = path;
        result.Success = path.Count >= 2;
        result.TotalLength = CalculatePathLength(path);
        result.BendCount = CountBends(path);

        var costSvc = new RealTimeCostService();
        result.EstimatedCost = costSvc.CalculateSinglePipeCost(result.TotalLength, PipeMaterial.PPRC_PN20, options.Diameter);

        if (string.IsNullOrEmpty(result.Message))
            result.Message = $"Rota bulundu: {result.TotalLength / 1000.0:F2} m, {result.BendCount} dirsek.";

        return result;
    }

    public List<PipeEntity> CreatePipesFromRoute(RouteResult route, RouteOptions options)
    {
        var pipes = new List<PipeEntity>();
        for (int i = 0; i < route.Waypoints.Count - 1; i++)
        {
            var pipe = new PipeEntity(route.Waypoints[i], route.Waypoints[i + 1], options.Diameter)
            {
                SystemType = options.SystemType,
                Layer = options.SystemType == MechanicalSystemType.DomesticColdWater ? "MEP_TEMIZ_SU" : "MEP_SICAK_SU",
                Color = options.SystemType == MechanicalSystemType.DomesticColdWater ? 0xFF0088FF : 0xFFFF4444
            };
            pipes.Add(pipe);
        }
        return pipes;
    }

    private List<Vector3D> AStarSearch(Vector3D start, Vector3D end, RouteOptions options, List<CadBoundingBox> obstacles)
    {
        double step = options.GridStep;
        var openSet = new PriorityQueue<Vector3D, double>();
        var cameFrom = new Dictionary<string, Vector3D>();
        var gScore = new Dictionary<string, double>();
        var visited = new HashSet<string>();

        string Key(Vector3D v) => $"{Math.Round(v.X / step) * step},{Math.Round(v.Y / step) * step}";

        openSet.Enqueue(start, 0);
        gScore[Key(start)] = 0;

        Vector3D[] directions = options.PreferOrthogonal
            ? new[] { new Vector3D(step, 0, 0), new Vector3D(-step, 0, 0), new Vector3D(0, step, 0), new Vector3D(0, -step, 0) }
            : new[] { new Vector3D(step, 0, 0), new Vector3D(-step, 0, 0), new Vector3D(0, step, 0), new Vector3D(0, -step, 0),
                      new Vector3D(step, step, 0), new Vector3D(-step, step, 0), new Vector3D(step, -step, 0), new Vector3D(-step, -step, 0) };

        int maxIter = 5000;
        int iter = 0;

        while (openSet.Count > 0 && iter++ < maxIter)
        {
            var current = openSet.Dequeue();
            string ck = Key(current);

            if (current.DistanceTo(end) < step * 1.5)
            {
                var path = ReconstructPath(cameFrom, current, start);
                path.Add(end);
                return SimplifyPath(path);
            }

            if (visited.Contains(ck)) continue;
            visited.Add(ck);

            foreach (var dir in directions)
            {
                var neighbor = new Vector3D(current.X + dir.X, current.Y + dir.Y, current.Z);
                string nk = Key(neighbor);

                if (visited.Contains(nk)) continue;
                if (options.AvoidObstacles && IsInsideObstacle(neighbor, obstacles)) continue;

                double tentG = gScore.GetValueOrDefault(ck, double.MaxValue) + dir.Length();

                if (tentG < gScore.GetValueOrDefault(nk, double.MaxValue))
                {
                    cameFrom[nk] = current;
                    gScore[nk] = tentG;
                    double fScore = tentG + neighbor.DistanceTo(end);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }

        return new List<Vector3D>();
    }

    private List<Vector3D> ReconstructPath(Dictionary<string, Vector3D> cameFrom, Vector3D current, Vector3D start)
    {
        double step = 100;
        string Key(Vector3D v) => $"{Math.Round(v.X / step) * step},{Math.Round(v.Y / step) * step}";

        var path = new List<Vector3D> { current };
        string ck = Key(current);
        while (cameFrom.ContainsKey(ck))
        {
            current = cameFrom[ck];
            ck = Key(current);
            path.Insert(0, current);
        }
        return path;
    }

    private List<Vector3D> SimplifyPath(List<Vector3D> path)
    {
        if (path.Count <= 2) return path;
        var simplified = new List<Vector3D> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            var prev = simplified[^1];
            var curr = path[i];
            var next = path[i + 1];
            var d1 = curr - prev;
            var d2 = next - curr;
            bool sameDir = Math.Abs(d1.X * d2.Y - d1.Y * d2.X) > 1e-6;
            if (sameDir) simplified.Add(curr);
        }
        simplified.Add(path[^1]);
        return simplified;
    }

    private List<Vector3D> FallbackOrthogonalRoute(Vector3D start, Vector3D end)
    {
        var mid = new Vector3D(end.X, start.Y, start.Z);
        return new List<Vector3D> { start, mid, end };
    }

    private List<CadBoundingBox> CollectObstacles(RouteOptions options)
    {
        var obstacles = new List<CadBoundingBox>();
        foreach (var entity in _database.GetAllEntities())
        {
            if (entity is PipeEntity || entity is SanitaryFixtureEntity) continue;
            if (entity.Layer?.Contains("BUILD") == true || entity.Layer?.Contains("WALL") == true)
            {
                var bb = entity.GetBoundingBox();
                var expanded = new CadBoundingBox(
                    new Vector3D(bb.Min.X - options.WallOffset, bb.Min.Y - options.WallOffset, bb.Min.Z),
                    new Vector3D(bb.Max.X + options.WallOffset, bb.Max.Y + options.WallOffset, bb.Max.Z));
                obstacles.Add(expanded);
            }
        }
        return obstacles;
    }

    private bool IsInsideObstacle(Vector3D point, List<CadBoundingBox> obstacles)
    {
        foreach (var bb in obstacles)
            if (point.X >= bb.Min.X && point.X <= bb.Max.X && point.Y >= bb.Min.Y && point.Y <= bb.Max.Y)
                return true;
        return false;
    }

    private double CalculatePathLength(List<Vector3D> path)
    {
        double len = 0;
        for (int i = 0; i < path.Count - 1; i++)
            len += path[i].DistanceTo(path[i + 1]);
        return len;
    }

    private int CountBends(List<Vector3D> path)
    {
        int bends = 0;
        for (int i = 1; i < path.Count - 1; i++)
        {
            var d1 = path[i] - path[i - 1];
            var d2 = path[i + 1] - path[i];
            if (Math.Abs(d1.X * d2.Y - d1.Y * d2.X) > 1e-6) bends++;
        }
        return bends;
    }
}
