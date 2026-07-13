using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Afney.Cad.Presentation.Services;

// Gelişmiş AutoSave — versiyonlama, kurtarma diyaloğu, dosya rotasyonu
public class EnhancedAutoSaveService
{
    private readonly string _backupFolder;
    public int MaxVersions { get; set; } = 10;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsEnabled { get; set; } = true;

    public EnhancedAutoSaveService(string? projectName = null)
    {
        _backupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AfneyCAD", "AutoSave", projectName ?? "default");

        Directory.CreateDirectory(_backupFolder);
    }

    // Otomatik kayıt — versiyonlu
    public string SaveVersion(string projectName, byte[] data)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{projectName}_{timestamp}.bak";
        string filePath = Path.Combine(_backupFolder, fileName);

        File.WriteAllBytes(filePath, data);

        // Eski versiyonları temizle (rotasyon)
        RotateBackups(projectName);

        return filePath;
    }

    // JSON serialize ile kaydet
    public string SaveVersionJson(string projectName, string jsonData)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{projectName}_{timestamp}.afney.bak";
        string filePath = Path.Combine(_backupFolder, fileName);

        File.WriteAllText(filePath, jsonData, System.Text.Encoding.UTF8);
        RotateBackups(projectName);

        return filePath;
    }

    // Mevcut backup versiyonlarını listele
    public List<BackupVersion> ListVersions(string? projectFilter = null)
    {
        var versions = new List<BackupVersion>();

        if (!Directory.Exists(_backupFolder)) return versions;

        var files = Directory.GetFiles(_backupFolder, "*.bak")
            .OrderByDescending(f => File.GetLastWriteTime(f));

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (projectFilter != null && !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            versions.Add(new BackupVersion
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                Size = new FileInfo(file).Length,
                LastModified = File.GetLastWriteTime(file),
                ProjectName = name.Contains('_') ? name[..name.LastIndexOf('_')] : name
            });
        }

        return versions;
    }

    // Kurtarma — en son backup'ı getir
    public string? GetLatestBackup(string projectName)
    {
        return ListVersions(projectName).FirstOrDefault()?.FilePath;
    }

    // Crash recovery — başlangıçta kontrol et
    public List<BackupVersion> CheckRecoveryFiles()
    {
        return ListVersions()
            .Where(v => v.LastModified > DateTime.Now.AddHours(-24))
            .ToList();
    }

    // Eski backup'ları temizle
    private void RotateBackups(string projectName)
    {
        var versions = ListVersions(projectName);
        if (versions.Count <= MaxVersions) return;

        var toDelete = versions.Skip(MaxVersions).ToList();
        foreach (var old in toDelete)
        {
            try { File.Delete(old.FilePath); }
            catch (Exception ex) { Serilog.Log.Debug("[AutoSave] Eski yedek silinemedi: {File} — {Error}", old.FilePath, ex.Message); }
        }
    }

    // Tüm backup'ları temizle
    public void PurgeAll()
    {
        if (Directory.Exists(_backupFolder))
        {
            foreach (var file in Directory.GetFiles(_backupFolder, "*.bak"))
            {
                try { File.Delete(file); } catch (Exception ex) { Serilog.Log.Debug("[AutoSave] Yedek silinemedi: {File} — {Error}", file, ex.Message); }
            }
        }
    }

    // Disk kullanım raporu
    public BackupStorageReport GetStorageReport()
    {
        var versions = ListVersions();
        return new BackupStorageReport
        {
            TotalVersions = versions.Count,
            TotalSizeBytes = versions.Sum(v => v.Size),
            TotalSizeMB = versions.Sum(v => v.Size) / (1024.0 * 1024.0),
            OldestBackup = versions.LastOrDefault()?.LastModified,
            NewestBackup = versions.FirstOrDefault()?.LastModified,
            BackupFolder = _backupFolder
        };
    }
}

public class BackupVersion
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ProjectName { get; set; } = "";
}

public class BackupStorageReport
{
    public int TotalVersions { get; set; }
    public long TotalSizeBytes { get; set; }
    public double TotalSizeMB { get; set; }
    public DateTime? OldestBackup { get; set; }
    public DateTime? NewestBackup { get; set; }
    public string BackupFolder { get; set; } = "";
}
