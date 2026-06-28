using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Render.Isometric;

// 3D İzometrik Render Servisi — 30-30 projeksiyon kuralı
// Referans: ISO 5456-3, ASHRAE Plumbing Isometric Drawing Standards
public class IsometricRenderService
{
    // İzometrik açılar (derece)
    public double AngleX { get; set; } = 30.0;
    public double AngleY { get; set; } = 30.0;
    public double ScaleFactor { get; set; } = 1.0;

    // Kamera parametreleri
    public Vector3D CameraPosition { get; set; } = new(1, 1, 1);
    public Vector3D LookAt { get; set; } = Vector3D.Zero;
    public double FieldOfView { get; set; } = 60.0;

    // 3D → 2D İzometrik projeksiyon
    public Vector3D ProjectToIsometric(Vector3D worldPoint)
    {
        double radX = AngleX * Math.PI / 180.0;
        double radY = AngleY * Math.PI / 180.0;

        double x2d = (worldPoint.X - worldPoint.Y) * Math.Cos(radX) * ScaleFactor;
        double y2d = (worldPoint.X + worldPoint.Y) * Math.Sin(radY) * ScaleFactor - worldPoint.Z * ScaleFactor;

        return new Vector3D(x2d, -y2d, 0);
    }

    // Kabinet (Cabinet) projeksiyon — Z ekseni %50 ölçekli
    public Vector3D ProjectToCabinet(Vector3D worldPoint)
    {
        double angle = 45.0 * Math.PI / 180.0;
        double x2d = worldPoint.X + worldPoint.Z * 0.5 * Math.Cos(angle);
        double y2d = worldPoint.Y + worldPoint.Z * 0.5 * Math.Sin(angle);
        return new Vector3D(x2d * ScaleFactor, y2d * ScaleFactor, 0);
    }

    // Perspektif projeksiyon (basit pinhole)
    public Vector3D ProjectToPerspective(Vector3D worldPoint, double focalLength = 500.0)
    {
        var relative = worldPoint - CameraPosition;
        double dist = relative.Length();
        if (dist < 1.0) return new Vector3D(worldPoint.X, worldPoint.Y, 0);

        double scale = focalLength / (focalLength + relative.Z);
        return new Vector3D(relative.X * scale, relative.Y * scale, 0);
    }

    // Entity listesini izometrik olarak dönüştür
    public List<ProjectedEntity> ProjectEntities(IEnumerable<CadEntity> entities, ProjectionMode mode = ProjectionMode.Isometric)
    {
        var result = new List<ProjectedEntity>();

        foreach (var entity in entities)
        {
            var bbox = entity.GetBoundingBox();
            var center3D = bbox.Center;

            var projected = mode switch
            {
                ProjectionMode.Isometric => ProjectToIsometric(center3D),
                ProjectionMode.Cabinet => ProjectToCabinet(center3D),
                ProjectionMode.Perspective => ProjectToPerspective(center3D),
                _ => ProjectToIsometric(center3D)
            };

            result.Add(new ProjectedEntity
            {
                OriginalEntity = entity,
                ProjectedCenter = projected,
                Depth = CalculateDepth(center3D, mode),
                IsVisible = true
            });
        }

        // Z-sıralama (painter's algorithm)
        result.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        return result;
    }

    // Derinlik hesabı (z-buffer benzeri)
    private double CalculateDepth(Vector3D point, ProjectionMode mode)
    {
        var dir = (point - CameraPosition);
        return dir.X + dir.Y + dir.Z; // basit depth sort
    }

    // Izometrik grid çizgileri üret
    public List<(Vector3D Start, Vector3D End)> GenerateIsometricGrid(double size = 10000, double spacing = 1000)
    {
        var lines = new List<(Vector3D, Vector3D)>();
        int count = (int)(size / spacing);

        for (int i = -count; i <= count; i++)
        {
            double offset = i * spacing;
            // X yönü
            var p1 = ProjectToIsometric(new Vector3D(offset, -size, 0));
            var p2 = ProjectToIsometric(new Vector3D(offset, size, 0));
            lines.Add((p1, p2));

            // Y yönü
            var p3 = ProjectToIsometric(new Vector3D(-size, offset, 0));
            var p4 = ProjectToIsometric(new Vector3D(size, offset, 0));
            lines.Add((p3, p4));
        }

        return lines;
    }

    // Eksen göstergesi (XYZ triad)
    public List<(Vector3D Start, Vector3D End, string Label, uint Color)> GenerateAxisIndicator(Vector3D origin, double length = 2000)
    {
        return new()
        {
            (ProjectToIsometric(origin), ProjectToIsometric(origin + new Vector3D(length, 0, 0)), "X", 0xFFFF0000),
            (ProjectToIsometric(origin), ProjectToIsometric(origin + new Vector3D(0, length, 0)), "Y", 0xFF00FF00),
            (ProjectToIsometric(origin), ProjectToIsometric(origin + new Vector3D(0, 0, length)), "Z", 0xFF0044FF),
        };
    }

    // ViewCube yönlendirme
    public void SetView(IsometricView view)
    {
        (AngleX, AngleY, CameraPosition) = view switch
        {
            IsometricView.SouthWest => (30, 30, new Vector3D(1, 1, 1)),
            IsometricView.SouthEast => (30, 30, new Vector3D(-1, 1, 1)),
            IsometricView.NorthWest => (30, 30, new Vector3D(1, -1, 1)),
            IsometricView.NorthEast => (30, 30, new Vector3D(-1, -1, 1)),
            IsometricView.Top => (0, 90, new Vector3D(0, 0, 1)),
            IsometricView.Front => (0, 0, new Vector3D(0, 1, 0)),
            IsometricView.Right => (90, 0, new Vector3D(1, 0, 0)),
            _ => (30, 30, new Vector3D(1, 1, 1))
        };
    }
}

public enum ProjectionMode { Isometric, Cabinet, Perspective }
public enum IsometricView { SouthWest, SouthEast, NorthWest, NorthEast, Top, Front, Right }

public class ProjectedEntity
{
    public CadEntity OriginalEntity { get; set; } = null!;
    public Vector3D ProjectedCenter { get; set; }
    public double Depth { get; set; }
    public bool IsVisible { get; set; }
}
