using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

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
   - IfcPipeSegment / IfcFlowSegment → PipeEntity (Layer "MEP-IMPORT")
   - IfcDuctSegment                  → DuctEntity (dairesel veya dikdörtgen kesit, Layer "MEP-IMPORT")

   3D EXTRUSION (bu oturumda eklendi):
   - IFCEXTRUDEDAREASOLID'in Depth (yükseklik) argümanı ARTIK gerçekten kullanılıyor.
     Önceden parse ediliyordu (product.Height) ama BuildCadEntities hiç okumuyordu —
     tüm duvarlar/döşemeler/kapılar/pencereler Z=0'da DÜZ (tamamen 2D) çiziliyordu,
     3D görünüme geçilince mimari model yassı bir "krep" gibi görünüyordu.
   - Artık her eleman gerçek IFC yüksekliğiyle (yoksa mühendislik varsayılanıyla) tam
     bir 3D tel-kafes (wireframe) kutu olarak ekstrüde ediliyor (alt döngü + üst döngü +
     4 dikey kenar — 12 çizgi). Bu, gerçek bir solid-mesh/B-Rep motoru DEĞİL (AfneyCAD'in
     render motoru SkiaSharp tabanlı 2D/izometrik bir çizim motoru, tam 3D mesh render
     desteklemiyor) — ama artık koordinatlar gerçek 3D uzayda, 3D görünümde (Toggle3DView)
     ve kesit/izometrik çıktılarda doğru yükseklikte görünüyorlar.

   ROTASYON + KARMAŞIK PROFİLLER (bu oturumda eklendi):
   - IFCAXIS2PLACEMENT3D'nin RefDirection'ı (arg[2]) artık okunuyor — önceden sadece
     Location (arg[0]) okunuyor, rotasyon TAMAMEN YOK SAYILIYORDU. Yani IFC dosyasında
     45° döndürülmüş bir duvar, AfneyCAD'e 0° (eksene paralel) olarak giriyordu. Artık
     her ürünün RotationRad'ı hesaplanıp tüm köşe noktalarına uygulanıyor ("eğik duvarlar").
   - IFCARBITRARYCLOSEDPROFILEDEF (keyfi çokgen kesit, IfcPolyline üzerinden) ve
     IFCCIRCLEPROFILEDEF (dairesel kesit — kolon/boru gibi) artık destekleniyor.
     Önceden SADECE IFCRECTANGLEPROFILEDEF (dikdörtgen kesit) destekleniyordu; L-şekilli,
     çokgen veya dairesel kesitli duvarlar/kolonlar hep varsayılan dikdörtgene düşüyordu.

   MEP İÇERİ AKTARIMI (bu oturumda eklendi):
   - IFCPIPESEGMENT / IFCDUCTSEGMENT / IFCFLOWSEGMENT artık PipeEntity/DuctEntity olarak
     içeri aktarılıyor. Geometri, aynı IFCEXTRUDEDAREASOLID yolu üzerinden ama duvarlardan
     FARKLI yorumlanıyor: Position (arg[1]) → yerel başlangıç noktası + eksen yönü
     (Axis, arg[1] — RefDirection DEĞİL), Depth (arg[3]) → segment uzunluğu (yükseklik
     değil). Kesit: IFCCIRCLEPROFILEDEF → dairesel boru/kanal, IFCRECTANGLEPROFILEDEF →
     dikdörtgen kanal. `result.MepCount` artık gerçekten artırılıyor (önceden ölü alandı).
   - `IfcImportOptions.ImportMep` (varsayılan true) ile açılıp kapatılabilir.

   KAVİSLİ DUVAR EKSENLERİ (bu oturumda eklendi):
   - IFCTRIMMEDCURVE (IFCCIRCLE üzerine kurulu) tipindeki `Axis` temsili artık destekleniyor
     — Revit/ArchiCAD'in ürettiği en yaygın "yaylı duvar" biçimi. Duvarın
     IFCPRODUCTDEFINITIONSHAPE.Representations listesinde RepresentationIdentifier='Axis'
     olan bir IFCSHAPEREPRESENTATION bulunursa, içindeki IFCTRIMMEDCURVE/IFCCIRCLE 16
     segmente tessellate edilip `IfcProduct.CurvedAxisPoints` doldurulur; BuildCadEntities
     bu durumda duvarı TEK düz kutu yerine yay boyunca art arda dizilmiş düz duvar
     segmentleri (her biri kendi yönünde ekstrüde edilmiş kutu tel-kafesi) olarak çizer.
     Trim parametreleri sadece SAYISAL (IfcParameterValue / çıplak sayı) biçimde
     destekleniyor — IfcCartesianPoint tabanlı trim (nokta ile kırpma) desteklenmiyor.

   SINIRLAMALAR (kalan, kasıtlı kapsam dışı):
   - Koordinat dönüşümü: IFCLOCALPLACEMENT yalnızca X/Y öteleme + Z-ekseni rotasyonu
     destekler (3D eğik/devrik yerleşim değil — MEP altlığı için yeterli).
   - Birim: mm varsayılır (IfcSIUnit METRE ise 1000 ile çarpılır).
   - IFCCOMPOSITECURVE (birden fazla eğri/segment tipinin birleşimi) ve serbest-form
     IFCBSPLINECURVE tabanlı duvar eksenleri HÂLÂ desteklenmiyor — sadece TEK bir
     IFCTRIMMEDCURVE(IFCCIRCLE) parçası destekleniyor. Bu, regex/sözlük tabanlı bir STEP
     ayrıştırıcısı için gerçek NURBS/spline tessellation matematiği gerektirir ve MEP
     altlığı amacı için yatırım/getiri oranı düşük görüldü. Bilinçli olarak kapsam dışı.
   - MEP tarafında sadece düz (tek eksenli) segmentler destekleniyor — IfcFlowFitting
     (dirsek/te/redüksiyon), vana, pompa gibi MEP bağlantı elemanları HÂLÂ içeri
     aktarılmıyor (sadece düz boru/kanal gövdeleri).
   - Gerçek B-Rep/solid-mesh render desteği yok (yukarıdaki mimari elemanlar gibi MEP
     elemanları da tel-kafes/kenar çizgileriyle temsil ediliyor).
*/
public class IfcImportService
{
    private readonly CadDatabase _database;

    private const string LayerWall   = "ARCH-WALL";
    private const string LayerSlab   = "ARCH-SLAB";
    private const string LayerWindow = "ARCH-WINDOW";
    private const string LayerDoor   = "ARCH-DOOR";
    private const string LayerSpace  = "ARCH-SPACE";
    private const string LayerMep    = "MEP-IMPORT";

    // Renk sabitleri (ARGB)
    private const uint ColorWall   = 0xFF808080; // Gri
    private const uint ColorSlab   = 0xFF606060; // Koyu gri
    private const uint ColorWindow = 0xFF00BFFF; // Açık mavi
    private const uint ColorDoor   = 0xFFDEB887; // Bej
    private const uint ColorSpace  = 0xFF404040; // Soluk gri
    private const uint ColorMep    = 0xFFE67E22; // Turuncu — MEP-IMPORT içeri aktarılan borular/kanallar

    public IfcImportService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: IFC Dosyasını Analiz Et (önizleme — database'e yazmaz)
       NEDEN: Dialog'da import öncesi içerik bilgisi göstermek için.
    */
    public IfcImportResult AnalyzeFile(string filePath, IfcImportOptions? options = null)
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
            var lines    = File.ReadAllLines(filePath);
            var entities = ParseIfcEntities(lines);

            double unitScale = options.ScaleFactor > 0 ? options.ScaleFactor : DetectUnitScale(entities);
            var placements   = ParsePlacements(entities);
            var products     = ParseProducts(entities, placements, unitScale);

            foreach (var product in products)
            {
                switch (product.IfcType)
                {
                    case "IFCWALL":
                    case "IFCWALLSTANDARDCASE": if (options.ImportWalls)   result.WallCount++;   break;
                    case "IFCSLAB":             if (options.ImportSlabs)   result.SlabCount++;   break;
                    case "IFCWINDOW":           if (options.ImportWindows) result.WindowCount++; break;
                    case "IFCDOOR":             if (options.ImportDoors)   result.DoorCount++;   break;
                    case "IFCSPACE":            if (options.ImportSpaces)  result.SpaceCount++;  break;
                    case "IFCPIPESEGMENT":
                    case "IFCDUCTSEGMENT":
                    case "IFCFLOWSEGMENT":      if (options.ImportMep)     result.MepCount++;    break;
                }
            }

            if (options.ImportWalls)   result.Layers.Add(LayerWall);
            if (options.ImportSlabs)   result.Layers.Add(LayerSlab);
            if (options.ImportWindows) result.Layers.Add(LayerWindow);
            if (options.ImportDoors)   result.Layers.Add(LayerDoor);
            if (options.ImportSpaces)  result.Layers.Add(LayerSpace);
            if (options.ImportMep)     result.Layers.Add(LayerMep);

            result.Success = true;
            result.Warnings.Add($"Birim ölçek: ×{unitScale} (mm cinsinden)");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Analiz hatası: {ex.Message}");
        }

        return result;
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
            var lines    = File.ReadAllLines(filePath);
            var entities = ParseIfcEntities(lines);

            double unitScale = options.ScaleFactor > 0 ? options.ScaleFactor : DetectUnitScale(entities);
            var placements   = ParsePlacements(entities);
            var products     = ParseProducts(entities, placements, unitScale);

            EnsureLayers();

            foreach (var product in products)
            {
                bool skip = product.IfcType switch
                {
                    "IFCWALL" or "IFCWALLSTANDARDCASE" => !options.ImportWalls,
                    "IFCSLAB"                          => !options.ImportSlabs,
                    "IFCWINDOW"                        => !options.ImportWindows,
                    "IFCDOOR"                          => !options.ImportDoors,
                    "IFCSPACE"                         => !options.ImportSpaces,
                    "IFCPIPESEGMENT" or "IFCDUCTSEGMENT" or "IFCFLOWSEGMENT" => !options.ImportMep,
                    _                                  => true
                };

                if (skip) { result.SkippedCount++; continue; }

                foreach (var e in BuildCadEntities(product))
                    _database.AddEntity(e);

                switch (product.IfcType)
                {
                    case "IFCWALL":
                    case "IFCWALLSTANDARDCASE": result.WallCount++;   break;
                    case "IFCSLAB":             result.SlabCount++;   break;
                    case "IFCWINDOW":           result.WindowCount++; break;
                    case "IFCDOOR":             result.DoorCount++;   break;
                    case "IFCSPACE":            result.SpaceCount++;  break;
                    case "IFCPIPESEGMENT":
                    case "IFCDUCTSEGMENT":
                    case "IFCFLOWSEGMENT":      result.MepCount++;    break;
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

    /*
       NE: Yerleşim Bilgisi (IfcPlacementInfo)
       NEDEN: Önceden sadece konum (Vector3D) tutuluyordu — rotasyon bilgisi hiç
              hesaplanmıyordu, bu yüzden döndürülmüş elemanlar hep 0° içeri aktarılıyordu.
    */
    private readonly struct IfcPlacementInfo
    {
        public Vector3D Position { get; init; }
        public double RotationRad { get; init; }
    }

    private static Dictionary<int, IfcPlacementInfo> ParsePlacements(Dictionary<int, IfcRawEntity> entities)
    {
        var result = new Dictionary<int, IfcPlacementInfo>();

        foreach (var e in entities.Values)
        {
            if (e.Type != "IFCLOCALPLACEMENT") continue;

            // IFCLOCALPLACEMENT(#parentId, #axisPlacementId)
            if (e.Args.Count >= 2 && TryParseRef(e.Args[1], out int axisId) &&
                entities.TryGetValue(axisId, out var axis) &&
                (axis.Type == "IFCAXIS2PLACEMENT3D" || axis.Type == "IFCAXIS2PLACEMENT2D"))
            {
                Vector3D position = default;

                // IFCAXIS2PLACEMENT3D(Location, Axis, RefDirection)
                if (axis.Args.Count >= 1 && TryParseRef(axis.Args[0], out int locId) &&
                    entities.TryGetValue(locId, out var loc) &&
                    loc.Type == "IFCCARTESIANPOINT")
                {
                    position = ParseCartesianPoint(loc);
                }

                // NE/NEDEN — GERÇEK, ÖNCEDEN VAR OLAN HATA: RefDirection (arg[2]) hiç
                // okunmuyordu. IFC'de yerel X ekseninin dünya koordinatındaki yönünü
                // RefDirection verir; Z etrafındaki rotasyon açısı atan2(RefDir.Y, RefDir.X)'tir.
                double rotation = 0;
                if (axis.Type == "IFCAXIS2PLACEMENT3D" && axis.Args.Count >= 3 &&
                    TryParseRef(axis.Args[2], out int refDirId) &&
                    entities.TryGetValue(refDirId, out var refDir) &&
                    refDir.Type == "IFCDIRECTION" && refDir.Args.Count >= 2)
                {
                    if (double.TryParse(refDir.Args[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double dx) &&
                        double.TryParse(refDir.Args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double dy) &&
                        (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9))
                    {
                        rotation = Math.Atan2(dy, dx);
                    }
                }

                result[e.Id] = new IfcPlacementInfo { Position = position, RotationRad = rotation };
            }
        }
        return result;
    }

    private static Vector3D ParseCartesianPoint(IfcRawEntity pointEntity)
    {
        double x = 0, y = 0, z = 0;
        var coords = pointEntity.Args;
        if (coords.Count >= 1) double.TryParse(coords[0], NumberStyles.Any, CultureInfo.InvariantCulture, out x);
        if (coords.Count >= 2) double.TryParse(coords[1], NumberStyles.Any, CultureInfo.InvariantCulture, out y);
        if (coords.Count >= 3) double.TryParse(coords[2], NumberStyles.Any, CultureInfo.InvariantCulture, out z);
        return new Vector3D(x, y, z);
    }

    private static List<IfcProduct> ParseProducts(
        Dictionary<int, IfcRawEntity> entities,
        Dictionary<int, IfcPlacementInfo> placements,
        double scale)
    {
        var result = new List<IfcProduct>();
        var productTypes = new HashSet<string>
        {
            "IFCWALL", "IFCWALLSTANDARDCASE", "IFCSLAB",
            "IFCWINDOW", "IFCDOOR", "IFCSPACE",
            "IFCPIPESEGMENT", "IFCDUCTSEGMENT", "IFCFLOWSEGMENT"
        };

        foreach (var e in entities.Values)
        {
            if (!productTypes.Contains(e.Type)) continue;

            var product = new IfcProduct { Id = e.Id, IfcType = e.Type };

            // GlobalId (args[0]), Name (args[2])
            if (e.Args.Count >= 3)
                product.Name = e.Args[2].Trim('\'');

            // ObjectPlacement → konum + rotasyon
            if (e.Args.Count >= 6 && TryParseRef(e.Args[5], out int placId) &&
                placements.TryGetValue(placId, out var placementInfo))
            {
                var pos = placementInfo.Position;
                product.Origin = new Vector3D(pos.X * scale, pos.Y * scale, pos.Z * scale);
                product.RotationRad = placementInfo.RotationRad;
            }

            // Representation → boyut (BoundingBox fallback)
            if (e.Args.Count >= 7 && TryParseRef(e.Args[6], out int repId) &&
                entities.TryGetValue(repId, out var rep))
            {
                if (product.IfcType is "IFCPIPESEGMENT" or "IFCDUCTSEGMENT" or "IFCFLOWSEGMENT")
                {
                    ExtractMepGeometry(rep, entities, scale, product);
                }
                else
                {
                    ExtractDimensions(rep, entities, scale, product);
                    if (product.IfcType is "IFCWALL" or "IFCWALLSTANDARDCASE")
                        ExtractCurvedAxis(rep, entities, scale, product);
                }
            }

            result.Add(product);
        }
        return result;
    }

    /*
       NE: Boyutları Çıkar (ExtractDimensions)
       NEDEN — GERÇEK, ÖNCEDEN VAR OLAN BİR HATA: Bu metod önceden `rep` parametresini HİÇ
       KULLANMIYORDU — tüm entity sözlüğünü global olarak tarayıp bulduğu İLK/SON
       IFCRECTANGLEPROFILEDEF/IFCEXTRUDEDAREASOLID'i kullanıyordu. Yani birden fazla farklı
       boyutta duvar/döşeme içeren gerçek bir IFC dosyasında, TÜM elemanlar rastgele AYNI
       (yanlış) boyutları alıyordu. Artık `rep` (IFCPRODUCTDEFINITIONSHAPE) üzerinden gerçek
       STEP referans grafiği izleniyor: Representations → IFCSHAPEREPRESENTATION → Items →
       IFCEXTRUDEDAREASOLID → (SweptArea → IFCRECTANGLEPROFILEDEF, Depth) — yani her ürün
       SADECE KENDİ geometrisini alıyor.
    */
    private static void ExtractDimensions(IfcRawEntity rep,
        Dictionary<int, IfcRawEntity> entities, double scale, IfcProduct product)
    {
        // IFCPRODUCTDEFINITIONSHAPE(Name, Description, Representations)
        if (rep.Type != "IFCPRODUCTDEFINITIONSHAPE" || rep.Args.Count < 3) return;

        foreach (int shapeRepId in ParseRefList(rep.Args[2]))
        {
            if (!entities.TryGetValue(shapeRepId, out var shapeRep)) continue;
            if (shapeRep.Type != "IFCSHAPEREPRESENTATION" || shapeRep.Args.Count < 4) continue;

            // IFCSHAPEREPRESENTATION(ContextOfItems, RepresentationIdentifier, RepresentationType, Items)
            foreach (int itemId in ParseRefList(shapeRep.Args[3]))
            {
                if (!entities.TryGetValue(itemId, out var item)) continue;
                if (item.Type != "IFCEXTRUDEDAREASOLID" || item.Args.Count < 4) continue;

                // IFCEXTRUDEDAREASOLID(SweptArea, Position, ExtrudedDirection, Depth)
                if (double.TryParse(item.Args[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double height))
                {
                    product.Height = height * scale;
                }

                if (!TryParseRef(item.Args[0], out int profileId) || !entities.TryGetValue(profileId, out var profile))
                    continue;

                if (profile.Type == "IFCRECTANGLEPROFILEDEF" && profile.Args.Count >= 5)
                {
                    // NE/NEDEN — GERÇEK HATA: IFCRECTANGLEPROFILEDEF(ProfileType,
                    // ProfileName, Position, XDim, YDim) şemasında XDim index 3, YDim index 4'tedir.
                    // Önceki kod index 2 (Position — bir referans, sayı DEĞİL) ve index 3'ü
                    // okuyordu; yani XDim aslında hiç okunmuyordu, Position bir sayı gibi
                    // parse edilmeye ÇALIŞILIYORDU (başarısız oluyordu, sessizce 0 kalıyordu).
                    if (double.TryParse(profile.Args[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double xDim) &&
                        double.TryParse(profile.Args[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double yDim))
                    {
                        product.Width = xDim * scale;
                        product.Depth = yDim * scale;
                    }
                }
                else if (profile.Type == "IFCARBITRARYCLOSEDPROFILEDEF" && profile.Args.Count >= 3)
                {
                    // NE/NEDEN: Önceden desteklenmiyordu — L-şekilli, çokgen veya düzensiz
                    // kesitli duvarlar/kolonlar hep varsayılan dikdörtgene düşüyordu.
                    // IfcArbitraryClosedProfileDef(ProfileType, ProfileName, OuterCurve)
                    if (TryParseRef(profile.Args[2], out int curveId) &&
                        entities.TryGetValue(curveId, out var curve) &&
                        curve.Type == "IFCPOLYLINE" && curve.Args.Count >= 1)
                    {
                        var outline = new List<Vector3D>();
                        foreach (int ptId in ParseRefList(curve.Args[0]))
                        {
                            if (entities.TryGetValue(ptId, out var ptEntity) && ptEntity.Type == "IFCCARTESIANPOINT")
                                outline.Add(ParseCartesianPoint(ptEntity) * scale);
                        }
                        if (outline.Count >= 3) product.OutlinePoints = outline;
                    }
                }
                else if (profile.Type == "IFCCIRCLEPROFILEDEF" && profile.Args.Count >= 4)
                {
                    // NE/NEDEN: Önceden desteklenmiyordu — dairesel kesitli kolonlar/borular
                    // varsayılan dikdörtgene düşüyordu. IfcCircleProfileDef(ProfileType,
                    // ProfileName, Position, Radius). 16 kenarlı poligon yaklaşımı yeterli
                    // hassasiyette (tel-kafes render zaten segment tabanlı).
                    if (double.TryParse(profile.Args[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double radius))
                    {
                        product.OutlinePoints = BuildCirclePolygon(radius * scale, 16);
                    }
                }
            }
        }
    }

    /*
       NE: MEP Segment Geometrisi (ExtractMepGeometry)
       NEDEN: IFCPIPESEGMENT/IFCDUCTSEGMENT/IFCFLOWSEGMENT, ExtractDimensions'ın (duvar/
              döşeme için Depth=yükseklik varsayımı) AKSİNE, IFCEXTRUDEDAREASOLID'i FARKLI
              yorumlar: Position (arg[1]) → segmentin yerel BAŞLANGIÇ noktası + eksen yönü
              (Axis, arg[1] — RefDirection DEĞİL, o duvar rotasyonu için kullanılıyor);
              Depth (arg[3]) → o eksen boyunca segment UZUNLUĞU (yükseklik değil). Bu yüzden
              ExtractDimensions'tan ayrı, kendi anlamına sahip bir metod olarak yazıldı.
    */
    private static void ExtractMepGeometry(IfcRawEntity rep,
        Dictionary<int, IfcRawEntity> entities, double scale, IfcProduct product)
    {
        if (rep.Type != "IFCPRODUCTDEFINITIONSHAPE" || rep.Args.Count < 3) return;

        foreach (int shapeRepId in ParseRefList(rep.Args[2]))
        {
            if (!entities.TryGetValue(shapeRepId, out var shapeRep)) continue;
            if (shapeRep.Type != "IFCSHAPEREPRESENTATION" || shapeRep.Args.Count < 4) continue;

            foreach (int itemId in ParseRefList(shapeRep.Args[3]))
            {
                if (!entities.TryGetValue(itemId, out var item)) continue;
                if (item.Type != "IFCEXTRUDEDAREASOLID" || item.Args.Count < 4) continue;

                Vector3D localOrigin = default;
                Vector3D axisDir = new Vector3D(0, 0, 1); // IFC varsayılanı: Axis belirtilmezse dünya-Z

                if (TryParseRef(item.Args[1], out int posId) && entities.TryGetValue(posId, out var posPlacement) &&
                    posPlacement.Type == "IFCAXIS2PLACEMENT3D")
                {
                    if (posPlacement.Args.Count >= 1 && TryParseRef(posPlacement.Args[0], out int locId) &&
                        entities.TryGetValue(locId, out var locEnt) && locEnt.Type == "IFCCARTESIANPOINT")
                    {
                        localOrigin = ParseCartesianPoint(locEnt);
                    }
                    if (posPlacement.Args.Count >= 2 && TryParseRef(posPlacement.Args[1], out int axId) &&
                        entities.TryGetValue(axId, out var axEnt) && axEnt.Type == "IFCDIRECTION")
                    {
                        axisDir = ParseDirection(axEnt, axisDir);
                    }
                }

                if (!double.TryParse(item.Args[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double depth))
                    continue;
                depth *= scale;

                double axisLen = axisDir.Length();
                var normAxis = axisLen > 1e-9 ? axisDir / axisLen : new Vector3D(0, 0, 1);

                var localStart = localOrigin * scale;
                var localEnd = localStart + normAxis * depth;

                product.MepLocalStart = localStart;
                product.MepLocalEnd = localEnd;

                if (!TryParseRef(item.Args[0], out int profileId) || !entities.TryGetValue(profileId, out var profile))
                    return;

                if (profile.Type == "IFCCIRCLEPROFILEDEF" && profile.Args.Count >= 4)
                {
                    if (double.TryParse(profile.Args[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double radius))
                    {
                        product.MepDiameter = radius * 2 * scale;
                        product.MepCircular = true;
                    }
                }
                else if (profile.Type == "IFCRECTANGLEPROFILEDEF" && profile.Args.Count >= 5)
                {
                    if (double.TryParse(profile.Args[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double xDim) &&
                        double.TryParse(profile.Args[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double yDim))
                    {
                        product.MepWidth = xDim * scale;
                        product.MepHeightDim = yDim * scale;
                        product.MepCircular = false;
                    }
                }
                return; // İlk (tek) extrusion segmenti yeterli — MEP gövdeleri düz/tekil.
            }
        }
    }

    private static Vector3D ParseDirection(IfcRawEntity dirEntity, Vector3D fallback)
    {
        if (dirEntity.Args.Count < 2) return fallback;
        bool okX = double.TryParse(dirEntity.Args[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double x);
        bool okY = double.TryParse(dirEntity.Args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y);
        double z = 0;
        if (dirEntity.Args.Count >= 3)
            double.TryParse(dirEntity.Args[2], NumberStyles.Any, CultureInfo.InvariantCulture, out z);
        return (okX && okY) ? new Vector3D(x, y, z) : fallback;
    }

    /*
       NE: Kavisli Duvar Ekseni Çıkarımı (ExtractCurvedAxis)
       NEDEN: Gerçek Revit/ArchiCAD IFC dışa aktarımlarında bir duvarın 'Body' (kesit
              extrusion) temsilinin YANINDA, ayrı bir 'Axis' temsili (RepresentationIdentifier
              = 'Axis') olabilir — duvarın merkez hattı (centerline) yolunu tanımlar. Bu yol
              düz değil de IFCTRIMMEDCURVE(IFCCIRCLE) ise duvar aslında bir YAY üzerinde
              kıvrılıyordur. Önceden bu Axis temsili hiç okunmuyordu, her duvar hep düz
              (Body extrusion'ın kendi yerel dikdörtgeni) olarak çiziliyordu.
              Sadece TEK bir IFCTRIMMEDCURVE(IFCCIRCLE) parçası destekleniyor — çok parçalı
              IFCCOMPOSITECURVE veya serbest-form spline eksenler kapsam dışı (bkz. dosya başı
              SINIRLAMALAR notu).
    */
    private static void ExtractCurvedAxis(IfcRawEntity rep,
        Dictionary<int, IfcRawEntity> entities, double scale, IfcProduct product)
    {
        if (rep.Type != "IFCPRODUCTDEFINITIONSHAPE" || rep.Args.Count < 3) return;

        foreach (int shapeRepId in ParseRefList(rep.Args[2]))
        {
            if (!entities.TryGetValue(shapeRepId, out var shapeRep)) continue;
            if (shapeRep.Type != "IFCSHAPEREPRESENTATION" || shapeRep.Args.Count < 4) continue;

            // IFCSHAPEREPRESENTATION(ContextOfItems, RepresentationIdentifier, RepresentationType, Items)
            string repIdentifier = shapeRep.Args[1].Trim().Trim('\'');
            if (!string.Equals(repIdentifier, "Axis", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (int itemId in ParseRefList(shapeRep.Args[3]))
            {
                if (!entities.TryGetValue(itemId, out var item)) continue;
                if (item.Type != "IFCTRIMMEDCURVE" || item.Args.Count < 4) continue;

                // IFCTRIMMEDCURVE(BasisCurve, Trim1, Trim2, SenseAgreement, MasterRepresentation)
                if (!TryParseRef(item.Args[0], out int curveId) || !entities.TryGetValue(curveId, out var curve) ||
                    curve.Type != "IFCCIRCLE" || curve.Args.Count < 2)
                    continue;

                // IFCCIRCLE(Position, Radius)
                if (!TryParseRef(curve.Args[0], out int posId) || !entities.TryGetValue(posId, out var posEnt) ||
                    (posEnt.Type != "IFCAXIS2PLACEMENT3D" && posEnt.Type != "IFCAXIS2PLACEMENT2D"))
                    continue;

                Vector3D center = default;
                if (posEnt.Args.Count >= 1 && TryParseRef(posEnt.Args[0], out int locId) &&
                    entities.TryGetValue(locId, out var locEnt) && locEnt.Type == "IFCCARTESIANPOINT")
                {
                    center = ParseCartesianPoint(locEnt) * scale;
                }

                if (!double.TryParse(curve.Args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double radius))
                    continue;
                radius *= scale;

                double t1 = ParseTrimParam(item.Args[1], 0.0);
                double t2 = ParseTrimParam(item.Args[2], Math.PI * 2);

                if (item.Args.Count >= 4)
                {
                    bool senseAgreement = item.Args[3].Trim().Equals(".T.", StringComparison.OrdinalIgnoreCase);
                    if (!senseAgreement) (t1, t2) = (t2, t1);
                }

                double sweep = t2 - t1;
                if (Math.Abs(sweep) < 1e-9) continue;

                const int segments = 16;
                var pts = new List<Vector3D>(segments + 1);
                for (int i = 0; i <= segments; i++)
                {
                    double t = t1 + sweep * i / segments;
                    pts.Add(new Vector3D(center.X + radius * Math.Cos(t), center.Y + radius * Math.Sin(t), center.Z));
                }

                product.CurvedAxisPoints = pts;
                return;
            }
        }
    }

    /// <summary>IFCTRIMMEDCURVE'ün Trim1/Trim2 argümanından ilk sayısal parametreyi (radyan açı) çıkarır.
    /// Sadece sayısal (IfcParameterValue veya çıplak sayı) biçim destekleniyor — IfcCartesianPoint
    /// tabanlı (nokta ile) trim desteklenmiyor, bulunamazsa fallback döner.</summary>
    private static double ParseTrimParam(string arg, double fallback)
    {
        var m = Regex.Match(arg, @"-?\d+(\.\d*)?([eE][-+]?\d+)?");
        return m.Success && double.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val)
            ? val
            : fallback;
    }

    /// <summary>IFC LIST argümanını ("(#12,#34)") ayrıştırıp içindeki entity ID'lerini döner.</summary>
    private static IEnumerable<int> ParseRefList(string arg)
    {
        arg = arg.Trim();
        if (arg.StartsWith('(') && arg.EndsWith(')'))
            arg = arg[1..^1];

        foreach (var part in SplitArgs(arg))
            if (TryParseRef(part, out int id))
                yield return id;
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
        double rot = p.RotationRad;

        switch (p.IfcType)
        {
            case "IFCWALL":
            case "IFCWALLSTANDARDCASE":
            {
                // NE/NEDEN: Önceden sadece plan görünümü (4 düz çizgi, Z=0, rotasyonsuz)
                // çiziliyordu. Artık: (a) IfcExtrudedAreaSolid'in Depth'i (Height) gerçekten
                // kullanılıyor — tam bir 3D kutu tel-kafesi; (b) RotationRad uygulanıyor —
                // döndürülmüş duvarlar artık doğru açıda; (c) OutlinePoints varsa (keyfi/
                // dairesel kesit) dikdörtgen yerine gerçek poligon ekstrüde ediliyor.
                double height = p.Height > 0 ? p.Height : 3000;
                if (p.CurvedAxisPoints is { Count: >= 2 } axisPts)
                {
                    // NE/NEDEN: Yay/kavisli duvar ekseni bulundu — tek düz kutu yerine,
                    // yay boyunca art arda dizilmiş düz duvar segmentleri (her biri kendi
                    // yönünde ekstrüde edilmiş kutu tel-kafesi) çiziliyor.
                    foreach (var line in MakeCurvedWallWireframe(axisPts, origin, rot, w, height, LayerWall, ColorWall))
                        yield return line;
                }
                else if (p.OutlinePoints is { Count: >= 3 } outline)
                {
                    foreach (var line in MakeExtrudedPolygonWireframe(outline, origin, rot, height, LayerWall, ColorWall))
                        yield return line;
                }
                else
                {
                    var b1 = RotateAndTranslate(origin, 0, 0, rot);
                    var b2 = RotateAndTranslate(origin, d, 0, rot);
                    var b3 = RotateAndTranslate(origin, d, w, rot);
                    var b4 = RotateAndTranslate(origin, 0, w, rot);
                    foreach (var line in MakeExtrudedBoxWireframe(b1, b2, b3, b4, height, LayerWall, ColorWall))
                        yield return line;
                }
                break;
            }
            case "IFCSLAB":
            {
                // Döşeme kalınlığı = IFC extrusion Depth (yoksa 200mm standart döşeme kalınlığı).
                double thickness = p.Height > 0 ? p.Height : 200;
                if (p.OutlinePoints is { Count: >= 3 } outline)
                {
                    foreach (var line in MakeExtrudedPolygonWireframe(outline, origin, rot, thickness, LayerSlab, ColorSlab))
                        yield return line;
                }
                else
                {
                    var b1 = RotateAndTranslate(origin, 0, 0, rot);
                    var b2 = RotateAndTranslate(origin, w, 0, rot);
                    var b3 = RotateAndTranslate(origin, w, d, rot);
                    var b4 = RotateAndTranslate(origin, 0, d, rot);
                    foreach (var line in MakeExtrudedBoxWireframe(b1, b2, b3, b4, thickness, LayerSlab, ColorSlab))
                        yield return line;
                }
                break;
            }
            case "IFCWINDOW":
            {
                // Pencere: eşik yüksekliğinden (varsayılan 900mm) başlayıp gerçek pencere
                // yüksekliği (IFC Height, yoksa 1200mm) kadar Z ekseninde ekstrüde edilir.
                double ww = p.Width > 0 ? p.Width : 900;
                double winHeight = p.Height > 0 ? p.Height : 1200;
                const double sillHeight = 900;
                var b1 = RotateAndTranslate(origin, 0,  -25, rot, sillHeight);
                var b2 = RotateAndTranslate(origin, ww, -25, rot, sillHeight);
                var b3 = RotateAndTranslate(origin, ww,  25, rot, sillHeight);
                var b4 = RotateAndTranslate(origin, 0,   25, rot, sillHeight);
                foreach (var line in MakeExtrudedBoxWireframe(b1, b2, b3, b4, winHeight, LayerWindow, ColorWindow))
                    yield return line;
                break;
            }
            case "IFCDOOR":
            {
                // Kapı: zeminden (Z=0) gerçek kapı yüksekliğine (IFC Height, yoksa 2100mm) kadar ekstrüde edilir.
                double dw = p.Width > 0 ? p.Width : 900;
                double doorHeight = p.Height > 0 ? p.Height : 2100;
                var b1 = RotateAndTranslate(origin, 0,  -25, rot);
                var b2 = RotateAndTranslate(origin, dw, -25, rot);
                var b3 = RotateAndTranslate(origin, dw,  25, rot);
                var b4 = RotateAndTranslate(origin, 0,   25, rot);
                foreach (var line in MakeExtrudedBoxWireframe(b1, b2, b3, b4, doorHeight, LayerDoor, ColorDoor))
                    yield return line;

                // Açılış yönünü gösteren kapı yayı (plan görünümünde, zeminde)
                var swingStart = RotateAndTranslate(origin, dw, 0, rot);
                var swingEnd   = RotateAndTranslate(origin, 0, -dw, rot);
                yield return MakeLine(swingStart, swingEnd, LayerDoor, ColorDoor);
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
            case "IFCPIPESEGMENT":
            case "IFCFLOWSEGMENT":
            {
                // NE/NEDEN: MEP segment yerel koordinatları (MepLocalStart/End), duvarlarla
                // aynı RotateAndTranslate mekanizmasıyla (X/Y öteleme + Z-ekseni rotasyonu)
                // dünya koordinatına çevrilir — ExtractMepGeometry'de zaten yerel eksen
                // yönü×Depth uygulanmış olduğu için burada sadece dünya dönüşümü kalır.
                var mepStart = p.MepLocalStart ?? new Vector3D(0, 0, 0);
                var mepEnd   = p.MepLocalEnd   ?? new Vector3D(1000, 0, 0);
                var worldStart = RotateAndTranslate(origin, mepStart.X, mepStart.Y, rot, mepStart.Z);
                var worldEnd   = RotateAndTranslate(origin, mepEnd.X, mepEnd.Y, rot, mepEnd.Z);
                double diameter = p.MepDiameter > 0 ? p.MepDiameter : 100; // Varsayılan DN100
                yield return new PipeEntity(worldStart, worldEnd, diameter) { Layer = LayerMep, Color = ColorMep };
                break;
            }
            case "IFCDUCTSEGMENT":
            {
                var mepStart = p.MepLocalStart ?? new Vector3D(0, 0, 0);
                var mepEnd   = p.MepLocalEnd   ?? new Vector3D(1000, 0, 0);
                var worldStart = RotateAndTranslate(origin, mepStart.X, mepStart.Y, rot, mepStart.Z);
                var worldEnd   = RotateAndTranslate(origin, mepEnd.X, mepEnd.Y, rot, mepEnd.Z);

                DuctEntity duct = p.MepCircular
                    ? new DuctEntity(worldStart, worldEnd, p.MepDiameter > 0 ? p.MepDiameter : 315)
                    : new DuctEntity(worldStart, worldEnd,
                        p.MepWidth > 0 ? p.MepWidth : 400,
                        p.MepHeightDim > 0 ? p.MepHeightDim : 300);
                duct.Layer = LayerMep;
                duct.Color = ColorMep;
                yield return duct;
                break;
            }
        }
    }

    /*
       NE: Yerel Koordinatı Döndür ve Öteleme (RotateAndTranslate)
       NEDEN: Bir ürünün yerel (profil) koordinat sistemindeki (localX, localY, localZ)
              noktasını, ürünün RotationRad'ı kadar Z ekseni etrafında döndürüp origin'e
              ötelenmiş dünya koordinatına çevirir. Önceden rotasyon hiç uygulanmıyordu.
    */
    private static Vector3D RotateAndTranslate(Vector3D origin, double localX, double localY, double rotationRad, double localZ = 0)
    {
        double cos = Math.Cos(rotationRad), sin = Math.Sin(rotationRad);
        double rx = localX * cos - localY * sin;
        double ry = localX * sin + localY * cos;
        return new Vector3D(origin.X + rx, origin.Y + ry, origin.Z + localZ);
    }

    /*
       NE: Keyfi Poligon Ekstrüzyonu (MakeExtrudedPolygonWireframe)
       NEDEN: MakeExtrudedBoxWireframe sadece 4 köşeli dikdörtgenler içindi.
              IFCARBITRARYCLOSEDPROFILEDEF/IFCCIRCLEPROFILEDEF'ten gelen N köşeli
              (keyfi çokgen veya daire yaklaşımı) kesitleri de aynı mantıkla (alt döngü +
              üst döngü + dikey kenarlar) ekstrüde etmek için genelleştirilmiş hali.
    */
    private static IEnumerable<LineEntity> MakeExtrudedPolygonWireframe(
        List<Vector3D> localOutline, Vector3D origin, double rotationRad, double height, string layer, uint color)
    {
        var bottom = localOutline.Select(pt => RotateAndTranslate(origin, pt.X, pt.Y, rotationRad)).ToList();
        var top = bottom.Select(b => new Vector3D(b.X, b.Y, b.Z + height)).ToList();

        int n = bottom.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            yield return MakeLine(bottom[i], bottom[j], layer, color); // alt döngü
            yield return MakeLine(top[i], top[j], layer, color);       // üst döngü
            yield return MakeLine(bottom[i], top[i], layer, color);    // dikey kenar
        }
    }

    /*
       NE: Kavisli Duvar Tel-Kafesi (MakeCurvedWallWireframe)
       NEDEN: CurvedAxisPoints (tessellate edilmiş yay noktaları, yerel koordinatlarda)
              önce dünya koordinatına çevrilir, sonra ardışık her nokta çifti kendi yönünde
              (duvar kalınlığı kadar ötelenmiş) bir MakeExtrudedBoxWireframe kutusu olarak
              çizilir — yay boyunca art arda dizilmiş düz duvar segmentleri zinciri.
    */
    private static IEnumerable<LineEntity> MakeCurvedWallWireframe(
        List<Vector3D> localAxisPoints, Vector3D origin, double rotationRad, double thickness, double height, string layer, uint color)
    {
        var worldPts = localAxisPoints.Select(pt => RotateAndTranslate(origin, pt.X, pt.Y, rotationRad, pt.Z)).ToList();
        double half = thickness / 2.0;

        for (int i = 0; i < worldPts.Count - 1; i++)
        {
            var a = worldPts[i];
            var b = worldPts[i + 1];
            var dir = b - a;
            double len = dir.Length();
            if (len < 1e-6) continue;

            var norm = new Vector3D(-dir.Y / len, dir.X / len, 0);
            var b1 = a - norm * half;
            var b2 = a + norm * half;
            var b3 = b + norm * half;
            var b4 = b - norm * half;

            foreach (var line in MakeExtrudedBoxWireframe(b1, b2, b3, b4, height, layer, color))
                yield return line;
        }
    }

    /// <summary>Dairesel kesiti (IfcCircleProfileDef) N kenarlı poligon olarak yaklaşıklar (yerel koordinatlarda, merkez=orijin).</summary>
    private static List<Vector3D> BuildCirclePolygon(double radius, int segments)
    {
        var pts = new List<Vector3D>();
        for (int i = 0; i < segments; i++)
        {
            double a = 2 * Math.PI * i / segments;
            pts.Add(new Vector3D(Math.Cos(a) * radius, Math.Sin(a) * radius, 0));
        }
        return pts;
    }

    /*
       NE: 3D Kutu Tel-Kafesi Ekstrüzyonu (MakeExtrudedBoxWireframe)
       NEDEN: b1..b4 (Z=taban) tabanlı bir dikdörtgeni, verilen yükseklik kadar +Z yönünde
              ekstrüde ederek gerçek bir 3D kutu tel-kafesi (12 kenar: alt döngü + üst döngü +
              4 dikey kenar) üretir. AfneyCAD'in render motoru tam bir B-Rep/solid-mesh motoru
              olmadığı için (SkiaSharp tabanlı 2D/izometrik çizim), "gerçek 3D geometri" burada
              doğru Z koordinatlarına sahip bir tel-kafes anlamına gelir — 3D görünümde ve
              izometrik/kesit çıktılarında elemanın gerçek yüksekliğini doğru gösterir.
    */
    private static IEnumerable<LineEntity> MakeExtrudedBoxWireframe(
        Vector3D b1, Vector3D b2, Vector3D b3, Vector3D b4, double height, string layer, uint color)
    {
        var t1 = new Vector3D(b1.X, b1.Y, b1.Z + height);
        var t2 = new Vector3D(b2.X, b2.Y, b2.Z + height);
        var t3 = new Vector3D(b3.X, b3.Y, b3.Z + height);
        var t4 = new Vector3D(b4.X, b4.Y, b4.Z + height);

        // Alt döngü
        yield return MakeLine(b1, b2, layer, color);
        yield return MakeLine(b2, b3, layer, color);
        yield return MakeLine(b3, b4, layer, color);
        yield return MakeLine(b4, b1, layer, color);

        // Üst döngü
        yield return MakeLine(t1, t2, layer, color);
        yield return MakeLine(t2, t3, layer, color);
        yield return MakeLine(t3, t4, layer, color);
        yield return MakeLine(t4, t1, layer, color);

        // Dikey kenarlar
        yield return MakeLine(b1, t1, layer, color);
        yield return MakeLine(b2, t2, layer, color);
        yield return MakeLine(b3, t3, layer, color);
        yield return MakeLine(b4, t4, layer, color);
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
        EnsureLayer(LayerMep,    ColorMep,    "İçeri Aktarılan MEP (Boru/Kanal)");
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

        // NE: Z-ekseni rotasyonu (radyan) — IFCAXIS2PLACEMENT3D'nin RefDirection'ından.
        public double RotationRad { get; set; }

        // NE: Keyfi (dikdörtgen olmayan) profil kesitinin yerel köşe noktaları (varsa).
        // NEDEN: IFCARBITRARYCLOSEDPROFILEDEF / IFCCIRCLEPROFILEDEF'ten doldurulur; doluysa
        //        Width/Depth'e dayalı dikdörtgen kutu yerine bu poligon ekstrüde edilir.
        public List<Vector3D>? OutlinePoints { get; set; }

        // NE: Kavisli (yay) duvar ekseni noktaları — yerel koordinatlarda, tessellate edilmiş.
        // NEDEN: IFCTRIMMEDCURVE(IFCCIRCLE) tabanlı 'Axis' temsilinden doldurulur; doluysa
        //        duvar tek düz kutu yerine bu yol boyunca art arda dizilmiş segmentler olarak çizilir.
        public List<Vector3D>? CurvedAxisPoints { get; set; }

        // NE: MEP (boru/kanal) segment geometrisi — yerel koordinatlarda başlangıç/bitiş noktaları.
        // NEDEN: ExtractMepGeometry'den doldurulur (IFCEXTRUDEDAREASOLID'in Position+Axis+Depth'i).
        public Vector3D? MepLocalStart { get; set; }
        public Vector3D? MepLocalEnd { get; set; }
        public double MepDiameter { get; set; }
        public double MepWidth { get; set; }
        public double MepHeightDim { get; set; }
        public bool MepCircular { get; set; } = true;
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
    public bool ImportMep     { get; set; } = true;   // IfcPipeSegment/IfcDuctSegment/IfcFlowSegment
    public double ScaleFactor { get; set; } = 0;      // 0 = otomatik tespit
    public bool PreviewOnly   { get; set; } = false;
}

public class IfcImportResult
{
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public int WallCount    { get; set; }
    public int SlabCount    { get; set; }
    public int WindowCount  { get; set; }
    public int DoorCount    { get; set; }
    public int SpaceCount   { get; set; }
    public int SkippedCount { get; set; }
    public int MepCount { get; set; }
    public int TotalEntities { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors   { get; set; } = [];
    public List<string> Layers   { get; set; } = [];

    public int ImportedCount => WallCount + SlabCount + WindowCount + DoorCount + SpaceCount + MepCount;
    public int TotalCount    => ImportedCount;

    public override string ToString() =>
        $"IFC Import: {ImportedCount} eleman " +
        $"(Duvar={WallCount}, Döşeme={SlabCount}, Pencere={WindowCount}, Kapı={DoorCount}, MEP={MepCount}) " +
        $"— {(Success ? "BAŞARILI" : "HATA")}";
}
