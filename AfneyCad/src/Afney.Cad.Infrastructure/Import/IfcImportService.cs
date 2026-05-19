using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Infrastructure.Import;

/*
   NE: IFC İçeri Aktarma Servisi (IfcImportService)
   NEDEN: Revit, ArchiCAD, Archicad, Tekla gibi yazılımlardan üretilen mimari modelleri
          AfneyCAD'e çizerek MEP tesisat tasarımına altlık oluşturmak.

   DESTEKLENEN: IFC 2x3 ve IFC 4 STEP/P21 metin formatı (.ifc)

   İÇERİ AKTARILAN ELEMANLAR:
   - IfcWall / IfcWallStandardCase  → LineEntity (plan görünümü)
   - IfcSlab                        → RectangleEntity (kat döşemesi sınırı)
   - IfcWindow / IfcDoor            → LineEntity + açıklık sembolü
   - IfcSpace                       → Layer "ARCH-SPACE" metin etiketi

   SINIRLAMALAR:
   - 3D geometri (IfcExtrudedAreaSolid) parse edilmez; yerine BoundingBox kullanılır.
   - Koordinat dönüşümü: IFCLOCALPLACEMENT yalnızca X/Y öteleme desteklenir.
   - Birim: mm varsayılır (IfcSIUnit METRE ise 1000 ile çarpılır).
*/
public class IfcImportService
{
    private readonly CadDatabase _database;

    private const string LayerWall   = "ARCH-WALL";
    private const string LayerSlab   = "ARCH-SLAB";
    private const string LayerWindow = "ARCH-WINDOW";
    private const string LayerDoor   = "ARCH-DOOR";
    private const string LayerSpace  = "ARCH-SPACE";

    // Renk sabitleri (ARGB)
    private const uint ColorWall   = 0xFF808080; // Gri
    private const uint ColorSlab   = 0xFF606060; // Koyu gri
    private const uint ColorWindow = 0xFF00BFFF; // Açık mavi
    private const uint ColorDoor   = 0xFFDEB887; // Bej
    private const uint ColorSpace  = 0xFF404040; // Soluk gri

    public IfcImportService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: IFC Dosyasını İçeri Aktar
       NEDEN: Mimari modeli AfneyCAD veritabanına eklemek.
       DÖNÜŞ: İçeri aktarılan eleman sayıları ve uyarı mesajları
    */
    public IfcImportResult Import(string filePath, IfcImportOptions? options = null)
    {
        options ??= new IfcImportOptions();
        var result = new IfcImportResult { FilePath = filePath };

        if (!File.Exists(filePath))
        {
            result.Errors.Add($"Dosya bulunamadı: {filePath}");
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            var entities = ParseIfcEntities(lines);

            double unitScale = DetectUnitScale(entities); // mm=1.0, m=1000.0
            var placements = ParsePlacements(entities);
            var products   = ParseProducts(entities, placements, unitScale);

            EnsureLayers();

            foreach (var product in products)
            {
                if (!options.ImportWalls    && product.IfcType.Contains("WALL"))   continue;
                if (!options.ImportSlabs    && product.IfcType == "IFCSLAB")       continue;
                if (!options.ImportWindows  && product.IfcType == "IFCWINDOW")     continue;
                if (!options.ImportDoors    && product.IfcType == "IFCDOOR")       continue;
                if (!options.ImportSpaces   && product.IfcType == "IFCSPACE")      continue;

                var cadEntities = BuildCadEntities(product);
                foreach (var e in cadEntities)
                {
                    _database.AddEntity(e);
                }

                switch (product.IfcType)
                {
                    case "IFCWALL":
                    case "IFCWALLSTANDARDCASE": result.WallCount++;   break;
                    case "IFCSLAB":             result.SlabCount++;   break;
                    case "IFCWINDOW":           result.WindowCount++; break;
                    case "IFCDOOR":             result.DoorCount++;   break;
                    case "IFCSPACE":            result.SpaceCount++;  break;
                }
            }

            result.Success = true;
            result.Warnings.Add($"Birim ölçek: ×{unitScale} (mm cinsinden)");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Parse hatası: {ex.Message}");
            Serilog.Log.Error(ex, "IFC import hatası: {File}", filePath);
        }

        return result;
    }

    // ── PARSER ────────────────────────────────────────────────────────────────

    private static Dictionary<int, IfcRawEntity> ParseIfcEntities(string[] lines)
    {
        var dict = new Dictionary<int, IfcRawEntity>();
        // IFC STEP satırı: #ID = IFCTYPE(arg1,arg2,...);
        var linePattern = new Regex(@"^#(\d+)\s*=\s*([A-Z0-9]+)\((.*)\)\s*;?\s*$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        foreach (var line in lines)
        {
            var m = linePattern.Match(line.Trim());
            if (!m.Success) continue;

            int id = int.Parse(m.Groups[1].Value);
            string type = m.Groups[2].Value;
            string args = m.Groups[3].Value;

            dict[id] = new IfcRawEntity { Id = id, Type = type, RawArgs = args, Args = SplitArgs(args) };
        }
        return dict;
    }

    // IFC arg'larını virgülle böler (iç içe parantezlere dikkat eder)
    private static List<string> SplitArgs(string raw)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(raw[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(raw[start..].Trim());
        return result;
    }

    private static double DetectUnitScale(Dictionary<int, IfcRawEntity> entities)
    {
        foreach (var e in entities.Values)
        {
            if (e.Type == "IFCSIUNIT" && e.RawArgs.Contains("LENGTHUNIT"))
            {
                if (e.RawArgs.Contains("METRE") && !e.RawArgs.Contains("MILLI"))
                    return 1000.0; // IFC metre → AfneyCAD mm
            }
        }
        return 1.0; // Varsayılan: mm
    }

    private static Dictionary<int, Vector3D> ParsePlacements(Dictionary<int, IfcRawEntity> entities)
    {
        var result = new Dictionary<int, Vector3D>();

        foreach (var e in entities.Values)
        {
            if (e.Type != "IFCLOCALPLACEMENT") continue;

            // IFCLOCALPLACEMENT(#parentId, #axisPlacementId)
            if (e.Args.Count >= 2 && TryParseRef(e.Args[1], out int axisId) &&
                entities.TryGetValue(axisId, out var axis) &&
                (axis.Type == "IFCAXIS2PLACEMENT3D" || axis.Type == "IFCAXIS2PLACEMENT2D"))
            {
                // IFCAXIS2PLACEMENT3D(#locationId, ...)
                if (axis.Args.Count >= 1 && TryParseRef(axis.Args[0], out int locId) &&
                    entities.TryGetValue(locId, out var loc) &&
                    loc.Type == "IFCCARTESIANPOINT")
                {
                    double x = 0, y = 0, z = 0;
                    var coords = loc.Args;
                    if (coords.Count >= 1) double.TryParse(coords[0], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out x);
                    if (coords.Count >= 2) double.TryParse(coords[1], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out y);
                    if (coords.Count >= 3) double.TryParse(coords[2], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out z);
                    result[e.Id] = new Vector3D(x, y, z);
                }
            }
        }
        return result;
    }

    private static List<IfcProduct> ParseProducts(
        Dictionary<int, IfcRawEntity> entities,
        Dictionary<int, Vector3D> placements,
        double scale)
    {
        var result = new List<IfcProduct>();
        var productTypes = new HashSet<string>
        {
            "IFCWALL", "IFCWALLSTANDARDCASE", "IFCSLAB",
            "IFCWINDOW", "IFCDOOR", "IFCSPACE"
        };

        foreach (var e in entities.Values)
        {
            if (!productTypes.Contains(e.Type)) continue;

            var product = new IfcProduct { Id = e.Id, IfcType = e.Type };

            // GlobalId (args[0]), Name (args[2])
            if (e.Args.Count >= 3)
                product.Name = e.Args[2].Trim('\'');

            // ObjectPlacement → konum
            if (e.Args.Count >= 6 && TryParseRef(e.Args[5], out int placId) &&
                placements.TryGetValue(placId, out var pos))
            {
                product.Origin = new Vector3D(pos.X * scale, pos.Y * scale, pos.Z * scale);
            }

            // Representation → boyut (BoundingBox fallback)
            if (e.Args.Count >= 7 && TryParseRef(e.Args[6], out int repId) &&
                entities.TryGetValue(repId, out var rep))
            {
                ExtractDimensions(rep, entities, scale, product);
            }

            result.Add(product);
        }
        return result;
    }

    private static void ExtractDimensions(IfcRawEntity rep,
        Dictionary<int, IfcRawEntity> entities, double scale, IfcProduct product)
    {
        // IFCPRODUCTDEFINITIONSHAPE → IFCSHAPEREPRESENTATION → geometri arama
        // Basit yaklaşım: IFCRECTANGLEPROFILEDEF veya IFCEXTRUDEDAREASOLID bul
        foreach (var e in entities.Values)
        {
            if (e.Type == "IFCRECTANGLEPROFILEDEF" && e.Args.Count >= 4)
            {
                if (double.TryParse(e.Args[2], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double xDim) &&
                    double.TryParse(e.Args[3], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double yDim))
                {
                    product.Width  = xDim * scale;
                    product.Depth  = yDim * scale;
                }
            }
            if (e.Type == "IFCEXTRUDEDAREASOLID" && e.Args.Count >= 4)
            {
                if (double.TryParse(e.Args[3], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double height))
                {
                    product.Height = height * scale;
                }
            }
        }
    }

    private static bool TryParseRef(string arg, out int id)
    {
        id = 0;
        arg = arg.Trim();
        if (arg.StartsWith('#'))
            return int.TryParse(arg[1..], out id);
        return false;
    }

    // ── CAD ENTITY ÜRETİMİ ────────────────────────────────────────────────────

    private static IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> BuildCadEntities(IfcProduct p)
    {
        double w = p.Width  > 0 ? p.Width  : 200;  // Varsayılan duvar kalınlığı 200mm
        double d = p.Depth  > 0 ? p.Depth  : 3000; // Varsayılan uzunluk 3m
        var origin = p.Origin;

        switch (p.IfcType)
        {
            case "IFCWALL":
            case "IFCWALLSTANDARDCASE":
            {
                // Plan görünümü: iki paralel çizgi (duvar kalınlığı)
                var p1 = new Vector3D(origin.X,       origin.Y,       0);
                var p2 = new Vector3D(origin.X + d,   origin.Y,       0);
                var p3 = new Vector3D(origin.X,       origin.Y + w,   0);
                var p4 = new Vector3D(origin.X + d,   origin.Y + w,   0);
                yield return MakeLine(p1, p2, LayerWall, ColorWall);
                yield return MakeLine(p3, p4, LayerWall, ColorWall);
                yield return MakeLine(p1, p3, LayerWall, ColorWall);
                yield return MakeLine(p2, p4, LayerWall, ColorWall);
                break;
            }
            case "IFCSLAB":
            {
                var p1 = new Vector3D(origin.X,     origin.Y,     0);
                var p2 = new Vector3D(origin.X + w, origin.Y,     0);
                var p3 = new Vector3D(origin.X + w, origin.Y + d, 0);
                var p4 = new Vector3D(origin.X,     origin.Y + d, 0);
                yield return MakeLine(p1, p2, LayerSlab, ColorSlab);
                yield return MakeLine(p2, p3, LayerSlab, ColorSlab);
                yield return MakeLine(p3, p4, LayerSlab, ColorSlab);
                yield return MakeLine(p4, p1, LayerSlab, ColorSlab);
                break;
            }
            case "IFCWINDOW":
            {
                double ww = p.Width > 0 ? p.Width : 900;
                var p1 = new Vector3D(origin.X,       origin.Y - 50, 0);
                var p2 = new Vector3D(origin.X + ww,  origin.Y - 50, 0);
                var mid = new Vector3D(origin.X + ww / 2, origin.Y, 0);
                yield return MakeLine(p1, p2, LayerWindow, ColorWindow);
                yield return MakeLine(new Vector3D(origin.X, origin.Y - 100, 0), mid, LayerWindow, ColorWindow);
                yield return MakeLine(new Vector3D(origin.X + ww, origin.Y - 100, 0), mid, LayerWindow, ColorWindow);
                break;
            }
            case "IFCDOOR":
            {
                double dw = p.Width > 0 ? p.Width : 900;
                var p1 = new Vector3D(origin.X,       origin.Y, 0);
                var p2 = new Vector3D(origin.X + dw,  origin.Y, 0);
                var arc = new Vector3D(origin.X,       origin.Y - dw, 0); // Kapı yay başlangıç
                yield return MakeLine(p1, p2, LayerDoor, ColorDoor);
                yield return MakeLine(p2, arc, LayerDoor, ColorDoor);
                break;
            }
            case "IFCSPACE":
            {
                // Sadece metin etiketi
                var label = new TextEntity(p.Name, origin, 200)
                {
                    Layer = LayerSpace,
                    Color = ColorSpace
                };
                yield return label;
                break;
            }
        }
    }

    private static LineEntity MakeLine(Vector3D start, Vector3D end, string layer, uint color) =>
        new(start, end) { Layer = layer, Color = color };

    private void EnsureLayers()
    {
        EnsureLayer(LayerWall,   ColorWall,   "Mimari Duvarlar");
        EnsureLayer(LayerSlab,   ColorSlab,   "Döşemeler");
        EnsureLayer(LayerWindow, ColorWindow, "Pencereler");
        EnsureLayer(LayerDoor,   ColorDoor,   "Kapılar");
        EnsureLayer(LayerSpace,  ColorSpace,  "Mekanlar");
    }

    private void EnsureLayer(string name, uint color, string description)
    {
        bool exists = _database.GetLayers()
            .Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            _database.AddLayer(new Afney.Cad.Domain.Tables.CadLayer(name)
            {
                Description = description
            });
        }
    }

    // ── İÇ VERİ MODELLERİ ────────────────────────────────────────────────────

    private class IfcRawEntity
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string RawArgs { get; set; } = "";
        public List<string> Args { get; set; } = [];
    }

    private class IfcProduct
    {
        public int Id { get; set; }
        public string IfcType { get; set; } = "";
        public string Name { get; set; } = "";
        public Vector3D Origin { get; set; } = new(0, 0, 0);
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Height { get; set; }
    }
}

// ── GENEL VERİ MODELLERİ ─────────────────────────────────────────────────────

public class IfcImportOptions
{
    public bool ImportWalls   { get; set; } = true;
    public bool ImportSlabs   { get; set; } = true;
    public bool ImportWindows { get; set; } = true;
    public bool ImportDoors   { get; set; } = true;
    public bool ImportSpaces  { get; set; } = false;
}

public class IfcImportResult
{
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public int WallCount   { get; set; }
    public int SlabCount   { get; set; }
    public int WindowCount { get; set; }
    public int DoorCount   { get; set; }
    public int SpaceCount  { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors   { get; set; } = [];

    public int TotalImported => WallCount + SlabCount + WindowCount + DoorCount + SpaceCount;

    public override string ToString() =>
        $"IFC Import: {TotalImported} eleman " +
        $"(Duvar={WallCount}, Döşeme={SlabCount}, Pencere={WindowCount}, Kapı={DoorCount}) " +
        $"— {(Success ? "BAŞARILI" : "HATA")}";
}
