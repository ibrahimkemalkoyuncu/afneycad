using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Afney.Cad.Application.Licensing;

public enum LicenseStatus
{
    Valid,
    Expired,
    Invalid,
    Trial
}

/*
    NE: Lisans Yöneticisi (License Manager)
    NEDEN: Uygulamanın kullanım hakkını (lisans) doğrulamak ve yönetmek için.
    GÖREV:
    - Lisans anahtarını dosyadan okur.
    - Anahtarın geçerliliğini kontrol eder (Algoritma).
    - Geçerli değilse kullanıcıyı uyarır veya kısıtlar.
*/
public class LicenseManager
{
    private const string LICENSE_SALT = "AFNEY_CAD_ENTERPRISE_2026_SALT";
    private readonly string _licenseFilePath;

    public LicenseManager()
    {
        // Kullanıcı Profil Klasöründe Sakla
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _licenseFilePath = Path.Combine(appData, "AfneyCad", "license.key");
    }

    /// <summary>
    /// Mevcut lisans durumunu kontrol et
    /// </summary>
    public LicenseStatus Validate()
    {
        if (!File.Exists(_licenseFilePath)) return LicenseStatus.Trial; // Lisans dosyası yoksa Trial varsayalım veya Invalid
        
        try
        {
            string key = File.ReadAllText(_licenseFilePath).Trim();
            return ValidateKey(key);
        }
        catch
        {
            return LicenseStatus.Invalid;
        }
    }
    
    /// <summary>
    /// Verilen anahtarı doğrula
    /// </summary>
    public LicenseStatus ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return LicenseStatus.Invalid;
        key = key.Trim().ToUpperInvariant();

#if DEBUG
        // MÜHENDİSLİK: Statik demo key önceden Release derlemesinde de HER ZAMAN geçerliydi —
        // yani üründe her kullanıcı bu sabit metni yazarak tam lisanslı erişim elde edebiliyordu.
        // Artık sadece geliştirme (DEBUG) derlemelerinde çalışıyor; müşteriye giden Release EXE'de
        // bu kod yolu derlemeye hiç dahil edilmiyor.
        if (key.Equals("AFNEY-2026-ENTP-DEMO", StringComparison.OrdinalIgnoreCase))
            return LicenseStatus.Valid;
#endif

        // Format Kontrolü (AFNEY-XXXX-XXXX-XXXXXXXX)
        var parts = key.Split('-');
        if (parts.Length != 4 || parts[0] != "AFNEY") return LicenseStatus.Invalid;

        string customerId = parts[1];
        string serial = parts[2];
        string checksum = parts[3];

        if (customerId.Length != 4 || serial.Length != 4 || checksum.Length != ChecksumLength)
            return LicenseStatus.Invalid;

        string expectedChecksum = ComputeChecksum(customerId, serial);
        return checksum == expectedChecksum ? LicenseStatus.Valid : LicenseStatus.Invalid;
    }

    /*
       MÜHENDİSLİK: Checksum önceden HMAC-SHA256 çıktısının sadece İLK 4 HEX KARAKTERİNE
       (16 bit → 65.536 olasılık) kesiliyordu — algoritma/salt hiç bilinmese bile, ValidateKey()'e
       karşı düz kaba kuvvet (brute force) ile saniyeler içinde geçerli bir anahtar bulunabilirdi.
       Artık 8 hex karaktere (32 bit → ~4.3 milyar olasılık) çıkarıldı. NOT: Bu, daha önce
       üretilmiş/dağıtılmış lisans anahtarlarını GEÇERSİZ KILAR (bilinçli, onaylanmış format
       değişikliği — henüz gerçek müşteriye anahtar dağıtılmadığı varsayımıyla).
    */
    private const int ChecksumLength = 8;

    /// <summary>
    /// HMAC-SHA256 tabanlı checksum: müşteri kodu + seri numarası üzerinden doğrulama bloğu üretir.
    /// </summary>
    private static string ComputeChecksum(string customerId, string serial)
    {
        byte[] saltBytes = Encoding.UTF8.GetBytes(LICENSE_SALT);
        byte[] payload = Encoding.UTF8.GetBytes($"AFNEY-{customerId}-{serial}");

        using var hmac = new HMACSHA256(saltBytes);
        byte[] hash = hmac.ComputeHash(payload);
        return Convert.ToHexString(hash).Substring(0, ChecksumLength);
    }

    /*
       MÜHENDİSLİK: Önceden `public` idi ve anahtar üretme algoritması, doğrulama koduyla AYNI
       müşteri-taraflı assembly'de (Afney.Cad.Application.dll) sevk ediliyordu — herhangi bir
       kullanıcı decompiler'a ihtiyaç duymadan reflection ile doğrudan çağırıp kendi lisansını
       üretebilirdi. Artık `internal` — sadece bu assembly içinden ve InternalsVisibleTo ile
       yetkilendirilen ayrı, müşteriye sevk EDİLMEYEN Afney.Cad.LicenseTool konsol aracından
       erişilebiliyor (bkz. tools/Afney.Cad.LicenseTool).
    */
    internal static string GenerateKey(string customerId, string? serial = null)
    {
        customerId = customerId.Trim().ToUpperInvariant().PadLeft(4, '0');
        if (customerId.Length > 4) customerId = customerId[..4];

        serial ??= Convert.ToHexString(RandomNumberGenerator.GetBytes(2));
        serial = serial.ToUpperInvariant().PadLeft(4, '0');
        if (serial.Length > 4) serial = serial[..4];

        string checksum = ComputeChecksum(customerId, serial);
        return $"AFNEY-{customerId}-{serial}-{checksum}";
    }
    
    /// <summary>
    /// Lisansı kaydet
    /// </summary>
    public void SaveLicense(string key)
    {
        string? dir = Path.GetDirectoryName(_licenseFilePath);
        if (string.IsNullOrEmpty(dir)) return;

        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_licenseFilePath, key);
    }

    /// <summary>
    /// Lisansı sil (Deactivate)
    /// </summary>
    public void RemoveLicense()
    {
        if (File.Exists(_licenseFilePath)) File.Delete(_licenseFilePath);
    }
}
