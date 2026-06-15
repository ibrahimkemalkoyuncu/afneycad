using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Enerji Performans Servisi (EnergyPerformanceService)
   NEDEN: EPBD (Binaların Enerji Performansı Direktifi) + TS 825:2023 — bina
          enerji sertifikası. Türkiye'de enerji kimlik belgesi (EKB) zorunlu.
          Sınıf A'dan G'ye kadar puanlama.

   KAPSAM:
   - Isı kaybı: yapı + infiltrasyon (TS 825)
   - Isıtma enerji ihtiyacı: QH = HT × (Ti - Te,ortalama) × saatler
   - Soğutma enerji: QC = Σ kazanımlar × solar + iç + transmisyon
   - DHW enerji: EN 15316 tabanlı
   - Birincil enerji dönüşümü + CO₂
   - EKB Sınıfı: A++=<25, A=<50, B=<75, C=<100, D=<125, E=<150, F=<175, G>175 kWh/m²
*/
public class EnergyPerformanceService
{
    // ── Yapı Elemanı ─────────────────────────────────────────────────────────────

    public class BuildingElement
    {
        public string Name     { get; set; } = "";
        public double AreaM2   { get; set; }
        public double U_Wpm2K  { get; set; }   // U-değeri W/(m²K)
        public double PsiM     { get; set; } = 0;  // Lineer ısı köprüsü Ψ×L
    }

    // ── Hesap Girdisi ────────────────────────────────────────────────────────────

    public class EnergyInput
    {
        public double ConditionedAreaM2  { get; set; }   // Isıtılan alan (m²)
        public double ConditionedVolumeM3{ get; set; }   // Hacim (m³)
        public string City               { get; set; } = "İstanbul";
        public string BuildingType       { get; set; } = "Konut";
        public double InternalTempC      { get; set; } = 20;
        public double AirChangeRate      { get; set; } = 0.5;  // 1/h (infiltrasyon)

        public List<BuildingElement> Elements { get; set; } = [];

        // Sistem verimler
        public double HeatingSystemEff   { get; set; } = 0.90;   // Kazan/ısı pompası COP
        public double DHWSystemEff       { get; set; } = 0.85;
        public bool   HasSolarDHW        { get; set; } = false;
        public double SolarFractionDHW   { get; set; } = 0.40;
        public double CoolingSystemEff   { get; set; } = 3.50;   // Klima EER/SEER
        public bool   HasCooling         { get; set; } = true;
        public double LightingWpm2       { get; set; } = 8.0;    // W/m²
        public double OccupantsCount     { get; set; } = 4;
    }

    // ── Şehir İklim Veritabanı ────────────────────────────────────────────────────

    public static readonly Dictionary<string, (double HDD, double CDD, double TeMean, double TsJul, double HorizSolKwh)> CityClimate = new()
    {
        ["İstanbul"]  = (1300, 200, 13.5,  26, 1550),
        ["Ankara"]    = (2700, 350, 11.5,  26, 1700),
        ["İzmir"]     = (900,  600, 17.0,  33, 2000),
        ["Bursa"]     = (1900, 280, 13.0,  28, 1650),
        ["Antalya"]   = (500,  800, 18.5,  35, 2200),
        ["Konya"]     = (2800, 400, 11.0,  28, 1900),
        ["Samsun"]    = (1500, 100, 13.0,  24, 1400),
        ["Diyarbakır"]= (2400, 700, 14.0,  35, 2100),
        ["Erzurum"]   = (4500,  50,  5.5,  20, 1700),
        ["Trabzon"]   = (1300,  50, 13.5,  23, 1300),
    };

    // ── Hesap Sonucu ─────────────────────────────────────────────────────────────

    public class EnergyResult
    {
        public double HeatTransCoeffHT       { get; set; }  // W/K (yapı + infiltrasyon)
        public double HeatingNeedKwhpm2      { get; set; }  // kWh/(m²yıl) net ısıtma
        public double CoolingNeedKwhpm2      { get; set; }  // kWh/(m²yıl) net soğutma
        public double DHWNeedKwhpm2          { get; set; }  // kWh/(m²yıl) sıhhi sıcak su
        public double LightingNeedKwhpm2     { get; set; }  // kWh/(m²yıl) aydınlatma
        public double PrimaryEnergyKwhpm2    { get; set; }  // kWh/(m²yıl) birincil enerji
        public double CO2Kgpm2              { get; set; }   // kg CO₂/(m²yıl)
        public string EnergyClass           { get; set; } = "";
        public double Score                 { get; set; }   // Sertifika skoru
        public List<string> Recommendations { get; set; } = [];
        public double UWallMean             { get; set; }   // Ortalama duvar U-değeri
        public double HeatLossWK            { get; set; }   // Toplam ısı kaybı W/K
    }

    // ── Hesap Motoru ─────────────────────────────────────────────────────────────

    public static EnergyResult Calculate(EnergyInput inp)
    {
        var result = new EnergyResult();
        if (!CityClimate.TryGetValue(inp.City, out var climate))
            climate = CityClimate["İstanbul"];

        // ── 1. Yapı Isı Kaybı HT ─────────────────────────────────────────────────
        double htTransmission = 0;
        double totalArea = 0;
        double sumUA = 0;
        foreach (var el in inp.Elements)
        {
            double htEl = el.U_Wpm2K * el.AreaM2 + el.PsiM;
            htTransmission += htEl;
            totalArea += el.AreaM2;
            sumUA += el.U_Wpm2K * el.AreaM2;
        }
        // İnfiltrasyon: HV = 0.34 × n × V (W/K)
        double htVentilation = 0.34 * inp.AirChangeRate * inp.ConditionedVolumeM3;
        double ht = htTransmission + htVentilation;
        result.HeatTransCoeffHT = ht;
        result.HeatLossWK = ht;
        result.UWallMean = totalArea > 0 ? sumUA / totalArea : 0;

        // ── 2. Net Isıtma İhtiyacı (TS 825) ──────────────────────────────────────
        // QH_net = HT × HDD × 24 / 1000 (kWh) — iç kazanımlar %30 düşürülür
        double qHeatNet = ht * climate.HDD * 24.0 / 1000.0 * 0.85;  // 0.85: iç kazanım faktörü
        result.HeatingNeedKwhpm2 = qHeatNet / inp.ConditionedAreaM2;

        // ── 3. Soğutma İhtiyacı ──────────────────────────────────────────────────
        if (inp.HasCooling && climate.CDD > 0)
        {
            double qCoolNet = ht * climate.CDD * 24.0 / 1000.0 * 0.4;  // solar+iç kazanımlar artırır
            qCoolNet += inp.OccupantsCount * 100.0 * 800.0 / 1000.0;   // iç kazanımlar (100W/kişi × 800h)
            result.CoolingNeedKwhpm2 = qCoolNet / inp.ConditionedAreaM2;
        }

        // ── 4. DHW İhtiyacı (EN 15316) ───────────────────────────────────────────
        // Konut: 35 kWh/(m²yıl) ortalama; diğerleri tip katsayısı
        double dhwBasePm2 = inp.BuildingType switch
        {
            "Otel"     => 60,
            "Hastane"  => 80,
            "Ofis"     => 5,
            _          => 35   // Konut / Genel
        };
        double solarRed = inp.HasSolarDHW ? inp.SolarFractionDHW : 0;
        result.DHWNeedKwhpm2 = dhwBasePm2 * (1 - solarRed);

        // ── 5. Aydınlatma ─────────────────────────────────────────────────────────
        result.LightingNeedKwhpm2 = inp.LightingWpm2 * 2200 / 1000.0;  // 2200 h/yıl

        // ── 6. Birincil Enerji ────────────────────────────────────────────────────
        double peHeat  = result.HeatingNeedKwhpm2 / inp.HeatingSystemEff * 1.05;  // fp=1.05 doğalgaz
        double peCool  = result.CoolingNeedKwhpm2 / inp.CoolingSystemEff * 2.50;  // fp=2.50 elektrik
        double peDHW   = result.DHWNeedKwhpm2     / inp.DHWSystemEff     * 1.05;
        double peLight = result.LightingNeedKwhpm2 * 2.50;
        result.PrimaryEnergyKwhpm2 = peHeat + peCool + peDHW + peLight;
        result.Score = result.PrimaryEnergyKwhpm2;
        result.CO2Kgpm2 = (peHeat + peDHW) * 0.204 + (peCool + peLight) * 0.483;

        // ── 7. Sertifika Sınıfı ──────────────────────────────────────────────────
        result.EnergyClass = result.Score switch
        {
            <= 25  => "A++",
            <= 50  => "A+",
            <= 75  => "A",
            <= 100 => "B",
            <= 125 => "C",
            <= 150 => "D",
            <= 175 => "E",
            <= 225 => "F",
            _      => "G"
        };

        // ── 8. Öneriler ──────────────────────────────────────────────────────────
        if (result.UWallMean > 0.40) result.Recommendations.Add($"Duvar U ortalaması {result.UWallMean:F2} W/(m²K) > 0.40 — dış cephe yalıtımı öncelikli.");
        if (inp.AirChangeRate > 0.6) result.Recommendations.Add("Sızıntı oranı >0.6 h⁻¹ — ısı köprüsü + hava sızdırmazlık iyileştirmesi yapın.");
        if (!inp.HasSolarDHW && result.DHWNeedKwhpm2 > 20) result.Recommendations.Add("Güneş kolektörü eklenerek DHW enerjisi %35-60 azaltılabilir.");
        if (inp.LightingWpm2 > 10) result.Recommendations.Add($"Aydınlatma {inp.LightingWpm2} W/m² — LED geçişi ve sensör ile ~%40 tasarruf.");
        if (inp.CoolingSystemEff < 3.0 && inp.HasCooling) result.Recommendations.Add("Soğutma EER < 3.0 — A++ sınıfı klima ile değiştirin.");

        return result;
    }

    // ── TS 825:2023 U-Değeri Limitleri ──────────────────────────────────────────

    public static readonly Dictionary<string, double> TS825_U_Limits = new()
    {
        ["Dış Duvar (1.Bölge)"]  = 0.57,
        ["Dış Duvar (2.Bölge)"]  = 0.48,
        ["Dış Duvar (3.Bölge)"]  = 0.40,
        ["Dış Duvar (4.Bölge)"]  = 0.30,
        ["Çatı/Tavan (Tüm)"]     = 0.20,
        ["Döşeme/Zemin (Tüm)"]   = 0.45,
        ["Pencere (1.Bölge)"]    = 2.40,
        ["Pencere (2.Bölge)"]    = 2.20,
        ["Pencere (3.Bölge)"]    = 2.00,
        ["Pencere (4.Bölge)"]    = 1.80,
    };
}
