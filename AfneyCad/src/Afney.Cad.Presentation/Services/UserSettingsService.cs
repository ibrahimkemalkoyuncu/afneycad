using System.IO;
using System.Text.Json;

namespace Afney.Cad.Presentation.Services;

public class UserSettings
{
    public bool GridDotMode        { get; set; } = false;
    public bool OrthoMode          { get; set; } = false;
    public bool OsnapEnabled       { get; set; } = true;
    public bool PolarTracking      { get; set; } = false;
    public bool DynamicInput       { get; set; } = true;
    public bool LeftPanelVisible   { get; set; } = false;
    public bool RightPanelVisible  { get; set; } = false;
    public string ActiveDimStyle   { get; set; } = "Standard";
    public double DimTextHeight    { get; set; } = 250.0;
    public string LastOpenFolder   { get; set; } = "";
    public string LastSaveFolder   { get; set; } = "";
    public int WindowWidth         { get; set; } = 1200;
    public int WindowHeight        { get; set; } = 800;
    public bool WindowMaximized    { get; set; } = true;
    public string Theme            { get; set; } = "Dark";
    public string Language         { get; set; } = "tr-TR";
}

public class UserSettingsService
{
    private UserSettings _settings = new();
    private readonly string _configPath;

    public UserSettings Settings => _settings;

    public UserSettingsService()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfneyCAD");
        Directory.CreateDirectory(appData);
        _configPath = Path.Combine(appData, "user_settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var s = JsonSerializer.Deserialize<UserSettings>(json);
                if (s != null) _settings = s;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("[Ayarlar] Kullanıcı ayarları okunamadı, varsayılanlar kullanılıyor: {Error}", ex.Message);
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("[Ayarlar] Kullanıcı ayarları kaydedilemedi: {Error}", ex.Message);
        }
    }
}
