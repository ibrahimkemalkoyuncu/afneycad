using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Bulut Senkronizasyon / Proje Yedekleme Servisi (CloudBackupService)
   NEDEN: Proje dosyasını OneDrive/Google Drive/Dropbox gibi cloud-synced bir klasöre
          zaman damgalı kopyalar oluşturarak otomatik yedekler.

   TASARIM:
   - Cloud API entegrasyonu gerektirmez — kullanıcı cloud istemcisinin senkronize
     ettiği yerel klasörü hedef olarak seçer (ör. C:\Users\...\OneDrive\AfneyBackup).
   - Yedek dosyaları: <ProjeAdı>_backup_<yyyyMMdd_HHmmss>.afney.bak
   - Maksimum yedek sayısı: 20 (eskiler otomatik silinir).
   - Otomatik yedekleme: yapılandırılmış aralıkta (dakika) arkaplanda çalışır.
*/
public class CloudBackupService : IDisposable
{
    private const int MAX_BACKUPS = 20;

    private string _backupDirectory;
    private System.Windows.Threading.DispatcherTimer? _autoTimer;
    private bool _autoEnabled;

    public string BackupDirectory => _backupDirectory;
    public bool   AutoEnabled     => _autoEnabled;
    public int    AutoIntervalMin { get; private set; } = 15;

    public event Action<string>?   BackupCompleted;
    public event Action<Exception>? BackupFailed;

    public CloudBackupService()
    {
        // Varsayılan: Belgelerim/AfneyCAD/CloudBackup
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _backupDirectory = Path.Combine(docs, "AfneyCAD", "CloudBackup");
        EnsureDirectory();
    }

    // ── Hedef Klasörü Değiştir ────────────────────────────────────────────────────

    public void SetBackupDirectory(string path)
    {
        _backupDirectory = path;
        EnsureDirectory();
    }

    // ── Tek Seferlik Yedek ───────────────────────────────────────────────────────

    public async Task<string> BackupAsync(string sourceFilePath, string projectName)
    {
        EnsureDirectory();

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName  = $"{projectName}_backup_{timestamp}.afney.bak";
        string destPath  = Path.Combine(_backupDirectory, fileName);

        await Task.Run(() =>
        {
            if (File.Exists(sourceFilePath))
                File.Copy(sourceFilePath, destPath, overwrite: true);
            else
            {
                // Kaynak dosya yoksa boş placeholder yaz (proje henüz kaydedilmemiş)
                File.WriteAllText(destPath, $"AfneyCAD Backup Placeholder — {projectName} — {timestamp}");
            }
        });

        PurgeOldBackups(projectName);
        BackupCompleted?.Invoke(destPath);
        return destPath;
    }

    // Senkron overload (UI thread'den çağırmak için)
    public string Backup(string sourceFilePath, string projectName)
    {
        EnsureDirectory();
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName  = $"{projectName}_backup_{timestamp}.afney.bak";
        string destPath  = Path.Combine(_backupDirectory, fileName);

        try
        {
            if (File.Exists(sourceFilePath))
                File.Copy(sourceFilePath, destPath, overwrite: true);
            else
                File.WriteAllText(destPath, $"AfneyCAD Backup — {projectName} — {timestamp}");

            PurgeOldBackups(projectName);
            BackupCompleted?.Invoke(destPath);
        }
        catch (Exception ex)
        {
            BackupFailed?.Invoke(ex);
        }

        return destPath;
    }

    // ── Otomatik Yedekleme ────────────────────────────────────────────────────────

    public void StartAuto(string sourceFilePath, string projectName, int intervalMinutes = 15)
    {
        StopAuto();
        AutoIntervalMin = intervalMinutes;
        _autoEnabled    = true;

        _autoTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(intervalMinutes)
        };
        _autoTimer.Tick += async (_, _) =>
        {
            try   { await BackupAsync(sourceFilePath, projectName); }
            catch (Exception ex) { BackupFailed?.Invoke(ex); }
        };
        _autoTimer.Start();
    }

    public void StopAuto()
    {
        _autoTimer?.Stop();
        _autoTimer   = null;
        _autoEnabled = false;
    }

    // ── Yedek Listesi ─────────────────────────────────────────────────────────────

    public List<BackupEntry> ListBackups(string? projectName = null)
    {
        if (!Directory.Exists(_backupDirectory)) return [];

        var pattern = projectName != null ? $"{projectName}_backup_*.afney.bak" : "*_backup_*.afney.bak";

        return Directory.GetFiles(_backupDirectory, pattern)
            .Select(f => new BackupEntry
            {
                FilePath    = f,
                FileName    = Path.GetFileName(f),
                SizeKb      = (int)(new FileInfo(f).Length / 1024),
                CreatedAt   = File.GetLastWriteTime(f)
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    // ── Eski Yedekleri Temizle ────────────────────────────────────────────────────

    private void PurgeOldBackups(string projectName)
    {
        var entries = ListBackups(projectName);
        foreach (var old in entries.Skip(MAX_BACKUPS))
        {
            try { File.Delete(old.FilePath); } catch { /* yoksay */ }
        }
    }

    private void EnsureDirectory()
    {
        if (!Directory.Exists(_backupDirectory))
            Directory.CreateDirectory(_backupDirectory);
    }

    public void Dispose() => StopAuto();

    public class BackupEntry
    {
        public string   FilePath  { get; set; } = "";
        public string   FileName  { get; set; } = "";
        public int      SizeKb    { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
