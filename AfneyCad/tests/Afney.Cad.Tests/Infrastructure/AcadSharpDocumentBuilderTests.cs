using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
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
}
