using System.IO;
using System.Text.Json;

namespace Afney.Cad.Presentation.Services;

public class RecentFilesService
{
    private readonly List<string> _files = new();
    private readonly string _configPath;
    private const int MaxFiles = 15;

    public RecentFilesService()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfneyCAD");
        Directory.CreateDirectory(appData);
        _configPath = Path.Combine(appData, "recent_files.json");
        Load();
    }

    public IReadOnlyList<string> Files => _files;

    public void AddFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        _files.Remove(filePath);
        _files.Insert(0, filePath);
        if (_files.Count > MaxFiles) _files.RemoveAt(_files.Count - 1);
        Save();
    }

    public void RemoveFile(string filePath)
    {
        _files.Remove(filePath);
        Save();
    }

    public void Clear()
    {
        _files.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    _files.Clear();
                    foreach (var f in list.Where(File.Exists).Take(MaxFiles))
                        _files.Add(f);
                }
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_files, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }
}
