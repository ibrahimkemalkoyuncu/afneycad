using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Afney.Cad.Infrastructure.Import;

// IFC PropertySet çıkarma ve yazma servisi — IFC 2x3/4 uyumlu
public class IfcPropertyService
{
    // ═══ IMPORT: IfcPropertySet Çıkarma ═══

    // STEP dosyasından tüm property set'leri çıkar
    public Dictionary<string, Dictionary<string, string>> ExtractPropertySets(string[] stepLines)
    {
        var propertySets = new Dictionary<string, Dictionary<string, string>>();
        var entities = new Dictionary<string, (string Type, string Args)>();

        foreach (var line in stepLines)
        {
            if (!line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string id = line[..eq].Trim();
            string rest = line[(eq + 1)..].TrimEnd(';').Trim();
            int paren = rest.IndexOf('(');
            if (paren < 0) continue;
            entities[id] = (rest[..paren].Trim().ToUpperInvariant(), rest[(paren + 1)..].TrimEnd(')'));
        }

        // IfcPropertySet → IfcPropertySingleValue listesi
        foreach (var (id, (type, args)) in entities)
        {
            if (type != "IFCPROPERTYSET") continue;

            string setName = ExtractStringParam(args, 2) ?? "Unknown";
            var props = new Dictionary<string, string>();

            // Property referanslarını çıkar
            var propRefs = args.Split('(').LastOrDefault()?.TrimEnd(')').Split(',')
                .Where(s => s.Trim().StartsWith("#")).Select(s => s.Trim());

            if (propRefs != null)
            {
                foreach (var propRef in propRefs)
                {
                    if (entities.TryGetValue(propRef, out var propEnt))
                    {
                        if (propEnt.Type == "IFCPROPERTYSINGLEVALUE")
                        {
                            string propName = ExtractStringParam(propEnt.Args, 0) ?? "";
                            string propValue = ExtractPropertyValue(propEnt.Args, entities);
                            if (!string.IsNullOrEmpty(propName))
                                props[propName] = propValue;
                        }
                    }
                }
            }

            if (props.Count > 0)
                propertySets[setName] = props;
        }

        return propertySets;
    }

    // Duvar kalınlığı çıkar (mm)
    public double ExtractWallThickness(Dictionary<string, Dictionary<string, string>> propertySets)
    {
        foreach (var (setName, props) in propertySets)
        {
            if (props.TryGetValue("Width", out var width) && double.TryParse(width, NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                return w > 1 ? w : w * 1000; // m→mm dönüşümü
            if (props.TryGetValue("Thickness", out var thick) && double.TryParse(thick, NumberStyles.Any, CultureInfo.InvariantCulture, out var t))
                return t > 1 ? t : t * 1000;
        }
        return 200; // varsayılan 200mm
    }

    // Malzeme adı çıkar
    public string ExtractMaterial(Dictionary<string, Dictionary<string, string>> propertySets)
    {
        foreach (var (_, props) in propertySets)
        {
            if (props.TryGetValue("Material", out var mat)) return mat;
            if (props.TryGetValue("Reference", out var r)) return r;
        }
        return "Concrete";
    }

    // Yangın dayanım sınıfı çıkar
    public string ExtractFireRating(Dictionary<string, Dictionary<string, string>> propertySets)
    {
        foreach (var (_, props) in propertySets)
        {
            if (props.TryGetValue("FireRating", out var fr)) return fr;
            if (props.TryGetValue("FireResistanceRating", out var frr)) return frr;
        }
        return "";
    }

    // IfcBuildingStorey elevation çıkar
    public List<StoreyInfo> ExtractStoreyElevations(string[] stepLines)
    {
        var storeys = new List<StoreyInfo>();
        var entities = new Dictionary<string, (string Type, string Args)>();

        foreach (var line in stepLines)
        {
            if (!line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string id = line[..eq].Trim();
            string rest = line[(eq + 1)..].TrimEnd(';').Trim();
            int paren = rest.IndexOf('(');
            if (paren < 0) continue;
            entities[id] = (rest[..paren].Trim().ToUpperInvariant(), rest[(paren + 1)..].TrimEnd(')'));
        }

        foreach (var (id, (type, args)) in entities)
        {
            if (type != "IFCBUILDINGSTOREY") continue;

            string name = ExtractStringParam(args, 2) ?? "Unknown";
            var parts = args.Split(',');
            double elevation = 0;

            // Son parametre genelde elevation
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (double.TryParse(parts[i].Trim().TrimEnd('.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var elev))
                {
                    if (Math.Abs(elev) < 1000) // metre cinsinden
                    {
                        elevation = elev * 1000; // m→mm
                        break;
                    }
                    elevation = elev;
                    break;
                }
            }

            storeys.Add(new StoreyInfo { Id = id, Name = name, ElevationMm = elevation });
        }

        return storeys.OrderBy(s => s.ElevationMm).ToList();
    }

    // ═══ EXPORT: IfcPropertySet Yazma ═══

    // PropertySet STEP satırları üret
    public List<string> GeneratePropertySet(ref int entityId, string setName, Dictionary<string, (string Value, string Type)> properties)
    {
        var lines = new List<string>();
        var propIds = new List<string>();

        foreach (var (propName, (value, ifcType)) in properties)
        {
            string propId = $"#{entityId++}";
            string valueStr = ifcType switch
            {
                "IfcReal" => $"IFCREAL({value})",
                "IfcInteger" => $"IFCINTEGER({value})",
                "IfcBoolean" => $"IFCBOOLEAN(.{value.ToUpperInvariant()}.)",
                "IfcLabel" => $"IFCLABEL('{value}')",
                "IfcText" => $"IFCTEXT('{value}')",
                _ => $"IFCLABEL('{value}')"
            };

            lines.Add($"{propId}=IFCPROPERTYSINGLEVALUE('{propName}',$,{valueStr},$);");
            propIds.Add(propId);
        }

        string setId = $"#{entityId++}";
        string guid = Guid.NewGuid().ToString("N")[..22];
        lines.Add($"{setId}=IFCPROPERTYSET('{guid}',$,'{setName}',$,({string.Join(",", propIds)}));");

        return lines;
    }

    // IfcMaterialLayer STEP satırları üret
    public List<string> GenerateMaterialLayer(ref int entityId, string materialName, double thicknessMm)
    {
        var lines = new List<string>();

        string matId = $"#{entityId++}";
        lines.Add($"{matId}=IFCMATERIAL('{materialName}');");

        string layerId = $"#{entityId++}";
        lines.Add($"{layerId}=IFCMATERIALLAYER({matId},{(thicknessMm / 1000.0).ToString("F4", CultureInfo.InvariantCulture)},$);");

        string layerSetId = $"#{entityId++}";
        lines.Add($"{layerSetId}=IFCMATERIALLAYERSET(({layerId}),'{materialName} Layer');");

        return lines;
    }

    // Revit IFC parameter extraction
    public Dictionary<string, string> ExtractRevitParameters(string[] stepLines, string targetEntityId)
    {
        var result = new Dictionary<string, string>();
        var propertySets = ExtractPropertySets(stepLines);

        string[] revitSets = { "Pset_PipeSegmentCommon", "Pset_FlowTerminalCommon",
            "Pset_DuctSegmentCommon", "Pset_WallCommon", "Pset_SlabCommon",
            "Pset_DoorCommon", "Pset_WindowCommon" };

        foreach (var setName in revitSets)
        {
            if (propertySets.TryGetValue(setName, out var props))
            {
                foreach (var (k, v) in props)
                    result[k] = v;
            }
        }

        if (propertySets.TryGetValue("Revit Type Parameters", out var revitType))
            foreach (var (k, v) in revitType) result[$"Revit_{k}"] = v;

        return result;
    }

    // ═══ Yardımcılar ═══

    private string? ExtractStringParam(string args, int index)
    {
        var parts = args.Split(',');
        if (index < parts.Length)
        {
            var val = parts[index].Trim().Trim('\'');
            return val == "$" ? null : val;
        }
        return null;
    }

    private string ExtractPropertyValue(string args, Dictionary<string, (string Type, string Args)> entities)
    {
        var parts = args.Split(',');
        // 3. parametre genelde değer referansı
        for (int i = 2; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (p.StartsWith("IFCREAL(") || p.StartsWith("IFCINTEGER(") || p.StartsWith("IFCLABEL(") || p.StartsWith("IFCTEXT("))
            {
                return p.Split('(', ')').Skip(1).FirstOrDefault()?.Trim('\'') ?? "";
            }
            if (p.StartsWith("#") && entities.TryGetValue(p, out var refEnt))
            {
                return refEnt.Args.Split(',').FirstOrDefault()?.Trim().Trim('\'') ?? "";
            }
            if (double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                return p;
        }
        return "";
    }
}

public class StoreyInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double ElevationMm { get; set; }
}
