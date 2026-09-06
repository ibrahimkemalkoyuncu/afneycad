using System;
using System.IO;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Katman Durumu Kalıcılık Servisi (LayerStatePersistenceService)
   NEDEN: LayerStateManagerService'in adlandırılmış state listesini, SheetSetPersistenceService
          ile AYNI sidecar deseniyle ("<dosya>.layerstates.json") proje dosyasının yanına
          kaydeder/okur. Gerçek DWG/DXF formatına dokunmaz, sidecar bulunamazsa (eski proje veya
          hiç isimlendirilmiş state kaydedilmemiş proje) sessizce boş bir yöneticiyle devam eder.
   NOT: Eski, isimsiz "<dosya>.layerstate" mekanizması (MainWindow.FileOps.cs:
        SaveLayerState/LoadLayerState — sadece o anki gizli katman listesini oturum sürekliliği
        için tutar) DEĞİŞTİRİLMEDİ, bu YENİ ve AYRI bir özellik (isimlendirilmiş, çoklu, geri
        çağrılabilir state'ler) — ikisi birbirinden bağımsız çalışır.
*/
public static class LayerStatePersistenceService
{
    private const string SidecarSuffix = ".layerstates.json";

    public static string GetSidecarPath(string projectFilePath) => projectFilePath + SidecarSuffix;

    public static void Save(string projectFilePath, LayerStateManagerService manager)
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

    public static void Load(string projectFilePath, LayerStateManagerService manager)
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
            // Bozuk sidecar — eski (boş) durumla devam et, dosya açmayı ASLA başarısız kılma.
        }
    }
}
