using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Application.Services;

// Gelişmiş seçim modları: Fence, Polygon, Last, All, Previous
public class AdvancedSelectionService
{
    private readonly CadDatabase _database;
    private List<CadEntity>? _previousSelection;

    public AdvancedSelectionService(CadDatabase database) => _database = database;

    // Fence seçimi — çizgi boyunca kesişen tüm entity'ler
    public List<CadEntity> SelectByFence(List<Vector3D> fencePoints)
    {
        var result = new List<CadEntity>();
        if (fencePoints.Count < 2) return result;

        foreach (var entity in _database.GetAllEntities())
        {
            var bbox = entity.GetBoundingBox();
            for (int i = 0; i < fencePoints.Count - 1; i++)
            {
                if (LineIntersectsBBox(fencePoints[i], fencePoints[i + 1], bbox))
                {
                    result.Add(entity);
                    break;
                }
            }
        }

        _previousSelection = result;
        return result;
    }

    // Polygon seçimi — kapalı çokgen içindeki tüm entity'ler
    public List<CadEntity> SelectByPolygon(List<Vector3D> polygonPoints)
    {
        var result = new List<CadEntity>();
        if (polygonPoints.Count < 3) return result;

        foreach (var entity in _database.GetAllEntities())
        {
            var center = entity.GetBoundingBox().Center;
            if (IsPointInPolygon(center, polygonPoints))
                result.Add(entity);
        }

        _previousSelection = result;
        return result;
    }

    // Son seçimi geri getir
    public List<CadEntity> SelectPrevious() => _previousSelection ?? new List<CadEntity>();

    // Tüm entity'leri seç
    public List<CadEntity> SelectAll() => _database.GetAllEntities().ToList();

    // Katman bazlı seçim
    public List<CadEntity> SelectByLayer(string layerName)
        => _database.GetAllEntities().Where(e => e.Layer == layerName).ToList();

    // Tip bazlı seçim
    public List<CadEntity> SelectByType<T>() where T : CadEntity
        => _database.GetAllEntities().OfType<T>().Cast<CadEntity>().ToList();

    // Renk bazlı seçim
    public List<CadEntity> SelectByColor(uint color)
        => _database.GetAllEntities().Where(e => e.Color == color).ToList();

    private bool LineIntersectsBBox(Vector3D p1, Vector3D p2, Afney.Cad.Geometry.Primitives.CadBoundingBox bbox)
    {
        double minX = Math.Min(bbox.Min.X, bbox.Max.X);
        double maxX = Math.Max(bbox.Min.X, bbox.Max.X);
        double minY = Math.Min(bbox.Min.Y, bbox.Max.Y);
        double maxY = Math.Max(bbox.Min.Y, bbox.Max.Y);

        if (Math.Max(p1.X, p2.X) < minX || Math.Min(p1.X, p2.X) > maxX) return false;
        if (Math.Max(p1.Y, p2.Y) < minY || Math.Min(p1.Y, p2.Y) > maxY) return false;

        return true;
    }

    private bool IsPointInPolygon(Vector3D point, List<Vector3D> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Y < point.Y && polygon[j].Y >= point.Y || polygon[j].Y < point.Y && polygon[i].Y >= point.Y)
                && (polygon[i].X + (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < point.X))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }
}
