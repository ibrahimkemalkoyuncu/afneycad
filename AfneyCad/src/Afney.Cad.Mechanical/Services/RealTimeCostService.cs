using System.Text.Json;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Statik Malzeme Fiyat Kataloğu (RealTimeCostService — isim yanıltıcı, aşağıya bak)
   NEDEN: "RealTimeCostService" adı gerçek zamanlı bir piyasa/API entegrasyonu çağrıştırıyor
          ama böyle bir entegrasyon YOK — bu sınıf sabit (`LoadDefaultPrices`), elle
          girilmiş bir birim fiyat tablosu + parametrik hesap motoru (`SetUnitPrice` ile
          manuel güncellenebilir, JSON import/export edilebilir). Sınıf adı geniş çapta
          kullanıldığı için (CostDashboardPanel vb.) bu oturumda YENİDEN ADLANDIRILMADI —
          bunun yerine GERÇEK BİR HATA düzeltildi (aşağıdaki not).

   DÜZELTİLEN GERÇEK HATA (sadece isimlendirme değil):
   `CalculateProjectCost` önceden `pipe.SystemType` (DomesticColdWater/WasteWater/... —
   BORU MALZEMESİ DEĞİL, akışkan sistemi) ile `_priceTable`'da (PPRC_PN20/PVC_SN4/... —
   gerçek malzeme adları) arama yapıyordu. Bu iki değer kümesi HİÇ KESİŞMİYOR — yani
   TryGetValue neredeyse her zaman başarısız oluyor ve HER BORU sessizce varsayılan
   50.0 TRY/m fiyata düşüyordu, gerçek malzemesi ne olursa olsun. Artık doğru alan
   `pipe.PipeMaterialType` (Enums.PipeMaterial: PPRC_PN20/PPRC_PN25/PVC_SN4/PEX_b/
   Steel_Galvanized/Silent_PP) kullanılıyor ve fiyat tablosu bu enum'un GERÇEK üye
   adlarıyla eşleşecek şekilde güncellendi.
*/

public class UnitPrice
{
    public string Material   { get; set; } = "";
    public string Category   { get; set; } = "";
    public double PricePerMeter { get; set; }
    public double FittingPrice  { get; set; }
    public string Currency      { get; set; } = "TRY";
}

public class CostBreakdown
{
    public double PipeCost      { get; set; }
    public double FittingCost   { get; set; }
    public double FixtureCost   { get; set; }
    public double LaborCost     { get; set; }
    public double TotalCost     { get; set; }
    public int    PipeCount     { get; set; }
    public double TotalLength   { get; set; }
    public int    FittingCount  { get; set; }
    public int    FixtureCount  { get; set; }
    public string Currency      { get; set; } = "TRY";

    public string Summary => $"Boru: {PipeCost:N0} | Fitting: {FittingCost:N0} | Cihaz: {FixtureCost:N0} | İşçilik: {LaborCost:N0} | TOPLAM: {TotalCost:N0} {Currency}";
}

public class RealTimeCostService
{
    private readonly Dictionary<string, UnitPrice> _priceTable = new();
    private double _laborMultiplier = 0.35;

    public RealTimeCostService()
    {
        LoadDefaultPrices();
    }

    private void LoadDefaultPrices()
    {
        // NEDEN Enums.PipeMaterial ile BİREBİR AYNI üye adları: GetPipePrice artık
        // pipe.PipeMaterialType.ToString() ile arama yapıyor — anahtar eşleşmezse
        // (aşağıdaki try/catch yorumuna bak) fiyat sessizce varsayılana düşerdi.
        AddPrice(nameof(PipeMaterial.Generic),          "Boru", 40.0, 10.0);
        AddPrice(nameof(PipeMaterial.PPRC_PN20),        "Boru", 45.0, 12.0);
        AddPrice(nameof(PipeMaterial.PPRC_PN25),        "Boru", 55.0, 15.0);
        AddPrice(nameof(PipeMaterial.PVC_SN4),          "Boru", 35.0, 8.0);
        AddPrice(nameof(PipeMaterial.PEX_b),            "Boru", 65.0, 14.0);
        AddPrice(nameof(PipeMaterial.Steel_Galvanized),  "Boru", 95.0, 25.0);
        AddPrice(nameof(PipeMaterial.Silent_PP),        "Boru", 120.0, 20.0);

        AddPrice("Lavabo",        "Cihaz", 850.0, 0);
        AddPrice("Klozet",        "Cihaz", 1200.0, 0);
        AddPrice("Dus",           "Cihaz", 650.0, 0);
        AddPrice("Kuvet",         "Cihaz", 2800.0, 0);
        AddPrice("Eviye_Tek",     "Cihaz", 950.0, 0);
        AddPrice("Eviye_Cift",    "Cihaz", 1400.0, 0);
        AddPrice("Pisuvar",       "Cihaz", 750.0, 0);
        AddPrice("Camasir_Mak",   "Cihaz", 0, 0);
        AddPrice("Bulasik_Mak",   "Cihaz", 0, 0);
        AddPrice("Sofben",        "Cihaz", 3500.0, 0);
        AddPrice("Yer_Suzgeci",   "Cihaz", 120.0, 0);

        AddPrice("Dirsek_90",  "Fitting", 0, 18.0);
        AddPrice("Te",         "Fitting", 0, 25.0);
        AddPrice("Reduksiyon", "Fitting", 0, 15.0);
        AddPrice("Vana",       "Fitting", 0, 85.0);
    }

    private void AddPrice(string material, string category, double pricePerMeter, double fittingPrice)
    {
        _priceTable[material] = new UnitPrice
        {
            Material = material,
            Category = category,
            PricePerMeter = pricePerMeter,
            FittingPrice = fittingPrice
        };
    }

    public void SetUnitPrice(string material, double pricePerMeter, double fittingPrice = 0)
    {
        if (_priceTable.ContainsKey(material))
        {
            _priceTable[material].PricePerMeter = pricePerMeter;
            _priceTable[material].FittingPrice = fittingPrice;
        }
        else
        {
            AddPrice(material, "Özel", pricePerMeter, fittingPrice);
        }
    }

    public CostBreakdown CalculateProjectCost(CadDatabase database)
    {
        var breakdown = new CostBreakdown();

        foreach (var entity in database.GetAllEntities())
        {
            if (entity is PipeEntity pipe)
            {
                double length = pipe.GetLength() / 1000.0;
                string matKey = pipe.PipeMaterialType.ToString();
                double unitPrice = GetPipePrice(matKey, pipe.InnerDiameter);

                breakdown.PipeCost += length * unitPrice;
                breakdown.TotalLength += length;
                breakdown.PipeCount++;
            }
            else if (entity is SanitaryFixtureEntity fixture)
            {
                breakdown.FixtureCost += GetFixturePrice(fixture.FixtureType);
                breakdown.FixtureCount++;
            }
        }

        breakdown.FittingCount = (int)(breakdown.PipeCount * 1.5);
        breakdown.FittingCost = breakdown.FittingCount * 18.0;

        breakdown.LaborCost = (breakdown.PipeCost + breakdown.FittingCost + breakdown.FixtureCost) * _laborMultiplier;

        breakdown.TotalCost = breakdown.PipeCost + breakdown.FittingCost + breakdown.FixtureCost + breakdown.LaborCost;

        return breakdown;
    }

    public double CalculateSinglePipeCost(double lengthMm, PipeMaterial material, double diameter)
    {
        double lengthM = lengthMm / 1000.0;
        double unitPrice = GetPipePrice(material.ToString(), diameter);
        return lengthM * unitPrice;
    }

    private double GetPipePrice(string material, double diameter)
    {
        double basePrice = _priceTable.TryGetValue(material, out var p) ? p.PricePerMeter : 50.0;
        double dnFactor = 1.0 + (diameter - 15.0) / 100.0;
        return basePrice * Math.Max(dnFactor, 0.5);
    }

    private double GetFixturePrice(string fixtureType)
    {
        if (fixtureType.Contains("Lavabo") || fixtureType.Contains("Washbasin")) return 850;
        if (fixtureType.Contains("Klozet") || fixtureType.Contains("WC") || fixtureType.Contains("Toilet")) return 1200;
        if (fixtureType.Contains("Duş") || fixtureType.Contains("Shower")) return 650;
        if (fixtureType.Contains("Küvet") || fixtureType.Contains("Bathtub")) return 2800;
        if (fixtureType.Contains("Eviye") && fixtureType.Contains("Çift")) return 1400;
        if (fixtureType.Contains("Eviye") || fixtureType.Contains("Sink")) return 950;
        if (fixtureType.Contains("Pisuvar") || fixtureType.Contains("Urinal")) return 750;
        if (fixtureType.Contains("Isıtıcı") || fixtureType.Contains("Şofben")) return 3500;
        if (fixtureType.Contains("FloorDrain") || fixtureType.Contains("Süzgec")) return 120;
        return 500;
    }

    public string ExportPriceTableJson()
    {
        return JsonSerializer.Serialize(_priceTable, new JsonSerializerOptions { WriteIndented = true });
    }

    public void ImportPriceTableJson(string json)
    {
        var table = JsonSerializer.Deserialize<Dictionary<string, UnitPrice>>(json);
        if (table != null)
            foreach (var kv in table)
                _priceTable[kv.Key] = kv.Value;
    }
}
