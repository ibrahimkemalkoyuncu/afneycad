using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Infrastructure.Export;

// Gelişmiş DWG Export — linetype, lineweight, text style, hatch pattern koruması
public class EnhancedDwgExportService
{
    private readonly CadDatabase _database;

    public EnhancedDwgExportService(CadDatabase database) => _database = database;

    // Linetype tablosu — AutoCAD standart linetypes
    public static readonly Dictionary<string, double[]> LinetypePatterns = new()
    {
        ["Continuous"] = Array.Empty<double>(),
        ["DASHED"] = new[] { 5.0, -2.5 },
        ["HIDDEN"] = new[] { 2.5, -1.25 },
        ["CENTER"] = new[] { 12.5, -2.5, 2.5, -2.5 },
        ["PHANTOM"] = new[] { 25.0, -2.5, 2.5, -2.5, 2.5, -2.5 },
        ["DOT"] = new[] { 0.0, -2.5 },
        ["DASHDOT"] = new[] { 5.0, -2.5, 0.0, -2.5 },
        ["BORDER"] = new[] { 12.5, -2.5, 2.5, -2.5, 2.5, -2.5 },
        ["DIVIDE"] = new[] { 5.0, -2.5, 0.0, -2.5, 0.0, -2.5 },
    };

    // Lineweight tablosu (mm → AutoCAD index)
    public static readonly Dictionary<double, int> LineweightMap = new()
    {
        [0.0] = 0, [0.05] = 5, [0.09] = 9, [0.13] = 13,
        [0.15] = 15, [0.18] = 18, [0.20] = 20, [0.25] = 25,
        [0.30] = 30, [0.35] = 35, [0.40] = 40, [0.50] = 50,
        [0.53] = 53, [0.60] = 60, [0.70] = 70, [0.80] = 80,
        [0.90] = 90, [1.00] = 100, [1.06] = 106, [1.20] = 120,
        [1.40] = 140, [1.58] = 158, [2.00] = 200, [2.11] = 211,
    };

    // Text style tablosu
    public static readonly Dictionary<string, TextStyleDef> TextStyles = new()
    {
        ["Standard"] = new("Standard", "txt", 0, 1.0),
        ["Romans"] = new("Romans", "romans.shx", 0, 1.0),
        ["Simplex"] = new("Simplex", "simplex.shx", 0, 1.0),
        ["Arial"] = new("Arial", "arial.ttf", 0, 1.0),
        ["ISO"] = new("ISO", "isocp.shx", 0, 1.0),
    };

    // Hatch pattern tablosu (AutoCAD standart)
    public static readonly Dictionary<string, HatchPatternDef> HatchPatterns = new()
    {
        ["ANSI31"] = new("ANSI31", 45, 0, 0, 3.175, new[] { 0.0 }),
        ["ANSI32"] = new("ANSI32", 45, 0, 0, 9.525, new[] { 0.0 }),
        ["ANSI33"] = new("ANSI33", 45, 0, 0, 0.79375, new[] { 0.0 }),
        ["ANSI37"] = new("ANSI37", 45, 0, 0, 3.175, new[] { 0.0 }),
        ["AR-CONC"] = new("AR-CONC", 0, 0, 0, 50, new[] { 40.0, -10.0 }),
        ["AR-SAND"] = new("AR-SAND", 0, 0, 0, 5, new[] { 2.0, -3.0 }),
        ["BRICK"] = new("BRICK", 0, 0, 0, 10, new[] { 25.0, -5.0 }),
        ["CROSS"] = new("CROSS", 0, 0, 0, 5, new[] { 2.5, -2.5 }),
        ["EARTH"] = new("EARTH", 0, 0, 0, 10, new[] { 12.5, -5.0 }),
        ["GRASS"] = new("GRASS", 90, 0, 0, 10, new[] { 5.0, -5.0 }),
        ["INSUL"] = new("INSUL", 0, 0, 0, 15, new[] { 7.5, -7.5 }),
        ["SOLID"] = new("SOLID", 0, 0, 0, 1, Array.Empty<double>()),
    };

    // Entity'den export metadata çıkar
    public ExportMetadata ExtractMetadata(CadEntity entity)
    {
        return new ExportMetadata
        {
            Layer = entity.Layer ?? "0",
            Color = entity.Color,
            AciColor = ArgbToAci(entity.Color),
            Linetype = DetectLinetype(entity),
            Lineweight = DetectLineweight(entity),
            TextStyle = entity is TextEntity ? "Standard" : null,
        };
    }

    // Tüm entity'lerin export istatistikleri
    public ExportStats CalculateStats()
    {
        var entities = _database.GetAllEntities().ToList();
        var stats = new ExportStats
        {
            TotalEntities = entities.Count,
            LineCount = entities.Count(e => e is LineEntity),
            CircleCount = entities.Count(e => e is CircleEntity),
            ArcCount = entities.Count(e => e is ArcEntity),
            TextCount = entities.Count(e => e is TextEntity),
            PolylineCount = entities.Count(e => e is LwPolylineEntity),
            BlockRefCount = entities.Count(e => e is Afney.Cad.Domain.Entities.Basic.BlockReferenceEntity),
            LayerCount = _database.GetLayers().Count(),
            Layers = _database.GetLayers().Select(l => l.Name).ToList(),
        };
        return stats;
    }

    private string DetectLinetype(CadEntity entity)
    {
        if (entity.Layer?.Contains("CENTER") == true) return "CENTER";
        if (entity.Layer?.Contains("HIDDEN") == true) return "HIDDEN";
        if (entity.Layer?.Contains("PHANTOM") == true) return "PHANTOM";
        return "Continuous";
    }

    private double DetectLineweight(CadEntity entity)
    {
        if (entity.Layer?.Contains("WALL") == true || entity.Layer?.Contains("DUVAR") == true) return 0.50;
        if (entity.Layer?.Contains("MEP") == true) return 0.35;
        if (entity.Layer?.Contains("DIM") == true || entity.Layer?.Contains("TEXT") == true) return 0.18;
        return 0.25;
    }

    private int ArgbToAci(uint argb)
    {
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);

        if (r > 200 && g < 50 && b < 50) return 1;
        if (r > 200 && g > 200 && b < 50) return 2;
        if (r < 50 && g > 200 && b < 50) return 3;
        if (r < 50 && g > 200 && b > 200) return 4;
        if (r < 50 && g < 50 && b > 200) return 5;
        if (r > 200 && g < 50 && b > 200) return 6;
        return 7;
    }
}

public record TextStyleDef(string Name, string FontFile, double FixedHeight, double WidthFactor);
public record HatchPatternDef(string Name, double Angle, double OriginX, double OriginY, double Scale, double[] Dashes);

public class ExportMetadata
{
    public string Layer { get; set; } = "0";
    public uint Color { get; set; }
    public int AciColor { get; set; }
    public string Linetype { get; set; } = "Continuous";
    public double Lineweight { get; set; }
    public string? TextStyle { get; set; }
}

public class ExportStats
{
    public int TotalEntities { get; set; }
    public int LineCount { get; set; }
    public int CircleCount { get; set; }
    public int ArcCount { get; set; }
    public int TextCount { get; set; }
    public int PolylineCount { get; set; }
    public int BlockRefCount { get; set; }
    public int LayerCount { get; set; }
    public List<string> Layers { get; set; } = new();
}
