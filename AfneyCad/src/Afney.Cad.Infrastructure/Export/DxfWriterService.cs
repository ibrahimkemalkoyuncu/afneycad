using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Infrastructure.Export;

/*
   NE: DXF R12 Metin Yazıcı (DxfWriterService)
   NEDEN: AfneyCAD viewport içeriğini herhangi bir CAD yazılımının (AutoCAD, LibreCAD,
          BricsCAD vb.) açabileceği DXF R12 ASCII formatında dışa aktarmak.

   DESTEKLENEN ENTITY TİPLERİ:
   - LineEntity     → DXF LINE
   - TextEntity     → DXF TEXT
   - CircleEntity   → DXF CIRCLE
   - ArcEntity      → DXF ARC
   - PolylineEntity → DXF POLYLINE / VERTEX
   - RectEntity     → DXF LINE (4 kenar)
   - BlockEntity    → DXF INSERT (referans olarak)

   DXF R12 SEÇİLDİ ÇÜNKÜ:
   - Harici kütüphane gerektirmez (tamamen metin tabanlı)
   - Tüm CAD programları DXF R12'yi açar
   - R2000+ özelliklerine gerek yoktur (annotation, xref önemsiz)
*/
public class DxfWriterService
{
    private readonly CadDatabase _database;

    public DxfWriterService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Veritabanını DXF'e Yaz
       NEDEN: Tüm entity'leri ve katmanları DXF R12 ASCII formatında dosyaya aktarmak.
    */
    public void WriteToFile(string filePath)
    {
        var sb = new StringBuilder(1024 * 64);
        var entities = _database.GetAllEntities().ToList();
        WriteHeader(sb);
        WriteTables(sb);
        var dimBlocks = WriteBlocksSection(sb, entities);
        WriteEntities(sb, entities, dimBlocks);
        WriteFooter(sb);
        File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);
    }

    /*
       NE: Seçili Entity Listesini DXF'e Yaz
       NEDEN: Yalnızca aktif katman veya seçili nesneleri dışa aktarmak için.
    */
    public void WriteEntitiesToFile(string filePath, IEnumerable<CadEntity> entities)
    {
        var sb = new StringBuilder(1024 * 32);
        var list = entities.ToList();
        WriteHeader(sb);
        WriteTables(sb);
        var dimBlocks = WriteBlocksSection(sb, list);
        WriteEntities(sb, list, dimBlocks);
        WriteFooter(sb);
        File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);
    }

    // ── HEADER ──────────────────────────────────────────────────────────────────

    private static void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine("  0");
        sb.AppendLine("SECTION");
        sb.AppendLine("  2");
        sb.AppendLine("HEADER");

        // DXF R12 minimum header
        Group(sb, 9, "$ACADVER");
        Group(sb, 1, "AC1009");   // R12

        Group(sb, 9, "$INSUNITS");
        Group(sb, 70, "4");       // 4 = mm

        Group(sb, 9, "$MEASUREMENT");
        Group(sb, 70, "1");       // 1 = metric

        sb.AppendLine("  0");
        sb.AppendLine("ENDSEC");
    }

    // ── TABLES (Layers) ──────────────────────────────────────────────────────────

    private void WriteTables(StringBuilder sb)
    {
        sb.AppendLine("  0");
        sb.AppendLine("SECTION");
        sb.AppendLine("  2");
        sb.AppendLine("TABLES");

        // LTYPE table (minimal — CONTINUOUS only)
        sb.AppendLine("  0");
        sb.AppendLine("TABLE");
        sb.AppendLine("  2");
        sb.AppendLine("LTYPE");
        Group(sb, 70, "1");
        sb.AppendLine("  0");
        sb.AppendLine("LTYPE");
        Group(sb, 2,  "CONTINUOUS");
        Group(sb, 70, "64");
        Group(sb, 3,  "Solid line");
        Group(sb, 72, "65");
        Group(sb, 73, "0");
        Group(sb, 40, "0.0");
        sb.AppendLine("  0");
        sb.AppendLine("ENDTAB");

        // LAYER table
        var layers = _database.GetLayers().ToList();
        sb.AppendLine("  0");
        sb.AppendLine("TABLE");
        sb.AppendLine("  2");
        sb.AppendLine("LAYER");
        Group(sb, 70, layers.Count.ToString());

        foreach (var layer in layers)
        {
            int aciColor = ArgbToAci(layer.Color);
            sb.AppendLine("  0");
            sb.AppendLine("LAYER");
            Group(sb, 2,  layer.Name);
            Group(sb, 70, layer.IsVisible ? "0" : "1");
            Group(sb, 62, (layer.IsVisible ? aciColor : -aciColor).ToString());
            Group(sb, 6,  "CONTINUOUS");
        }

        // Default layer "0"
        sb.AppendLine("  0");
        sb.AppendLine("LAYER");
        Group(sb, 2,  "0");
        Group(sb, 70, "0");
        Group(sb, 62, "7");
        Group(sb, 6,  "CONTINUOUS");

        sb.AppendLine("  0");
        sb.AppendLine("ENDTAB");

        sb.AppendLine("  0");
        sb.AppendLine("ENDSEC");
    }

    // ── BLOCKS (anonim ölçü blokları) ──────────────────────────────────────────────

    /*
       NE: Ölçü Bloğu Geometrisi (DimBlockGeometry)
       NEDEN: Gerçek bir DXF DIMENSION entity'si, çizim geometrisini (ok, uzatma çizgisi, metin)
              kendi içinde taşımaz — AutoCAD'in de yaptığı gibi anonim bir BLOCK'a (*D1, *D2, ...)
              koyar ve DIMENSION entity'si sadece o bloğu + tanım noktalarını referanslar. Bu sayede
              AutoCAD'de dimension GERÇEK bir DIMENSION nesnesi olarak seçilir/düzenlenir —
              önceki davranış (düz LINE+TEXT) sadece görsel olarak benziyordu, CAD anlamda
              "dimension" değildi.
    */
    private readonly record struct DimBlockGeometry(
        string BlockName, Vector3D DimLineP1, Vector3D DimLineP2,
        Vector3D Ext1Origin, Vector3D Ext2Origin, Vector3D TextPos,
        double TextRotation, double TextHeight, string Text, int TypeFlag);

    /*
       NE: BLOCKS Bölümünü Yaz (WriteBlocksSection)
       NEDEN: DIMENSION entity'leri ENTITIES bölümünden ÖNCE gelen BLOCKS bölümünde tanımlı
              olmalı (DXF dosya sırası zorunluluğu) — bu yüzden önce tüm Linear/Aligned
              DimensionEntity'ler için blok geometrisi hesaplanıp yazılıyor, sonra ENTITIES
              bölümünde bu bloklara referans veren DIMENSION kayıtları üretiliyor.

       KAPSAM: Radius/Angular DimensionType'lar hâlâ eski (patlatılmış LINE+TEXT) yönteme
              düşüyor — R12 DIMENSION group code'ları radius/angular için (15/25/35 merkez
              noktası, ek leader uzunluğu vb.) daha karmaşık ve hatalı üretilirse AutoCAD'in
              dosyayı reddetmesi riski var; bu yüzden sadece iyi anlaşılan Linear/Aligned için
              gerçek DIMENSION üretiliyor (bilinçli, dokümante edilmiş kapsam sınırı).
    */
    private static Dictionary<Guid, DimBlockGeometry> WriteBlocksSection(StringBuilder sb, List<CadEntity> entities)
    {
        var dims = entities.OfType<DimensionEntity>()
            .Where(d => d.DimType == DimensionType.Linear || d.DimType == DimensionType.Aligned)
            .ToList();

        var result = new Dictionary<Guid, DimBlockGeometry>();

        sb.AppendLine("  0");
        sb.AppendLine("SECTION");
        sb.AppendLine("  2");
        sb.AppendLine("BLOCKS");

        int counter = 1;
        foreach (var dim in dims)
        {
            var geo = ComputeDimensionGeometry(dim, $"*D{counter++}");
            result[dim.Id] = geo;

            string layer = dim.Layer ?? "0";
            int aci = ArgbToAci(dim.Color);

            sb.AppendLine("  0");
            sb.AppendLine("BLOCK");
            Group(sb, 8, layer);
            Group(sb, 2, geo.BlockName);
            Group(sb, 70, "1"); // 1 = anonim blok (dimension/hatch türetilmiş)
            GroupXYZ(sb, 10, 20, 30, new Vector3D(0, 0, 0));
            Group(sb, 3, geo.BlockName);

            WriteDxfLine(sb, layer, aci, geo.DimLineP1, geo.DimLineP2);
            WriteDxfLine(sb, layer, aci, geo.Ext1Origin, geo.DimLineP1);
            WriteDxfLine(sb, layer, aci, geo.Ext2Origin, geo.DimLineP2);
            WriteDxfText(sb, layer, aci, geo.Text, geo.TextPos, geo.TextHeight, geo.TextRotation);

            sb.AppendLine("  0");
            sb.AppendLine("ENDBLK");
        }

        sb.AppendLine("  0");
        sb.AppendLine("ENDSEC");

        return result;
    }

    private static DimBlockGeometry ComputeDimensionGeometry(DimensionEntity dim, string blockName)
    {
        if (dim.DimType == DimensionType.Aligned)
        {
            var seg = dim.SecondPoint - dim.FirstPoint;
            double len = seg.Length();
            var dir = len > 1e-9 ? new Vector3D(seg.X / len, seg.Y / len, 0) : new Vector3D(1, 0, 0);
            var perp = new Vector3D(-dir.Y, dir.X, 0);
            var dp = dim.DimLinePoint - dim.FirstPoint;
            double off = dp.X * perp.X + dp.Y * perp.Y;
            var dimP1 = new Vector3D(dim.FirstPoint.X + perp.X * off, dim.FirstPoint.Y + perp.Y * off, 0);
            var dimP2 = new Vector3D(dim.SecondPoint.X + perp.X * off, dim.SecondPoint.Y + perp.Y * off, 0);
            double angle = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;
            var mid = new Vector3D((dimP1.X + dimP2.X) / 2, (dimP1.Y + dimP2.Y) / 2, 0);

            return new DimBlockGeometry(blockName, dimP1, dimP2, dim.FirstPoint, dim.SecondPoint,
                mid, angle, dim.TextHeight, dim.GetDxfText(), 1); // 70=1 → Aligned
        }

        // Linear (Rotated/horizontal/vertical)
        bool horiz = Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X) >= Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
        if (horiz)
        {
            double dimY = dim.DimLinePoint.Y;
            var dimP1 = new Vector3D(dim.FirstPoint.X, dimY, 0);
            var dimP2 = new Vector3D(dim.SecondPoint.X, dimY, 0);
            var mid = new Vector3D((dim.FirstPoint.X + dim.SecondPoint.X) / 2, dimY + dim.TextHeight * 0.6, 0);
            return new DimBlockGeometry(blockName, dimP1, dimP2, dim.FirstPoint, dim.SecondPoint,
                mid, 0, dim.TextHeight, dim.GetDxfText(), 0); // 70=0 → Linear
        }
        else
        {
            double dimX = dim.DimLinePoint.X;
            var dimP1 = new Vector3D(dimX, dim.FirstPoint.Y, 0);
            var dimP2 = new Vector3D(dimX, dim.SecondPoint.Y, 0);
            var mid = new Vector3D(dimX + dim.TextHeight * 0.6, (dim.FirstPoint.Y + dim.SecondPoint.Y) / 2, 0);
            return new DimBlockGeometry(blockName, dimP1, dimP2, dim.FirstPoint, dim.SecondPoint,
                mid, 90, dim.TextHeight, dim.GetDxfText(), 0);
        }
    }

    private static void WriteDimensionEntity(StringBuilder sb, DimensionEntity dim, DimBlockGeometry geo)
    {
        sb.AppendLine("  0");
        sb.AppendLine("DIMENSION");
        Group(sb, 8, dim.Layer ?? "0");
        Group(sb, 2, geo.BlockName);
        GroupXYZ(sb, 10, 20, 30, geo.DimLineP1);
        GroupXYZ(sb, 11, 21, 31, geo.TextPos);
        GroupXYZ(sb, 13, 23, 33, geo.Ext1Origin);
        GroupXYZ(sb, 14, 24, 34, geo.Ext2Origin);
        Group(sb, 70, geo.TypeFlag.ToString());
        Group(sb, 1, geo.Text);
        Group(sb, 3, "STANDARD");
    }

    // ── ENTITIES ────────────────────────────────────────────────────────────────

    private static void WriteEntities(StringBuilder sb, List<CadEntity> entities, Dictionary<Guid, DimBlockGeometry> dimBlocks)
    {
        sb.AppendLine("  0");
        sb.AppendLine("SECTION");
        sb.AppendLine("  2");
        sb.AppendLine("ENTITIES");

        foreach (var entity in entities)
        {
            switch (entity)
            {
                case LineEntity line:
                    WriteLine(sb, line);
                    break;
                case TextEntity text:
                    WriteText(sb, text);
                    break;
                case CircleEntity circle:
                    WriteCircle(sb, circle);
                    break;
                case ArcEntity arc:
                    WriteArc(sb, arc);
                    break;
                case DimensionEntity dim when dimBlocks.TryGetValue(dim.Id, out var geo):
                    WriteDimensionEntity(sb, dim, geo);
                    break;
                case DimensionEntity dim:
                    WriteDimension(sb, dim); // Radius/Angular — patlatılmış fallback (bkz. WriteBlocksSection notu)
                    break;
                case LwPolylineEntity poly:
                    WritePolyline(sb, poly);
                    break;
                case SplineEntity spline:
                    WriteSpline(sb, spline);
                    break;
                case HatchEntity hatch:
                    WriteHatch(sb, hatch);
                    break;
                case PipeEntity pipe:
                    WritePipe(sb, pipe);
                    break;
                case SolidEntity solid:
                    WriteSolid(sb, solid);
                    break;
            }
        }

        sb.AppendLine("  0");
        sb.AppendLine("ENDSEC");
    }

    private static void WriteLine(StringBuilder sb, LineEntity line)
    {
        sb.AppendLine("  0");
        sb.AppendLine("LINE");
        Group(sb, 8,  line.Layer ?? "0");
        Group(sb, 62, ArgbToAci(line.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, line.StartPoint);
        GroupXYZ(sb, 11, 21, 31, line.EndPoint);
    }

    private static void WriteText(StringBuilder sb, TextEntity text)
    {
        sb.AppendLine("  0");
        sb.AppendLine("TEXT");
        Group(sb, 8,  text.Layer ?? "0");
        Group(sb, 62, ArgbToAci(text.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, text.Position);
        Group(sb, 40, text.Height.ToString("F2", CultureInfo.InvariantCulture));
        Group(sb, 1,  text.Text);
        Group(sb, 50, text.Rotation.ToString("F2", CultureInfo.InvariantCulture));
    }

    private static void WriteCircle(StringBuilder sb, CircleEntity circle)
    {
        sb.AppendLine("  0");
        sb.AppendLine("CIRCLE");
        Group(sb, 8,  circle.Layer ?? "0");
        Group(sb, 62, ArgbToAci(circle.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, circle.Center);
        Group(sb, 40, circle.Radius.ToString("F4", CultureInfo.InvariantCulture));
    }

    private static void WriteArc(StringBuilder sb, ArcEntity arc)
    {
        sb.AppendLine("  0");
        sb.AppendLine("ARC");
        Group(sb, 8,  arc.Layer ?? "0");
        Group(sb, 62, ArgbToAci(arc.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, arc.Center);
        Group(sb, 40, arc.Radius.ToString("F4", CultureInfo.InvariantCulture));
        // ArcEntity angles are in radians; DXF R12 needs degrees
        Group(sb, 50, (arc.StartAngle * 180.0 / Math.PI).ToString("F4", CultureInfo.InvariantCulture));
        Group(sb, 51, (arc.EndAngle   * 180.0 / Math.PI).ToString("F4", CultureInfo.InvariantCulture));
    }

    private static void WriteDimension(StringBuilder sb, DimensionEntity dim)
    {
        string layer = dim.Layer ?? "0";
        int aci = ArgbToAci(dim.Color);

        if (dim.DimType == DimensionType.Linear)
        {
            bool horiz = Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X) >= Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
            if (horiz)
            {
                double dimY = dim.DimLinePoint.Y;
                WriteDxfLine(sb, layer, aci, new Vector3D(dim.FirstPoint.X, dimY, 0), new Vector3D(dim.SecondPoint.X, dimY, 0));
                WriteDxfLine(sb, layer, aci, dim.FirstPoint, new Vector3D(dim.FirstPoint.X, dimY, 0));
                WriteDxfLine(sb, layer, aci, dim.SecondPoint, new Vector3D(dim.SecondPoint.X, dimY, 0));
                WriteDxfText(sb, layer, aci, dim.GetDxfText(), new Vector3D((dim.FirstPoint.X + dim.SecondPoint.X) / 2, dimY + dim.TextHeight * 0.6, 0), dim.TextHeight, 0);
            }
            else
            {
                double dimX = dim.DimLinePoint.X;
                WriteDxfLine(sb, layer, aci, new Vector3D(dimX, dim.FirstPoint.Y, 0), new Vector3D(dimX, dim.SecondPoint.Y, 0));
                WriteDxfLine(sb, layer, aci, dim.FirstPoint, new Vector3D(dimX, dim.FirstPoint.Y, 0));
                WriteDxfLine(sb, layer, aci, dim.SecondPoint, new Vector3D(dimX, dim.SecondPoint.Y, 0));
                WriteDxfText(sb, layer, aci, dim.GetDxfText(), new Vector3D(dimX + dim.TextHeight * 0.6, (dim.FirstPoint.Y + dim.SecondPoint.Y) / 2, 0), dim.TextHeight, 90);
            }
        }
        else if (dim.DimType == DimensionType.Aligned)
        {
            var seg = dim.SecondPoint - dim.FirstPoint;
            double len = seg.Length();
            if (len < 1e-9) return;
            var dir  = new Vector3D(seg.X / len, seg.Y / len, 0);
            var perp = new Vector3D(-dir.Y, dir.X, 0);
            var dp   = dim.DimLinePoint - dim.FirstPoint;
            double off = dp.X * perp.X + dp.Y * perp.Y;
            var dimP1 = new Vector3D(dim.FirstPoint.X + perp.X * off, dim.FirstPoint.Y + perp.Y * off, 0);
            var dimP2 = new Vector3D(dim.SecondPoint.X + perp.X * off, dim.SecondPoint.Y + perp.Y * off, 0);
            WriteDxfLine(sb, layer, aci, dimP1, dimP2);
            WriteDxfLine(sb, layer, aci, dim.FirstPoint, dimP1);
            WriteDxfLine(sb, layer, aci, dim.SecondPoint, dimP2);
            double angle = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;
            var mid = new Vector3D((dimP1.X + dimP2.X) / 2, (dimP1.Y + dimP2.Y) / 2, 0);
            WriteDxfText(sb, layer, aci, dim.GetDxfText(), mid, dim.TextHeight, angle);
        }
        else if (dim.DimType == DimensionType.Radius)
        {
            WriteDxfLine(sb, layer, aci, dim.FirstPoint, dim.SecondPoint);
            var dir = dim.SecondPoint - dim.FirstPoint;
            double len = dir.Length();
            if (len < 1e-9) return;
            var norm = new Vector3D(dir.X / len, dir.Y / len, 0);
            var textPos = new Vector3D(dim.SecondPoint.X + norm.X * dim.TextHeight, dim.SecondPoint.Y + norm.Y * dim.TextHeight, 0);
            WriteDxfText(sb, layer, aci, dim.GetDxfText(), textPos, dim.TextHeight, 0);
        }
        else if (dim.DimType == DimensionType.Angular)
        {
            WriteDxfLine(sb, layer, aci, dim.AngularVertex, dim.FirstPoint);
            WriteDxfLine(sb, layer, aci, dim.AngularVertex, dim.SecondPoint);
            var v1 = dim.FirstPoint - dim.AngularVertex;
            var v2 = dim.SecondPoint - dim.AngularVertex;
            double midAngle = (Math.Atan2(v1.Y, v1.X) + Math.Atan2(v2.Y, v2.X)) / 2;
            double r = Math.Min(v1.Length(), v2.Length()) * 0.6;
            var textPos = new Vector3D(dim.AngularVertex.X + Math.Cos(midAngle) * r * 1.3, dim.AngularVertex.Y + Math.Sin(midAngle) * r * 1.3, 0);
            WriteDxfText(sb, layer, aci, dim.GetDxfText(), textPos, dim.TextHeight, 0);
        }
    }

    private static void WriteDxfLine(StringBuilder sb, string layer, int aci, Vector3D p1, Vector3D p2)
    {
        sb.AppendLine("  0"); sb.AppendLine("LINE");
        Group(sb, 8, layer); Group(sb, 62, aci.ToString());
        GroupXYZ(sb, 10, 20, 30, p1); GroupXYZ(sb, 11, 21, 31, p2);
    }

    private static void WriteDxfText(StringBuilder sb, string layer, int aci, string text, Vector3D pos, double height, double rotation)
    {
        sb.AppendLine("  0"); sb.AppendLine("TEXT");
        Group(sb, 8, layer); Group(sb, 62, aci.ToString());
        GroupXYZ(sb, 10, 20, 30, pos);
        Group(sb, 40, height.ToString("F2", CultureInfo.InvariantCulture));
        Group(sb, 1, text);
        Group(sb, 50, rotation.ToString("F2", CultureInfo.InvariantCulture));
    }

    private static void WritePolyline(StringBuilder sb, LwPolylineEntity poly)
    {
        var pts = poly.Vertices?.ToList();
        if (pts == null || pts.Count < 2) return;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            WriteDxfLine(sb, poly.Layer ?? "0", ArgbToAci(poly.Color), pts[i], pts[i + 1]);
        }
        if (poly.IsClosed && pts.Count > 2)
            WriteDxfLine(sb, poly.Layer ?? "0", ArgbToAci(poly.Color), pts[^1], pts[0]);
    }

    private static void WriteSpline(StringBuilder sb, SplineEntity spline)
    {
        var pts = spline.ControlPoints?.ToList();
        if (pts == null || pts.Count < 2) return;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            WriteDxfLine(sb, spline.Layer ?? "0", ArgbToAci(spline.Color), pts[i], pts[i + 1]);
        }
    }

    private static void WriteHatch(StringBuilder sb, HatchEntity hatch)
    {
        var verts = hatch.BoundaryVertices?.ToList();
        if (verts == null || verts.Count < 3) return;
        string layer = hatch.Layer ?? "0";
        int aci = ArgbToAci(hatch.Color);
        for (int i = 0; i < verts.Count; i++)
        {
            int j = (i + 1) % verts.Count;
            WriteDxfLine(sb, layer, aci, verts[i], verts[j]);
        }
    }

    // ── FOOTER ──────────────────────────────────────────────────────────────────

    private static void WriteFooter(StringBuilder sb)
    {
        sb.AppendLine("  0");
        sb.AppendLine("EOF");
    }

    // ── YARDIMCI METODLAR ────────────────────────────────────────────────────────

    private static void Group(StringBuilder sb, int code, string value)
    {
        sb.AppendLine($"{code,3}");
        sb.AppendLine(value);
    }

    private static void GroupXYZ(StringBuilder sb, int gx, int gy, int gz, Vector3D v)
    {
        sb.AppendLine($"{gx,3}");
        sb.AppendLine(v.X.ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine($"{gy,3}");
        sb.AppendLine(v.Y.ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine($"{gz,3}");
        sb.AppendLine(v.Z.ToString("F4", CultureInfo.InvariantCulture));
    }

    /*
       NE: SolidEntity'yi 3DFACE Listesi Olarak Yaz (WriteSolid)
       NEDEN: DXF R12, bir B-Rep katı cismi (Topology.Solid — keyfi çok kenarlı Face/Loop/
              TopologyEdge grafiği) DOĞRUDAN temsil edemez; R12'nin tek düz-yüzey ilkeli
              3DFACE'tir (4 köşe, üçgen için 4. köşe 3.'nün tekrarı). Bu yüzden BRepTessellator
              (zaten Direct3DViewportControl/IfcExportService.ExportWall'da kullanılan AYNI
              üçgenleme yolu) ile Solid üçgenlere bölünüp HER üçgen ayrı bir 3DFACE olarak
              yazılır.
       ROUND-TRIP KAPSAM SINIRI (bilinçli, dokümante): DXF'te ayrı 3DFACE'leri TEK bir Solid'e
              geri gruplamanın standart bir yolu yok (POLYFACE MESH, R12 modunda ACadSharp
              tarafından okunamıyor — doğrulandı; XDATA/APPID tabanlı gruplama da bu okuyucuda
              güvenilir değil — doğrulandı). Bu yüzden DxfImportService, aynı (Layer, Color)
              ikilisini paylaşan TÜM 3DFACE'leri TEK bir SolidEntity'ye kaynaştırır
              (BRepBuilder.FromTriangleSoup) — aynı dosyada FARKLI katman/renkte birden fazla
              Solid varsa doğru ayrışır; AYNI katman+renkte birden fazla Solid varsa içeri
              aktarımda BİRLEŞİR (nadir, kabul edilebilir bir sınır — kullanıcı farklı
              solid'leri farklı katman/renkte tutarak bunu önleyebilir).
    */
    private static void WriteSolid(StringBuilder sb, SolidEntity solid)
    {
        var (verts, faces) = BRepTessellator.Tessellate(solid.Solid);
        if (verts.Count < 3 || faces.Count == 0) return;

        string layer = solid.Layer ?? "0";
        int aci = ArgbToAci(solid.Color);

        foreach (var (a, b, c) in faces)
            WriteDxfFace(sb, layer, aci, verts[a], verts[b], verts[c]);
    }

    private static void WriteDxfFace(StringBuilder sb, string layer, int aci, Vector3D p1, Vector3D p2, Vector3D p3)
    {
        sb.AppendLine("  0");
        sb.AppendLine("3DFACE");
        Group(sb, 8, layer);
        Group(sb, 62, aci.ToString());
        GroupXYZ(sb, 10, 20, 30, p1);
        GroupXYZ(sb, 11, 21, 31, p2);
        GroupXYZ(sb, 12, 22, 32, p3);
        GroupXYZ(sb, 13, 23, 33, p3); // Üçgen: 4. köşe 3.'nün tekrarı (DXF 3DFACE dejenere-quad kuralı).
    }

    // R12 DXF'te PipeEntity → merkez çizgisi (LINE) olarak yazılır.
    // R12 formatı LwPolyline genişliğini desteklemez; boru gösterimi ince çizgidir.
    private static void WritePipe(StringBuilder sb, PipeEntity pipe)
    {
        sb.AppendLine("  0");
        sb.AppendLine("LINE");
        Group(sb, 8,  pipe.Layer ?? "0");
        Group(sb, 62, ArgbToAci(pipe.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, pipe.StartPoint);
        GroupXYZ(sb, 11, 21, 31, pipe.EndPoint);
    }

    // Tam AutoCAD ACI renk tablosu (255 giriş).
    // Kaynak: Autodesk ACI Color Reference — AutoCAD Color Index palette.
    // En yakın ACI'yı bulmak için RGB uzaklık (öklid) karşılaştırması yapılır.
    private static readonly (int aci, byte r, byte g, byte b)[] s_aciTable =
    {
        // Standart renkler (1-9)
        ( 1, 255,   0,   0), ( 2, 255, 255,   0), ( 3,   0, 255,   0),
        ( 4,   0, 255, 255), ( 5,   0,   0, 255), ( 6, 255,   0, 255),
        ( 7, 255, 255, 255), ( 8,  65,  65,  65), ( 9, 128, 128, 128),
        // Kırmızı bantı (10-19)
        (10, 255,   0,   0), (11, 255, 127, 127), (12, 165,   0,   0),
        (13, 165,  82,  82), (14, 127,   0,   0), (15, 127,  63,  63),
        (16,  76,   0,   0), (17,  76,  38,  38), (18,  38,   0,   0),
        (19,  38,  19,  19),
        // Kırmızı-turuncu bantı (20-29)
        (20, 255,  63,   0), (21, 255, 159, 127), (22, 165,  41,   0),
        (23, 165, 103,  82), (24, 127,  31,   0), (25, 127,  79,  63),
        (26,  76,  19,   0), (27,  76,  47,  38), (28,  38,   9,   0),
        (29,  38,  24,  19),
        // Turuncu bantı (30-39)
        (30, 255, 127,   0), (31, 255, 191, 127), (32, 165,  82,   0),
        (33, 165, 124,  82), (34, 127,  63,   0), (35, 127,  95,  63),
        (36,  76,  38,   0), (37,  76,  57,  38), (38,  38,  19,   0),
        (39,  38,  28,  19),
        // Sarı-turuncu bantı (40-49)
        (40, 255, 191,   0), (41, 255, 223, 127), (42, 165, 124,   0),
        (43, 165, 145,  82), (44, 127,  95,   0), (45, 127, 111,  63),
        (46,  76,  57,   0), (47,  76,  66,  38), (48,  38,  28,   0),
        (49,  38,  33,  19),
        // Sarı bantı (50-59)
        (50, 255, 255,   0), (51, 255, 255, 127), (52, 165, 165,   0),
        (53, 165, 165,  82), (54, 127, 127,   0), (55, 127, 127,  63),
        (56,  76,  76,   0), (57,  76,  76,  38), (58,  38,  38,   0),
        (59,  38,  38,  19),
        // Sarı-yeşil bantı (60-69)
        (60, 191, 255,   0), (61, 223, 255, 127), (62, 124, 165,   0),
        (63, 145, 165,  82), (64,  95, 127,   0), (65, 111, 127,  63),
        (66,  57,  76,   0), (67,  66,  76,  38), (68,  28,  38,   0),
        (69,  33,  38,  19),
        // Açık yeşil bantı (70-79)
        (70, 127, 255,   0), (71, 191, 255, 127), (72,  82, 165,   0),
        (73, 124, 165,  82), (74,  63, 127,   0), (75,  95, 127,  63),
        (76,  38,  76,   0), (77,  57,  76,  38), (78,  19,  38,   0),
        (79,  28,  38,  19),
        // Yeşil-sarı bantı (80-89)
        (80,  63, 255,   0), (81, 159, 255, 127), (82,  41, 165,   0),
        (83, 103, 165,  82), (84,  31, 127,   0), (85,  79, 127,  63),
        (86,  19,  76,   0), (87,  47,  76,  38), (88,   9,  38,   0),
        (89,  24,  38,  19),
        // Yeşil bantı (90-99)
        (90,   0, 255,   0), (91, 127, 255, 127), (92,   0, 165,   0),
        (93,  82, 165,  82), (94,   0, 127,   0), (95,  63, 127,  63),
        (96,   0,  76,   0), (97,  38,  76,  38), (98,   0,  38,   0),
        (99,  19,  38,  19),
        // Yeşil-cyan bantı (100-109)
        (100,   0, 255,  63), (101, 127, 255, 159), (102,   0, 165,  41),
        (103,  82, 165, 103), (104,   0, 127,  31), (105,  63, 127,  79),
        (106,   0,  76,  19), (107,  38,  76,  47), (108,   0,  38,   9),
        (109,  19,  38,  24),
        // Açık teal bantı (110-119)
        (110,   0, 255, 127), (111, 127, 255, 191), (112,   0, 165,  82),
        (113,  82, 165, 124), (114,   0, 127,  63), (115,  63, 127,  95),
        (116,   0,  76,  38), (117,  38,  76,  57), (118,   0,  38,  19),
        (119,  19,  38,  28),
        // Yeşil-cyan bantı (120-129)
        (120,   0, 255, 191), (121, 127, 255, 223), (122,   0, 165, 124),
        (123,  82, 165, 145), (124,   0, 127,  95), (125,  63, 127, 111),
        (126,   0,  76,  57), (127,  38,  76,  66), (128,   0,  38,  28),
        (129,  19,  38,  33),
        // Cyan bantı (130-139)
        (130,   0, 255, 255), (131, 127, 255, 255), (132,   0, 165, 165),
        (133,  82, 165, 165), (134,   0, 127, 127), (135,  63, 127, 127),
        (136,   0,  76,  76), (137,  38,  76,  76), (138,   0,  38,  38),
        (139,  19,  38,  38),
        // Açık mavi-cyan bantı (140-149)
        (140,   0, 191, 255), (141, 127, 223, 255), (142,   0, 124, 165),
        (143,  82, 145, 165), (144,   0,  95, 127), (145,  63, 111, 127),
        (146,   0,  57,  76), (147,  38,  66,  76), (148,   0,  28,  38),
        (149,  19,  33,  38),
        // Mavi-cyan bantı (150-159)
        (150,   0, 127, 255), (151, 127, 191, 255), (152,   0,  82, 165),
        (153,  82, 124, 165), (154,   0,  63, 127), (155,  63,  95, 127),
        (156,   0,  38,  76), (157,  38,  57,  76), (158,   0,  19,  38),
        (159,  19,  28,  38),
        // Mavi bantı (160-169)
        (160,   0,  63, 255), (161, 127, 159, 255), (162,   0,  41, 165),
        (163,  82, 103, 165), (164,   0,  31, 127), (165,  63,  79, 127),
        (166,   0,  19,  76), (167,  38,  47,  76), (168,   0,   9,  38),
        (169,  19,  24,  38),
        // Saf mavi bantı (170-179)
        (170,   0,   0, 255), (171, 127, 127, 255), (172,   0,   0, 165),
        (173,  82,  82, 165), (174,   0,   0, 127), (175,  63,  63, 127),
        (176,   0,   0,  76), (177,  38,  38,  76), (178,   0,   0,  38),
        (179,  19,  19,  38),
        // Mavi-mor bantı (180-189)
        (180,  63,   0, 255), (181, 159, 127, 255), (182,  41,   0, 165),
        (183, 103,  82, 165), (184,  31,   0, 127), (185,  79,  63, 127),
        (186,  19,   0,  76), (187,  47,  38,  76), (188,   9,   0,  38),
        (189,  24,  19,  38),
        // Mor bantı (190-199)
        (190, 127,   0, 255), (191, 191, 127, 255), (192,  82,   0, 165),
        (193, 124,  82, 165), (194,  63,   0, 127), (195,  95,  63, 127),
        (196,  38,   0,  76), (197,  57,  38,  76), (198,  19,   0,  38),
        (199,  28,  19,  38),
        // Mor-pembe bantı (200-209)
        (200, 191,   0, 255), (201, 223, 127, 255), (202, 124,   0, 165),
        (203, 145,  82, 165), (204,  95,   0, 127), (205, 111,  63, 127),
        (206,  57,   0,  76), (207,  66,  38,  76), (208,  28,   0,  38),
        (209,  33,  19,  38),
        // Magenta bantı (210-219)
        (210, 255,   0, 255), (211, 255, 127, 255), (212, 165,   0, 165),
        (213, 165,  82, 165), (214, 127,   0, 127), (215, 127,  63, 127),
        (216,  76,   0,  76), (217,  76,  38,  76), (218,  38,   0,  38),
        (219,  38,  19,  38),
        // Magenta-kırmızı bantı (220-229)
        (220, 255,   0, 191), (221, 255, 127, 223), (222, 165,   0, 124),
        (223, 165,  82, 145), (224, 127,   0,  95), (225, 127,  63, 111),
        (226,  76,   0,  57), (227,  76,  38,  66), (228,  38,   0,  28),
        (229,  38,  19,  33),
        // Kırmızı-pembe bantı (230-239)
        (230, 255,   0, 127), (231, 255, 127, 191), (232, 165,   0,  82),
        (233, 165,  82, 124), (234, 127,   0,  63), (235, 127,  63,  95),
        (236,  76,   0,  38), (237,  76,  38,  57), (238,  38,   0,  19),
        (239,  38,  19,  28),
        // Pembe-kırmızı bantı (240-249)
        (240, 255,   0,  63), (241, 255, 127, 159), (242, 165,   0,  41),
        (243, 165,  82, 103), (244, 127,   0,  31), (245, 127,  63,  79),
        (246,  76,   0,  19), (247,  76,  38,  47), (248,  38,   0,   9),
        (249,  38,  19,  24),
        // Gri skalası (250-255)
        (250,  26,  26,  26), (251,  51,  51,  51), (252,  77,  77,  77),
        (253, 102, 102, 102), (254, 153, 153, 153), (255, 204, 204, 204),
    };

    private static int ArgbToAci(uint argb)
    {
        byte r = (byte)(argb >> 16);
        byte g = (byte)(argb >> 8);
        byte b = (byte)argb;

        int bestAci = 7;
        double bestDist = double.MaxValue;
        foreach (var (aci, ar, ag, ab) in s_aciTable)
        {
            double dist = Math.Sqrt(Math.Pow(r - ar, 2) + Math.Pow(g - ag, 2) + Math.Pow(b - ab, 2));
            if (dist < bestDist) { bestDist = dist; bestAci = aci; }
        }
        return bestAci;
    }
}
