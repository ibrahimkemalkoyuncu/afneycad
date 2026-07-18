using System.IO;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Mechanical.Entities;
using DocumentFormat.OpenXml.Packaging;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: WordExportService Testleri (WordExportServiceTests)
   NEDEN: Raporlama kategorisinde 4M FineSANI'nin Word/Excel/PDF üçlü çıktı setinden Word
          eksikti — AfneyCAD'de sadece Excel ve PDF vardı. Bu testler, üretilen .docx
          dosyasının gerçekten açılabilir (geçerli OOXML) olduğunu ve proje verisini
          (boru/armatür metrajı) gerçekten içerdiğini doğruluyor.
*/
public class WordExportServiceTests
{
    private static string TempDocxPath() =>
        Path.Combine(Path.GetTempPath(), $"afneycad_word_test_{System.Guid.NewGuid():N}.docx");

    [Fact]
    public void WriteToFile_ProducesValidOpenableDocx()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25));

        var svc = new WordExportService(db);
        string path = TempDocxPath();

        try
        {
            svc.WriteToFile(path, projectName: "Test Projesi", engineer: "Test Mühendis");

            Assert.True(File.Exists(path));

            using var doc = WordprocessingDocument.Open(path, false);
            Assert.NotNull(doc.MainDocumentPart);
            Assert.NotNull(doc.MainDocumentPart!.Document.Body);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WriteToFile_ContainsProjectNameAndPipeData()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), 50)
        {
            SystemType = Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticColdWater
        });

        var svc = new WordExportService(db);
        string path = TempDocxPath();

        try
        {
            svc.WriteToFile(path, projectName: "Benzersiz Proje Adı XYZ", engineer: "İbrahim");

            using var doc = WordprocessingDocument.Open(path, false);
            string bodyText = doc.MainDocumentPart!.Document.Body!.InnerText;

            Assert.Contains("Benzersiz Proje Adı XYZ", bodyText);
            Assert.Contains("Temiz Soğuk Su", bodyText);
            Assert.Contains("Malzeme Metrajı", bodyText);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WriteToFile_EmptyDatabase_StillProducesValidDocx()
    {
        var db = new CadDatabase();
        var svc = new WordExportService(db);
        string path = TempDocxPath();

        try
        {
            svc.WriteToFile(path);

            Assert.True(File.Exists(path));
            using var doc = WordprocessingDocument.Open(path, false);
            Assert.NotNull(doc.MainDocumentPart);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
