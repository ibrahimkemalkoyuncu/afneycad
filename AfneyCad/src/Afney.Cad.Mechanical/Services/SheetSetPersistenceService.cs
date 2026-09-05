using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Pafta Seti Kalıcılık Servisi (SheetSetPersistenceService)
   NEDEN (Session #74): SheetIndexService (pafta/antet numaraları) ve RevisionTrackingService
          (Rev.A/B/C revizyon geçmişi + proje başlık bloğu) daha önce SADECE oturum ömürlüydü —
          uygulama kapanınca veya (RevisionTrackingService özelinde) diyalog her açıldığında
          sıfırlanıyorlardı. Denetimde ayrıca proje dosyası formatının GERÇEK bir DWG (ACadSharp
          R2004+ binary, bkz. DwgExportService) veya DXF R12 ASCII (bkz. DxfWriterService) olduğu
          — yani AutoCAD/BricsCAD gibi üçüncü taraf yazılımların da açabileceği endüstri standardı
          bir interop formatı olduğu doğrulandı. Bu formatların içine, resmi uzantı noktaları
          (DWG'de XRecord/Named Object Dictionary) kullanmadan keyfi bir "sheetset" JSON bölümü
          gömmek format bütünlüğünü ve dış yazılımlarla uyumluluğu riske atar.

   ÇÖZÜM: Proje dosyasının (.dwg/.dxf/.afney) TAM YANINA, aynı ada sahip bir
          "<dosya>.sheetset.json" yardımcı (sidecar) dosyası yazılır/okunur — tıpkı halihazırda
          var olan gizli katman durumu mekanizmasında ("<dosya>.layerstate", bkz.
          MainWindow.FileOps.cs: SaveLayerState/LoadLayerState) olduğu gibi. Bu yaklaşım:
          - Var olan DWG/DXF yazıcı/okuyucu kodunu HİÇ değiştirmez (regresyon riski yok).
          - Eski proje dosyaları (sidecar'sız) sorunsuz açılmaya devam eder — sidecar bulunamazsa
            sessizce boş bir pafta seti ile başlanır.
          - Üçüncü taraf CAD yazılımlarıyla dosya paylaşıldığında sidecar yoksa hiçbir sorun çıkmaz;
            AfneyCAD ile tekrar açıldığında ise pafta/revizyon geçmişi eksiksiz geri gelir.

   KAPSAM: SheetIndexService (pafta indeksi) + RevisionTrackingService (revizyon geçmişi ve
           proje başlık bloğu) tek bir JSON belgesinde birleştirilerek saklanır.
*/
public static class SheetSetPersistenceService
{
    private const string SidecarSuffix = ".sheetset.json";

    /// <summary>Verilen proje dosya yoluna karşılık gelen sidecar dosya yolunu döndürür.</summary>
    public static string GetSidecarPath(string projectFilePath) => projectFilePath + SidecarSuffix;

    private class Envelope
    {
        public int      FormatVersion    { get; set; } = 1;
        public string?  SheetIndexJson   { get; set; }
        public string?  RevisionTrackingJson { get; set; }
        public DateTime SavedAt          { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// SheetIndexService ve RevisionTrackingService durumunu, proje dosyasının yanına bir
    /// sidecar JSON dosyası olarak kaydeder. Kalıcı DWG/DXF dosyasının kendisine DOKUNMAZ.
    /// </summary>
    public static void Save(string projectFilePath, SheetIndexService sheetIndex, RevisionTrackingService revisions)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath)) return;

        var envelope = new Envelope
        {
            SheetIndexJson       = sheetIndex.ToJson(),
            RevisionTrackingJson = revisions.ToJson(),
            SavedAt              = DateTime.Now
        };

        string json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        string sidecarPath = GetSidecarPath(projectFilePath);

        // Atomik yazma: önce geçici dosyaya yaz, sonra taşı — yarım kalmış (bozuk) sidecar riskini önler.
        string tempPath = sidecarPath + ".tmp";
        File.WriteAllText(tempPath, json, Encoding.UTF8);
        File.Copy(tempPath, sidecarPath, overwrite: true);
        File.Delete(tempPath);
    }

    /// <summary>
    /// Proje dosyasının yanındaki sidecar dosyayı bulup SheetIndexService ve
    /// RevisionTrackingService'e yükler. Sidecar yoksa (eski proje dosyası veya hiç pafta/revizyon
    /// üretilmemiş proje) SESSİZCE hiçbir şey yapmaz — dosya açma işlemini asla engellemez.
    /// </summary>
    public static void Load(string projectFilePath, SheetIndexService sheetIndex, RevisionTrackingService revisions)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath)) return;

        string sidecarPath = GetSidecarPath(projectFilePath);
        if (!File.Exists(sidecarPath)) return;

        try
        {
            string json = File.ReadAllText(sidecarPath, Encoding.UTF8);
            var envelope = JsonSerializer.Deserialize<Envelope>(json);
            if (envelope == null) return;

            if (!string.IsNullOrWhiteSpace(envelope.SheetIndexJson))
                sheetIndex.LoadFromJson(envelope.SheetIndexJson);

            if (!string.IsNullOrWhiteSpace(envelope.RevisionTrackingJson))
                revisions.LoadFromJson(envelope.RevisionTrackingJson);
        }
        catch
        {
            // Bozuk sidecar — eski (boş) durumla devam et, dosya açmayı ASLA başarısız kılma.
        }
    }
}
