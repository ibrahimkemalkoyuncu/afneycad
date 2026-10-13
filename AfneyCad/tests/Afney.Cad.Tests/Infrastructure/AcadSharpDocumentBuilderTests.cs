using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: AcadSharpDocumentBuilder — $INSUNITS Meta Verisi Testi
   NEDEN: Denetim taraması bulgusu: DXF/DWG export ortak builder'ı `doc.Header.InsUnits`'i
          hiç ayarlamıyordu (ACadSharp varsayılanı Unitless'ta kalıyordu). AfneyCAD içi
          round-trip'i bozmuyor (Unitless için de unitScale=1.0 varsayılıyor, mm-native
          koordinatlarla zaten tutarlı) ama GERÇEK AutoCAD/başka MEP yazılımına aktarılan
          dosyalarda (asıl export amacı) birim meta verisi eksik kalıyordu — export
          amacının ta kendisi olan üçüncü-taraf uyumluluk için best-practice açığıydı.
          Bu test hem $INSUNITS'in DXF çıktısında yer aldığını hem de export→re-import
          round-trip'inin koordinatlarda ölçek kayması yaratmadığını doğruluyor.
*/
public class AcadSharpDocumentBuilderTests
{
    [Fact]
    public void ExportedDxf_DeclaresMillimeterInsUnits()
    {
        var db = new CadDatabase();
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0)));

        var path = Path.Combine(Path.GetTempPath(), $"afneycad_insunits_test_{System.Guid.NewGuid():N}.dxf");
        try
        {
            new AdvancedDxfWriterService(db).WriteToFile(path);
            string dxf = File.ReadAllText(path);

            Assert.Contains("$INSUNITS", dxf);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExportThenReimportDxf_PreservesCoordinatesWithoutScaleDrift()
    {
        var db = new CadDatabase();
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(2500, 0, 0))); // 2500mm

        var path = Path.Combine(Path.GetTempPath(), $"afneycad_roundtrip_test_{System.Guid.NewGuid():N}.dxf");
        try
        {
            new AdvancedDxfWriterService(db).WriteToFile(path);

            var reimported = new DxfImportService().ImportDxf(path);
            var line = Assert.Single(reimported.OfType<LineEntity>());

            double length = line.StartPoint.DistanceTo(line.EndPoint);
            Assert.True(System.Math.Abs(length - 2500.0) < 0.5,
                $"Export→re-import sonrası beklenen 2500mm, gerçek: {length}mm — INSUNITS ölçek kayması olabilir.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /*
       NE: TB-02 — Spline/Hatch/Solid/Duct Artık DWG/DXF Export'tan Dışlanmıyor
       NEDEN: AcadSharpDocumentBuilder.ToAcad() önceden yalnızca 6 entity tipini
              (Line/Circle/Arc/Text/LwPolyline/Pipe) destekliyordu; geri kalanı sessizce
              `skippedEntitySummary`'e düşüyordu. Bu test, bu dört tipin artık GERÇEKTEN
              ModelSpace'e yazıldığını (ve "atlandı" listesinde olmadığını) doğruluyor.
    */
    [Fact]
    public void Build_SplineHatchSolidDuct_AreNoLongerSkipped()
    {
        var db = new CadDatabase();

        var spline = new SplineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(500, 500, 0),
            new Vector3D(1000, 0, 0), new Vector3D(1500, 500, 0)
        });
        db.AddEntity(spline);

        var hatch = new HatchEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0),
            new Vector3D(1000, 1000, 0), new Vector3D(0, 1000, 0)
        }, 0xFFFFFFFF);
        db.AddEntity(hatch);

        var boxSolid = BRepBuilder.ExtrudeBox(Vector3D.Zero, Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000, "TestBox");
        db.AddEntity(new SolidEntity(boxSolid));

        db.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 400, 300));

        var doc = AcadSharpDocumentBuilder.Build(db, out var skipped);

        Assert.DoesNotContain(skipped, s => s.StartsWith("SplineEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("HatchEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("SolidEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("DuctEntity"));

        Assert.Contains(doc.ModelSpace.Entities, e => e is ACadSharp.Entities.Spline);
        Assert.Contains(doc.ModelSpace.Entities, e => e is ACadSharp.Entities.LwPolyline lw && lw.IsClosed); // Hatch sınırı
        Assert.Contains(doc.ModelSpace.Entities, e => e is ACadSharp.Entities.LwPolyline lw && !lw.IsClosed); // Duct merkez hattı
        // Solid bir kutu (6 yüz × 2 üçgen = 12 üçgen) — en az birkaç Face3D üretilmeli.
        Assert.True(doc.ModelSpace.Entities.Count(e => e is ACadSharp.Entities.Face3D) >= 6);
    }

    [Fact]
    public void Build_Spline_PreservesControlPointCount()
    {
        var db = new CadDatabase();
        var spline = new SplineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(500, 500, 0), new Vector3D(1000, 0, 0)
        });
        db.AddEntity(spline);

        var doc = AcadSharpDocumentBuilder.Build(db, out _);
        var acSpline = Assert.Single(doc.ModelSpace.Entities.OfType<ACadSharp.Entities.Spline>());

        Assert.Equal(spline.ControlPoints.Count, acSpline.ControlPoints.Count);
        Assert.Equal(spline.Degree, acSpline.Degree);
    }

    /*
       NE: TB-02 — Dimension/Elbow/Tee/Wall/Room Artık DWG/DXF Export'tan Dışlanmıyor
       NEDEN: `ToAcad()` bu 5 tipi hiç tanımıyordu, hepsi `skippedEntitySummary`'e düşüyordu.
    */
    [Fact]
    public void Build_DimensionElbowTeeWallRoom_AreNoLongerSkipped()
    {
        var db = new CadDatabase();

        db.AddEntity(new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0),
            new Vector3D(0, 200, 0), DimensionType.Linear));

        db.AddEntity(new ElbowEntity(new Vector3D(0, 0, 0), 100, new Vector3D(-1, 0, 0), new Vector3D(0, 1, 0)));

        db.AddEntity(new TeeEntity(new Vector3D(500, 500, 0), 100, 50, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)));

        db.AddEntity(new WallEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0), 200));

        var roomBoundary = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0),
            new Vector3D(2000, 2000, 0), new Vector3D(0, 2000, 0)
        }, true);
        db.AddEntity(new RoomEntity(roomBoundary));

        var doc = AcadSharpDocumentBuilder.Build(db, out var skipped);

        Assert.DoesNotContain(skipped, s => s.StartsWith("DimensionEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("ElbowEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("TeeEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("WallEntity"));
        Assert.DoesNotContain(skipped, s => s.StartsWith("RoomEntity"));

        // Dimension → en az bir çizgi + bir metin (ölçü çizgisi + uzatma çizgileri + değer metni)
        Assert.Contains(doc.ModelSpace.Entities, e => e is ACadSharp.Entities.TextEntity);
        // Wall → kapalı 4 köşeli polyline
        Assert.Contains(doc.ModelSpace.Entities, e => e is ACadSharp.Entities.LwPolyline lw && lw.IsClosed && lw.Vertices.Count == 4);
        // Room → kapalı polyline (kendi Boundary'si)
        Assert.Contains(doc.ModelSpace.Entities, e => e is ACadSharp.Entities.LwPolyline lw && lw.IsClosed && lw.Vertices.Count == 4);
        // Elbow/Tee → en az 4 ek çizgi (2+2)
        Assert.True(doc.ModelSpace.Entities.Count(e => e is ACadSharp.Entities.Line) >= 4);
    }
}
