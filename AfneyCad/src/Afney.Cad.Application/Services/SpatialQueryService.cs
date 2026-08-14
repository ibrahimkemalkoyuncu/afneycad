using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.SpatialIndex.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Application.Services;

// QuadTree sarmalayıcı — viewport frustum culling ve hızlı spatial query
public class SpatialQueryService
{
    private QuadTree _tree;
    private readonly CadDatabase _database;
    private int _entityCount;
    private bool _isDirty = true;

    public SpatialQueryService(CadDatabase database)
    {
        _database = database;
        _tree = new QuadTree(new CadBoundingBox(
            new Vector3D(-1e9, -1e9, -1e9),
            new Vector3D(1e9, 1e9, 1e9)));

        _database.EntityAdded += _ => _isDirty = true;
        _database.EntityRemoved += _ => _isDirty = true;
        _database.EntityUpdated += _ => _isDirty = true;
    }

    // İndeksi yeniden oluştur (tam rebuild)
    public void Rebuild()
    {
        var allEntities = _database.GetAllEntities().ToList();

        var bounds = ComputeWorldBounds(allEntities);
        _tree = new QuadTree(bounds);

        foreach (var entity in allEntities)
            _tree.Insert(entity);

        _entityCount = allEntities.Count;
        _isDirty = false;
    }

    // Viewport görünür alanındaki entity'leri sorgula
    public IEnumerable<CadEntity> QueryVisible(CadBoundingBox viewportBounds)
    {
        if (_isDirty) Rebuild();

        var found = new HashSet<CadEntity>();
        _tree.QueryRange(viewportBounds, found);
        return found;
    }

    // Nokta etrafında radius sorgusu (snap/selection için)
    public IEnumerable<CadEntity> QueryRadius(Vector3D center, double radius)
    {
        var searchBox = new CadBoundingBox(
            new Vector3D(center.X - radius, center.Y - radius, center.Z - radius),
            new Vector3D(center.X + radius, center.Y + radius, center.Z + radius));
        return QueryVisible(searchBox);
    }

    // En yakın entity'yi bul
    public CadEntity? FindNearest(Vector3D point, double maxRadius = 5000)
    {
        var candidates = QueryRadius(point, maxRadius);
        return candidates
            .OrderBy(e => (e.GetBoundingBox().Center - point).Length())
            .FirstOrDefault();
    }

    // Frustum culling istatistikleri
    public SpatialStats GetStats()
    {
        if (_isDirty) Rebuild();

        return new SpatialStats
        {
            TotalEntities = _entityCount,
            TreeDepth = EstimateTreeDepth(),
            IsDirty = _isDirty,
            LastRebuildEntityCount = _entityCount
        };
    }

    private CadBoundingBox ComputeWorldBounds(List<CadEntity> entities)
    {
        if (entities.Count == 0)
            return new CadBoundingBox(new Vector3D(-1e6, -1e6, -1e6), new Vector3D(1e6, 1e6, 1e6));

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var ent in entities)
        {
            var bb = ent.GetBoundingBox();
            if (bb.Min.X < minX) minX = bb.Min.X;
            if (bb.Min.Y < minY) minY = bb.Min.Y;
            if (bb.Min.Z < minZ) minZ = bb.Min.Z;
            if (bb.Max.X > maxX) maxX = bb.Max.X;
            if (bb.Max.Y > maxY) maxY = bb.Max.Y;
            if (bb.Max.Z > maxZ) maxZ = bb.Max.Z;
        }

        double margin = Math.Max(maxX - minX, maxY - minY) * 0.1;
        return new CadBoundingBox(
            new Vector3D(minX - margin, minY - margin, minZ - margin),
            new Vector3D(maxX + margin, maxY + margin, maxZ + margin));
    }

    private int EstimateTreeDepth() => (int)Math.Ceiling(Math.Log2(Math.Max(_entityCount, 1)));
}

public class SpatialStats
{
    public int TotalEntities { get; set; }
    public int TreeDepth { get; set; }
    public bool IsDirty { get; set; }
    public int LastRebuildEntityCount { get; set; }
}
