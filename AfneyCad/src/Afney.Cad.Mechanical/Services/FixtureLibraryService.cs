using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Vitrifiye (Reseptör) Kütüphanesi Servisi (FixtureLibraryService)
   NEDEN: FINE SANI standardında, tüm sıhhi cihazların mühendislik parametrelerini (LU, DN, bağlantı tipi)
          merkezi bir katalogdan yönetmek ve UI'a sunmak için.
   
   KATALOG:
   - Her cihaz için: Tip, İsim (TR/EN), Yük Birimi (LU), Min. Bağlantı Çapı,
     Soğuk/Sıcak su ihtiyacı, Pis su çapı, Kategori, Sembol bilgisi
   - TS 1258 ve DIN 1988 standartlarına uygun
*/
public class FixtureLibraryService
{
    public class FixtureDefinition
    {
        public string Id { get; set; } = "";
        public string NameTR { get; set; } = "";
        public string NameEN { get; set; } = "";
        public string Category { get; set; } = "";
        public double LoadUnit { get; set; }           // Yük Birimi (LU / DU)
        public double MinColdWaterDN { get; set; }      // Min soğuk su bağlantı çapı (mm)
        public double MinHotWaterDN { get; set; }       // Min sıcak su bağlantı çapı (mm) - 0 = YOK
        public double WasteDN { get; set; }             // Pis su çıkış çapı (mm)
        public double FlowRateLps { get; set; }         // Tasarım debisi (lt/sn)
        public bool RequiresHotWater { get; set; }
        public bool RequiresVent { get; set; }          // Havalandırma gerekiyor mu
        public string SymbolType { get; set; } = "";    // Render sembol tipi
        public double SymbolWidth { get; set; }         // Sembol genişliği (mm)
        public double SymbolHeight { get; set; }        // Sembol yüksekliği (mm)
        public string Standard { get; set; } = "";      // Referans standart
    }

    private readonly List<FixtureDefinition> _catalog = new();
    private readonly string _catalogFilePath;

    public FixtureLibraryService()
    {
        string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Catalogs");
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        _catalogFilePath = System.IO.Path.Combine(dir, "FixtureLibrary.json");
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        if (System.IO.File.Exists(_catalogFilePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(_catalogFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<List<FixtureDefinition>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    _catalog.Clear();
                    _catalog.AddRange(loaded);
                    return;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Vitrifiye kataloğu JSON okuma hatası. Varsayılanlar yüklenecek.");
            }
        }

        // Dosya yoksa veya okunamadıysa
        LoadDefaults();
        SaveCatalog();
    }

    private void SaveCatalog()
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(_catalog, options);
            System.IO.File.WriteAllText(_catalogFilePath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Vitrifiye kataloğu JSON yazma hatası.");
        }
    }

    private void LoadDefaults()
    {
        _catalog.Clear();
        _catalog.AddRange(new List<FixtureDefinition>
        {
            // --- TUVALET ---
            new() { Id = "WC-001", NameTR = "Klozet (Rezervuarlı)", NameEN = "WC (Cistern)", Category = "Tuvalet",
                LoadUnit = 3.0, MinColdWaterDN = 15, MinHotWaterDN = 0, WasteDN = 100, FlowRateLps = 0.10,
                RequiresHotWater = false, RequiresVent = true, SymbolType = "WC", SymbolWidth = 400, SymbolHeight = 600, Standard = "TS 1258" },
    
            new() { Id = "WC-002", NameTR = "Klozet (Gömme Rezervuar)", NameEN = "WC (Concealed Cistern)", Category = "Tuvalet",
                LoadUnit = 3.0, MinColdWaterDN = 15, MinHotWaterDN = 0, WasteDN = 100, FlowRateLps = 0.10,
                RequiresHotWater = false, RequiresVent = true, SymbolType = "WC_Concealed", SymbolWidth = 400, SymbolHeight = 550, Standard = "TS 1258" },
    
            new() { Id = "WC-003", NameTR = "Pisuvar", NameEN = "Urinal", Category = "Tuvalet",
                LoadUnit = 2.0, MinColdWaterDN = 15, MinHotWaterDN = 0, WasteDN = 50, FlowRateLps = 0.05,
                RequiresHotWater = false, RequiresVent = true, SymbolType = "Urinal", SymbolWidth = 350, SymbolHeight = 400, Standard = "TS 1258" },
    
            new() { Id = "WC-004", NameTR = "Alaturka WC", NameEN = "Squat Toilet", Category = "Tuvalet",
                LoadUnit = 3.0, MinColdWaterDN = 15, MinHotWaterDN = 0, WasteDN = 100, FlowRateLps = 0.10,
                RequiresHotWater = false, RequiresVent = true, SymbolType = "SquatToilet", SymbolWidth = 500, SymbolHeight = 600, Standard = "TS 1258" },
    
            new() { Id = "BI-001", NameTR = "Bide", NameEN = "Bidet", Category = "Tuvalet",
                LoadUnit = 1.0, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 50, FlowRateLps = 0.05,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Bidet", SymbolWidth = 350, SymbolHeight = 500, Standard = "DIN 1988" },
    
            // --- LAVABO ---
            new() { Id = "LV-001", NameTR = "Lavabo (Yarım Ayak)", NameEN = "Washbasin (Pedestal)", Category = "Lavabo",
                LoadUnit = 1.5, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 40, FlowRateLps = 0.07,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Washbasin", SymbolWidth = 500, SymbolHeight = 400, Standard = "TS 1258" },
    
            new() { Id = "LV-002", NameTR = "Lavabo (Tezgah Üstü)", NameEN = "Washbasin (Countertop)", Category = "Lavabo",
                LoadUnit = 1.5, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 40, FlowRateLps = 0.07,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Washbasin_Counter", SymbolWidth = 450, SymbolHeight = 450, Standard = "TS 1258" },
    
            new() { Id = "LV-003", NameTR = "Mini Lavabo", NameEN = "Cloakroom Basin", Category = "Lavabo",
                LoadUnit = 1.0, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 32, FlowRateLps = 0.05,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "MiniBowl", SymbolWidth = 350, SymbolHeight = 300, Standard = "DIN 1988" },
    
            // --- BANYO ---
            new() { Id = "DU-001", NameTR = "Duş Teknesi", NameEN = "Shower Tray", Category = "Banyo",
                LoadUnit = 2.0, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 50, FlowRateLps = 0.15,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Shower", SymbolWidth = 800, SymbolHeight = 800, Standard = "TS 1258" },
    
            new() { Id = "DU-002", NameTR = "Duş Kabini", NameEN = "Shower Enclosure", Category = "Banyo",
                LoadUnit = 2.0, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 50, FlowRateLps = 0.15,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "ShowerCabin", SymbolWidth = 900, SymbolHeight = 900, Standard = "TS 1258" },
    
            new() { Id = "KV-001", NameTR = "Banyo Küveti", NameEN = "Bathtub", Category = "Banyo",
                LoadUnit = 3.0, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 50, FlowRateLps = 0.30,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Bathtub", SymbolWidth = 700, SymbolHeight = 1600, Standard = "TS 1258" },
    
            // --- MUTFAK ---
            new() { Id = "EV-001", NameTR = "Mutfak Eviyesi (Tek)", NameEN = "Kitchen Sink (Single)", Category = "Mutfak",
                LoadUnit = 2.0, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 50, FlowRateLps = 0.10,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Sink_Single", SymbolWidth = 500, SymbolHeight = 400, Standard = "TS 1258" },
    
            new() { Id = "EV-002", NameTR = "Mutfak Eviyesi (Çift)", NameEN = "Kitchen Sink (Double)", Category = "Mutfak",
                LoadUnit = 2.5, MinColdWaterDN = 15, MinHotWaterDN = 15, WasteDN = 50, FlowRateLps = 0.15,
                RequiresHotWater = true, RequiresVent = false, SymbolType = "Sink_Double", SymbolWidth = 800, SymbolHeight = 450, Standard = "TS 1258" },
    
            new() { Id = "BM-001", NameTR = "Bulaşık Makinesi", NameEN = "Dishwasher", Category = "Mutfak",
                LoadUnit = 1.5, MinColdWaterDN = 15, MinHotWaterDN = 0, WasteDN = 40, FlowRateLps = 0.05,
                RequiresHotWater = false, RequiresVent = false, SymbolType = "Dishwasher", SymbolWidth = 600, SymbolHeight = 600, Standard = "DIN 1988" },
    
            // --- ÇAMAŞIRHANE ---
            new() { Id = "CM-001", NameTR = "Çamaşır Makinesi", NameEN = "Washing Machine", Category = "Çamaşırhane",
                LoadUnit = 1.5, MinColdWaterDN = 15, MinHotWaterDN = 0, WasteDN = 50, FlowRateLps = 0.05,
                RequiresHotWater = false, RequiresVent = false, SymbolType = "WashingMachine", SymbolWidth = 600, SymbolHeight = 600, Standard = "DIN 1988" },
    
            // --- TEMİZLİK ---
            new() { Id = "DS-001", NameTR = "Döşeme Süzgeci (Yer Gideri)", NameEN = "Floor Drain", Category = "Temizlik",
                LoadUnit = 0.5, MinColdWaterDN = 0, MinHotWaterDN = 0, WasteDN = 75, FlowRateLps = 0,
                RequiresHotWater = false, RequiresVent = true, SymbolType = "FloorDrain", SymbolWidth = 200, SymbolHeight = 200, Standard = "TS EN 12056" },
        });
    }

    // --- SORGULAMA METODLARİ ---

    public List<FixtureDefinition> GetAll() => _catalog.ToList();

    public List<FixtureDefinition> GetByCategory(string category)
        => _catalog.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

    public FixtureDefinition? GetById(string id)
        => _catalog.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public List<FixtureDefinition> Search(string query)
        => _catalog.Where(f => f.NameTR.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               f.NameEN.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               f.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<string> GetCategories()
        => _catalog.Select(f => f.Category).Distinct().OrderBy(c => c).ToList();

    public int GetTotalCount() => _catalog.Count;

    /*
       NE: Vitrifiyen'den SanitaryFixtureEntity Oluştur
       NEDEN: Katalogdan seçilen cihazı CAD entity'ye dönüştürmek.
    */
    public Entities.SanitaryFixtureEntity CreateEntity(string fixtureId, Vector3D position)
    {
        var def = GetById(fixtureId);
        if (def == null) throw new ArgumentException($"Fixture ID '{fixtureId}' bulunamadı.");

        var entity = new Entities.SanitaryFixtureEntity(position, def.NameTR, def.LoadUnit);
        entity.Color = 0xFF00FF00;

        return entity;
    }
}
