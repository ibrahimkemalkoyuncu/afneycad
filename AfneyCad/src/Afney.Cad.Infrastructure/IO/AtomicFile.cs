using System;
using System.IO;
using System.Text;

namespace Afney.Cad.Infrastructure.IO;

/*
   NE: Atomik Dosya Yazma Yardımcısı (AtomicFile)
   NEDEN: DXF/DWG/IFC/Excel/Word export servislerinin HİÇBİRİ temp-dosya+rename deseni
          kullanmıyordu — her biri doğrudan hedef yola yazıyordu. Yazma sırasında bir çökme,
          disk dolması veya izin hatası hem yeni içeriği YARIM/bozuk bırakıyor hem de kullanıcının
          üzerine yazdığı ÖNCEKİ geçerli dosyayı geri getirilemez şekilde kaybettiriyordu (önceden
          var olan dosya hiç yedeklenmeden doğrudan üzerine yazılıyordu).

   NASIL: Her yazım önce hedef klasördeki gizli bir ".tmp" dosyasına yapılır (aynı disk bölümünde
          olduğu için sonraki adım gerçekten atomiktir), ardından File.Replace/Move ile TEK bir
          dosya sistemi işleminde hedef yola aktarılır. Yazma sırasında bir hata oluşursa (exception
          fırlatılırsa) yarım kalan temp dosyası silinir ve orijinal hedef dosyaya HİÇ dokunulmamış
          olur — exception olduğu gibi çağırana fırlatılmaya devam eder.
*/
public static class AtomicFile
{
    /// <summary>Metin içeriğini atomik olarak yazar (DXF/IFC gibi StringBuilder tabanlı yazıcılar için).</summary>
    public static void WriteAllText(string filePath, string content, Encoding encoding)
    {
        WriteVia(filePath, tempPath => File.WriteAllText(tempPath, content, encoding));
    }

    /// <summary>
    /// Kendi dosya API'sini kullanan yazıcılar (ACadSharp DwgWriter, ClosedXML, OpenXml) için:
    /// writeAction'a HEDEF yerine geçici bir yol verilir; writeAction hatasız tamamlanırsa
    /// geçici dosya atomik olarak hedef yola taşınır.
    /// </summary>
    public static void WriteVia(string filePath, Action<string> writeAction)
    {
        string? dir = Path.GetDirectoryName(filePath);
        string tempPath = Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            writeAction(tempPath);
            Commit(tempPath, filePath);
        }
        catch
        {
            TryDeleteTemp(tempPath);
            throw;
        }
    }

    private static void Commit(string tempPath, string finalPath)
    {
        try
        {
            if (File.Exists(finalPath))
                File.Replace(tempPath, finalPath, null);
            else
                File.Move(tempPath, finalPath);
        }
        catch (IOException)
        {
            // File.Replace/Move farklı disk bölümleri (örn. temp klasörü farklı sürücüde) veya
            // dosya kilitleme durumlarında başarısız olabilir — Copy+Delete'e düş.
            File.Copy(tempPath, finalPath, overwrite: true);
            TryDeleteTemp(tempPath);
        }
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try { if (File.Exists(tempPath)) File.Delete(tempPath); }
        catch { /* En kötü ihtimalle bir .tmp dosyası kalır — orijinal veri kaybı yok, kritik değil. */ }
    }
}
