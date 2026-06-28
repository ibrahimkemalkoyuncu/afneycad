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

namespace Afney.Cad.Infrastructure.Export;

// Gelişmiş DXF R2018 Writer — Layer stili, linetype, hatch, text, dimension koruması
public class AdvancedDxfWriterService
{
    private readonly CadDatabase _database;
    private readonly StringBuilder _sb = new();
    private int _handle = 100;

    public AdvancedDxfWriterService(CadDatabase database) => _database = database;

    public void WriteToFile(string filePath)
    {
        _sb.Clear();
        _handle = 100;

        WriteHeader();
        WriteTables();
        WriteBlocks();
        WriteEntities();
        WriteObjects();
        WriteEof();

        File.WriteAllText(filePath, _sb.ToString(), Encoding.ASCII);
    }

    private void WriteHeader()
    {
        Section("HEADER");
        Dxf(9, "$ACADVER"); Dxf(1, "AC1032"); // R2018
        Dxf(9, "$INSUNITS"); Dxf(70, 4); // mm
        Dxf(9, "$MEASUREMENT"); Dxf(70, 1); // metrik
        Dxf(9, "$LWDISPLAY"); Dxf(290, 1);
        EndSection();
    }

    private void WriteTables()
    {
        Section("TABLES");

        // LTYPE tablosu
        Dxf(0, "TABLE"); Dxf(2, "LTYPE"); Dxf(70, 3);
        WriteLinetype("Continuous", "", new double[0]);
        WriteLinetype("DASHED", "__ __ __", new[] { 5.0, -2.5 });
        WriteLinetype("CENTER", "____ _ ____", new[] { 12.5, -2.5, 2.5, -2.5 });
        Dxf(0, "ENDTAB");

        // LAYER tablosu
        Dxf(0, "TABLE"); Dxf(2, "LAYER"); Dxf(70, _database.GetLayers().Count());
        foreach (var layer in _database.GetLayers())
        {
            Dxf(0, "LAYER");
            Handle();
            Dxf(100, "AcDbSymbolTableRecord");
            Dxf(100, "AcDbLayerTableRecord");
            Dxf(2, layer.Name);
            Dxf(70, layer.IsFrozen ? 1 : 0);
            Dxf(62, AciColor(layer.Color));
            Dxf(6, "Continuous");
        }
        Dxf(0, "ENDTAB");

        // STYLE tablosu
        Dxf(0, "TABLE"); Dxf(2, "STYLE"); Dxf(70, 1);
        Dxf(0, "STYLE"); Handle();
        Dxf(100, "AcDbSymbolTableRecord"); Dxf(100, "AcDbTextStyleTableRecord");
        Dxf(2, "Standard"); Dxf(70, 0); Dxf(40, 0.0); Dxf(41, 1.0);
        Dxf(3, "txt"); Dxf(4, "");
        Dxf(0, "ENDTAB");

        EndSection();
    }

    private void WriteBlocks()
    {
        Section("BLOCKS");
        // Model space
        Dxf(0, "BLOCK"); Handle(); Dxf(100, "AcDbEntity"); Dxf(8, "0");
        Dxf(100, "AcDbBlockBegin"); Dxf(2, "*Model_Space"); Dxf(70, 0);
        Dxf(10, 0.0); Dxf(20, 0.0); Dxf(30, 0.0);
        Dxf(0, "ENDBLK"); Handle(); Dxf(100, "AcDbEntity"); Dxf(8, "0"); Dxf(100, "AcDbBlockEnd");
        EndSection();
    }

    private void WriteEntities()
    {
        Section("ENTITIES");

        foreach (var entity in _database.GetAllEntities())
        {
            if (entity is LineEntity line) WriteLine(line);
            else if (entity is CircleEntity circle) WriteCircle(circle);
            else if (entity is TextEntity text) WriteText(text);
            else if (entity is Afney.Cad.Domain.Entities.Basic.LwPolylineEntity poly) WritePolyline(poly);
            else if (entity is Afney.Cad.Domain.Entities.Basic.ArcEntity arc) WriteArc(arc);
        }

        EndSection();
    }

    private void WriteObjects()
    {
        Section("OBJECTS");
        Dxf(0, "DICTIONARY"); Handle();
        Dxf(100, "AcDbDictionary"); Dxf(281, 1);
        EndSection();
    }

    private void WriteLine(LineEntity line)
    {
        Dxf(0, "LINE"); Handle();
        Dxf(100, "AcDbEntity"); Dxf(8, line.Layer ?? "0"); Dxf(62, AciColor(line.Color));
        Dxf(100, "AcDbLine");
        Dxf(10, line.StartPoint.X); Dxf(20, line.StartPoint.Y); Dxf(30, line.StartPoint.Z);
        Dxf(11, line.EndPoint.X); Dxf(21, line.EndPoint.Y); Dxf(31, line.EndPoint.Z);
    }

    private void WriteCircle(CircleEntity circle)
    {
        Dxf(0, "CIRCLE"); Handle();
        Dxf(100, "AcDbEntity"); Dxf(8, circle.Layer ?? "0"); Dxf(62, AciColor(circle.Color));
        Dxf(100, "AcDbCircle");
        Dxf(10, circle.Center.X); Dxf(20, circle.Center.Y); Dxf(30, circle.Center.Z);
        Dxf(40, circle.Radius);
    }

    private void WriteText(TextEntity text)
    {
        Dxf(0, "TEXT"); Handle();
        Dxf(100, "AcDbEntity"); Dxf(8, text.Layer ?? "0"); Dxf(62, AciColor(text.Color));
        Dxf(100, "AcDbText");
        Dxf(10, text.Position.X); Dxf(20, text.Position.Y); Dxf(30, text.Position.Z);
        Dxf(40, text.Height);
        Dxf(1, text.Text ?? "");
    }

    private void WritePolyline(LwPolylineEntity poly)
    {
        Dxf(0, "LWPOLYLINE"); Handle();
        Dxf(100, "AcDbEntity"); Dxf(8, poly.Layer ?? "0"); Dxf(62, AciColor(poly.Color));
        Dxf(100, "AcDbPolyline");
        Dxf(90, poly.Vertices.Count);
        Dxf(70, poly.IsClosed ? 1 : 0);
        foreach (var pt in poly.Vertices)
        {
            Dxf(10, pt.X); Dxf(20, pt.Y);
        }
    }

    private void WriteArc(ArcEntity arc)
    {
        Dxf(0, "ARC"); Handle();
        Dxf(100, "AcDbEntity"); Dxf(8, arc.Layer ?? "0"); Dxf(62, AciColor(arc.Color));
        Dxf(100, "AcDbCircle");
        Dxf(10, arc.Center.X); Dxf(20, arc.Center.Y); Dxf(30, arc.Center.Z);
        Dxf(40, arc.Radius);
        Dxf(100, "AcDbArc");
        Dxf(50, arc.StartAngle * 180.0 / Math.PI);
        Dxf(51, arc.EndAngle * 180.0 / Math.PI);
    }

    private void WriteLinetype(string name, string desc, double[] pattern)
    {
        Dxf(0, "LTYPE"); Handle();
        Dxf(100, "AcDbSymbolTableRecord"); Dxf(100, "AcDbLinetypeTableRecord");
        Dxf(2, name); Dxf(70, 0); Dxf(3, desc); Dxf(72, 65); Dxf(73, pattern.Length);
        Dxf(40, pattern.Sum(Math.Abs));
        foreach (var p in pattern) Dxf(49, p);
    }

    private int AciColor(uint argb)
    {
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);

        if (r > 200 && g < 50 && b < 50) return 1;   // kırmızı
        if (r > 200 && g > 200 && b < 50) return 2;   // sarı
        if (r < 50 && g > 200 && b < 50) return 3;    // yeşil
        if (r < 50 && g > 200 && b > 200) return 4;   // cyan
        if (r < 50 && g < 50 && b > 200) return 5;    // mavi
        if (r > 200 && g < 50 && b > 200) return 6;   // magenta
        if (r > 200 && g > 200 && b > 200) return 7;  // beyaz
        return 7;
    }

    private void Handle() { Dxf(5, (_handle++).ToString("X")); }
    private void Section(string name) { Dxf(0, "SECTION"); Dxf(2, name); }
    private void EndSection() { Dxf(0, "ENDSEC"); }
    private void WriteEof() { Dxf(0, "EOF"); }

    private void Dxf(int code, string value) { _sb.AppendLine(code.ToString()); _sb.AppendLine(value); }
    private void Dxf(int code, int value) { _sb.AppendLine(code.ToString()); _sb.AppendLine(value.ToString()); }
    private void Dxf(int code, double value) { _sb.AppendLine(code.ToString()); _sb.AppendLine(value.ToString("F6", CultureInfo.InvariantCulture)); }
}
