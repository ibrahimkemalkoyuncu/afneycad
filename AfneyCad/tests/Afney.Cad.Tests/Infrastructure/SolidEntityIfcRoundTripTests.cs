using System;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: SolidEntity IFC Round-Trip Testleri
   NEDEN: Denetim raporu bulgusu — SolidEntity (CSG Boolean UNION/SUBTRACT/INTERSECT sonuçları)
          IFC export'a hiç bağlı değildi. Bu testler IfcExportService.ExportSolid (Solid →
          IFCPOLYGONALFACESET tessellation, IFCBUILDINGELEMENTPROXY olarak) + IfcImportService
          (ExtractTessellationGeometry + BRepBuilder.FromTriangleSoup) zincirinin GERÇEKTEN
          çalıştığını kanıtlar.

   DXF'TEN FARKI: Her SolidEntity kendi AYRI IFC ürünüdür (IFCBUILDINGELEMENTPROXY) — DXF'teki
   gibi çapraz-entity Layer/Color gruplama heuristiğine GEREK YOK, birden fazla Solid aynı
   dosyada birbirinden EKSİKSİZ ayrışır (bkz. Test #2 — DXF'in aksine AYNI katman/renkte bile
   olsalar ayrı kalırlar).

   ÖNCEKI OTURUMDAN DERS (kullanıcı talimatı): IFC round-trip'te daha önce ciddi bir koordinat
   parse hatası bulunmuştu (UnwrapCoordList — çift parantezli IFCCARTESIANPOINT/IFCDIRECTION
   biçimi). Bu testler koordinatların GERÇEKTEN doğru okunduğunu (sadece "bir şeyler import
   edildi" değil, bounding box/hacim/vertex sayısının TAM eşleştiğini) sayısal olarak doğrular.
*/
public class SolidEntityIfcRoundTripTests
{
    [Fact]
    public void SolidEntity_IfcRoundTrip_PreservesVolumeAndBoundingBox()
    {
        var boxSolid = BRepBuilder.ExtrudeBox(new Vector3D(1000, 2000, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 1500, 3000, "TestBox");
        var solidEntity = new SolidEntity(boxSolid) { Layer = "0", Color = 0xFFFF0000 };

        var exportSvc = new IfcExportService();
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcsolid_test_{Guid.NewGuid():N}.ifc");

        try
        {
            exportSvc.ExportToIfc(new CadEntity[] { solidEntity }, path);
            string content = File.ReadAllText(path);

            Assert.Contains("IFCBUILDINGELEMENTPROXY", content);
            Assert.Contains("IFCPOLYGONALFACESET", content);
            Assert.Contains("IFCCARTESIANPOINTLIST3D", content);

            var importDb = new CadDatabase();
            var importSvc = new IfcImportService(importDb);
            var result = importSvc.Import(path);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(1, result.SolidCount);

            var importedSolids = importDb.GetAllEntities().OfType<SolidEntity>().ToList();
            Assert.Single(importedSolids);

            var rebuilt = importedSolids[0].Solid;
            Assert.True(rebuilt.IsValid(), "İçeri aktarılan Solid Euler açısından geçersiz.");
            Assert.Equal(8, rebuilt.GetVertices().Count());

            double expectedVolume = 2000.0 * 1500.0 * 3000.0;
            double relativeError = Math.Abs(rebuilt.GetVolume() - expectedVolume) / expectedVolume;
            Assert.True(relativeError < 1e-3, $"Hacim sapması çok yüksek: {relativeError}");

            var (min, max) = rebuilt.GetBoundingBox();
            Assert.Equal(1000.0, min.X, precision: 1);
            Assert.Equal(2000.0, min.Y, precision: 1);
            Assert.Equal(0.0, min.Z, precision: 1);
            Assert.Equal(3000.0, max.X, precision: 1);
            Assert.Equal(3500.0, max.Y, precision: 1);
            Assert.Equal(3000.0, max.Z, precision: 1);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SolidEntity_IfcRoundTrip_MultipleSolidsOnSameLayerAndColor_StayFullySeparate()
    {
        // NE/NEDEN: DXF'in AKSİNE (bkz. SolidEntityDxfExportTests — aynı layer/renk birleşir),
        // IFC'de her Solid kendi AYRI IFCBUILDINGELEMENTPROXY ürünüdür — Layer/Color import
        // gruplamasına HİÇ dayanmaz, bu yüzden aynı katman/renkte bile olsalar tam ayrışır.
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(5000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var entityA = new SolidEntity(boxA) { Layer = "0", Color = 0xFFFFFFFF };
        var entityB = new SolidEntity(boxB) { Layer = "0", Color = 0xFFFFFFFF };

        var exportSvc = new IfcExportService();
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcsolid_multi_test_{Guid.NewGuid():N}.ifc");

        try
        {
            exportSvc.ExportToIfc(new CadEntity[] { entityA, entityB }, path);

            var importDb = new CadDatabase();
            var importSvc = new IfcImportService(importDb);
            var result = importSvc.Import(path);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(2, result.SolidCount);

            var importedSolids = importDb.GetAllEntities().OfType<SolidEntity>().ToList();
            Assert.Equal(2, importedSolids.Count);

            var volumes = importedSolids.Select(s => s.Solid.GetVolume()).OrderBy(v => v).ToList();
            Assert.True(Math.Abs(volumes[0] - 1_000_000_000.0) / 1_000_000_000.0 < 1e-3);
            Assert.True(Math.Abs(volumes[1] - 8_000_000_000.0) / 8_000_000_000.0 < 1e-3);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
