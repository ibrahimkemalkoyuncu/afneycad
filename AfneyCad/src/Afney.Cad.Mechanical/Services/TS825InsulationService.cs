using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: TS 825 Isı Yalıtım Hesap Servisi (TS825InsulationService)
   NEDEN: "TS 825 Binalarda Isı Yalıtım Kuralları" standardına uygun olarak
          yapı elemanlarının ısı geçirgenlik katsayısını (U), gerekli yalıtım
          kalınlığını, ısı kaybını ve yıllık ısıtma enerjisi ihtiyacını hesaplamak.

   STANDART REFERANSLARI:
   - TS 825:2013 Tablo 2 — Yapı elemanları için maksimum U (W/m²K) sınır değerleri
   - TS 825 / TS EN ISO 6946 — Yüzeysel ısıl direnç (Rsi, Rse) değerleri
   - TS 825 Tablo 1 — 4 iklim (derece-gün) bölgesi ve dış tasarım sıcaklıkları
   - TS 825 — Yıllık ısıtma enerjisi için ısıtma derece-gün (DD, taban 18 °C) yaklaşımı

   TEMEL BAĞINTILAR:
   - Katman direnci        : R_i   = d_i / λ_i           (m²K/W)
   - Toplam ısıl direnç    : R_top = Rsi + Σ R_i + Rse   (m²K/W)
   - Isı geçirgenlik kats. : U     = 1 / R_top           (W/m²K)
   - Isı kaybı             : Q     = U · A · (Ti − Te)   (W)
   - Gerekli yalıtım kal.  : d_yal = λ_yal · (1/U_max − 1/U_mevcut)   (m)
   - Yıllık enerji         : E     = U · A · DD · 24 / 1000           (kWh/yıl)
*/
public class TS825InsulationService
{
    // ── İklim bölgesi (TS 825 Tablo 1) ───────────────────────────────────────
    public enum ClimateZone
    {
        Bolge1 = 1, // İzmir, Antalya, Adana (ılıman)
        Bolge2,     // İstanbul, Balıkesir, Sakarya
        Bolge3,     // Ankara, Bursa, Konya
        Bolge4,     // Erzurum, Kars, Ağrı (soğuk)
    }

    // ── Yapı elemanı tipi ────────────────────────────────────────────────────
    public enum ElementType
    {
        DisDuvar,      // Dış duvar
        CatiTeras,     // Çatı / teras
        Doseme,        // Döşeme (zemine oturan)
        PencereKapi,   // Pencere / kapı (U doğrudan girilir)
    }

    // ── Katman modeli ────────────────────────────────────────────────────────
    // Malzeme adı, ısıl iletkenlik λ (W/mK), kalınlık d (m)
    public record Layer(string Malzeme, double LambdaWmK, double KalinlikM);

    // ── TS 825:2013 Tablo 2 — Maksimum U değerleri (W/m²K) ───────────────────
    public static double GetMaxU(ClimateZone zone, ElementType el) => el switch
    {
        ElementType.DisDuvar    => zone switch { ClimateZone.Bolge1 => 0.70, ClimateZone.Bolge2 => 0.60, ClimateZone.Bolge3 => 0.50, _ => 0.40 },
        ElementType.CatiTeras   => zone switch { ClimateZone.Bolge1 => 0.45, ClimateZone.Bolge2 => 0.40, ClimateZone.Bolge3 => 0.30, _ => 0.25 },
        ElementType.Doseme      => zone switch { ClimateZone.Bolge1 => 0.70, ClimateZone.Bolge2 => 0.60, ClimateZone.Bolge3 => 0.50, _ => 0.45 },
        ElementType.PencereKapi => zone switch { ClimateZone.Bolge1 => 2.40, ClimateZone.Bolge2 => 2.40, ClimateZone.Bolge3 => 2.00, _ => 1.80 },
        _ => 0.50
    };

    // ── Dış tasarım sıcaklığı (°C) — TS 825 iklim bölgesi ─────────────────────
    public static double GetDesignOutdoorTemp(ClimateZone zone) => zone switch
    {
        ClimateZone.Bolge1 =>  3.0,
        ClimateZone.Bolge2 =>  0.0,
        ClimateZone.Bolge3 => -3.0,
        _                  => -12.0,
    };

    // ── Isıtma derece-gün (taban 18 °C) — TS 825 bölge yaklaşık değerleri ─────
    public static double GetDegreeDays(ClimateZone zone) => zone switch
    {
        ClimateZone.Bolge1 => 1600.0,
        ClimateZone.Bolge2 => 2100.0,
        ClimateZone.Bolge3 => 3300.0,
        _                  => 4800.0,
    };

    // ── Yüzeysel ısıl direnç (m²K/W) — TS EN ISO 6946 ─────────────────────────
    public static (double Rsi, double Rse) GetSurfaceResistances(ElementType el) => el switch
    {
        ElementType.DisDuvar    => (0.13, 0.04), // yatay ısı akışı
        ElementType.CatiTeras   => (0.10, 0.04), // yukarı ısı akışı
        ElementType.Doseme      => (0.17, 0.04), // aşağı ısı akışı
        ElementType.PencereKapi => (0.13, 0.04),
        _ => (0.13, 0.04)
    };

    public static string ZoneCity(ClimateZone zone) => zone switch
    {
        ClimateZone.Bolge1 => "İzmir, Antalya, Adana",
        ClimateZone.Bolge2 => "İstanbul, Balıkesir, Sakarya",
        ClimateZone.Bolge3 => "Ankara, Bursa, Konya",
        _                  => "Erzurum, Kars, Ağrı",
    };

    public static string ElementName(ElementType el) => el switch
    {
        ElementType.DisDuvar    => "Dış Duvar",
        ElementType.CatiTeras   => "Çatı / Teras",
        ElementType.Doseme      => "Döşeme (Zemin)",
        ElementType.PencereKapi => "Pencere / Kapı",
        _ => el.ToString()
    };

    // ── Sonuç modeli ─────────────────────────────────────────────────────────
    public class InsulationResult
    {
        public ClimateZone Zone { get; set; }
        public ElementType Element { get; set; }
        public double AreaM2 { get; set; }
        public double Rsi { get; set; }
        public double Rse { get; set; }
        public double RLayers { get; set; }        // katmanların toplam direnci
        public double RTotal { get; set; }         // Rsi + katmanlar + Rse
        public double UCurrent { get; set; }       // mevcut U (W/m²K)
        public double UMax { get; set; }           // TS 825 sınır U
        public bool MeetsLimit { get; set; }       // U_mevcut ≤ U_max ?
        public double InsulationLambda { get; set; }
        public double RequiredInsulationThicknessM { get; set; }
        public double IndoorTempC { get; set; }
        public double OutdoorTempC { get; set; }
        public double HeatLossW { get; set; }      // Q = U·A·ΔT
        public double DegreeDays { get; set; }
        public double AnnualEnergyKwh { get; set; }
        public string Standard { get; set; } = "TS 825:2013";
        public List<string> Notes { get; } = new();
    }

    /*
       NE: TS 825 yalıtım / ısı kaybı hesabı
       PARAMETRELER:
       - zone            : İklim bölgesi (1–4)
       - el              : Yapı elemanı tipi
       - areaM2          : Eleman alanı (m²)
       - layers          : Malzeme katmanları (iç → dış). Pencere/kapı için boş olabilir.
       - indoorTempC     : İç tasarım sıcaklığı (°C), varsayılan 20
       - outdoorTempOverride : Dış sıcaklık (°C) — null ise TS 825 bölge değeri
       - insulationLambda: Eklenecek yalıtım malzemesi λ (W/mK), varsayılan 0.035
       - directU         : Pencere/kapı için doğrudan U (W/m²K); >0 ise katman hesabı atlanır
    */
    public InsulationResult Calculate(
        ClimateZone zone,
        ElementType el,
        double areaM2,
        IEnumerable<Layer> layers,
        double indoorTempC = 20.0,
        double? outdoorTempOverride = null,
        double insulationLambda = 0.035,
        double directU = 0.0)
    {
        // ── Girdi doğrulama ──────────────────────────────────────────────────
        if (areaM2 <= 0)
            throw new ArgumentException("Alan pozitif bir değer olmalıdır (m²).");
        if (insulationLambda <= 0)
            throw new ArgumentException("Yalıtım ısıl iletkenliği (λ) pozitif olmalıdır (W/mK).");

        var layerList = layers?.ToList() ?? new List<Layer>();
        var (rsi, rse) = GetSurfaceResistances(el);
        double uMax = GetMaxU(zone, el);
        double te = outdoorTempOverride ?? GetDesignOutdoorTemp(zone);
        double dd = GetDegreeDays(zone);

        var result = new InsulationResult
        {
            Zone = zone,
            Element = el,
            AreaM2 = areaM2,
            Rsi = rsi,
            Rse = rse,
            UMax = uMax,
            InsulationLambda = insulationLambda,
            IndoorTempC = indoorTempC,
            OutdoorTempC = te,
            DegreeDays = dd,
        };

        double uCurrent;
        if (el == ElementType.PencereKapi && directU > 0)
        {
            // Pencere/kapı: U doğrudan üretici beyanından alınır
            uCurrent = directU;
            result.RLayers = 0;
            result.RTotal = 1.0 / uCurrent;
            result.Notes.Add("Pencere/kapı U değeri doğrudan girilmiştir (katman hesabı yapılmaz).");
        }
        else
        {
            foreach (var lay in layerList)
            {
                if (lay.LambdaWmK <= 0)
                    throw new ArgumentException($"'{lay.Malzeme}' katmanı için λ pozitif olmalıdır.");
                if (lay.KalinlikM <= 0)
                    throw new ArgumentException($"'{lay.Malzeme}' katmanı için kalınlık pozitif olmalıdır (m).");
            }

            double rLayers = layerList.Sum(l => l.KalinlikM / l.LambdaWmK);
            double rTotal = rsi + rLayers + rse;
            if (rTotal <= 0) throw new InvalidOperationException("Toplam ısıl direnç hesaplanamadı.");

            result.RLayers = rLayers;
            result.RTotal = rTotal;
            uCurrent = 1.0 / rTotal;
        }

        result.UCurrent = uCurrent;
        result.MeetsLimit = uCurrent <= uMax + 1e-9;

        // ── Gerekli yalıtım kalınlığı ────────────────────────────────────────
        // d_yal = λ_yal · (1/U_max − 1/U_mevcut).  U_mevcut ≤ U_max ise 0.
        if (el == ElementType.PencereKapi)
        {
            result.RequiredInsulationThicknessM = 0;
            if (!result.MeetsLimit)
                result.Notes.Add($"Pencere U={uCurrent:F2} > sınır {uMax:F2} — daha düşük U'lu (ör. çift/üçlü cam, düşük-e kaplama) ürün seçilmelidir.");
        }
        else if (!result.MeetsLimit)
        {
            double dIns = insulationLambda * (1.0 / uMax - 1.0 / uCurrent);
            result.RequiredInsulationThicknessM = Math.Max(0, dIns);
            result.Notes.Add(
                $"Mevcut U={uCurrent:F3} > TS 825 sınırı {uMax:F2} W/m²K. " +
                $"λ={insulationLambda:F3} W/mK yalıtım ile en az {dIns * 100:F1} cm eklenmelidir.");
        }
        else
        {
            result.RequiredInsulationThicknessM = 0;
            result.Notes.Add($"Mevcut U={uCurrent:F3} ≤ TS 825 sınırı {uMax:F2} W/m²K — yalıtım yeterli. Ek yalıtım gerekmez.");
        }

        // ── Isı kaybı ve yıllık enerji ───────────────────────────────────────
        double dT = indoorTempC - te;
        result.HeatLossW = uCurrent * areaM2 * dT;
        // E = U · A · DD · 24 / 1000  (derece-gün ısı ihtiyacını doğrudan içerir)
        result.AnnualEnergyKwh = uCurrent * areaM2 * dd * 24.0 / 1000.0;

        result.Notes.Add($"İklim bölgesi: {(int)zone}. Bölge ({ZoneCity(zone)}), dış tasarım sıcaklığı {te:F0} °C, DD={dd:F0} gün·K.");
        result.Notes.Add($"Isı kaybı Q = U·A·ΔT = {uCurrent:F3}·{areaM2:F1}·{dT:F0} = {result.HeatLossW:F0} W.");
        result.Notes.Add($"Yıllık ısıtma enerjisi E = U·A·DD·24/1000 = {result.AnnualEnergyKwh:F0} kWh/yıl.");

        return result;
    }
}
