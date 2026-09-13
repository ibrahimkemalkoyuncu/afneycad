using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: BcfExportService Testleri
   NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: ClashReportDialog sadece CSV
          dışa aktarabiliyordu, BCF (buildingSMART standardı) yoktu. Bu testler üretilen ZIP'in
          gerçekten geçerli bir BCF 2.1 iskeleti (kök bcf.version + konu başına markup.bcf/
          viewpoint.bcfv) taşıdığını ve her çakışma için ayrı bir konu oluşturulduğunu kilitler.
*/
public class BcfExportServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"afneycad_bcf_test_{Guid.NewGuid():N}.bcf");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static ClashResult MakeClash(ClashSeverity severity = ClashSeverity.Warning) => new()
    {
        Type = ClashType.MechanicalVsMechanical,
        EntityA_Id = Guid.NewGuid(),
        EntityB_Id = Guid.NewGuid(),
        Position = new Vector3D(1000, 2000, 500),
        Severity = severity,
        Message = "Test çakışması"
    };

    [Fact]
    public void Export_ProducesValidZipWithVersionFile()
    {
        BcfExportService.Export(new[] { MakeClash() }, _path);

        using var zip = ZipFile.OpenRead(_path);
        var versionEntry = zip.GetEntry("bcf.version");

        Assert.NotNull(versionEntry);
    }

    [Fact]
    public void Export_OneTopicFolderPerClash_WithMarkupAndViewpoint()
    {
        var clashes = new List<ClashResult> { MakeClash(), MakeClash(ClashSeverity.Critical) };
        BcfExportService.Export(clashes, _path);

        using var zip = ZipFile.OpenRead(_path);

        foreach (var clash in clashes)
        {
            string folder = clash.Id.ToString() + "/";
            Assert.NotNull(zip.GetEntry(folder + "markup.bcf"));
            Assert.NotNull(zip.GetEntry(folder + "viewpoint.bcfv"));
        }
    }

    [Fact]
    public void Export_MarkupContainsSeverityAsPriorityAndMessageAsTitle()
    {
        var clash = MakeClash(ClashSeverity.Critical);
        BcfExportService.Export(new[] { clash }, _path);

        using var zip = ZipFile.OpenRead(_path);
        var entry = zip.GetEntry(clash.Id + "/markup.bcf")!;
        using var reader = new StreamReader(entry.Open());
        string content = reader.ReadToEnd();

        Assert.Contains("Critical", content);
        Assert.Contains("Test çakışması", content);
        Assert.Contains(clash.Id.ToString(), content);
    }

    [Fact]
    public void Export_OverwritesExistingFile()
    {
        BcfExportService.Export(new[] { MakeClash() }, _path);
        BcfExportService.Export(new[] { MakeClash(), MakeClash() }, _path);

        using var zip = ZipFile.OpenRead(_path);
        // bcf.version + 2 konu * 2 dosya = 5 entry
        Assert.Equal(5, zip.Entries.Count);
    }
}
