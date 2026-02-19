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

        // 1. Statik Demo Key (Geliştirici Erişimi İçin)
        if (key.Equals("AFNEY-2026-ENTP-DEMO", StringComparison.OrdinalIgnoreCase)) 
            return LicenseStatus.Valid;
            
        // 2. Format Kontrolü (AFNEY-XXXX-XXXX-XXXX)
        var parts = key.Split('-');
        if (parts.Length != 4 || parts[0] != "AFNEY") return LicenseStatus.Invalid;

        // 3. Basit Checksum Kontrolü (Örnek Algoritma: Hash'in son hanesi)
        // Gerçek Enterprise için RSA/DSA kullanılmalıdır.
        // Şimdilik sadece format ve demo key yeterli.
        
        return LicenseStatus.Invalid;
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
