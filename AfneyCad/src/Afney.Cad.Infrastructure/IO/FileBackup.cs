using System;
using System.IO;

namespace Afney.Cad.Infrastructure.IO;

/*
   NE: Kaydetmeden Önce Yedekleme (FileBackup)
   NEDEN: SaveToFile önceden mevcut dosyanın üzerine HİÇBİR yedek almadan doğrudan yazıyordu.
          AtomicFile yazma sırasında bir ÇÖKME olursa dosyanın bozulmasını önler, ama uygulama
          hafızasındaki içerik yanlış/boş olduğunda (ör. bir kurtarma akışı yanlışlıkla
          neredeyse-boş bir proje yükleyip ardından Kaydet'e basılırsa) BAŞARILI bir yazma bile
          gerçek içeriği kalıcı olarak yok edebiliyordu — gerçek bir olayda tam olarak bu
          yaşandı (1.3MB'lık bir mimari proje 8KB'a düştü). Artık kaydetmeden ÖNCE mevcut dosya
          AutoCAD'in kendi ".bak" kuralıyla (aynı ad, .bak uzantısı) yedekleniyor.
*/
public static class FileBackup
{
    /// <summary>
    /// Hedef dosya zaten varsa, üzerine yazılmadan önce ".bak" uzantısıyla yedeğini alır.
    /// Yedekleme başarısız olursa exception fırlatmaz, sadece false döner — kaydetme
    /// akışının geçici bir disk/izin sorunu yüzünden tamamen engellenmemesi için.
    /// </summary>
    public static bool BackupBeforeOverwrite(string filePath, out Exception? error)
    {
        error = null;
        try
        {
            if (!File.Exists(filePath)) return false; // Yeni dosya — yedeklenecek önceki içerik yok
            string backupPath = Path.ChangeExtension(filePath, ".bak");
            File.Copy(filePath, backupPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }
}
