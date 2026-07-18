using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Infrastructure.Import;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC Import — 3D Extrusion Testleri (IfcImportExtrusionTests)
   NEDEN: IfcImportService bu oturumdan önce üç gerçek hata içeriyordu:
          1. IFCEXTRUDEDAREASOLID'in yüksekliği (Depth) parse ediliyordu ama hiç
             kullanılmıyordu — tüm duvarlar/döşemeler Z=0'da düz çiziliyordu.
          2. ExtractDimensions, `rep` parametresini hiç kullanmadan TÜM dosyayı global
             taradığı için birden fazla farklı boyutlu duvar içeren bir dosyada tüm
             duvarlar rastgele aynı (yanlış) boyutu alıyordu.
          3. IFCRECTANGLEPROFILEDEF'in XDim/YDim argüman indeksleri yanlıştı (Position
             bir sayı gibi parse edilmeye çalışılıyordu).
          Bu testler sentetik ama gerçekçi bir IFC STEP dosyasıyla üçünün de
          düzeltildiğini doğruluyor.
*/
public class IfcImportExtrusionTests
{
    // İki farklı boyutlu duvar içeren minimal ama gerçekçi bir IFC STEP dosyası.
    // Wall1: kalınlık 200mm, uzunluk 3000mm, yükseklik 2700mm — origin (0,0,0)
    // Wall2: kalınlık 150mm, uzunluk 4000mm, yükseklik 3500mm — origin (5000,0,0)
    private const string SyntheticIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #20=IFCCARTESIANPOINT(5000.,0.,0.);
        #21=IFCAXIS2PLACEMENT3D(#20,$,$);
        #22=IFCLOCALPLACEMENT($,#21);
        #30=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,200.,3000.);
        #31=IFCCARTESIANPOINT(0.,0.,0.);
        #32=IFCAXIS2PLACEMENT3D(#31,$,$);
        #33=IFCEXTRUDEDAREASOLID(#30,#32,$,2700.);
        #34=IFCSHAPEREPRESENTATION($,$,$,(#33));
        #35=IFCPRODUCTDEFINITIONSHAPE($,$,(#34));
        #40=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,150.,4000.);
        #41=IFCCARTESIANPOINT(0.,0.,0.);
        #42=IFCAXIS2PLACEMENT3D(#41,$,$);
        #43=IFCEXTRUDEDAREASOLID(#40,#42,$,3500.);
        #44=IFCSHAPEREPRESENTATION($,$,$,(#43));
        #45=IFCPRODUCTDEFINITIONSHAPE($,$,(#44));
        #50=IFCWALL('GUID1',$,'Wall1',$,$,#12,#35,$);
        #51=IFCWALL('GUID2',$,'Wall2',$,$,#22,#45,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static string WriteTempIfcFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_test_{System.Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, SyntheticIfc);
        return path;
    }

    [Fact]
    public void Import_WallsGetRealHeightExtrusion_NotFlatAtZZero()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile();

        try
        {
            var result = svc.Import(path, new IfcImportOptions { ImportWalls = true, ImportSlabs = false, ImportWindows = false, ImportDoors = false });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(2, result.WallCount);

            var lines = db.GetAllEntities().OfType<LineEntity>().Where(l => l.Layer == "ARCH-WALL").ToList();

            // Her duvar 12 kenarlı tam bir 3D kutu tel-kafesi üretmeli (2 duvar × 12 = 24).
            Assert.Equal(24, lines.Count);

            // ESKİ DAVRANIŞTA tüm noktalar Z=0'daydı (düz/flat). Artık gerçek yükseklikte
            // (üst döngü + dikey kenarlar) Z>0 noktalar olmalı.
            Assert.Contains(lines, l => l.StartPoint.Z > 0 || l.EndPoint.Z > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_TwoDifferentSizedWalls_GetDifferentDimensions_NotGloballySharedBug()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile();

        try
        {
            svc.Import(path, new IfcImportOptions { ImportWalls = true, ImportSlabs = false, ImportWindows = false, ImportDoors = false });

            var lines = db.GetAllEntities().OfType<LineEntity>().Where(l => l.Layer == "ARCH-WALL").ToList();

            // Wall1'in tel-kafesindeki maksimum Z (yüksekliği) 2700 olmalı.
            var wall1TopZ = lines.Where(l => l.StartPoint.X < 5000 && l.EndPoint.X < 5000)
                                  .SelectMany(l => new[] { l.StartPoint.Z, l.EndPoint.Z }).Max();
            // Wall2'nin (origin X=5000'den başlayan) maksimum Z'si 3500 olmalı.
            var wall2TopZ = lines.Where(l => l.StartPoint.X >= 5000 || l.EndPoint.X >= 5000)
                                  .SelectMany(l => new[] { l.StartPoint.Z, l.EndPoint.Z }).Max();

            Assert.Equal(2700, wall1TopZ, precision: 1);
            Assert.Equal(3500, wall2TopZ, precision: 1);
            Assert.NotEqual(wall1TopZ, wall2TopZ); // ESKİ HATA: ikisi de aynı (rastgele son okunan) değeri alırdı
        }
        finally
        {
            File.Delete(path);
        }
    }
}
