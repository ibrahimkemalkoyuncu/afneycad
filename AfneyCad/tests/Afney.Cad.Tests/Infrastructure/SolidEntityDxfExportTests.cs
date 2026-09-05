using System;
using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.Import;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: SolidEntity DXF Round-Trip Testleri
   NEDEN: Denetim raporu bulgusu — SolidEntity (CSG Boolean UNION/SUBTRACT/INTERSECT sonuçları)
          DXF export'a hiç bağlı değildi, kaydedip yeniden açınca solid'ler sessizce kayboluyordu.
          Bu testler DxfWriterService.WriteSolid (Solid → 3DFACE üçgen listesi) + DxfImportService
          (3DFACE'leri Layer+Color'a göre gruplayıp BRepBuilder.FromTriangleSoup ile Solid'e
          geri kaynaştırma) zincirinin GERÇEKTEN çalıştığını — export→import sonrası hacim/
          vertex/bounding box'ın (yaklaşık) korunduğunu kanıtlar.

   KAPSAM SINIRI (bilinçli, testte de doğrulanıyor): DXF R12'de ayrı 3DFACE'leri TEK bir Solid'e
   geri gruplamanın standart bir yolu yok (POLYFACE MESH R12'de ACadSharp tarafından okunamıyor,
   XDATA/APPID tabanlı gruplama bu okuyucuda güvenilir değil — ikisi de elle doğrulandı). Bu
   yüzden import, aynı (Layer, Color) ikilisini paylaşan TÜM 3DFACE'leri TEK bir SolidEntity'ye
   kaynaştırır — bir testte bu sınır (iki farklı solid AYNI layer/renkte ise birleşir) de
   doğrulanıyor.
*/
public class SolidEntityDxfExportTests
{
    private static string ExportAndReturnPath(CadDatabase db)
    {
        var path = Path.Combine(Path.GetTempPath(), $"afneycad_solid_test_{Guid.NewGuid():N}.dxf");
        new DxfWriterService(db).WriteToFile(path);
        return path;
    }

    [Fact]
    public void SolidEntity_DxfRoundTrip_PreservesBoundingBoxAndVolume()
    {
        var db = new CadDatabase();
        var boxSolid = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 1500, 3000);
        var solidEntity = new SolidEntity(boxSolid) { Layer = "0", Color = 0xFFFF0000 };
        db.AddEntity(solidEntity);

        var path = ExportAndReturnPath(db);
        try
        {
            var dxfText = File.ReadAllText(path);
            Assert.Contains("3DFACE", dxfText);

            var imported = new DxfImportService().ImportDxf(path);
            var importedSolids = imported.OfType<SolidEntity>().ToList();

            Assert.Single(importedSolids);
            var rebuilt = importedSolids[0].Solid;
            Assert.True(rebuilt.IsValid(), "İçeri aktarılan Solid Euler açısından geçersiz.");

            double expectedVolume = 2000.0 * 1500.0 * 3000.0;
            double relativeError = Math.Abs(rebuilt.GetVolume() - expectedVolume) / expectedVolume;
            Assert.True(relativeError < 1e-3, $"Hacim sapması çok yüksek: {relativeError}");

            var (min, max) = rebuilt.GetBoundingBox();
            Assert.Equal(0.0, min.X, precision: 1);
            Assert.Equal(2000.0, max.X, precision: 1);
            Assert.Equal(1500.0, max.Y, precision: 1);
            Assert.Equal(3000.0, max.Z, precision: 1);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SolidEntity_DxfRoundTrip_TwoSolidsOnDifferentLayers_ImportSeparately()
    {
        var db = new CadDatabase();
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(5000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        db.AddEntity(new SolidEntity(boxA) { Layer = "SOLID-A", Color = 0xFF00FF00 });
        db.AddEntity(new SolidEntity(boxB) { Layer = "SOLID-B", Color = 0xFF0000FF });

        var path = ExportAndReturnPath(db);
        try
        {
            var imported = new DxfImportService().ImportDxf(path);
            var importedSolids = imported.OfType<SolidEntity>().ToList();

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

    [Fact]
    public void SolidEntity_DxfRoundTrip_TwoSolidsOnSameLayerAndColor_MergeOnImport()
    {
        // NE/NEDEN: DXF R12 kapsam sınırının (bkz. sınıf başı not) kendisi — aynı katman+renkte
        // iki ayrı Solid, içeri aktarımda TEK bir SolidEntity'ye birleşir. Bu, bilinçli bir
        // sınır olduğu için burada AÇIKÇA kanıtlanıyor (sürpriz regresyon değil).
        var db = new CadDatabase();
        var boxA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        var boxB = BRepBuilder.ExtrudeBox(new Vector3D(5000, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1000, 1000, 1000);
        db.AddEntity(new SolidEntity(boxA) { Layer = "0", Color = 0xFFFFFFFF });
        db.AddEntity(new SolidEntity(boxB) { Layer = "0", Color = 0xFFFFFFFF });

        var path = ExportAndReturnPath(db);
        try
        {
            var imported = new DxfImportService().ImportDxf(path);
            var importedSolids = imported.OfType<SolidEntity>().ToList();

            Assert.Single(importedSolids); // Bilinçli sınır: aynı layer+color → tek Solid'e birleşir.
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
