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

        // 1. Statik Demo Key (Geliştirici Erişimi İçin)
        if (key.Equals("AFNEY-2026-ENTP-DEMO", StringComparison.OrdinalIgnoreCase))
            return LicenseStatus.Valid;

        // 2. Format Kontrolü (AFNEY-XXXX-XXXX-XXXX)
        var parts = key.Split('-');
        if (parts.Length != 4 || parts[0] != "AFNEY") return LicenseStatus.Invalid;

        string customerId = parts[1];
        string serial = parts[2];
        string checksum = parts[3];

        if (customerId.Length != 4 || serial.Length != 4 || checksum.Length != 4)
            return LicenseStatus.Invalid;

        string expectedChecksum = ComputeChecksum(customerId, serial);
        return checksum == expectedChecksum ? LicenseStatus.Valid : LicenseStatus.Invalid;
    }

    /// <summary>
    /// HMAC-SHA256 tabanlı checksum: müşteri kodu + seri numarası üzerinden 4 haneli doğrulama bloğu üretir.
    /// </summary>
    private static string ComputeChecksum(string customerId, string serial)
    {
        byte[] saltBytes = Encoding.UTF8.GetBytes(LICENSE_SALT);
        byte[] payload = Encoding.UTF8.GetBytes($"AFNEY-{customerId}-{serial}");

        using var hmac = new HMACSHA256(saltBytes);
        byte[] hash = hmac.ComputeHash(payload);
        return Convert.ToHexString(hash).Substring(0, 4);
    }

    /// <summary>
    /// Belirli bir müşteri için geçerli bir lisans anahtarı üretir (satış/aktivasyon aracı tarafından kullanılır).
    /// </summary>
    public static string GenerateKey(string customerId, string? serial = null)
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
