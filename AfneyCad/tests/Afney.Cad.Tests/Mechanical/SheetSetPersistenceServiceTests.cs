using System;
using System.IO;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Pafta Seti Kalıcılık Servisi Test Birimi (SheetSetPersistenceServiceTests)
   NEDEN (Session #74): SheetIndexService ve RevisionTrackingService artık proje dosyasının
          yanına bir sidecar JSON dosyası ("<dosya>.sheetset.json") olarak kalıcı kaydediliyor.
          Bu testler:
          1. Kaydet → Yükle round-trip'inin pafta ve revizyon verisini kaybetmeden geri getirdiğini,
          2. Sidecar dosyası HİÇ olmayan (eski proje dosyası) durumda yükleme işleminin
             sessizce (hatasız) boş durumla devam ettiğini — yani ESKİ proje dosyalarının hâlâ
             sorunsuz açılabildiğini,
          3. Bozuk/geçersiz bir sidecar dosyasının da uygulamayı çökertmediğini
          doğrular.
*/
public class SheetSetPersistenceServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SheetSetPersistenceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AfneyCadTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort cleanup */ }
    }

    [Fact]
    public void Save_ThenLoad_RestoresSheetIndexAndRevisions()
    {
        string projectPath = Path.Combine(_tempDir, "Proje1.dwg");

        var sheetIndex = new SheetIndexService();
        sheetIndex.RegisterSheet(null, "Zemin Kat Tesisat", "Proje 1");

        var revisions = new RevisionTrackingService();
        revisions.TitleBlock.ProjectName = "Proje 1";
        revisions.AddRevision("İlk Yayın", "Kemal", "İlk çizim seti", RevisionTrackingService.RevisionStatus.Yayınlandı);

        SheetSetPersistenceService.Save(projectPath, sheetIndex, revisions);

        Assert.True(File.Exists(SheetSetPersistenceService.GetSidecarPath(projectPath)));

        // Taze örneklere yükle — proje yeniden açılmış gibi davran.
        var loadedSheetIndex = new SheetIndexService();
        var loadedRevisions  = new RevisionTrackingService();
        SheetSetPersistenceService.Load(projectPath, loadedSheetIndex, loadedRevisions);

        Assert.Single(loadedSheetIndex.Sheets);
        Assert.Equal("M-01", loadedSheetIndex.Sheets[0].Number);
        Assert.Equal("Zemin Kat Tesisat", loadedSheetIndex.Sheets[0].Name);

        Assert.Single(loadedRevisions.Revisions);
        Assert.Equal("A", loadedRevisions.Revisions[0].RevCode);
        Assert.Equal("İlk Yayın", loadedRevisions.Revisions[0].Description);
        Assert.Equal("Proje 1", loadedRevisions.TitleBlock.ProjectName);
    }

    [Fact]
    public void Load_NoSidecarFile_OldProjectFile_LeavesEmptyStateWithoutThrowing()
    {
        // ESKİ proje dosyası senaryosu: sidecar hiç üretilmemiş.
        string projectPath = Path.Combine(_tempDir, "EskiProje.dwg");
        File.WriteAllText(projectPath, "0\nSECTION\n0\nEOF\n"); // Sahte DXF-benzeri içerik yeterli

        var sheetIndex = new SheetIndexService();
        var revisions  = new RevisionTrackingService();

        var ex = Record.Exception(() => SheetSetPersistenceService.Load(projectPath, sheetIndex, revisions));

        Assert.Null(ex);
        Assert.Empty(sheetIndex.Sheets);
        Assert.Empty(revisions.Revisions);
    }

    [Fact]
    public void Load_CorruptSidecarFile_DoesNotThrowAndLeavesEmptyState()
    {
        string projectPath = Path.Combine(_tempDir, "BozukProje.dwg");
        File.WriteAllText(SheetSetPersistenceService.GetSidecarPath(projectPath), "{ bu gecerli bir json degil ");

        var sheetIndex = new SheetIndexService();
        var revisions  = new RevisionTrackingService();

        var ex = Record.Exception(() => SheetSetPersistenceService.Load(projectPath, sheetIndex, revisions));

        Assert.Null(ex);
        Assert.Empty(sheetIndex.Sheets);
        Assert.Empty(revisions.Revisions);
    }

    [Fact]
    public void Save_OverwritesPreviousSidecar_NoStaleData()
    {
        string projectPath = Path.Combine(_tempDir, "Proje2.dwg");

        var sheetIndex = new SheetIndexService();
        var revisions  = new RevisionTrackingService();
        sheetIndex.RegisterSheet(null, "İlk Hal", "Proje 2");
        SheetSetPersistenceService.Save(projectPath, sheetIndex, revisions);

        // İkinci kaydetmede önceki pafta silinip yeni bir tane eklendiyse sidecar bunu yansıtmalı.
        sheetIndex.Clear();
        sheetIndex.RegisterSheet(null, "Guncel Hal", "Proje 2");
        SheetSetPersistenceService.Save(projectPath, sheetIndex, revisions);

        var loaded = new SheetIndexService();
        var loadedRevisions = new RevisionTrackingService();
        SheetSetPersistenceService.Load(projectPath, loaded, loadedRevisions);

        Assert.Single(loaded.Sheets);
        Assert.Equal("Guncel Hal", loaded.Sheets[0].Name);
    }
}
