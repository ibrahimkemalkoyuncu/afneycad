using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Sprinkler Hesap Servisi (NFPA13SprinklerService)
   NEDEN: NFPA 13 (Amerikan) sprinkler sistemi tasarımı — yoğunluk/alan tablosu imperial
          NFPA 13 kaynağından metriğe çevrilmiştir (bkz. `HazardClass` altındaki değerler).
          FINE MEP yalnızca boru çizimi yapar; yoğunluk/alan metodu yoktur.
          Yangın departmanı onayı için hesap raporu zorunlu.

   ⚠ NOT — İKİ AYRI STANDART, İKİ AYRI SERVİS: `FireFightingService.cs` da benzer bir
   sprinkler tasarım hesabı içerir ama farklı bir standardı (EN 12845 — Avrupa) uygular;
   iki servisin `HazardClass` enum'ları AYNI İSİMLERİ (LightHazard/OrdinaryHazard...)
   taşısa da FARKLI sayısal değerler üretir (biri NFPA13, diğeri EN12845 kaynaklı). Bu
   kasıtlı bir tasarım değil, iki ayrı oturumda bağımsız eklenmiş servislerin isim
   çakışmasıdır (bir web araştırma ajanı tarafından tespit edildi). Hangi projede hangi
   standardın geçerli olduğuna göre DOĞRU servisi seçin — ikisini karıştırmayın.

   HESAP YÖNTEMİ:
   - Tehlike Sınıfı → Tasarım Yoğunluğu + Tasarım Alanı
   - Sprinkler debisi: q = K × √P (K-faktörü, P bar cinsinden basınç)
   - Pik alan: en uzak N sprinkler birlikte çalışır
   - Boru boyutlandırma: Hazen-Williams Q = 0.2785 × C × d^2.63 × S^0.54
*/
public class NFPA13SprinklerService
{
    // ── Tehlike Sınıfları ─────────────────────────────────────────────────────────

    public enum HazardClass
    {
        LightHazard,          // Hafif tehlike — ofis, konut, otel odaları
        OrdinaryHazard1,      // Orta tehlike 1 — depolar, otoparklar
        OrdinaryHazard2,      // Orta tehlike 2 — üretim, depolama
        ExtraHazard1,         // Yüksek tehlike 1 — boyama, yangın riski yüksek
        ExtraHazard2,         // Yüksek tehlike 2 — yanıcı sıvı deposu
        EarlySuppressionFastResponse  // ESFR — yüksek raf depolama
    }

    // ── Sprinkler Tipi ────────────────────────────────────────────────────────────

    public enum SprinklerType
    {
        Upright,        // Dik (yukarı atan)
        Pendent,        // Sarkık (aşağı atan) — en yaygın
        Sidewall,       // Yan duvar tipi
        Extended,       // Uzatılmış kapsama — büyük hacimler
        ESFR            // Erken baskılama hızlı tepki
    }

    // ── Sprinkler Girdisi ─────────────────────────────────────────────────────────

    public class SprinklerInput
    {
        public HazardClass  Hazard          { get; set; } = HazardClass.OrdinaryHazard1;
        public SprinklerType Type           { get; set; } = SprinklerType.Pendent;
        public double       AreaM2          { get; set; }   // Korunan alan (m²)
        public double       KFactor         { get; set; } = 80;    // K-faktörü (L/dak/bar^0.5) — standart: 80
        public double       MaxCoverageM2   { get; set; } = 12;    // Sprinkler başına maks alan
        public double       MinPressureBar  { get; set; } = 0.70;  // Min çalışma basıncı
        public double       StaticPressureBar { get; set; } = 7.0; // Şebeke statik basıncı
        public bool         DryPipe         { get; set; } = false; // Kuru sistem mi?
        public string       BuildingType    { get; set; } = "Ofis";
    }

    // ── Hesap Sonucu ─────────────────────────────────────────────────────────────

    public class SprinklerResult
    {
        public double DesignDensityLpmdpm2  { get; set; }  // Tasarım yoğunluğu L/(dak·m²)
        public double DesignAreaM2          { get; set; }  // Tasarım alanı (m²)
        public int    ActiveSprinklerCount  { get; set; }  // Eş zamanlı çalışan sprinkler sayısı
        public double SprinklerFlowLpd      { get; set; }  // Tek sprinkler debisi (L/dak)
        public double TotalDesignFlowLpd    { get; set; }  // Toplam tasarım debisi (L/dak)
        public double TotalDesignFlowM3h    { get; set; }  // m³/sa
        public int    TotalSprinklerCount   { get; set; }  // Toplam sprinkler sayısı
        public double MinPressureBarRequired { get; set; } // Gereken min basınç
        public double ResidualPressureBar   { get; set; }  // Artık basınç
        public string SupplyPipeSize        { get; set; } = ""; // Ana besleme borusu DN
        public string BranchPipeSize        { get; set; } = ""; // Dal borusu DN
        public List<string> Compliance      { get; set; } = [];
        public List<string> Warnings        { get; set; } = [];
    }

    // ── NFPA 13 Tasarım Parametreleri ────────────────────────────────────────────

    private static (double densityLpmdpm2, double areaM2) GetDesignParams(HazardClass h) => h switch
    {
        HazardClass.LightHazard       => (4.1,  139),
        HazardClass.OrdinaryHazard1   => (6.1,  139),
        HazardClass.OrdinaryHazard2   => (8.2,  139),
        HazardClass.ExtraHazard1      => (12.2, 232),
        HazardClass.ExtraHazard2      => (16.3, 186),
        HazardClass.EarlySuppressionFastResponse => (20.4, 92),
        _ => (6.1, 139)
    };

    // ── Hesap ─────────────────────────────────────────────────────────────────────

    public static SprinklerResult Calculate(SprinklerInput inp)
    {
        var result = new SprinklerResult();
        var (density, designArea) = GetDesignParams(inp.Hazard);
        result.DesignDensityLpmdpm2 = density;
        result.DesignAreaM2 = designArea;

        // Sprinkler sayısı
        result.TotalSprinklerCount = (int)Math.Ceiling(inp.AreaM2 / inp.MaxCoverageM2);
        result.ActiveSprinklerCount = (int)Math.Ceiling(designArea / inp.MaxCoverageM2);

        // Tek sprinkler debisi: q = density × coverage (L/dak)
        result.SprinklerFlowLpd = density * inp.MaxCoverageM2;

        // Gerekli basınç: P = (q/K)² → q'yı K-faktörü ile kontrol et
        double pRequired = Math.Pow(result.SprinklerFlowLpd / inp.KFactor, 2);
        result.MinPressureBarRequired = Math.Max(pRequired, inp.MinPressureBar);

        // Toplam tasarım debisi
        result.TotalDesignFlowLpd = result.SprinklerFlowLpd * result.ActiveSprinklerCount;
        result.TotalDesignFlowM3h = result.TotalDesignFlowLpd * 60.0 / 1000.0;

        // Boru boyutu — Hazen-Williams yaklaşımı (C=120 paslanmaz, S=0.001)
        // Q = 0.2785 × C × d^2.63 × S^0.54  →  d = (Q / (0.2785×C×S^0.54))^(1/2.63)
        double qLps = result.TotalDesignFlowLpd / 60.0;
        result.SupplyPipeSize = PipeSizeHW(qLps, 0.001);
        result.BranchPipeSize = PipeSizeHW(result.SprinklerFlowLpd / 60.0 * 4, 0.005); // 4 sprinkler/dal

        result.ResidualPressureBar = inp.StaticPressureBar - result.MinPressureBarRequired - 0.5; // 0.5 bar kayıp varsayımı

        // Uyumluluk kontrolleri
        if (result.ResidualPressureBar >= 0.7)
            result.Compliance.Add($"✓ Artık basınç {result.ResidualPressureBar:F2} bar ≥ 0.70 bar — yeterli");
        else
            result.Warnings.Add($"⚠ Artık basınç {result.ResidualPressureBar:F2} bar < 0.70 bar — pompa gerekli");

        if (inp.MaxCoverageM2 <= MaxCoverageForHazard(inp.Hazard))
            result.Compliance.Add($"✓ Sprinkler başına kapsama {inp.MaxCoverageM2} m² ≤ limit {MaxCoverageForHazard(inp.Hazard)} m²");
        else
            result.Warnings.Add($"⚠ Kapsama alanı {inp.MaxCoverageM2} m² > NFPA 13 limiti {MaxCoverageForHazard(inp.Hazard)} m²");

        if (inp.DryPipe)
            result.Warnings.Add("Kuru sistem: su dolum süresi ≤ 60 sn olmalı (NFPA 13 §7.3) — hava kompresörü boyutlandırın.");

        return result;
    }

    private static double MaxCoverageForHazard(HazardClass h) => h switch
    {
        HazardClass.LightHazard     => 18.6,
        HazardClass.OrdinaryHazard1 => 12.1,
        HazardClass.OrdinaryHazard2 => 12.1,
        HazardClass.ExtraHazard1    => 9.3,
        HazardClass.ExtraHazard2    => 9.3,
        _                           => 9.3
    };

    // Hazen-Williams ters hesabı — L/s → DN
    private static string PipeSizeHW(double qLps, double slope)
    {
        // d^2.63 = Q / (0.2785 × 120 × S^0.54)
        double denominator = 0.2785 * 120 * Math.Pow(slope, 0.54);
        double d2_63 = qLps / denominator;
        double dM = Math.Pow(d2_63, 1.0 / 2.63);
        double dMm = dM * 1000;
        return dMm < 25 ? "DN25" : dMm < 32 ? "DN32" : dMm < 40 ? "DN40" :
               dMm < 50 ? "DN50" : dMm < 65 ? "DN65" : dMm < 80 ? "DN80" :
               dMm < 100 ? "DN100" : "DN125+";
    }

    // ── Tehlike Sınıfı Açıklamaları ──────────────────────────────────────────────

    public static string HazardDescription(HazardClass h) => h switch
    {
        HazardClass.LightHazard       => "Hafif — Ofis, konut, otel odası, kilise, okul sınıfı",
        HazardClass.OrdinaryHazard1   => "Orta-1 — Otopark, kantin, laundry, fırıncılık",
        HazardClass.OrdinaryHazard2   => "Orta-2 — Kuru temizleme, kütüphane, imalat",
        HazardClass.ExtraHazard1      => "Yüksek-1 — Mobilya üretimi, kauçuk işleme",
        HazardClass.ExtraHazard2      => "Yüksek-2 — Yağ bazlı boya üretimi, lak",
        HazardClass.EarlySuppressionFastResponse => "ESFR — Yüksek raf depolama (>7.5m palet)",
        _ => ""
    };
}
