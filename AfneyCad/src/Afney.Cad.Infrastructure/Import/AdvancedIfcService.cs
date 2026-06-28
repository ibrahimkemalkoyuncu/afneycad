using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Afney.Cad.Infrastructure.Import;

// IFC 2x3 / IFC4 Import/Export — LOD 300 seviyesi
public class AdvancedIfcService
{
    // ═══ IFC IMPORT (STEP parser genişletilmiş) ═══
    public IfcImportResult ImportIfc(string filePath, CadDatabase database)
    {
        var result = new IfcImportResult();
        if (!File.Exists(filePath)) return result;

        var lines = File.ReadAllLines(filePath);
        var entities = new Dictionary<string, IfcParsedEntity>();

        foreach (var line in lines)
        {
            if (!line.StartsWith("#")) continue;
            var parsed = ParseStepLine(line);
            if (parsed != null) entities[parsed.Id] = parsed;
        }

        // IfcWall → LineEntity çiftleri (duvar genişliği ile)
        foreach (var ent in entities.Values.Where(e => e.Type == "IFCWALL" || e.Type == "IFCWALLSTANDARDCASE"))
        {
            var wallEntities = ConvertWall(ent, entities);
            foreach (var we in wallEntities) database.AddEntity(we);
            result.WallCount++;
            result.TotalEntities += wallEntities.Count;
        }

        // IfcSlab → Polyline (döşeme sınırı)
        foreach (var ent in entities.Values.Where(e => e.Type == "IFCSLAB"))
        {
            var slabEntities = ConvertSlab(ent, entities);
            foreach (var se in slabEntities) database.AddEntity(se);
            result.SlabCount++;
            result.TotalEntities += slabEntities.Count;
        }

        // IfcDoor / IfcWindow → Sembol
        foreach (var ent in entities.Values.Where(e => e.Type == "IFCDOOR"))
        {
            var doorEntity = ConvertOpening(ent, entities, "KAPI");
            if (doorEntity != null) { database.AddEntity(doorEntity); result.DoorCount++; result.TotalEntities++; }
        }

        foreach (var ent in entities.Values.Where(e => e.Type == "IFCWINDOW"))
        {
            var winEntity = ConvertOpening(ent, entities, "PENCERE");
            if (winEntity != null) { database.AddEntity(winEntity); result.WindowCount++; result.TotalEntities++; }
        }

        // IfcSpace → Oda etiketi
        foreach (var ent in entities.Values.Where(e => e.Type == "IFCSPACE"))
        {
            var spaceEntity = ConvertSpace(ent, entities);
            if (spaceEntity != null) { database.AddEntity(spaceEntity); result.SpaceCount++; result.TotalEntities++; }
        }

        // IfcPipeSegment, IfcFlowTerminal (MEP)
        foreach (var ent in entities.Values.Where(e => e.Type == "IFCPIPESEGMENT" || e.Type == "IFCFLOWSEGMENT"))
        {
            result.MepCount++;
        }

        return result;
    }

    // ═══ IFC EXPORT (LOD 300) ═══
    public void ExportIfc(CadDatabase database, string filePath, IfcExportOptions options)
    {
        var sb = new StringBuilder();
        int entityId = 1;

        // HEADER
        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine($"FILE_DESCRIPTION(('ViewDefinition [CoordinationView_V2.0]'),'2;1');");
        sb.AppendLine($"FILE_NAME('{Path.GetFileName(filePath)}','{DateTime.Now:yyyy-MM-ddTHH:mm:ss}',('AfneyCAD'),('AfneyCAD v2.0'),'','AfneyCAD','');");
        sb.AppendLine("FILE_SCHEMA(('IFC4'));");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");

        // IFC Hierarchy: Project → Site → Building → Storey
        string projId = $"#{entityId++}";
        sb.AppendLine($"{projId}=IFCPROJECT('{NewGuid()}',#2,'{options.ProjectName}','AfneyCAD Export',$,$,$,(#10),#9);");

        string ownerId = $"#{entityId++}";
        sb.AppendLine($"{ownerId}=IFCOWNERHISTORY(#3,#4,$,.ADDED.,$,$,$,0);");

        string personId = $"#{entityId++}";
        sb.AppendLine($"{personId}=IFCPERSON($,'{options.Author}','',$,$,$,$,$);");

        string orgId = $"#{entityId++}";
        sb.AppendLine($"{orgId}=IFCORGANIZATION($,'AfneyCAD','',$,$);");

        // Units
        string unitsId = $"#{entityId++}";
        sb.AppendLine($"#9=IFCUNITASSIGNMENT((#11,#12,#13));");
        sb.AppendLine($"#10=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#14,$);");
        sb.AppendLine($"#11=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);");
        sb.AppendLine($"#12=IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.);");
        sb.AppendLine($"#13=IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.);");
        sb.AppendLine($"#14=IFCAXIS2PLACEMENT3D(#15,$,$);");
        sb.AppendLine($"#15=IFCCARTESIANPOINT((0.,0.,0.));");
        entityId = 20;

        // Site
        string siteId = $"#{entityId++}";
        sb.AppendLine($"{siteId}=IFCSITE('{NewGuid()}',{ownerId},'{options.SiteName}','',.ELEMENT.,$,$,$,.ELEMENT.,$,$,$,$,$);");

        // Building
        string bldgId = $"#{entityId++}";
        sb.AppendLine($"{bldgId}=IFCBUILDING('{NewGuid()}',{ownerId},'{options.BuildingName}','',.ELEMENT.,$,$,$,.ELEMENT.,$,$,$);");

        // Storey
        string storeyId = $"#{entityId++}";
        sb.AppendLine($"{storeyId}=IFCBUILDINGSTOREY('{NewGuid()}',{ownerId},'Ground Floor','',.ELEMENT.,$,$,$,.ELEMENT.,0.);");

        // Spatial containment
        sb.AppendLine($"#{entityId++}=IFCRELAGGREGATES('{NewGuid()}',{ownerId},$,$,{projId},({siteId}));");
        sb.AppendLine($"#{entityId++}=IFCRELAGGREGATES('{NewGuid()}',{ownerId},$,$,{siteId},({bldgId}));");
        sb.AppendLine($"#{entityId++}=IFCRELAGGREGATES('{NewGuid()}',{ownerId},$,$,{bldgId},({storeyId}));");

        // Export entities as IfcBuildingElementProxy (LOD 300 — geometry included)
        var productIds = new List<string>();
        foreach (var entity in database.GetAllEntities())
        {
            if (entity is LineEntity line)
            {
                string ptStart = $"#{entityId++}";
                sb.AppendLine($"{ptStart}=IFCCARTESIANPOINT(({F(line.StartPoint.X / 1000.0)},{F(line.StartPoint.Y / 1000.0)},{F(line.StartPoint.Z / 1000.0)}));");

                string ptEnd = $"#{entityId++}";
                sb.AppendLine($"{ptEnd}=IFCCARTESIANPOINT(({F(line.EndPoint.X / 1000.0)},{F(line.EndPoint.Y / 1000.0)},{F(line.EndPoint.Z / 1000.0)}));");

                string polyId = $"#{entityId++}";
                sb.AppendLine($"{polyId}=IFCPOLYLINE(({ptStart},{ptEnd}));");

                string shapeId = $"#{entityId++}";
                sb.AppendLine($"{shapeId}=IFCSHAPEREPRESENTATION(#10,'Body','Curve2D',({polyId}));");

                string prodDefId = $"#{entityId++}";
                sb.AppendLine($"{prodDefId}=IFCPRODUCTDEFINITIONSHAPE($,$,({shapeId}));");

                string proxyId = $"#{entityId++}";
                string layerName = entity.Layer ?? "0";
                sb.AppendLine($"{proxyId}=IFCBUILDINGELEMENTPROXY('{NewGuid()}',{ownerId},'{layerName}','',$,$,{prodDefId},$,$);");
                productIds.Add(proxyId);
            }
        }

        // Contain products in storey
        if (productIds.Any())
        {
            sb.AppendLine($"#{entityId++}=IFCRELCONTAINEDINSPATIALSTRUCTURE('{NewGuid()}',{ownerId},$,$,({string.Join(",", productIds)}),{storeyId});");
        }

        sb.AppendLine("ENDSEC;");
        sb.AppendLine("END-ISO-10303-21;");

        File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);
    }

    private IfcParsedEntity? ParseStepLine(string line)
    {
        int eqIdx = line.IndexOf('=');
        if (eqIdx < 0) return null;
        string id = line[..eqIdx].Trim();
        string rest = line[(eqIdx + 1)..].TrimEnd(';').Trim();
        int parenIdx = rest.IndexOf('(');
        if (parenIdx < 0) return null;
        string type = rest[..parenIdx].Trim().ToUpperInvariant();
        string args = rest[(parenIdx + 1)..].TrimEnd(')');
        return new IfcParsedEntity { Id = id, Type = type, RawArgs = args };
    }

    private List<CadEntity> ConvertWall(IfcParsedEntity wall, Dictionary<string, IfcParsedEntity> entities)
    {
        var result = new List<CadEntity>();
        var pts = ExtractPoints(wall, entities);
        if (pts.Count >= 2)
        {
            result.Add(new LineEntity(pts[0], pts[1]) { Layer = "IFC_WALL", Color = 0xFF888888 });
        }
        return result;
    }

    private List<CadEntity> ConvertSlab(IfcParsedEntity slab, Dictionary<string, IfcParsedEntity> entities)
    {
        var result = new List<CadEntity>();
        var pts = ExtractPoints(slab, entities);
        if (pts.Count >= 3)
        {
            var poly = new LwPolylineEntity(pts, true) { Layer = "IFC_SLAB", Color = 0xFF666666 };
            result.Add(poly);
        }
        return result;
    }

    private CadEntity? ConvertOpening(IfcParsedEntity ent, Dictionary<string, IfcParsedEntity> entities, string label)
    {
        var pts = ExtractPoints(ent, entities);
        if (pts.Count == 0) return null;
        return new TextEntity(label, pts[0], 200) { Layer = $"IFC_{label}", Color = 0xFF00AAFF };
    }

    private CadEntity? ConvertSpace(IfcParsedEntity ent, Dictionary<string, IfcParsedEntity> entities)
    {
        var name = ExtractStringParam(ent.RawArgs, 2);
        var pts = ExtractPoints(ent, entities);
        var pos = pts.Count > 0 ? pts[0] : Vector3D.Zero;
        return new TextEntity(name ?? "Space", pos, 300) { Layer = "IFC_SPACE", Color = 0xFF44FF44 };
    }

    private List<Vector3D> ExtractPoints(IfcParsedEntity ent, Dictionary<string, IfcParsedEntity> all)
    {
        var points = new List<Vector3D>();
        // Basit ref parsing — gerçek STEP parser daha karmaşık
        var refs = ent.RawArgs.Split(',').Where(s => s.Trim().StartsWith("#")).Select(s => s.Trim());
        foreach (var r in refs)
        {
            if (all.TryGetValue(r, out var refEnt) && refEnt.Type == "IFCCARTESIANPOINT")
            {
                var coords = refEnt.RawArgs.Replace("(", "").Replace(")", "").Split(',');
                if (coords.Length >= 2)
                {
                    double.TryParse(coords[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double x);
                    double.TryParse(coords[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y);
                    double z = coords.Length >= 3 && double.TryParse(coords[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double zz) ? zz : 0;
                    points.Add(new Vector3D(x * 1000, y * 1000, z * 1000)); // m → mm
                }
            }
        }
        return points;
    }

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

    private static string NewGuid() => Guid.NewGuid().ToString("N")[..22];
    private static string F(double v) => v.ToString("F6", CultureInfo.InvariantCulture);
}

public class IfcParsedEntity
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string RawArgs { get; set; } = "";
}

// IfcImportResult — IfcImportService.cs'de tanımlı, burada tekrar tanımlanmıyor

public class IfcExportOptions
{
    public string ProjectName { get; set; } = "AfneyCAD Project";
    public string SiteName { get; set; } = "Site";
    public string BuildingName { get; set; } = "Building";
    public string Author { get; set; } = "AfneyCAD";
}
