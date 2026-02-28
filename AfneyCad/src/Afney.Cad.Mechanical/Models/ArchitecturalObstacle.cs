using System;
using System.Collections.Generic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Mimari Engel (ArchitecturalObstacle)
    NEDEN: Mekanik tesisat borularının geçemeyeceği veya vitrifiyelerin yerleşemeyeceği alanları (Duvar, Kapı, Pencere, Kolon) tanımlamak için.
    
    NASIL (Mühendislik Modu):
    - Layer-Based Recognition ile otomatik oluşturulur.
    - Tipine göre (Door, Wall) yerleşim algoritmalarını etkiler.
*/
public enum ObstacleType
{
    Wall,
    Door,
    Window,
    Column,
    Furniture
}

public class ArchitecturalObstacle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceEntityId { get; set; } // Orijinal CAD nesnesinin ID'si (Sync için)
    public ObstacleType Type { get; set; }
    public List<Vector3D> Boundary { get; set; } = new();
    public string OriginalLayer { get; set; } = string.Empty;
    public double Height { get; set; } = 3000.0; // Varsayılan kat yüksekliği 3m

    public CadBoundingBox GetBoundingBox()
    {
        if (Boundary == null || Boundary.Count == 0) return CadBoundingBox.Empty;
        
        double minX = Boundary.Min(p => p.X);
        double minY = Boundary.Min(p => p.Y);
        double minZ = Boundary.Min(p => p.Z);
        
        double maxX = Boundary.Max(p => p.X);
        double maxY = Boundary.Max(p => p.Y);
        double maxZ = Boundary.Max(p => p.Z) + Height; // Z ekseninde yükseklik eklendi
        
        return new CadBoundingBox(new Vector3D(minX, minY, minZ), new Vector3D(maxX, maxY, maxZ));
    }
}
