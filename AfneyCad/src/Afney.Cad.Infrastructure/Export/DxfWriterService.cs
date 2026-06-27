using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Geometry.Primitives;

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
        WriteHeader(sb);
        WriteTables(sb);
        WriteEntities(sb, _database.GetAllEntities().ToList());
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
        WriteHeader(sb);
        WriteTables(sb);
        WriteEntities(sb, entities.ToList());
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

    // ── ENTITIES ────────────────────────────────────────────────────────────────

    private static void WriteEntities(StringBuilder sb, List<CadEntity> entities)
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
                case DimensionEntity dim:
                    WriteDimension(sb, dim);
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
        Group(sb, 40, text.Height.ToString("F2"));
        Group(sb, 1,  text.Text);
        Group(sb, 50, text.Rotation.ToString("F2"));
    }

    private static void WriteCircle(StringBuilder sb, CircleEntity circle)
    {
        sb.AppendLine("  0");
        sb.AppendLine("CIRCLE");
        Group(sb, 8,  circle.Layer ?? "0");
        Group(sb, 62, ArgbToAci(circle.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, circle.Center);
        Group(sb, 40, circle.Radius.ToString("F4"));
    }

    private static void WriteArc(StringBuilder sb, ArcEntity arc)
    {
        sb.AppendLine("  0");
        sb.AppendLine("ARC");
        Group(sb, 8,  arc.Layer ?? "0");
        Group(sb, 62, ArgbToAci(arc.Color).ToString());
        GroupXYZ(sb, 10, 20, 30, arc.Center);
        Group(sb, 40, arc.Radius.ToString("F4"));
        // ArcEntity angles are in radians; DXF R12 needs degrees
        Group(sb, 50, (arc.StartAngle * 180.0 / Math.PI).ToString("F4"));
        Group(sb, 51, (arc.EndAngle   * 180.0 / Math.PI).ToString("F4"));
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
        Group(sb, 40, height.ToString("F2"));
        Group(sb, 1, text);
        Group(sb, 50, rotation.ToString("F2"));
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
        sb.AppendLine(v.X.ToString("F4"));
        sb.AppendLine($"{gy,3}");
        sb.AppendLine(v.Y.ToString("F4"));
        sb.AppendLine($"{gz,3}");
        sb.AppendLine(v.Z.ToString("F4"));
    }

    /*
       NE: ARGB → AutoCAD Color Index (ACI) Dönüşümü
       NEDEN: DXF R12 ACI renk kodu kullanır (1-255). En yakın ACI rengini bul.
    */
    private static int ArgbToAci(uint argb)
    {
        byte r = (byte)(argb >> 16);
        byte g = (byte)(argb >> 8);
        byte b = (byte)argb;

        // ACI renk tablosunun en yaygın girişleri
        var aciMap = new[]
        {
            (1,  255,   0,   0),  // Kırmızı
            (2,  255, 255,   0),  // Sarı
            (3,    0, 255,   0),  // Yeşil
            (4,    0, 255, 255),  // Cyan
            (5,    0,   0, 255),  // Mavi
            (6,  255,   0, 255),  // Magenta
            (7,  255, 255, 255),  // Beyaz
            (8,  128, 128, 128),  // Koyu gri
            (9,  192, 192, 192),  // Açık gri
            (30, 255, 140,   0),  // Turuncu
            (11,   0, 140, 255),  // Açık mavi
            (12, 150,  80,  50),  // Kahverengi yakını
        };

        int bestAci = 7;
        double bestDist = double.MaxValue;
        foreach (var (aci, ar, ag, ab) in aciMap)
        {
            double dist = Math.Sqrt(Math.Pow(r - ar, 2) + Math.Pow(g - ag, 2) + Math.Pow(b - ab, 2));
            if (dist < bestDist) { bestDist = dist; bestAci = aci; }
        }
        return bestAci;
    }
}
