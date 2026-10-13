using System;
using System.IO;
using Afney.Cad.Database.Core;
using Afney.Cad.Infrastructure.Import;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC — Desteklenmeyen Tip Sayımı Testleri (TB-03/AUD-38)
   NEDEN: `IfcImportService.ParseProducts`, `productTypes` kümesi dışındaki HER IFC tipini
          (ör. IFCCOLUMN, IFCBEAM, IFCSTAIR — Revit/ArchiCAD çıktılarında çok yaygın) hiçbir
          uyarı olmadan sessizce atlıyordu — DWG/DXF importundaki AUD-24'ten ÖNCEKİ hatayla
          birebir aynı desen. Bu testler, bilinen-ama-desteklenmeyen tiplerin artık
          `UnsupportedTypeCounts`'ta sayıldığını ve desteklenen elemanların (IFCWALL) hâlâ
          doğru şekilde içeri aktarıldığını doğrular.
*/
public class IfcUnsupportedTypeTests
{
    private static string WriteTempIfcFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_test_{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, content);
        return path;
    }

    private const string WallAndColumnIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #50=IFCWALL('GUID_WALL1',$,'Wall1',$,$,#12,$,$);
        #60=IFCCOLUMN('GUID_COL1',$,'Column1',$,$,#12,$,$);
        #61=IFCBEAM('GUID_BEAM1',$,'Beam1',$,$,#12,$,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    [Fact]
    public void Import_UnsupportedElementTypes_AreCountedNotSilentlyDropped()
    {
        string path = WriteTempIfcFile(WallAndColumnIfc);
        try
        {
            var db = new CadDatabase();
            var svc = new IfcImportService(db);
            var result = svc.Import(path);

            Assert.True(result.Success);
            Assert.Equal(1, result.WallCount); // Desteklenen eleman hâlâ içeri aktarılıyor

            Assert.True(result.UnsupportedTypeCounts.TryGetValue("IFCCOLUMN", out int colCount));
            Assert.Equal(1, colCount);
            Assert.True(result.UnsupportedTypeCounts.TryGetValue("IFCBEAM", out int beamCount));
            Assert.Equal(1, beamCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Import_OnlySupportedTypes_UnsupportedCountsIsEmpty()
    {
        const string wallOnlyIfc = """
            ISO-10303-21;
            HEADER;
            ENDSEC;
            DATA;
            #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
            #10=IFCCARTESIANPOINT(0.,0.,0.);
            #11=IFCAXIS2PLACEMENT3D(#10,$,$);
            #12=IFCLOCALPLACEMENT($,#11);
            #50=IFCWALL('GUID_WALL1',$,'Wall1',$,$,#12,$,$);
            ENDSEC;
            END-ISO-10303-21;
            """;
        string path = WriteTempIfcFile(wallOnlyIfc);
        try
        {
            var db = new CadDatabase();
            var svc = new IfcImportService(db);
            var result = svc.Import(path);

            Assert.True(result.Success);
            Assert.Empty(result.UnsupportedTypeCounts);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
