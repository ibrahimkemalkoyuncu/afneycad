using System.IO;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Kat Yöneticisi Kalıcılık Servisi (LevelPersistenceService)
   NEDEN — Session #75 iş akışı denetiminde bulunan gerçek boşluk: LevelManager'ın kat listesi
          (MepLevel) hiçbir yere kaydedilmiyordu — belge kapatılıp yeniden açıldığında tüm kat
          düzenlemeleri kayboluyor, her seferinde varsayılan 4 kata dönülüyordu. Bu servis
          LayerStatePersistenceService/SheetSetPersistenceService ile AYNI sidecar deseniyle
          ("<dosya>.levels.json") proje dosyasının yanına kaydeder/okur. Sidecar bulunamazsa
          (eski proje) sessizce varsayılan katlarla devam eder — dosya açmayı ASLA engellemez.
*/
public static class LevelPersistenceService
{
    private const string SidecarSuffix = ".levels.json";

    public static string GetSidecarPath(string projectFilePath) => projectFilePath + SidecarSuffix;

    public static void Save(string projectFilePath, LevelManager manager)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath)) return;

        string json = manager.ToJson();
        string sidecarPath = GetSidecarPath(projectFilePath);

        // Atomik yazma: önce geçici dosyaya yaz, sonra taşı — yarım kalmış (bozuk) sidecar riskini önler.
        string tempPath = sidecarPath + ".tmp";
        File.WriteAllText(tempPath, json, Encoding.UTF8);
        File.Copy(tempPath, sidecarPath, overwrite: true);
        File.Delete(tempPath);
    }

    public static void Load(string projectFilePath, LevelManager manager)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath)) return;

        string sidecarPath = GetSidecarPath(projectFilePath);
        if (!File.Exists(sidecarPath)) return;

        try
        {
            string json = File.ReadAllText(sidecarPath, Encoding.UTF8);
            manager.LoadFromJson(json);
        }
        catch
        {
            // Bozuk sidecar — eski (boş/varsayılan) durumla devam et, dosya açmayı ASLA başarısız kılma.
        }
    }
}
