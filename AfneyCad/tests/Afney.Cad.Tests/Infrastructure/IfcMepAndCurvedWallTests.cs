using System;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC — MEP İçeri Aktarma ve Kavisli Duvar Testleri (IfcMepAndCurvedWallTests)
   NEDEN: Bu oturumda IfcImportService/IfcExportService'e üç gerçek eksiklik giderildi:
          1. IFCPIPESEGMENT/IFCDUCTSEGMENT/IFCFLOWSEGMENT hiç içeri aktarılmıyordu —
             result.MepCount hiçbir zaman artırılmayan ölü bir alandı.
          2. IFCTRIMMEDCURVE(IFCCIRCLE) tabanlı kavisli duvar eksenleri (Axis temsili)
             hiç okunmuyordu — her duvar hep tek düz kutu olarak çiziliyordu.
          3. DuctEntity IFC dışa aktarımda tamamen atlanıyordu.
   Bu testler sentetik ama gerçekçi IFC STEP dosyalarıyla üçünün de çalıştığını doğruluyor.
*/
public class IfcMepAndCurvedWallTests
{
    private static string WriteTempIfcFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_test_{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, content);
        return path;
    }

    // Dünya-X ekseninde uzanan, 100mm yarıçaplı (200mm çaplı), 4000mm uzunluğunda bir boru.
    private const string PipeSegmentIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(1000.,2000.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #20=IFCCARTESIANPOINT(0.,0.,0.);
        #21=IFCDIRECTION(1.,0.,0.);
        #22=IFCAXIS2PLACEMENT3D(#20,#21,$);
        #30=IFCCARTESIANPOINT(0.,0.,0.);
        #31=IFCAXIS2PLACEMENT3D(#30,$,$);
        #32=IFCCIRCLEPROFILEDEF(.AREA.,$,#31,100.);
        #33=IFCEXTRUDEDAREASOLID(#32,#22,$,4000.);
        #34=IFCSHAPEREPRESENTATION($,'Body',$,(#33));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34));
        #50=IFCPIPESEGMENT('GUID_PIPE1',$,'Pipe1',$,$,#12,#35,$,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    // Dikdörtgen (400x300mm) kesitli, 2000mm uzunluğunda bir HVAC kanalı.
    private const string DuctSegmentIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #20=IFCCARTESIANPOINT(0.,0.,0.);
        #21=IFCDIRECTION(1.,0.,0.);
        #22=IFCAXIS2PLACEMENT3D(#20,#21,$);
        #30=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,400.,300.);
        #33=IFCEXTRUDEDAREASOLID(#30,#22,$,2000.);
        #34=IFCSHAPEREPRESENTATION($,'Body',$,(#33));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34));
        #50=IFCDUCTSEGMENT('GUID_DUCT1',$,'Duct1',$,$,#12,#35,$,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    // Merkezi (1000,0,0), yarıçapı 2000mm olan, 0-90 derece arası (çeyrek daire) kavisli
    // bir duvar ekseni ('Axis' temsili) + normal düz Body extrusion (200x1000mm, 2700mm yükseklik).
    private const string CurvedWallIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #30=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,200.,1000.);
        #31=IFCCARTESIANPOINT(0.,0.,0.);
        #32=IFCAXIS2PLACEMENT3D(#31,$,$);
        #33=IFCEXTRUDEDAREASOLID(#30,#32,$,2700.);
        #34=IFCSHAPEREPRESENTATION($,'Body',$,(#33));
        #70=IFCCARTESIANPOINT(1000.,0.,0.);
        #71=IFCAXIS2PLACEMENT3D(#70,$,$);
        #72=IFCCIRCLE(#71,2000.);
        #73=IFCTRIMMEDCURVE(#72,(IFCPARAMETERVALUE(0.)),(IFCPARAMETERVALUE(1.5707963267948966)),.T.,.PARAMETER.);
        #74=IFCSHAPEREPRESENTATION($,'Axis',$,(#73));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34,#74));
        #50=IFCWALL('GUID_CURVEDWALL',$,'CurvedWall',$,$,#12,#35,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    [Fact]
    public void Import_PipeSegment_CreatesPipeEntity_WithCorrectDiameterAndLength()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(PipeSegmentIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = true
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.MepCount);

            var pipes = db.GetAllEntities().OfType<PipeEntity>().Where(p => p.Layer == "MEP-IMPORT").ToList();
            Assert.Single(pipes);

            var pipe = pipes[0];
            Assert.Equal(200, pipe.InnerDiameter, precision: 1); // 100mm yarıçap × 2
            Assert.Equal(4000, pipe.GetLength(), precision: 0);  // Depth = 4000mm boyunca +X
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_DuctSegment_CreatesRectangularDuctEntity_WithCorrectDimensions()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(DuctSegmentIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = true
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.MepCount);

            var ducts = db.GetAllEntities().OfType<DuctEntity>().Where(d => d.Layer == "MEP-IMPORT").ToList();
            Assert.Single(ducts);

            var duct = ducts[0];
            Assert.Equal(DuctShape.Rectangular, duct.Shape);
            Assert.Equal(400, duct.WidthMm, precision: 1);
            Assert.Equal(300, duct.HeightMm, precision: 1);
            Assert.Equal(2000, duct.GetLength(), precision: 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_WithImportMepFalse_SkipsMepEntities_MepCountStaysZero()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(PipeSegmentIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = false
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(0, result.MepCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.Empty(db.GetAllEntities().OfType<PipeEntity>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_CurvedWallAxis_ProducesMultipleSegmentsFollowingArc_NotSingleStraightBox()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(CurvedWallIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = true, ImportSlabs = false, ImportWindows = false, ImportDoors = false
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.WallCount);

            var lines = db.GetAllEntities().OfType<LineEntity>().Where(l => l.Layer == "ARCH-WALL").ToList();

            // 16 segmentlik tessellation → 16 × 12 kenar (her segment kendi kutu tel-kafesi) = 192.
            // ESKİ DAVRANIŞTA bu her zaman 12 (tek düz kutu) olurdu.
            Assert.Equal(192, lines.Count);

            // Çeyrek daire (0°→90°, merkez (1000,0), yarıçap 2000): başlangıç noktası (3000,0),
            // bitiş noktası (1000,2000) civarında. Düz bir çizgi olsaydı ara noktalar bu ikisini
            // birleştiren doğru üzerinde kalırdı — yay üzerindeki noktalar bu doğrudan sapmalı.
            var allPoints = lines.SelectMany(l => new[] { l.StartPoint, l.EndPoint }).ToList();

            // Yayın en "dışarı" noktası (45°) yaklaşık (1000+2000*cos45, 2000*sin45) = (2414,1414) civarında olmalı.
            bool anyMidArcPoint = allPoints.Any(p =>
                p.X > 2000 && p.X < 2800 && p.Y > 1000 && p.Y < 1800);
            Assert.True(anyMidArcPoint, "Yay ortası civarında (45°) bir nokta bulunamadı — düz çizgiye düşmüş olabilir.");

            // Düz bir doğru (start-end) üzerinde OLMAYAN noktalar olmalı (yay sapması).
            // Start≈(3000,0), End≈(1000,2000) doğrusu: x+y=3000. Yay üzerindeki noktalar bu
            // çizgiden belirgin şekilde uzak olmalı (dışbükey yay içeride kalır).
            bool anyOffLinePoint = allPoints.Any(p => Math.Abs(p.X + p.Y - 3000) > 200);
            Assert.True(anyOffLinePoint, "Tüm noktalar start-end doğrusu üzerinde — kavis uygulanmamış görünüyor.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_DuctEntity_ProducesIfcDuctSegmentLine()
    {
        var svc = new IfcExportService();
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0), 400, 300);
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_export_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(new CadEntity[] { duct }, path);
            string content = File.ReadAllText(path);

            Assert.Contains("IFCDUCTSEGMENT", content);
            Assert.Contains("IFCRECTANGLEPROFILEDEF", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Export_DuctEntity_Circular_ProducesCircleProfile()
    {
        var svc = new IfcExportService();
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 315);
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_export_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(new CadEntity[] { duct }, path);
            string content = File.ReadAllText(path);

            Assert.Contains("IFCDUCTSEGMENT", content);
            Assert.Contains("IFCCIRCLEPROFILEDEF", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
