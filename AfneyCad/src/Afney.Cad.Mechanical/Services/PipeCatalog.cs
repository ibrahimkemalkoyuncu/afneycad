using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Boru Katalog Servisi (PipeCatalog)
    NEDEN: Farklı malzeme standartlarına (DIN, TS, EN) göre nominal dış çap (DN/OD) ile hidrolik iç çap (ID) arasındaki ilişkiyi sağlamak için.
    
    KAYNAKLAR:
    - PPRC: DIN 8077 (SDR 11, SDR 6)
    - PVC: TS EN 1329
    - PEX: TS EN ISO 15875
*/
public static class PipeCatalog
{
    // Malzeme -> (Dış Çap -> İç Çap)
    private static readonly Dictionary<PipeMaterial, Dictionary<double, double>> _catalog = new();
    private static readonly string _catalogFilePath;

    static PipeCatalog()
    {
        string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Catalogs");
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        _catalogFilePath = System.IO.Path.Combine(dir, "PipeCatalog.json");
        LoadCatalog();
    }

    private static void LoadCatalog()
    {
        if (System.IO.File.Exists(_catalogFilePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(_catalogFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    _catalog.Clear();
                    foreach (var matKvp in loaded)
                    {
                        if (Enum.TryParse(matKvp.Key, out PipeMaterial mat))
                        {
                            var sizes = new Dictionary<double, double>();
                            foreach (var sizeKvp in matKvp.Value)
                            {
                                if (double.TryParse(sizeKvp.Key, out double od))
                                {
                                    sizes[od] = sizeKvp.Value;
                                }
                            }
                            _catalog[mat] = sizes;
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Boru kataloğu JSON okuma hatası. Varsayılanlar yüklenecek.");
            }
        }

        // Dosya yoksa veya okunamadıysa
        LoadDefaults();
        SaveCatalog();
    }

    private static void SaveCatalog()
    {
        try
        {
            var exportData = new Dictionary<string, Dictionary<string, double>>();
            foreach (var matKvp in _catalog)
            {
                var sizes = new Dictionary<string, double>();
                foreach (var sizeKvp in matKvp.Value)
                {
                    sizes[sizeKvp.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = sizeKvp.Value;
                }
                exportData[matKvp.Key.ToString()] = sizes;
            }

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(exportData, options);
            System.IO.File.WriteAllText(_catalogFilePath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Boru kataloğu JSON yazma hatası.");
        }
    }

    private static void LoadDefaults()
    {
        InitializePPRC_PN20(); // SDR 11 (Soğuk Su)
        InitializePPRC_PN25(); // SDR 6 (Sıcak Su / Kompozit)
        InitializePVC();       // Pis Su
        InitializePEX();       // Mobil
    }

    public static double GetInnerDiameter(PipeMaterial material, double outerDiameter)
    {
        // Eğer malzeme tanımlıysa ve çap listede varsa döndür
        if (_catalog.TryGetValue(material, out var sizes))
        {
            if (sizes.TryGetValue(outerDiameter, out double id))
                return id;

            // Tam eşleşme yoksa standart formülle tahmini ID
            return EstimateInnerDiameter(material, outerDiameter);
        }

        return outerDiameter * 0.9;
    }
    
    public static List<double> GetStandardDiameters(PipeMaterial material)
    {
        if (_catalog.TryGetValue(material, out var sizes))
        {
            return sizes.Keys.OrderBy(d => d).ToList();
        }
        return new List<double> { 20, 25, 32, 40, 50, 63, 75, 90, 110 }; // Default
    }

    private static double EstimateInnerDiameter(PipeMaterial material, double od)
    {
        return material switch
        {
            PipeMaterial.PPRC_PN20 => od * (1.0 - 2.0/11.0), // SDR 11
            PipeMaterial.PPRC_PN25 => od * (1.0 - 2.0/6.0),  // SDR 6
            PipeMaterial.PVC_SN4   => od * 0.94,             // Yaklaşık
            _ => od * 0.9
        };
    }

    private static void InitializePPRC_PN20() // SDR 11
    {
        var map = new Dictionary<double, double>
        {
            { 20, 16.2 }, { 25, 20.4 }, { 32, 26.0 }, { 40, 32.6 },
            { 50, 40.8 }, { 63, 51.4 }, { 75, 61.2 }, { 90, 73.6 }, { 110, 90.0 }
        };
        _catalog[PipeMaterial.PPRC_PN20] = map;
    }

    private static void InitializePPRC_PN25() // SDR 6 (Kalın Etli)
    {
        var map = new Dictionary<double, double>
        {
            { 20, 13.2 }, { 25, 16.6 }, { 32, 21.2 }, { 40, 26.6 },
            { 50, 33.2 }, { 63, 42.0 }, { 75, 50.0 }, { 90, 60.0 }, { 110, 73.2 }
        };
        _catalog[PipeMaterial.PPRC_PN25] = map;
    }

    private static void InitializePVC() // Pis Su (SN4 / Tip 1)
    {
        var map = new Dictionary<double, double>
        {
            { 50, 46.4 }, { 75, 71.2 }, { 110, 103.6 },
            { 125, 118.6 }, { 160, 152.0 }, { 200, 190.2 }
        };
        _catalog[PipeMaterial.PVC_SN4] = map;
    }
    
    private static void InitializePEX()
    {
        var map = new Dictionary<double, double>
        {
            { 16, 12.0 }, { 20, 16.0 }, { 25, 20.4 }, { 32, 26.2 }
        };
        _catalog[PipeMaterial.PEX_b] = map;
    }
}
