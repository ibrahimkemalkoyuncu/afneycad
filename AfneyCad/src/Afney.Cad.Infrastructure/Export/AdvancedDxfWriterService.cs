using ACadSharp.IO;
using Afney.Cad.Database.Core;

namespace Afney.Cad.Infrastructure.Export;

/// <summary>
/// Gelişmiş DXF (R2018/AC1032) Writer.
/// NOT: Önceden elle (StringBuilder ile) DXF metni üreten deneysel bir implementasyondu;
/// BLOCK_RECORD tablosu eksikliği gibi DXF R13+ spec ihlalleri içeriyordu ve gerçek
/// AutoCAD'de doğrulanmamıştı. Artık DwgExportService ile aynı, gerçek AutoCAD 2026'da
/// round-trip testle doğrulanmış AcadSharpDocumentBuilder + ACadSharp.IO.DxfWriter
/// altyapısını kullanıyor — DWG'de düzeltilen her entity-dönüşüm hatası (örn. boru
/// genişliği) otomatik olarak bu çıktıya da yansır.
/// </summary>
public class AdvancedDxfWriterService
{
    private readonly CadDatabase _database;

    public AdvancedDxfWriterService(CadDatabase database) => _database = database;

    public void WriteToFile(string filePath)
    {
        var doc = AcadSharpDocumentBuilder.Build(_database);
        using var writer = new DxfWriter(filePath, doc, false); // false = ASCII DXF
        writer.Write();
    }
}
