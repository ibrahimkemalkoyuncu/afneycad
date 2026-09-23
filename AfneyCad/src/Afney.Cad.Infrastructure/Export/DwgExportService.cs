using ACadSharp.IO;
using Afney.Cad.Database.Core;

namespace Afney.Cad.Infrastructure.Export;

/// <summary>
/// AfneyCAD veritabanını ACadSharp aracılığıyla gerçek DWG (R2004+) formatında dışa aktarır.
/// </summary>
public class DwgExportService
{
    private readonly CadDatabase _database;

    public DwgExportService(CadDatabase database) => _database = database;

    /*
       NE: DWG Dosyasına Yaz — Atlanan Entity Tiplerini Döndürür
       NEDEN: AcadSharpDocumentBuilder yalnızca 6 entity tipini destekliyor; geri kalanı
              (Spline/Hatch/Dimension/Solid/MEP fitting'leri) sessizce dışlanıyordu. Dönüş
              değeri artık çağırana (UI katmanı) hangi tiplerin kaç adet dışlandığını
              bildiriyor, böylece kullanıcı export sonrası veri kaybından haberdar olabilir.
    */
    public IReadOnlyList<string> WriteToFile(string filePath)
    {
        var doc = AcadSharpDocumentBuilder.Build(_database, out var skippedEntities);
        using var writer = new DwgWriter(filePath, doc);
        writer.Write();
        return skippedEntities;
    }
}
