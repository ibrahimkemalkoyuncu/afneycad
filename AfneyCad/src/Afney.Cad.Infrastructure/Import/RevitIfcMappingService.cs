using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Afney.Cad.Infrastructure.Import;

// Revit MEP IFC → AfneyCAD entity mapping servisi
// Revit IFC export formatındaki MEP entity'lerini tanır ve dönüştürür
public class RevitIfcMappingService
{
    private readonly CadDatabase _database;

    // Revit IFC system classification → AfneyCAD MechanicalSystemType mapping
    private static readonly Dictionary<string, MechanicalSystemType> SystemMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Domestic Cold Water"] = MechanicalSystemType.DomesticColdWater,
        ["Domestic Hot Water"] = MechanicalSystemType.DomesticHotWater,
        ["Sanitary"] = MechanicalSystemType.WasteWater,
        ["Waste"] = MechanicalSystemType.WasteWater,
        ["Storm"] = MechanicalSystemType.RainWater,
        ["Vent"] = MechanicalSystemType.Ventilation,
        ["Supply Air"] = MechanicalSystemType.Ventilation,
        ["Return Air"] = MechanicalSystemType.Ventilation,
        ["Exhaust Air"] = MechanicalSystemType.Ventilation,
        ["Fire Protection"] = MechanicalSystemType.FireProtection,
        ["Sprinkler"] = MechanicalSystemType.FireProtection,
        ["Gas"] = MechanicalSystemType.Gas,
    };

    // Revit material → AfneyCAD PipeMaterial mapping
    private static readonly Dictionary<string, PipeMaterial> MaterialMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PPR"] = PipeMaterial.PPRC_PN20,
        ["PP-R"] = PipeMaterial.PPRC_PN20,
        ["Polypropylene"] = PipeMaterial.PPRC_PN20,
        ["PVC"] = PipeMaterial.PVC_SN4,
        ["Polyvinyl Chloride"] = PipeMaterial.PVC_SN4,
        ["Galvanized Steel"] = PipeMaterial.Steel_Galvanized,
        ["Steel"] = PipeMaterial.Steel_Galvanized,
        ["Carbon Steel"] = PipeMaterial.Steel_Galvanized,
        ["PEX"] = PipeMaterial.PEX_b,
        ["Cross-linked Polyethylene"] = PipeMaterial.PEX_b,
    };

    // Revit fixture family → AfneyCAD fixture type mapping
    private static readonly Dictionary<string, (string Type, double FU)> FixtureMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Water Closet"] = ("WC", 2.5),
        ["Lavatory"] = ("Washbasin", 0.5),
        ["Sink"] = ("KitchenSink", 1.0),
        ["Kitchen Sink"] = ("KitchenSink", 1.0),
        ["Bathtub"] = ("Bathtub", 1.5),
        ["Shower"] = ("Shower", 1.0),
        ["Urinal"] = ("Urinal", 0.5),
        ["Floor Drain"] = ("FloorDrain", 0.0),
        ["Washing Machine"] = ("WashingMachine", 1.0),
        ["Dishwasher"] = ("Dishwasher", 1.0),
    };

    public RevitIfcMappingService(CadDatabase database) => _database = database;

    public RevitImportResult ImportRevitIfc(string filePath)
    {
        var result = new RevitImportResult();
        if (!File.Exists(filePath)) return result;

        var lines = File.ReadAllLines(filePath);
        var stepEntities = ParseStepFile(lines);

        // NE/NEDEN: O(n²) → O(n) — önceden ExtractPlacementPoints/ExtractMaterial/
        // ExtractSystemClassification her çağrıda `stepEntities.FirstOrDefault(...)` ile
        // TÜM listeyi lineer tarıyordu (her boru/cihaz/kanal dönüştürülürken tekrar tekrar).
        // IfcImportService.cs'deki desenle aynı: Id'ye göre bir kez Dictionary kuruluyor;
        // IFCMATERIAL/IFCSYSTEM referansları için de bir kez (hedef-Id → tanım-entity)
        // ön-indeks çıkarılıyor. Dönüşüm döngüleri artık O(1) sözlük sorgusu yapıyor.
        // (ToDictionary yerine döngü kullanılıyor — malformed STEP dosyasında yinelenen
        //  Id'ler ToDictionary'nin ArgumentException fırlatmasına yol açabilir; burada
        //  IfcImportService.cs'deki gibi son-yazan-kazanır davranışı tercih edildi.)
        var byId = new Dictionary<string, StepEntity>(StringComparer.Ordinal);
        foreach (var e in stepEntities) byId[e.Id] = e;
        var materialByTargetId = BuildReverseRefIndex(stepEntities, "IFCMATERIAL");
        var systemByTargetId = BuildReverseRefIndex(stepEntities, "IFCSYSTEM");

        // IfcPipeSegment → PipeEntity
        foreach (var ent in stepEntities.Where(e => e.Type is "IFCPIPESEGMENT" or "IFCFLOWSEGMENT"))
        {
            var pipe = ConvertToPipe(ent, byId, materialByTargetId, systemByTargetId);
            if (pipe != null)
            {
                _database.AddEntity(pipe);
                result.PipeCount++;
            }
        }

        // IfcFlowTerminal → SanitaryFixtureEntity
        foreach (var ent in stepEntities.Where(e => e.Type is "IFCFLOWTERMINAL" or "IFCSANITARYTERMINAL"))
        {
            var fixture = ConvertToFixture(ent, byId);
            if (fixture != null)
            {
                _database.AddEntity(fixture);
                result.FixtureCount++;
            }
        }

        // IfcDuctSegment → DuctEntity
        foreach (var ent in stepEntities.Where(e => e.Type is "IFCDUCTSEGMENT"))
        {
            var duct = ConvertToDuct(ent, byId);
            if (duct != null)
            {
                _database.AddEntity(duct);
                result.DuctCount++;
            }
        }

        // IfcValve → ValveEntity
        foreach (var ent in stepEntities.Where(e => e.Type is "IFCVALVE"))
        {
            result.ValveCount++;
        }

        // IfcFlowFitting (Elbow/Tee/Reducer)
        foreach (var ent in stepEntities.Where(e => e.Type is "IFCFLOWFITTING"))
        {
            result.FittingCount++;
        }

        result.TotalMepEntities = result.PipeCount + result.FixtureCount + result.DuctCount + result.ValveCount + result.FittingCount;
        return result;
    }

    private PipeEntity? ConvertToPipe(StepEntity ent, Dictionary<string, StepEntity> byId,
        Dictionary<string, StepEntity> materialByTargetId, Dictionary<string, StepEntity> systemByTargetId)
    {
        var points = ExtractPlacementPoints(ent, byId);
        if (points.Count < 2) return null;

        double dn = ExtractNominalDiameter(ent);
        string materialName = ExtractMaterial(ent, materialByTargetId);
        string systemName = ExtractSystemClassification(ent, systemByTargetId);

        var pipe = new PipeEntity(points[0], points[1], dn > 0 ? dn : 20)
        {
            PipeMaterialType = MaterialMapping.GetValueOrDefault(materialName, PipeMaterial.PPRC_PN20),
            SystemType = SystemMapping.GetValueOrDefault(systemName, MechanicalSystemType.DomesticColdWater),
            Layer = $"REVIT_MEP_{systemName.Replace(" ", "_")}",
            Temperature = systemName.Contains("Hot") ? 60 : 10
        };
        pipe.ApplySystemColor();
        return pipe;
    }

    private SanitaryFixtureEntity? ConvertToFixture(StepEntity ent, Dictionary<string, StepEntity> byId)
    {
        var points = ExtractPlacementPoints(ent, byId);
        if (points.Count == 0) return null;

        string name = ExtractName(ent);
        var match = FixtureMapping.FirstOrDefault(m => name.Contains(m.Key, StringComparison.OrdinalIgnoreCase));

        string fixtureType = match.Value.Type ?? "Unknown";
        double fu = match.Value.FU;

        return new SanitaryFixtureEntity(points[0], fixtureType, fu)
        {
            Layer = "REVIT_MEP_FIXTURES",
            Color = 0xFF00CCFF
        };
    }

    private DuctEntity? ConvertToDuct(StepEntity ent, Dictionary<string, StepEntity> byId)
    {
        var points = ExtractPlacementPoints(ent, byId);
        if (points.Count < 2) return null;

        return new DuctEntity(points[0], points[1], 400, 300)
        {
            Layer = "REVIT_MEP_DUCT"
        };
    }

    // ═══ STEP Parsing Helpers ═══

    private List<StepEntity> ParseStepFile(string[] lines)
    {
        var entities = new List<StepEntity>();
        foreach (var line in lines)
        {
            if (!line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string id = line[..eq].Trim();
            string rest = line[(eq + 1)..].TrimEnd(';').Trim();
            int paren = rest.IndexOf('(');
            if (paren < 0) continue;
            entities.Add(new StepEntity
            {
                Id = id,
                Type = rest[..paren].Trim().ToUpperInvariant(),
                Args = rest[(paren + 1)..].TrimEnd(')')
            });
        }
        return entities;
    }

    private List<Vector3D> ExtractPlacementPoints(StepEntity ent, Dictionary<string, StepEntity> byId)
    {
        var points = new List<Vector3D>();
        var refs = ent.Args.Split(',').Where(s => s.Trim().StartsWith("#")).Select(s => s.Trim());
        foreach (var r in refs)
        {
            byId.TryGetValue(r, out var refEnt);
            if (refEnt?.Type == "IFCCARTESIANPOINT")
            {
                var coords = refEnt.Args.Replace("(", "").Replace(")", "").Split(',');
                if (coords.Length >= 2)
                {
                    double.TryParse(coords[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double x);
                    double.TryParse(coords[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y);
                    double z = coords.Length >= 3 && double.TryParse(coords[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double zz) ? zz : 0;
                    points.Add(new Vector3D(x * 1000, y * 1000, z * 1000));
                }
            }
        }
        return points;
    }

    private double ExtractNominalDiameter(StepEntity ent)
    {
        var parts = ent.Args.Split(',');
        foreach (var p in parts)
        {
            if (double.TryParse(p.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                if (val > 5 && val < 1000) return val;
            }
        }
        return 0;
    }

    private string ExtractMaterial(StepEntity ent, Dictionary<string, StepEntity> materialByTargetId)
    {
        if (materialByTargetId.TryGetValue(ent.Id, out var matRef))
        {
            var name = matRef.Args.Split(',').FirstOrDefault()?.Trim().Trim('\'');
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return "PPR";
    }

    private string ExtractSystemClassification(StepEntity ent, Dictionary<string, StepEntity> systemByTargetId)
    {
        if (systemByTargetId.TryGetValue(ent.Id, out var sysRef))
        {
            var name = sysRef.Args.Split(',').Skip(2).FirstOrDefault()?.Trim().Trim('\'');
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return "Domestic Cold Water";
    }

    // NE: Bir referans tipinin (IFCMATERIAL/IFCSYSTEM) ARGS'ında geçen her entity Id'sini
    //     o referans entity'ye eşleyen ters-indeks (hedef Id → tanım entity).
    // NEDEN: ExtractMaterial/ExtractSystemClassification önceden her çağrıda TÜM entity
    //        listesini (`all.FirstOrDefault(e => e.Type == X && e.Args.Contains(ent.Id))`)
    //        tarıyordu. Bu, aynı orijinal "ilk eşleşen tanım entity'yi (doküman sırasına
    //        göre) döndür" semantiğini KORUYARAK bir kez hesaplanır: tanım entity'leri
    //        (tipik olarak boru/cihaz sayısından çok daha az) doküman sırasında gezilir,
    //        her biri hangi hedef Id'leri içeriyorsa (substring — orijinal davranışla
    //        birebir aynı) ve o hedef Id daha önce atanmamışsa indekse eklenir.
    private static Dictionary<string, StepEntity> BuildReverseRefIndex(List<StepEntity> all, string refType)
    {
        var index = new Dictionary<string, StepEntity>(StringComparer.Ordinal);
        foreach (var refEnt in all)
        {
            if (refEnt.Type != refType) continue;
            foreach (var candidate in all)
            {
                if (index.ContainsKey(candidate.Id)) continue;
                if (refEnt.Args.Contains(candidate.Id))
                    index[candidate.Id] = refEnt;
            }
        }
        return index;
    }

    private string ExtractName(StepEntity ent)
    {
        var parts = ent.Args.Split(',');
        return parts.Length > 2 ? parts[2].Trim().Trim('\'') : "";
    }

    private class StepEntity
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Args { get; set; } = "";
    }
}

public class RevitImportResult
{
    public int PipeCount { get; set; }
    public int FixtureCount { get; set; }
    public int DuctCount { get; set; }
    public int ValveCount { get; set; }
    public int FittingCount { get; set; }
    public int TotalMepEntities { get; set; }
}
