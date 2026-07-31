using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.IO;
using ACadSharp.Entities;
using ACadSharp.Tables;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities; // PipeEntity vb. için

namespace Afney.Cad.Infrastructure.Import;

/*
    NE: Profesyonel DWG İçe Aktarma Servisi
    NEDEN: AutoCAD (.dwg) dosyalarını renk, katman ve blok sadakatiyle okumak için.
    
    ÖZELLİKLER:
    - ACadSharp kütüphanesi üzerine kurulu.
    - 256 Renk (ACI) Desteği.
    - Blok (Insert) desteği (Patlatarak okuma - Explode).
    - Polilayn, Yay (Arc), Çember ve Çizgi desteği.
    - Metin (Text/MText) desteği.
*/
public class DwgImportService
{
    // ACI (AutoCAD Color Index) -> RGB Mapping Table (Standart 256 renk)
    // Performans için static cache
    private static readonly Dictionary<short, uint> _aciPalette = new();
    
    // Yüksek Performanslı Reflection Ön Belleği (14 saniyelik Hatch gecikmesini 0.1 saniyeye indirir)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Reflection.PropertyInfo?> _propCache = new();

    private static System.Reflection.PropertyInfo? GetCachedProperty(Type type, string propName)
    {
        string key = $"{type.FullName}::{propName}";
        return _propCache.GetOrAdd(key, _ => type.GetProperty(propName));
    }

    static DwgImportService()
    {
        InitializeAciPalette();
    }

    public List<CadEntity> ImportDwg(string filePath)
    {
        var entities = new List<CadEntity>();

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DWG dosyası bulunamadı: {filePath}");

        try
        {
            Serilog.Log.Information("[DWG] DwgReader nesnesi oluşturuluyor... (Dosya: {filePath})", filePath);
            using (var reader = new DwgReader(filePath))
            {
                Serilog.Log.Information("[DWG] reader.Read() metodu çağrılıyor. (Bu işlem uzun sürebilir veya ACadSharp kütüphanesi çökerse log burada kesilir).");
                var cadDoc = reader.Read();
                
                Serilog.Log.Information("[DWG] DWG dokümanı başarıyla okundu. Table katmanları çıkarılıyor...");

                // Layer tablosundan renkleri ve Linetype'ları oku ve cache'le
                var layerColors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                var layerLinetypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                var layerFrozen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                var layerLocked = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                foreach (var layer in cadDoc.Layers)
                {
                    layerColors[layer.Name] = MapColor(layer.Color);
                    layerLinetypes[layer.Name] = layer.LineType?.Name ?? "Continuous";

                    // Layer frozen/locked flag — Flags bitfield'dan çıkar
                    try
                    {
                        var flags = (int)layer.Flags;
                        layerFrozen[layer.Name] = (flags & 1) != 0;  // Bit 0 = Frozen
                        layerLocked[layer.Name] = (flags & 4) != 0;  // Bit 2 = Locked
                    }
                    catch
                    {
                        layerFrozen[layer.Name] = false;
                        layerLocked[layer.Name] = false;
                    }
                }

                // INSUNITS birim algılama (DWG header — AutoCAD $INSUNITS değişkeni)
                // 0=Unitless, 1=Inches, 2=Feet, 3=Miles, 4=Millimeters, 5=Centimeters, 6=Meters
                double unitScale = 1.0;
                try
                {
                    int insUnits = (int)cadDoc.Header.InsUnits;
                    unitScale = insUnits switch
                    {
                        1 => 25.4,      // Inches → mm
                        2 => 304.8,     // Feet → mm
                        4 => 1.0,       // Millimeters (varsayılan)
                        5 => 10.0,      // Centimeters → mm
                        6 => 1000.0,    // Meters → mm
                        _ => 1.0
                    };
                    if (Math.Abs(unitScale - 1.0) > 0.001)
                        Serilog.Log.Information("[DWG] Birim algılandı: INSUNITS={InsUnits}, ölçek: {Scale}x", insUnits, unitScale);
                }
                catch { /* INSUNITS okunamazsa mm varsay */ }

                Serilog.Log.Information("[DWG] Layer verisi çekildi. Toplam {count} model objesi dönüştürülüyor...", cadDoc.Entities.Count);

                /*
                   NE: INSUNITS Ölçeğini Uygula (unitScaleTransform)
                   NEDEN: Yukarıda hesaplanan `unitScale` daha önce SADECE loglanıyordu, hiçbir
                          entity'ye UYGULANMIYORDU — yani DWG metre/santimetre biriminde
                          çizilmişse (INSUNITS=5/6), içe aktarılan tüm koordinatlar "mm" sanılıp
                          olduğu gibi kullanılıyordu. Sonuç: mahal alan/çevre hesapları (ve DWG
                          birimine bağlı her ölçüm) dosyanın gerçek birimine göre sistematik
                          olarak yanlış çıkıyordu (kullanıcı: "Mahal tanımlama butonlarının
                          hepsi yanlış alan ölçüyor"). Artık en üst seviye transform Identity
                          değil, INSUNITS ölçeği — ConvertEntity'nin mevcut `result.Transform(transform)`
                          ve nested Insert `combinedTransform = transform * localTransform` akışı
                          sayesinde bu ölçek ağaçtaki HER entity'ye (bloklar dahil) otomatik yayılır.
                */
                var unitScaleTransform = Matrix4x4.CreateScale(unitScale);

                // Model Space (Multi-Threaded)
                int convertedCount = 0;
                var concurrentEntities = new System.Collections.Concurrent.ConcurrentBag<CadEntity>();

                int errorCount = 0;
                System.Threading.Tasks.Parallel.ForEach(cadDoc.Entities, entity =>
                {
                    try
                    {
                        var convertedList = ConvertEntity(entity, unitScaleTransform, layerColors, layerLinetypes);
                        foreach (var c in convertedList)
                        {
                            // Frozen layer entity'lerini işaretle
                            if (c.Layer != null && layerFrozen.TryGetValue(c.Layer, out bool frozen) && frozen)
                                c.IsSelected = false; // Frozen = seçilemez

                            concurrentEntities.Add(c);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Partial recovery — hatalı entity'yi atla, diğerlerine devam et
                        System.Threading.Interlocked.Increment(ref errorCount);
                        if (errorCount <= 10)
                            Serilog.Log.Warning("[DWG] Entity dönüştürme hatası (atlandı): {Type} — {Error}", entity.GetType().Name, ex.Message);
                    }

                    int currentCount = System.Threading.Interlocked.Increment(ref convertedCount);
                    if (currentCount % 10000 == 0)
                        Serilog.Log.Information("[DWG] Dönüştürülen ana obje sayısı: {Count}...", currentCount);
                });

                if (errorCount > 0)
                    Serilog.Log.Warning("[DWG] Toplam {ErrorCount} entity atlandı (partial recovery).", errorCount);
                
                entities.AddRange(concurrentEntities);
                
                Serilog.Log.Information("[DWG] Dönüştürme tamamlandı. Toplam {count} sonuç objesi oluşturuldu.", entities.Count);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DWG okuma hatası: {ex.Message}", ex);
        }

        return entities;
    }

    private IEnumerable<CadEntity> ConvertEntity(Entity entity, Matrix4x4 transform, Dictionary<string, uint> layerColors, Dictionary<string, string> layerLinetypes, ACadSharp.Color? parentColor = null, string? parentLinetype = null, int depth = 0, HashSet<string>? visitedBlocks = null)
    {
        // Renk Çözümleme (ByBlock ise parent rengi al)
        uint resolvedColor = 0xFFFFFFFF; // Default

        if (entity.Color.IsByBlock && parentColor.HasValue)
        {
             // Parent rengi (Blok rengi) kullanılır. Note: Parent rengi zaten resolved gelmeli veya ACadSharp.Color gelmeli?
             // ConvertEntity signature'da parentColor ACadSharp.Color tipinde. 
             // Eğer parentColor da ByLayer ise... recursion içinde çözülmüş olmalıydı ama burada ham Color taşıyoruz.
             // Basitleştirme: Blok rengi ByLayer ise, blok reference'ın Layer'ına bakılır.
             
             if (parentColor.Value.IsByLayer)
             {
                 // Blok reference'ın layer'ı neresiyse oradan almalıydık ama burada o bilgi yok.
                 // Insert entity işlenirken rengi çözüp pass etsek daha iyi olurdu.
                 // Şimdilik MapColor ile devam, ama ByLayer ise beyaz dönecek (eski logic).
                 // Geliştirelim:
                 resolvedColor = MapColor(parentColor.Value);
             }
             else
             {
                 resolvedColor = MapColor(parentColor.Value);
             }
        }
        else if (entity.Color.IsByLayer)
        {
             // Layer tablosundan bak
             if (!string.IsNullOrEmpty(entity.Layer.Name) && layerColors.TryGetValue(entity.Layer.Name, out var lColor))
             {
                 resolvedColor = lColor;
             }
             else
             {
                 resolvedColor = 0xFFFFFFFF; // Layer bulunamazsa Beyaz
             }
        }
        else
        {
             resolvedColor = MapColor(entity.Color);
        }

        // Linetype Çözümleme
        // Linetype Çözümleme (Reflection iptal edildi - Extreme Fast Track)
        string resolvedLinetype = "Continuous";
        if (entity.LineType != null)
        {
            string lTypeName = entity.LineType.Name;
            if (lTypeName.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                resolvedLinetype = parentLinetype ?? "Continuous";
            }
            else if (lTypeName.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                 if (!string.IsNullOrEmpty(entity.Layer?.Name) && layerLinetypes.TryGetValue(entity.Layer.Name, out var lType))
                 {
                     resolvedLinetype = lType;
                 }
            }
            else
            {
                resolvedLinetype = lTypeName;
            }
        }

        // --- Insert (Block Reference) Özel İşlemi (Recursion) ---
        if (entity is Insert insert)
        {
            if (depth > 50) yield break; // Recursive stack protection
            
            var blocks = visitedBlocks ?? new HashSet<string>();
            string blockName = insert.Block?.Name ?? "UNKNOWN";
            if (blocks.Contains(blockName)) yield break; // Cyclic reference prevention
            
            blocks.Add(blockName);

            // Block Attribute çıkarma
            if (insert.Attributes != null)
            {
                foreach (var attr in insert.Attributes)
                {
                    if (attr == null || string.IsNullOrEmpty(attr.Value)) continue;
                    var attrPos = new Vector3D(attr.InsertPoint.X, attr.InsertPoint.Y, attr.InsertPoint.Z);
                    var attrText = new Afney.Cad.Domain.Entities.Basic.TextEntity(
                        attr.Value, attrPos, attr.Height > 0 ? attr.Height : 100, attr.Rotation)
                    {
                        Layer = insert.Layer?.Name ?? "0",
                        Color = resolvedColor,
                        Linetype = "Continuous"
                    };
                    attrText.Transform(transform);
                    yield return attrText;
                }
            }

            // BasePoint Translation
            var basePointTrans = Matrix4x4.Identity;
            if (insert.Block != null && insert.Block.BlockEntity != null)
            {
                var bp = insert.Block.BlockEntity.BasePoint;
                basePointTrans = Matrix4x4.CreateTranslation(-bp.X, -bp.Y, -bp.Z);
            }

            // Scale
            var scaleMat = Matrix4x4.CreateScale(insert.XScale, insert.YScale, insert.ZScale);
            
            // Rotate (Z ekseninde)
            var rotMat = Matrix4x4.CreateRotationZ(insert.Rotation);
            
            // Translate
            var transMat = Matrix4x4.CreateTranslation(insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z);
            
            // Local Transform = Translate * Rotate * Scale * (-BasePoint)
            var localTransform = transMat * rotMat * scaleMat * basePointTrans;
            
            // Global Transform = Parent * Local
            var combinedTransform = transform * localTransform;

            // 2. Blok İçeriğini Dönüştür
            if (insert.Block != null && insert.Block.Entities.Any())
            {
                var newVisited = new HashSet<string>(blocks);
                foreach (var child in insert.Block.Entities)
                {
                    // Recursive call
                    // Insert rengini parentColor olarak geçiriyoruz
                    foreach (var childConverted in ConvertEntity(child, combinedTransform, layerColors, layerLinetypes, insert.Color, resolvedLinetype, depth + 1, newVisited))
                    {
                        yield return childConverted;
                    }
                }
            }
            yield break; // Insert'in kendisini bir "nokta" olarak döndürmüyoruz artık.
        }

        // --- Temel Geometri Dönüşümleri ---
        CadEntity? result = entity switch
        {
            Line l => MapLine(l),
            Arc a => MapArc(a),
            Circle c => MapCircle(c),
            LwPolyline pl => MapLwPolyline(pl),
            MText mt => MapMText(mt),
            ACadSharp.Entities.TextEntity t => MapText(t),
            ACadSharp.Entities.Spline spline => MapSpline(spline),
            ACadSharp.Entities.Ellipse ellipse => MapEllipse(ellipse),
            ACadSharp.Entities.Point point => MapPoint(point),
            Dimension dim => null,
            Hatch h => null,
            _ => null
        };
        
        // --- Dimension (Ölçülendirme) — tip sınıflama + anonymous block çıkarma ---
        if (result == null && entity is Dimension dimension)
        {
            // Dimension tipi metadata'sı (DimensionEntity'ye aktarılır)
            string dimType = dimension switch
            {
                DimensionLinear => "Linear",
                DimensionAligned => "Aligned",
                DimensionRadius => "Radius",
                DimensionDiameter => "Diameter",
                DimensionAngular2Line or DimensionAngular3Pt => "Angular",
                DimensionOrdinate => "Ordinate",
                _ => "Unknown"
            };
            string dimLayer = dimension.Layer?.Name ?? "DIM";

            // Dimension text ve measurement çıkarma
            Afney.Cad.Domain.Entities.Basic.TextEntity? dimTextEntity = null;
            try
            {
                string dimText = dimension.Text;
                double measurement = dimension.Measurement;
                var textMidPoint = dimension.TextMiddlePoint;

                if (string.IsNullOrEmpty(dimText) && measurement > 0)
                    dimText = measurement.ToString("F1");

                if (!string.IsNullOrEmpty(dimText))
                {
                    // DimStyle text height; 0 = "use style default", fall back to 2.5 model-units
                    double dimTxtH = 2.5;
                    try { var h = dimension.Style?.TextHeight; if (h.HasValue && h.Value > 0) dimTxtH = h.Value; }
                    catch (Exception ex) { Serilog.Log.Debug("[DWG] Dimension text height okunamadı, varsayılan kullanıldı: {Error}", ex.Message); }

                    dimTextEntity = new Afney.Cad.Domain.Entities.Basic.TextEntity(
                        dimText,
                        new Vector3D(textMidPoint.X, textMidPoint.Y, textMidPoint.Z),
                        dimTxtH
                    ) { Layer = dimLayer, Color = resolvedColor };
                    dimTextEntity.Transform(transform);
                }
            }
            catch (Exception ex) { Serilog.Log.Debug("[DWG] Dimension metni çıkarılamadı, etiketsiz devam edildi: {Error}", ex.Message); }
            if (dimTextEntity != null) yield return dimTextEntity;

            // Anonymous block'taki geometriyi çıkar
            if (dimension.Block != null)
            {
                foreach (var child in dimension.Block.Entities)
                {
                    foreach (var childConverted in ConvertEntity(child, transform, layerColors, layerLinetypes, dimension.Color, resolvedLinetype, depth + 1, visitedBlocks))
                    {
                        childConverted.Layer = dimLayer;
                        yield return childConverted;
                    }
                }
            }
            yield break;
        }

         // --- Hatch (Tarama) — solid fill + pattern name koruması ---
        if (result == null && entity is Hatch hatch)
        {
            // Hatch metadata (pattern name, solid fill, scale)
            string patternName = "ANSI31";
            bool isSolid = false;
            try
            {
                var patternProp = GetCachedProperty(hatch.GetType(), "PatternName");
                if (patternProp != null)
                    patternName = patternProp.GetValue(hatch)?.ToString() ?? "ANSI31";

                isSolid = patternName.Equals("SOLID", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) { Serilog.Log.Debug("[DWG] Hatch pattern adı okunamadı, varsayılan (ANSI31) kullanıldı: {Error}", ex.Message); }

            foreach (var path in hatch.Paths)
            {
                if (path == null) continue;

                // Edge Path: Doğrudan Edges koleksiyonunu kullan
                if (path.Edges != null && path.Edges.Count > 0)
                {
                    foreach (var edge in path.Edges)
                    {
                        if (edge == null) continue;
                        
                        // Line edge
                        if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Line lineEdge)
                        {
                            var p1 = new Vector3D(lineEdge.Start.X, lineEdge.Start.Y, 0);
                            var p2 = new Vector3D(lineEdge.End.X, lineEdge.End.Y, 0);
                            var lineEnt = new LineEntity(p1, p2)
                            {
                                Layer = hatch.Layer?.Name ?? "0",
                                Color = resolvedColor,
                                Linetype = resolvedLinetype
                            };
                            lineEnt.Transform(transform);
                            yield return lineEnt;
                        }
                        else if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Arc arcEdge)
                        {
                            // Arc edge: tessellate
                            int segments = 16;
                            double startAngle = arcEdge.StartAngle;
                            double endAngle = arcEdge.EndAngle;
                            if (arcEdge.CounterClockWise && endAngle < startAngle) endAngle += 2 * Math.PI;
                            if (!arcEdge.CounterClockWise && endAngle > startAngle) endAngle -= 2 * Math.PI;
                            double step = (endAngle - startAngle) / segments;
                            var arcPoints = new List<Vector3D>();
                            for (int i = 0; i <= segments; i++)
                            {
                                double angle = startAngle + step * i;
                                double x = arcEdge.Center.X + arcEdge.Radius * Math.Cos(angle);
                                double y = arcEdge.Center.Y + arcEdge.Radius * Math.Sin(angle);
                                arcPoints.Add(new Vector3D(x, y, 0));
                            }
                            if (arcPoints.Count > 1)
                            {
                                var poly = new LwPolylineEntity(arcPoints, false)
                                {
                                    Layer = hatch.Layer?.Name ?? "0",
                                    Color = resolvedColor,
                                    Linetype = resolvedLinetype
                                };
                                poly.Transform(transform);
                                yield return poly;
                            }
                        }
                        // Ellipse edge
                        else if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Ellipse ellipseEdge)
                        {
                            int segments = 32;
                            double majorLen = Math.Sqrt(ellipseEdge.MajorAxisEndPoint.X * ellipseEdge.MajorAxisEndPoint.X + ellipseEdge.MajorAxisEndPoint.Y * ellipseEdge.MajorAxisEndPoint.Y);
                            if (majorLen < 1e-9) majorLen = 1;
                            double minorLen = majorLen * ellipseEdge.MinorToMajorRatio;
                            double majorAngle = Math.Atan2(ellipseEdge.MajorAxisEndPoint.Y, ellipseEdge.MajorAxisEndPoint.X);
                            var ellipsePoints = new List<Vector3D>();
                            for (int i = 0; i <= segments; i++)
                            {
                                double t = ellipseEdge.StartAngle + (ellipseEdge.EndAngle - ellipseEdge.StartAngle) * i / segments;
                                double ex = majorLen * Math.Cos(t);
                                double ey = minorLen * Math.Sin(t);
                                double rx = ex * Math.Cos(majorAngle) - ey * Math.Sin(majorAngle);
                                double ry = ex * Math.Sin(majorAngle) + ey * Math.Cos(majorAngle);
                                ellipsePoints.Add(new Vector3D(ellipseEdge.Center.X + rx, ellipseEdge.Center.Y + ry, 0));
                            }
                            if (ellipsePoints.Count > 1)
                            {
                                var ellipsePoly = new LwPolylineEntity(ellipsePoints, false)
                                {
                                    Layer = hatch.Layer?.Name ?? "0",
                                    Color = resolvedColor,
                                    Linetype = resolvedLinetype
                                };
                                ellipsePoly.Transform(transform);
                                yield return ellipsePoly;
                            }
                        }
                        // Spline edge
                        else if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Spline splineEdge)
                        {
                            var splinePoints = new List<Vector3D>();
                            if (splineEdge.ControlPoints != null)
                            {
                                foreach (var cp in splineEdge.ControlPoints)
                                    splinePoints.Add(new Vector3D(cp.X, cp.Y, 0));
                            }
                            else if (splineEdge.FitPoints != null)
                            {
                                foreach (var fp in splineEdge.FitPoints)
                                    splinePoints.Add(new Vector3D(fp.X, fp.Y, 0));
                            }
                            if (splinePoints.Count > 1)
                            {
                                var splinePoly = new LwPolylineEntity(splinePoints, false)
                                {
                                    Layer = hatch.Layer?.Name ?? "0",
                                    Color = resolvedColor,
                                    Linetype = resolvedLinetype
                                };
                                splinePoly.Transform(transform);
                                yield return splinePoly;
                            }
                        }
                    }
                }
                
                // Polyline Path (Entities listesinden)
                if (path.Entities != null && path.Entities.Count > 0)
                {
                    foreach (var child in path.Entities)
                    {
                        foreach (var childConverted in ConvertEntity(child, transform, layerColors, layerLinetypes, hatch.Color, resolvedLinetype, depth + 1, visitedBlocks))
                        {
                            yield return childConverted;
                        }
                    }
                }
            }
            yield break;
        }

        if (result != null)
        {
            // Ortak Özellikler
            result.Layer = entity.Layer?.Name ?? "0";
            result.Color = resolvedColor;
            result.Linetype = resolvedLinetype;

            // OCS→WCS dönüşümü (entity normal vector != (0,0,1) ise)
            try
            {
                var normalProp = GetCachedProperty(entity.GetType(), "Normal");
                if (normalProp != null)
                {
                    dynamic normal = normalProp.GetValue(entity)!;
                    double nz = (double)normal.Z;
                    if (nz < 0.999)
                    {
                        double nx = (double)normal.X;
                        double ny = (double)normal.Y;
                        var ocsTransform = OcsToWcsMatrix(nx, ny, nz);
                        result.Transform(ocsTransform);
                    }
                }
            }
            catch (Exception ex) { Serilog.Log.Debug("[DWG] OCS normal vektörü okunamadı, entity WCS'de bırakıldı: {Error}", ex.Message); }

            // Parent transform uygula
            result.Transform(transform);

            yield return result;
        }
    }

    private CadEntity MapLine(Line l)
    {
        return new LineEntity(
            new Vector3D(l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z),
            new Vector3D(l.EndPoint.X, l.EndPoint.Y, l.EndPoint.Z)
        );
    }

    private CadEntity MapCircle(Circle c)
    {
        return new CircleEntity(
            new Vector3D(c.Center.X, c.Center.Y, c.Center.Z),
            c.Radius
        );
    }
    
    private CadEntity MapArc(Arc a)
    {
        // Arc Tessellation
        var segmentCount = 16; 
        var points = new List<Vector3D>();
        
        double start = a.StartAngle;
        double end = a.EndAngle;
        if (end < start) end += 2 * Math.PI;
        
        double step = (end - start) / segmentCount;
        
        for (int i = 0; i <= segmentCount; i++)
        {
            double angle = start + (step * i);
            double x = a.Center.X + a.Radius * Math.Cos(angle);
            double y = a.Center.Y + a.Radius * Math.Sin(angle);
            points.Add(new Vector3D(x, y, a.Center.Z));
        }

        return new LwPolylineEntity(points, false);
    }

    private CadEntity MapLwPolyline(LwPolyline pl)
    {
        var points = pl.Vertices.Select(v => new Vector3D(v.Location.X, v.Location.Y, 0)).ToList();
        return new LwPolylineEntity(points, pl.IsClosed);
    }
    
    private CadEntity? MapMText(MText mt)
    {
        string cleanValue = CleanMText(mt.Value ?? "");

        var textEntity = new Afney.Cad.Domain.Entities.Basic.TextEntity(
            cleanValue,
            new Vector3D(mt.InsertPoint.X, mt.InsertPoint.Y, mt.InsertPoint.Z),
            mt.Height,
            mt.Rotation
        );

        // MText justification (AttachmentPoint) — 1-9 arası grid pozisyon
        try
        {
            int attachment = (int)mt.AttachmentPoint;
            textEntity.Style = attachment switch
            {
                1 => "TopLeft", 2 => "TopCenter", 3 => "TopRight",
                4 => "MiddleLeft", 5 => "MiddleCenter", 6 => "MiddleRight",
                7 => "BottomLeft", 8 => "BottomCenter", 9 => "BottomRight",
                _ => "TopLeft"
            };
        }
        catch (Exception ex) { Serilog.Log.Debug("[DWG] MText hizalaması okunamadı, varsayılan (TopLeft) kullanıldı: {Error}", ex.Message); }

        // MText drawing direction
        try
        {
            var dirProp = GetCachedProperty(mt.GetType(), "DrawingDirection");
            if (dirProp != null)
            {
                int dir = (int)dirProp.GetValue(mt)!;
                if (dir == 3 || dir == 4) textEntity.Rotation = 90.0; // Vertical
            }
        }
        catch (Exception ex) { Serilog.Log.Debug("[DWG] MText yazı yönü okunamadı, varsayılan yatay kullanıldı: {Error}", ex.Message); }

        return textEntity;
    }
    
    private string CleanMText(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // 0. \U+xxxx AutoCAD Unicode Escape Çözümleme (EN ÖNCELİKLİ)
        // Türkçe karakterler DWG içinde \U+015E (Ş), \U+011E (Ğ) vb. olarak saklanabilir.
        // Regex: \U+ ile başlayan, 4 hex basamaklı dizileri Unicode char'a çevir.
        value = Regex.Replace(value, @"\\U\+([0-9A-Fa-f]{4})", m =>
        {
            int codePoint = Convert.ToInt32(m.Groups[1].Value, 16);
            return ((char)codePoint).ToString();
        });

        // 1. Yeni Satır (\P) -> Environment.NewLine
        value = value.Replace("\\P", Environment.NewLine);
        
        // 2. Format Gruplarını ve Komutları Temizle
        // Örnek: \pxsm0.75;{\fTimes New Roman|b0|i0|c0|p18;C51;025}
        
        // a. Grup parantezlerini kaldır {}
        // İçeriği tut, sadece süslü parantezleri sil. (Nested yapılar için basit replace yetmez ama çoğu MText düzdür)
        // Basit yaklaşım: Regex ile temizlemeden önce veya sonra.
        
        // b. Format komutları: \[herhangi bir karakter][parametreler];
        // \fTimes New Roman|b0|i0|c0|p18; -> Font değişimi
        // \C1; -> Renk
        // \A1; -> Alignment
        // \H10; -> Height
        // \pxsm0.75; -> Paragraph
        
        // Genel Regex: Ters eğik çizgi ile başla, opsiyonel komut harfleri, opsiyonel parametreler, noktalı virgül ile bitir.
        // Veya sadece süslü parantez içindeki format komutları.
        
        // Adım Adım Regex Temizliği:
        
        // 1. { ve } karakterlerini kaldır (Grup)
        // Not: Bazen { sembolü metin olarak kullanılır, o zaman \{ şeklinde kaçışlıdır.
        // Bizim regex'imiz sadece format parantezlerini hedeflemeli ama basitlik adına hepsini kaldıralım mı?
        // Hayır, önce komutları temizleyelim.
        
        // Komut Temizliği: \[Harf][Değerler];  Örn: \fArial; \H2; 
        // Ancak \pxsm... gibi uzun komutlar da var.
        
        // Regex 1: Noktalı virgül ile biten komutlar (En yaygın)
        // \\[a-zA-Z0-9.\-|]+;
        value = Regex.Replace(value, @"\\[a-zA-Z0-9.\-|]+;", "");
        
        // Regex 2: Süslü parantezler ve blok başlangıçları
        // { ve }
        value = value.Replace("{", "").Replace("}", "");
        
        // Regex 3: Tekil kaçış karakterleri (Örn: \L, \O - Underline/Overline start)
        value = Regex.Replace(value, @"\\[LOloKk]", "");
        
        // Regex 4: Boşluk kaçışı (\~)
        value = value.Replace("\\~", " ");
        
        // Regex 5: Kalan ters eğik çizgiler (Eğer metin değilse)
        // Çift ters eğik çizgiyi (\\) tek'e indir (?) - Hayır, dosya yolu olabilir.
        
        return value.Trim();
    }
    
    private CadEntity MapText(ACadSharp.Entities.TextEntity t)
    {
        return new Afney.Cad.Domain.Entities.Basic.TextEntity(
            t.Value ?? "",
            new Vector3D(t.InsertPoint.X, t.InsertPoint.Y, t.InsertPoint.Z),
            t.Height,
            t.Rotation
        );
    }

    
    private CadEntity? MapSpline(ACadSharp.Entities.Spline spline)
    {
        var points = new List<Vector3D>();
        if (spline.ControlPoints != null && spline.ControlPoints.Count > 1)
        {
            int segments = Math.Max(spline.ControlPoints.Count * 4, 32);
            for (int i = 0; i < spline.ControlPoints.Count; i++)
            {
                var cp = spline.ControlPoints[i];
                points.Add(new Vector3D(cp.X, cp.Y, cp.Z));
            }
        }
        else if (spline.FitPoints != null && spline.FitPoints.Count > 1)
        {
            foreach (var fp in spline.FitPoints)
                points.Add(new Vector3D(fp.X, fp.Y, fp.Z));
        }

        if (points.Count < 2) return null;
        return new SplineEntity(points);
    }

    private CadEntity MapEllipse(ACadSharp.Entities.Ellipse ellipse)
    {
        var center = new Vector3D(ellipse.Center.X, ellipse.Center.Y, ellipse.Center.Z);

        double majorX, majorY, majorZ;
        try
        {
            dynamic dynEllipse = ellipse;
            var ep = dynEllipse.EndMajorPoint;
            majorX = ep.X; majorY = ep.Y; majorZ = ep.Z;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("[DWG] Ellipse major axis noktası okunamadı, birim X ekseni varsayıldı (şekil hatalı olabilir): {Error}", ex.Message);
            majorX = 1; majorY = 0; majorZ = 0;
        }

        double majorLen = Math.Sqrt(majorX * majorX + majorY * majorY + majorZ * majorZ);
        if (majorLen < 1e-9) majorLen = 1;
        double minorLen = majorLen * ellipse.RadiusRatio;
        double majorAngle = Math.Atan2(majorY, majorX);

        int segments = 48;
        var points = new List<Vector3D>();
        for (int i = 0; i <= segments; i++)
        {
            double t = ellipse.StartParameter + (ellipse.EndParameter - ellipse.StartParameter) * i / segments;
            double x = majorLen * Math.Cos(t);
            double y = minorLen * Math.Sin(t);
            double rx = x * Math.Cos(majorAngle) - y * Math.Sin(majorAngle);
            double ry = x * Math.Sin(majorAngle) + y * Math.Cos(majorAngle);
            points.Add(new Vector3D(center.X + rx, center.Y + ry, center.Z));
        }

        bool closed = Math.Abs(ellipse.EndParameter - ellipse.StartParameter - Math.PI * 2) < 0.01;
        return new LwPolylineEntity(points, closed);
    }

    private CadEntity MapPoint(ACadSharp.Entities.Point point)
    {
        var pos = new Vector3D(point.Location.X, point.Location.Y, point.Location.Z);
        return new CircleEntity(pos, 1.0);
    }

    // --- OCS → WCS Dönüşüm Matrisi (Arbitrary Axis Algorithm — DXF Reference) ---
    private static Matrix4x4 OcsToWcsMatrix(double nx, double ny, double nz)
    {
        double ax, ay, az, bx, by, bz;
        double threshold = 1.0 / 64.0;

        if (Math.Abs(nx) < threshold && Math.Abs(ny) < threshold)
        {
            double len = Math.Sqrt(ny * ny + nz * nz);
            ax = 0; ay = -nz / len; az = ny / len;
        }
        else
        {
            double len = Math.Sqrt(nx * nx + ny * ny);
            ax = -ny / len; ay = nx / len; az = 0;
        }

        bx = ny * az - nz * ay;
        by = nz * ax - nx * az;
        bz = nx * ay - ny * ax;

        var m = new Matrix4x4();
        m[0, 0] = ax; m[0, 1] = bx; m[0, 2] = nx;
        m[1, 0] = ay; m[1, 1] = by; m[1, 2] = ny;
        m[2, 0] = az; m[2, 1] = bz; m[2, 2] = nz;
        return m;
    }

    // --- Renk Yönetimi (ACI -> RGBA) ---
    public static uint MapColor(ACadSharp.Color color)
    {
        if (color.IsTrueColor)
        {
            // R, G, B pack
            return (uint)((0xFF << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        if (_aciPalette.TryGetValue(color.Index, out var rgba))
        {
            return rgba;
        }

        return 0xFFFFFFFF; // Default/Fallback White if index is 256 (ByLayer) or missing
    }

    private static void InitializeAciPalette()
    {
        // Standart AutoCAD 9 Temel Renk
        _aciPalette[1] = 0xFFFF0000; // Red
        _aciPalette[2] = 0xFFFFFF00; // Yellow
        _aciPalette[3] = 0xFF00FF00; // Green
        _aciPalette[4] = 0xFF00FFFF; // Cyan
        _aciPalette[5] = 0xFF0000FF; // Blue
        _aciPalette[6] = 0xFFFF00FF; // Magenta
        _aciPalette[7] = 0xFFFFFFFF; // White/Black
        _aciPalette[8] = 0xFF808080; // Dark Gray
        _aciPalette[9] = 0xFFC0C0C0; // Light Gray

        // AutoCAD ACI 10-249 (24 Renk Tonu * 10 Varyasyon)
        // Çift indeksler: Tam doygunluk (%100 S)
        // Tek indeksler: Yarım doygunluk (%50 S)
        // Parlaklık seviyeleri L = { 0.5, 0.65, 0.8, 0.9, 0.95 }
        for (int hueIdx = 0; hueIdx < 24; hueIdx++)
        {
            double h = hueIdx * 15.0; // 0 ile 345 derece arası
            int baseIndex = 10 + (hueIdx * 10);
            
            for (int v = 0; v < 5; v++)
            {
                double l = 0.5 - (v * 0.1); // En parlaktan en koyuya doğru
                if (l < 0.1) l = 0.1;
                
                _aciPalette[(short)(baseIndex + v * 2)]     = HslToRgb(h, 1.0, l); // Even: %100 Saturation
                _aciPalette[(short)(baseIndex + v * 2 + 1)] = HslToRgb(h, 0.3, l); // Odd: %30 Saturation
            }
        }

        // 250-255 (Gri Tonlar)
        _aciPalette[250] = 0xFF333333; // Çok Koyu Gri
        _aciPalette[251] = 0xFF555555;
        _aciPalette[252] = 0xFF777777;
        _aciPalette[253] = 0xFF999999;
        _aciPalette[254] = 0xFFBBBBBB;
        _aciPalette[255] = 0xFFDDDDDD; // Çok Açık Gri
    }

    private static uint HslToRgb(double h, double s, double l)
    {
        byte r, g, b;
        if (s == 0)
        {
            r = g = b = (byte)(l * 255);
        }
        else
        {
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
            g = HueToRgb(p, q, h / 360.0);
            b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
        }
        return (uint)((0xFF << 24) | (r << 16) | (g << 8) | b);
    }

    private static byte HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;
        if (t < 1.0 / 6.0) return (byte)((p + (q - p) * 6.0 * t) * 255.0);
        if (t < 1.0 / 2.0) return (byte)(q * 255.0);
        if (t < 2.0 / 3.0) return (byte)((p + (q - p) * (2.0 / 3.0 - t) * 6.0) * 255.0);
        return (byte)(p * 255.0);
    }
}
