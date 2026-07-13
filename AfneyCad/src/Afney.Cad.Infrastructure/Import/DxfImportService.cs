using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Entities;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Infrastructure.Import;

// DXF Import — DwgImportService ile aynı kalitede (ConvertEntity delegasyonu)
public class DxfImportService
{
    private readonly DwgImportService _dwgService = new();

    public List<CadEntity> ImportDxf(string filePath, string targetLayer = "IMPORT")
    {
        var entities = new List<CadEntity>();

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DXF dosyası bulunamadı: {filePath}");

        try
        {
            Serilog.Log.Information("[DXF] DxfReader açılıyor: {Path}", filePath);
            using var reader = new DxfReader(filePath);
            var cadDoc = reader.Read();
            Serilog.Log.Information("[DXF] Doküman okundu. Entity: {Count}", cadDoc.Entities.Count);

            // Layer renk/linetype cache (DWG ile aynı mantık)
            var layerColors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            var layerLinetypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var layer in cadDoc.Layers)
            {
                layerColors[layer.Name] = DwgImportService.MapColor(layer.Color);
                layerLinetypes[layer.Name] = layer.LineType?.Name ?? "Continuous";
            }

            // INSUNITS birim algılama
            double unitScale = 1.0;
            try
            {
                int insUnits = (int)cadDoc.Header.InsUnits;
                unitScale = insUnits switch
                {
                    1 => 25.4, 2 => 304.8, 4 => 1.0, 5 => 10.0, 6 => 1000.0, _ => 1.0
                };
            }
            catch { }

            // Entity dönüşüm — DwgImportService.ConvertEntity ile aynı kalite
            int errorCount = 0;
            var concurrentEntities = new System.Collections.Concurrent.ConcurrentBag<CadEntity>();

            System.Threading.Tasks.Parallel.ForEach(cadDoc.Entities, entity =>
            {
                try
                {
                    var converted = ConvertEntityFull(entity, Matrix4x4.Identity, layerColors, layerLinetypes, unitScale);
                    foreach (var c in converted)
                        concurrentEntities.Add(c);
                }
                catch
                {
                    System.Threading.Interlocked.Increment(ref errorCount);
                }
            });

            entities.AddRange(concurrentEntities);

            if (errorCount > 0)
                Serilog.Log.Warning("[DXF] {Count} entity atlandı (partial recovery).", errorCount);

            Serilog.Log.Information("[DXF] Dönüşüm tamamlandı: {Count} entity.", entities.Count);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DXF import hatası: {ex.Message}", ex);
        }

        return entities;
    }

    // Tam entity dönüşümü — DWG ile aynı entity tipleri
    private IEnumerable<CadEntity> ConvertEntityFull(
        Entity entity, Matrix4x4 transform,
        Dictionary<string, uint> layerColors, Dictionary<string, string> layerLinetypes,
        double unitScale, int depth = 0, HashSet<string>? visitedBlocks = null)
    {
        // Renk çözümleme
        uint color = 0xFFFFFFFF;
        if (entity.Color.IsTrueColor)
            color = (uint)((0xFF << 24) | (entity.Color.R << 16) | (entity.Color.G << 8) | entity.Color.B);
        else if (entity.Color.IsByLayer && layerColors.TryGetValue(entity.Layer?.Name ?? "0", out var lc))
            color = lc;
        else
            color = DwgImportService.MapColor(entity.Color);

        string linetype = entity.LineType?.Name ?? "Continuous";
        if (linetype.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            linetype = layerLinetypes.GetValueOrDefault(entity.Layer?.Name ?? "0", "Continuous");

        // Insert (Block Reference) — recursive
        if (entity is Insert insert)
        {
            if (depth > 50) yield break;
            var blocks = visitedBlocks ?? new HashSet<string>();
            string blockName = insert.Block?.Name ?? "";
            if (blocks.Contains(blockName)) yield break;
            blocks.Add(blockName);

            var basePointTrans = Matrix4x4.Identity;
            if (insert.Block?.BlockEntity != null)
            {
                var bp = insert.Block.BlockEntity.BasePoint;
                basePointTrans = Matrix4x4.CreateTranslation(-bp.X, -bp.Y, -bp.Z);
            }

            var scaleMat = Matrix4x4.CreateScale(insert.XScale, insert.YScale, insert.ZScale);
            var rotMat = Matrix4x4.CreateRotationZ(insert.Rotation);
            var transMat = Matrix4x4.CreateTranslation(insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z);
            var combined = transform * transMat * rotMat * scaleMat * basePointTrans;

            // Attributes
            if (insert.Attributes != null)
            {
                foreach (var attr in insert.Attributes)
                {
                    if (string.IsNullOrEmpty(attr?.Value)) continue;
                    var attrText = new Afney.Cad.Domain.Entities.Basic.TextEntity(attr.Value,
                        new Vector3D(attr.InsertPoint.X, attr.InsertPoint.Y, attr.InsertPoint.Z),
                        attr.Height > 0 ? attr.Height : 100);
                    attrText.Layer = insert.Layer?.Name ?? "0";
                    attrText.Color = color;
                    attrText.Transform(transform);
                    yield return attrText;
                }
            }

            if (insert.Block?.Entities != null)
            {
                var newVisited = new HashSet<string>(blocks);
                foreach (var child in insert.Block.Entities)
                    foreach (var cc in ConvertEntityFull(child, combined, layerColors, layerLinetypes, unitScale, depth + 1, newVisited))
                        yield return cc;
            }
            yield break;
        }

        // Dimension
        if (entity is Dimension dim && dim.Block != null)
        {
            foreach (var child in dim.Block.Entities)
                foreach (var cc in ConvertEntityFull(child, transform, layerColors, layerLinetypes, unitScale, depth + 1))
                    yield return cc;
            yield break;
        }

        // Hatch
        if (entity is Hatch hatch)
        {
            foreach (var path in hatch.Paths)
            {
                if (path?.Edges != null)
                {
                    foreach (var edge in path.Edges)
                    {
                        if (edge is Hatch.BoundaryPath.Line le)
                        {
                            var lineEnt = new LineEntity(new Vector3D(le.Start.X, le.Start.Y, 0), new Vector3D(le.End.X, le.End.Y, 0))
                            { Layer = hatch.Layer?.Name ?? "0", Color = color };
                            lineEnt.Transform(transform);
                            yield return lineEnt;
                        }
                        else if (edge is Hatch.BoundaryPath.Arc ae)
                        {
                            var pts = new List<Vector3D>();
                            double sa = ae.StartAngle, ea = ae.EndAngle;
                            if (ae.CounterClockWise && ea < sa) ea += 2 * Math.PI;
                            double step = (ea - sa) / 16;
                            for (int i = 0; i <= 16; i++)
                            {
                                double a = sa + step * i;
                                pts.Add(new Vector3D(ae.Center.X + ae.Radius * Math.Cos(a), ae.Center.Y + ae.Radius * Math.Sin(a), 0));
                            }
                            if (pts.Count > 1)
                            {
                                var poly = new LwPolylineEntity(pts, false) { Layer = hatch.Layer?.Name ?? "0", Color = color };
                                poly.Transform(transform);
                                yield return poly;
                            }
                        }
                    }
                }
            }
            yield break;
        }

        // Temel geometri
        CadEntity? result = entity switch
        {
            Line l => new LineEntity(new Vector3D(l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z),
                                     new Vector3D(l.EndPoint.X, l.EndPoint.Y, l.EndPoint.Z)),
            Arc a => MapArc(a),
            Circle c => new CircleEntity(new Vector3D(c.Center.X, c.Center.Y, c.Center.Z), c.Radius),
            LwPolyline pl => new LwPolylineEntity(pl.Vertices.Select(v => new Vector3D(v.Location.X, v.Location.Y, 0)).ToList(), pl.IsClosed),
            MText mt => new Afney.Cad.Domain.Entities.Basic.TextEntity(mt.Value ?? "", new Vector3D(mt.InsertPoint.X, mt.InsertPoint.Y, mt.InsertPoint.Z), mt.Height, mt.Rotation),
            ACadSharp.Entities.TextEntity t => new Afney.Cad.Domain.Entities.Basic.TextEntity(t.Value ?? "", new Vector3D(t.InsertPoint.X, t.InsertPoint.Y, t.InsertPoint.Z), t.Height, t.Rotation),
            ACadSharp.Entities.Ellipse el => MapEllipse(el),
            ACadSharp.Entities.Spline sp => MapSpline(sp),
            ACadSharp.Entities.Point pt => new CircleEntity(new Vector3D(pt.Location.X, pt.Location.Y, pt.Location.Z), 1.0),
            _ => null
        };

        if (result != null)
        {
            result.Layer = entity.Layer?.Name ?? "0";
            result.Color = color;
            result.Linetype = linetype;
            result.Transform(transform);
            if (Math.Abs(unitScale - 1.0) > 0.001)
                result.Transform(Matrix4x4.CreateScale(unitScale, unitScale, unitScale));
            yield return result;
        }
    }

    private CadEntity MapArc(Arc a)
    {
        var points = new List<Vector3D>();
        double start = a.StartAngle, end = a.EndAngle;
        if (end < start) end += 2 * Math.PI;
        double step = (end - start) / 16;
        for (int i = 0; i <= 16; i++)
        {
            double angle = start + step * i;
            points.Add(new Vector3D(a.Center.X + a.Radius * Math.Cos(angle), a.Center.Y + a.Radius * Math.Sin(angle), a.Center.Z));
        }
        return new LwPolylineEntity(points, false);
    }

    private CadEntity MapEllipse(ACadSharp.Entities.Ellipse ellipse)
    {
        var center = new Vector3D(ellipse.Center.X, ellipse.Center.Y, ellipse.Center.Z);
        double majorX = 1, majorY = 0;
        try { dynamic d = ellipse; majorX = d.EndMajorPoint.X; majorY = d.EndMajorPoint.Y; }
        catch (Exception ex) { Serilog.Log.Warning("[DXF] Ellipse major axis noktası okunamadı, birim X ekseni varsayıldı (şekil hatalı olabilir): {Error}", ex.Message); }
        double majorLen = Math.Sqrt(majorX * majorX + majorY * majorY);
        if (majorLen < 1e-9) majorLen = 1;
        double minorLen = majorLen * ellipse.RadiusRatio;
        double majorAngle = Math.Atan2(majorY, majorX);
        var points = new List<Vector3D>();
        for (int i = 0; i <= 48; i++)
        {
            double t = ellipse.StartParameter + (ellipse.EndParameter - ellipse.StartParameter) * i / 48;
            double x = majorLen * Math.Cos(t);
            double y = minorLen * Math.Sin(t);
            points.Add(new Vector3D(center.X + x * Math.Cos(majorAngle) - y * Math.Sin(majorAngle),
                                     center.Y + x * Math.Sin(majorAngle) + y * Math.Cos(majorAngle), center.Z));
        }
        return new LwPolylineEntity(points, Math.Abs(ellipse.EndParameter - ellipse.StartParameter - Math.PI * 2) < 0.01);
    }

    private CadEntity? MapSpline(ACadSharp.Entities.Spline spline)
    {
        var points = new List<Vector3D>();
        if (spline.ControlPoints?.Count > 1)
            foreach (var cp in spline.ControlPoints) points.Add(new Vector3D(cp.X, cp.Y, cp.Z));
        else if (spline.FitPoints?.Count > 1)
            foreach (var fp in spline.FitPoints) points.Add(new Vector3D(fp.X, fp.Y, fp.Z));
        if (points.Count < 2) return null;
        return new SplineEntity(points);
    }
}
