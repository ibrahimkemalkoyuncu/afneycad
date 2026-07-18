using System;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Infrastructure.Import;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC Import — Rotasyon ve Karmaşık Profil Testleri (IfcRotationAndProfileTests)
   NEDEN: IfcImportService, dış bir ajan araştırmasıyla tespit edilen iki gerçek eksiği
          gideriyordu:
          1. IFCAXIS2PLACEMENT3D'nin RefDirection'ı (rotasyon) hiç okunmuyordu — döndürülmüş
             her duvar/kapı/pencere IFC dosyasındaki açısından bağımsız olarak hep 0°
             (eksene paralel) içeri aktarılıyordu.
          2. Sadece IFCRECTANGLEPROFILEDEF (dikdörtgen kesit) destekleniyordu —
             IFCARBITRARYCLOSEDPROFILEDEF (keyfi çokgen) ve IFCCIRCLEPROFILEDEF (dairesel)
             kesitli duvarlar/kolonlar hep varsayılan dikdörtgene düşüyordu.
   Bu testler sentetik ama gerçekçi IFC STEP dosyalarıyla üçünün de düzeldiğini doğruluyor.
*/
public class IfcRotationAndProfileTests
{
    private static string WriteTempIfcFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_test_{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, content);
        return path;
    }

    // 45° döndürülmüş, 200x1000mm kesitli, 2700mm yüksekliğinde bir duvar.
    private const string RotatedWallIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #15=IFCDIRECTION(0.70710678,0.70710678,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,#15);
        #12=IFCLOCALPLACEMENT($,#11);
        #30=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,200.,1000.);
        #31=IFCCARTESIANPOINT(0.,0.,0.);
        #32=IFCAXIS2PLACEMENT3D(#31,$,$);
        #33=IFCEXTRUDEDAREASOLID(#30,#32,$,2700.);
        #34=IFCSHAPEREPRESENTATION($,$,$,(#33));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34));
        #50=IFCWALL('GUID1',$,'RotatedWall',$,$,#12,#35,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    // Üçgen (keyfi çokgen) kesitli bir duvar — IfcArbitraryClosedProfileDef.
    private const string ArbitraryProfileIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #60=IFCCARTESIANPOINT(0.,0.,0.);
        #61=IFCCARTESIANPOINT(1000.,0.,0.);
        #62=IFCCARTESIANPOINT(0.,1000.,0.);
        #63=IFCPOLYLINE((#60,#61,#62));
        #64=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,$,#63);
        #32=IFCAXIS2PLACEMENT3D(#10,$,$);
        #33=IFCEXTRUDEDAREASOLID(#64,#32,$,3000.);
        #34=IFCSHAPEREPRESENTATION($,$,$,(#33));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34));
        #50=IFCWALL('GUID2',$,'TriangleWall',$,$,#12,#35,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    // Dairesel (500mm yarıçap) kesitli bir kolon/duvar — IfcCircleProfileDef.
    private const string CircleProfileIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #70=IFCAXIS2PLACEMENT3D(#10,$,$);
        #71=IFCCIRCLEPROFILEDEF(.AREA.,$,#70,500.);
        #32=IFCAXIS2PLACEMENT3D(#10,$,$);
        #33=IFCEXTRUDEDAREASOLID(#71,#32,$,2700.);
        #34=IFCSHAPEREPRESENTATION($,$,$,(#33));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34));
        #50=IFCWALL('GUID3',$,'RoundColumn',$,$,#12,#35,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    [Fact]
    public void Import_RotatedWall_AppliesRotationToGeometry_NotFlatAtZeroDegrees()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(RotatedWallIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions { ImportWalls = true, ImportSlabs = false, ImportWindows = false, ImportDoors = false });
            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.WallCount);

            var lines = db.GetAllEntities().OfType<LineEntity>().Where(l => l.Layer == "ARCH-WALL").ToList();
            Assert.Equal(12, lines.Count); // dikdörtgen kesit → tel-kafes kutu

            // ESKİ DAVRANIŞTA duvarın uzunluk ekseni hep dünya-X yönünde (Y=0) olurdu.
            // 45° döndürülmüş bir duvarda, X boyunca uzanan hiçbir kenar OLMAMALI —
            // en az bir köşe noktası hem X hem Y'de belirgin şekilde ilerlemiş olmalı.
            bool anyDiagonalPoint = lines.Any(l =>
                (l.StartPoint.X > 500 && l.StartPoint.Y > 500) ||
                (l.EndPoint.X > 500 && l.EndPoint.Y > 500));
            Assert.True(anyDiagonalPoint, "Duvar 45° döndürülmüş olmalıydı ama tüm noktalar eksene paralel görünüyor.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_ArbitraryClosedProfile_ExtrudesTriangleNotDefaultRectangle()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(ArbitraryProfileIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions { ImportWalls = true, ImportSlabs = false, ImportWindows = false, ImportDoors = false });
            Assert.True(result.Success, string.Join("; ", result.Errors));

            var lines = db.GetAllEntities().OfType<LineEntity>().Where(l => l.Layer == "ARCH-WALL").ToList();

            // Üçgen (3 köşe) → 3 kenar × (alt+üst+dikey) = 9 çizgi.
            // ESKİ DAVRANIŞTA bu her zaman 12 (varsayılan dikdörtgen kutu) olurdu.
            Assert.Equal(9, lines.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_CircleProfile_ExtrudesApproximatedCircleNotDefaultRectangle()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(CircleProfileIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions { ImportWalls = true, ImportSlabs = false, ImportWindows = false, ImportDoors = false });
            Assert.True(result.Success, string.Join("; ", result.Errors));

            var lines = db.GetAllEntities().OfType<LineEntity>().Where(l => l.Layer == "ARCH-WALL").ToList();

            // 16 kenarlı daire yaklaşımı → 16 × 3 = 48 çizgi (eskiden hep 12/dikdörtgen olurdu).
            Assert.Equal(48, lines.Count);

            // Merkezden en uzak nokta yaklaşık 500mm (yarıçap) olmalı.
            double maxDistFromOrigin = lines
                .SelectMany(l => new[] { l.StartPoint, l.EndPoint })
                .Max(pt => Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y));
            Assert.InRange(maxDistFromOrigin, 490, 510);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
