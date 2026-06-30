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

    public void WriteToFile(string filePath)
    {
        var doc = AcadSharpDocumentBuilder.Build(_database);
        using var writer = new DwgWriter(filePath, doc);
        writer.Write();
    }
}
