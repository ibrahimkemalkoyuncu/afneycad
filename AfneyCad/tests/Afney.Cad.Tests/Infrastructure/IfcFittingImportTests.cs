using System;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC — Bağlantı Elemanı (Dirsek/T-Parçası/Vana) İçeri Aktarma Testleri (IfcFittingImportTests)
   NEDEN: Bu oturumdan önce IfcImportService, IFCFLOWFITTING/IFCPIPEFITTING/IFCDUCTFITTING/
          IFCVALVE'yi HİÇ ele almıyordu — sadece düz boru/kanal gövdeleri (IFCPIPESEGMENT/
          IFCDUCTSEGMENT) içeri aktarılıyordu (kod içinde dosya başı SINIRLAMALAR notuyla
          itiraf edilmişti). Bu testler:
          1. Gerçekçi (Revit/ArchiCAD tarzı) IFCPIPEFITTING(.BEND.) / IFCDUCTFITTING(.TEE.) /
             IFCVALVE(.CHECK.) STEP parçalarının doğru ElbowEntity/TeeEntity/ValveEntity
             ürettiğini doğrular.
          2. AfneyCAD'in KENDİ IfcExportService'inin ürettiği ElbowEntity/TeeEntity IFC
             çıktısının (ObjectPlacement'ı standart IFCLOCALPLACEMENT sarmalayıcısı OLMADAN
             doğrudan bir IFCAXIS2PLACEMENT3D'ye referans veren, standart-dışı ama kendi
             round-trip'i için gerekli biçimi) geri okunabildiğini (round-trip) doğrular.
   KAPSAM DIŞI (bilinçli): Port-bağlantılı (IFCRELCONNECTSPORTS) karmaşık fitting senaryoları,
          CROSS/REDUCER PredefinedType'ları, IFCPUMP — bkz. IfcImportService dosya başı notu.
*/
public class IfcFittingImportTests
{
    private static string WriteTempIfcFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_test_{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, content);
        return path;
    }

    // 100mm çaplı (50mm yarıçap) bir dirsek (.BEND.), (500,300,0)'da, Y ekseninde (RefDirection)
    // yönlenmiş, standart IFCLOCALPLACEMENT üzerinden konumlanmış — gerçek bir Revit/ArchiCAD
    // IFC dosyasının IFCPIPEFITTING temsiline benzer.
    private const string PipeFittingBendIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(500.,300.,0.);
        #11=IFCDIRECTION(0.,1.,0.);
        #12=IFCAXIS2PLACEMENT3D(#10,$,#11);
        #13=IFCLOCALPLACEMENT($,#12);
        #20=IFCCARTESIANPOINT(0.,0.,0.);
        #21=IFCAXIS2PLACEMENT3D(#20,$,$);
        #22=IFCCIRCLEPROFILEDEF(.AREA.,$,#21,50.);
        #23=IFCEXTRUDEDAREASOLID(#22,#21,$,100.);
        #24=IFCSHAPEREPRESENTATION($,'Body',$,(#23));
        #25=IFCPRODUCTDEFINITIONSHAPE($,$,(#24));
        #50=IFCPIPEFITTING('GUID_ELBOW1',$,'Elbow1',$,$,#13,#25,$,.BEND.);
        ENDSEC;
        END-ISO-10303-21;
        """;

    // Kare (200x200mm) kesitli bir T-parçası (.TEE.) — IFCDUCTFITTING.
    private const string DuctFittingTeeIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #20=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,200.,200.);
        #21=IFCCARTESIANPOINT(0.,0.,0.);
        #22=IFCAXIS2PLACEMENT3D(#21,$,$);
        #23=IFCEXTRUDEDAREASOLID(#20,#22,$,200.);
        #24=IFCSHAPEREPRESENTATION($,'Body',$,(#23));
        #25=IFCPRODUCTDEFINITIONSHAPE($,$,(#24));
        #50=IFCDUCTFITTING('GUID_TEE1',$,'Tee1',$,$,#12,#25,$,.TEE.);
        ENDSEC;
        END-ISO-10303-21;
        """;

    // 80mm çaplı (40mm yarıçap) bir çek valf (.CHECK.).
    private const string CheckValveIfc = """
        ISO-10303-21;
        HEADER;
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.MILLI.);
        #10=IFCCARTESIANPOINT(0.,0.,0.);
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #20=IFCCARTESIANPOINT(0.,0.,0.);
        #21=IFCAXIS2PLACEMENT3D(#20,$,$);
        #22=IFCCIRCLEPROFILEDEF(.AREA.,$,#21,40.);
        #23=IFCEXTRUDEDAREASOLID(#22,#21,$,50.);
        #24=IFCSHAPEREPRESENTATION($,'Body',$,(#23));
        #25=IFCPRODUCTDEFINITIONSHAPE($,$,(#24));
        #50=IFCVALVE('GUID_VALVE1',$,'Valve1',$,$,#12,#25,$,.CHECK.);
        ENDSEC;
        END-ISO-10303-21;
        """;

    [Fact]
    public void Import_PipeFittingBend_CreatesElbowEntity_AtCorrectPositionAndDiameter()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(PipeFittingBendIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = true
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.FittingCount);

            var elbows = db.GetAllEntities().OfType<ElbowEntity>().ToList();
            Assert.Single(elbows);

            var elbow = elbows[0];
            Assert.Equal(500, elbow.Center.X, precision: 0);
            Assert.Equal(300, elbow.Center.Y, precision: 0);
            Assert.Equal(100, elbow.InnerDiameter, precision: 1); // 50mm yarıçap × 2
            Assert.Equal("MEP-IMPORT", elbow.Layer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_DuctFittingTee_CreatesTeeEntity()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(DuctFittingTeeIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = true
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.FittingCount);

            var tees = db.GetAllEntities().OfType<TeeEntity>().ToList();
            Assert.Single(tees);
            Assert.True(tees[0].MainDiameter > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_Valve_CreatesValveEntity_WithMappedValveTypeAndDiameter()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(CheckValveIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = true
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.FittingCount);

            var valves = db.GetAllEntities().OfType<ValveEntity>().ToList();
            Assert.Single(valves);

            var valve = valves[0];
            Assert.Equal(ValveType.CheckValve, valve.ValveType);
            Assert.Equal(80, valve.InnerDiameter, precision: 1); // 40mm yarıçap × 2
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_WithImportMepFalse_SkipsFittings_FittingCountStaysZero()
    {
        var db = new CadDatabase();
        var svc = new IfcImportService(db);
        string path = WriteTempIfcFile(PipeFittingBendIfc);

        try
        {
            var result = svc.Import(path, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = false
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(0, result.FittingCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.Empty(db.GetAllEntities().OfType<ElbowEntity>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTrip_ExportThenImport_ElbowAndTee_AreRecoveredWithCorrectTypeAndCenter()
    {
        // NE/NEDEN: AfneyCAD'in kendi IfcExportService'i (ExportElbow/ExportTee) ile üretilen
        // dosyanın, aynı AfneyCAD'in IfcImportService'i tarafından geri okunabildiğini
        // doğrular — özellikle ObjectPlacement'ın standart-dışı (doğrudan IFCAXIS2PLACEMENT3D
        // referanslı, IFCLOCALPLACEMENT sarmalayıcısız) biçiminin fallback ile ele alındığını.
        var elbow = new ElbowEntity(new Vector3D(1000, 2000, 0), 100, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
        var tee = new TeeEntity(new Vector3D(3000, 4000, 0), 100, 100, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));

        var exportSvc = new IfcExportService();
        string ifcPath = Path.Combine(Path.GetTempPath(), $"afneycad_export_test_{Guid.NewGuid():N}.ifc");

        try
        {
            exportSvc.ExportToIfc(new CadEntity[] { elbow, tee }, ifcPath);

            var db = new CadDatabase();
            var importSvc = new IfcImportService(db);
            var result = importSvc.Import(ifcPath, new IfcImportOptions
            {
                ImportWalls = false, ImportSlabs = false, ImportWindows = false,
                ImportDoors = false, ImportSpaces = false, ImportMep = true
            });

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(2, result.FittingCount);

            var importedElbows = db.GetAllEntities().OfType<ElbowEntity>().ToList();
            var importedTees = db.GetAllEntities().OfType<TeeEntity>().ToList();
            Assert.Single(importedElbows);
            Assert.Single(importedTees);

            // Dirsek geometrisi export'ta tam bir kare kutu (kenar=çap×2) olarak yazıldığı için
            // çap tam geri kazanılabilir. T-parçası kutusu (kenar=çap×3) yaklaşık bir değer
            // üretir (bkz. ExtractFittingGeometry NEDEN notu) — bu yüzden sadece pozitif olduğu
            // doğrulanıyor, tam eşitlik değil.
            Assert.Equal(1000, importedElbows[0].Center.X, precision: 0);
            Assert.Equal(2000, importedElbows[0].Center.Y, precision: 0);
            Assert.Equal(100, importedElbows[0].InnerDiameter, precision: 1);

            Assert.Equal(3000, importedTees[0].Center.X, precision: 0);
            Assert.Equal(4000, importedTees[0].Center.Y, precision: 0);
            Assert.True(importedTees[0].MainDiameter > 0);
        }
        finally
        {
            if (File.Exists(ifcPath)) File.Delete(ifcPath);
        }
    }
}
