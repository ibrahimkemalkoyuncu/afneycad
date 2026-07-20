using System.IO;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: DXF DIMENSION Entity Export Testleri
   NEDEN: Önceden DimensionEntity'ler DXF'e sadece düz LINE+TEXT olarak "patlatılarak"
          yazılıyordu — AutoCAD'de gerçek bir DIMENSION nesnesi olarak seçilip
          düzenlenemiyordu. Bu testler, Linear/Aligned tiplerin artık gerçek bir DXF
          DIMENSION entity'si (anonim BLOCK referanslı) olarak yazıldığını; Radius/Angular'ın
          ise dokümante edilmiş kapsam sınırı gereği hâlâ patlatılmış biçimde kaldığını
          doğruluyor.
*/
public class DxfDimensionExportTests
{
    private static string ExportSingleDimension(DimensionEntity dim)
    {
        var db = new CadDatabase();
        db.AddEntity(dim);
        var path = Path.Combine(Path.GetTempPath(), $"afneycad_dim_test_{System.Guid.NewGuid():N}.dxf");
        try
        {
            new DxfWriterService(db).WriteToFile(path);
            return File.ReadAllText(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LinearDimension_ExportsAsRealDimensionEntityWithBlock()
    {
        var dim = new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), new Vector3D(0, 300, 0), DimensionType.Linear);

        string dxf = ExportSingleDimension(dim);

        Assert.Contains("BLOCKS", dxf);
        Assert.Contains("*D1", dxf);
        Assert.Contains("DIMENSION", dxf);
        Assert.Contains("2000", dxf); // ölçü metni içinde uzunluk geçmeli
    }

    [Fact]
    public void AlignedDimension_ExportsAsRealDimensionEntity()
    {
        var dim = new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 1000, 0), new Vector3D(200, 0, 0), DimensionType.Aligned);

        string dxf = ExportSingleDimension(dim);

        Assert.Contains("DIMENSION", dxf);
        Assert.Contains("STANDARD", dxf); // dimstyle referansı (group 3)
        Assert.Contains("*D1", dxf);      // anonim blok referansı (group 2)
    }

    [Fact]
    public void RadiusDimension_StillFallsBackToExplodedLineText()
    {
        // Bilinçli kapsam sınırı: Radius/Angular için gerçek DIMENSION group code'ları
        // (merkez noktası, leader vb.) risk taşıdığından hâlâ eski (LINE+TEXT) yönteme düşüyor.
        var dim = new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(500, 0, 0), new Vector3D(0, 0, 0), DimensionType.Radius);

        string dxf = ExportSingleDimension(dim);

        Assert.DoesNotContain("DIMENSION", dxf);
        Assert.Contains("LINE", dxf);
        Assert.Contains("TEXT", dxf);
    }
}
