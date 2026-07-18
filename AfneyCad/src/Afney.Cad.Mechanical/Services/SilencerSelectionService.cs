using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Susturucu Seçim Servisi (SilencerSelectionService)
   NEDEN: FINE MEP'te olduğu gibi, susturucu (silencer) seçimi mühendisin dışarıdan bakıp elle
          girdiği bir işti. Bu servis, FanSelectionService'in aynı deseniyle Systemair/Halton/
          Trox/Lindab katalog verilerini içerir; debi+kanal boyutu+hedef zayıflama verildiğinde
          uygun susturucuyu filtreler.

   NASIL (Mühendislik Detayı):
   - Her susturucu, 8 oktav bantta (63..8000 Hz) ayrı bir Insertion Loss (dB) değerine sahiptir
     (ASHRAE Handbook HVAC Applications Ch. 48 / ISO 7235 test yöntemi).
   - AcousticAnalysisService.AnalyzeSystem'deki SilencerAttenuationDb girdisini, seçilen
     susturucunun ilgili oktav banttaki (genelde 500 Hz veya en kritik bant) IL değeriyle
     doldurmak için kullanılır — böylece gürültü bütçesi (noise budget) döngüsü kapanır.
   - Basınç kaybı (PressureDropPa), kanal statik basınç hesabına dahil edilmesi gereken ek
     bir dirençtir (susturucular tipik 400-800 Pa @ nominal debide).
*/
public class SilencerSelectionService
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    public enum SilencerType
    {
        Rectangular,    // Dikdörtgen kesitli, splitter'lı (baffle) susturucu
        Circular,       // Dairesel kesitli (dış gövde + iç absorbe tüp)
        Cellular        // Hücreli (yüksek performans, uzun boy)
    }

    public enum SilencerManufacturer { Systemair, Halton, Trox, Lindab }

    // ── Susturucu Modeli ─────────────────────────────────────────────────────────

    public class SilencerModel
    {
        public string ModelName { get; set; } = "";
        public SilencerManufacturer Manufacturer { get; set; }
        public SilencerType Type { get; set; }
        public string ConnectionMM { get; set; } = "";     // Bağlantı ebadı (DxH veya Ø)
        public double LengthMm { get; set; }                // Susturucu boyu
        public double MaxFlowM3h { get; set; }               // Nominal maks debi (m³/h)
        public double PressureDropPa { get; set; }           // Nominal debide basınç kaybı (Pa)

        // NE: Oktav bant ekleme kaybı (Insertion Loss, dB) — 63,125,250,500,1000,2000,4000,8000 Hz
        public double[] InsertionLossDb { get; set; } = new double[8];

        public string Application { get; set; } = "";
        public double PriceEur { get; set; }
    }

    public static readonly int[] OctaveBands = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };

    public class SilencerSelectionResult
    {
        public SilencerModel Silencer { get; set; } = null!;
        public double FlowMarginPct { get; set; }
        public double InsertionLossAtCriticalBandDb { get; set; }
        public int CriticalBandHz { get; set; }
    }

    // ── Susturucu Kataloğu ───────────────────────────────────────────────────────

    public static readonly List<SilencerModel> SilencerCatalog =
    [
        // ──── Systemair — Dikdörtgen Baffle Susturucu (Duct Silencer Serisi) ────
        new() {
            ModelName = "DSA 400x400-900", Manufacturer = SilencerManufacturer.Systemair, Type = SilencerType.Rectangular,
            ConnectionMM = "400x400", LengthMm = 900, MaxFlowM3h = 3500, PressureDropPa = 55,
            InsertionLossDb = new double[] { 6, 12, 22, 35, 38, 30, 18, 10 },
            Application = "Ofis/AVM tavan içi ana kanal", PriceEur = 280
        },
        new() {
            ModelName = "DSA 600x400-1200", Manufacturer = SilencerManufacturer.Systemair, Type = SilencerType.Rectangular,
            ConnectionMM = "600x400", LengthMm = 1200, MaxFlowM3h = 6200, PressureDropPa = 65,
            InsertionLossDb = new double[] { 8, 15, 28, 42, 45, 36, 22, 12 },
            Application = "Büyük AHU çıkışı, yüksek zayıflama gereksinimi", PriceEur = 460
        },

        // ──── Systemair — Dairesel Susturucu (CircularSilencer Serisi) ─────────
        new() {
            ModelName = "CS 160-600", Manufacturer = SilencerManufacturer.Systemair, Type = SilencerType.Circular,
            ConnectionMM = "DN160", LengthMm = 600, MaxFlowM3h = 500, PressureDropPa = 35,
            InsertionLossDb = new double[] { 3, 6, 12, 20, 24, 18, 10, 5 },
            Application = "Konut/ofis branşman hattı, DN160", PriceEur = 95
        },
        new() {
            ModelName = "CS 250-900", Manufacturer = SilencerManufacturer.Systemair, Type = SilencerType.Circular,
            ConnectionMM = "DN250", LengthMm = 900, MaxFlowM3h = 1400, PressureDropPa = 45,
            InsertionLossDb = new double[] { 4, 9, 18, 28, 32, 24, 14, 7 },
            Application = "Orta kapasiteli dairesel kanal hattı", PriceEur = 165
        },
        new() {
            ModelName = "CS 315-1200", Manufacturer = SilencerManufacturer.Systemair, Type = SilencerType.Circular,
            ConnectionMM = "DN315", LengthMm = 1200, MaxFlowM3h = 2400, PressureDropPa = 50,
            InsertionLossDb = new double[] { 5, 11, 21, 33, 36, 27, 16, 8 },
            Application = "Ana toplama hattı, dairesel", PriceEur = 240
        },

        // ──── Halton — Hücreli Yüksek Performans Susturucu (Cellular Serisi) ────
        new() {
            ModelName = "HCS 500x500-1800", Manufacturer = SilencerManufacturer.Halton, Type = SilencerType.Cellular,
            ConnectionMM = "500x500", LengthMm = 1800, MaxFlowM3h = 5000, PressureDropPa = 90,
            InsertionLossDb = new double[] { 10, 20, 36, 52, 55, 44, 28, 15 },
            Application = "Hastane/konser salonu — düşük NC hedefi (NC-20/25)", PriceEur = 780
        },

        // ──── Trox — Dikdörtgen Splitter Susturucu (MSA Serisi) ────────────────
        new() {
            ModelName = "MSA 300x300-600", Manufacturer = SilencerManufacturer.Trox, Type = SilencerType.Rectangular,
            ConnectionMM = "300x300", LengthMm = 600, MaxFlowM3h = 1800, PressureDropPa = 40,
            InsertionLossDb = new double[] { 4, 8, 16, 26, 29, 22, 13, 7 },
            Application = "Küçük ofis/mağaza şubesi", PriceEur = 165
        },
        new() {
            ModelName = "MSA 500x300-1200", Manufacturer = SilencerManufacturer.Trox, Type = SilencerType.Rectangular,
            ConnectionMM = "500x300", LengthMm = 1200, MaxFlowM3h = 3800, PressureDropPa = 58,
            InsertionLossDb = new double[] { 7, 14, 25, 38, 41, 32, 19, 10 },
            Application = "Restoran/toplantı odası besleme hattı", PriceEur = 320
        },

        // ──── Lindab — Dairesel Kompakt Susturucu (CSR Serisi) ─────────────────
        new() {
            ModelName = "CSR 125-500", Manufacturer = SilencerManufacturer.Lindab, Type = SilencerType.Circular,
            ConnectionMM = "DN125", LengthMm = 500, MaxFlowM3h = 300, PressureDropPa = 28,
            InsertionLossDb = new double[] { 2, 5, 10, 16, 19, 14, 8, 4 },
            Application = "WC/banyo egzoz branşmanı, DN125", PriceEur = 60
        },
    ];

    // ── Susturucu Arama ──────────────────────────────────────────────────────────

    public static List<SilencerSelectionResult> FindSilencers(
        double flowM3h, double targetInsertionLossDb, int criticalBandHz = 500,
        SilencerType? type = null, SilencerManufacturer? manufacturer = null,
        double safetyFlow = 1.10)
    {
        double reqFlow = flowM3h * safetyFlow;
        int bandIndex = Array.IndexOf(OctaveBands, criticalBandHz);
        if (bandIndex < 0) bandIndex = 3; // 500 Hz varsayılan

        var filtered = SilencerCatalog
            .Where(s => s.MaxFlowM3h >= reqFlow)
            .Where(s => type == null || s.Type == type)
            .Where(s => manufacturer == null || s.Manufacturer == manufacturer)
            .Where(s => s.InsertionLossDb[bandIndex] >= targetInsertionLossDb)
            .OrderBy(s => s.LengthMm)
            .ThenBy(s => s.PressureDropPa)
            .ToList();

        return filtered.Select(s => new SilencerSelectionResult
        {
            Silencer = s,
            FlowMarginPct = (s.MaxFlowM3h / reqFlow - 1) * 100,
            InsertionLossAtCriticalBandDb = s.InsertionLossDb[bandIndex],
            CriticalBandHz = criticalBandHz
        }).ToList();
    }

    public static SilencerSelectionResult? BestSilencer(double flowM3h, double targetInsertionLossDb, int criticalBandHz = 500) =>
        FindSilencers(flowM3h, targetInsertionLossDb, criticalBandHz).FirstOrDefault();

    /*
       NE: Oktav Bant Bazında Toplam Gürültü Bütçesi Kapatma (ApplyToNoiseBudget)
       NEDEN: AcousticAnalysisService.AnalyzeSystem tek bir SilencerInsertionLossDb (skaler)
              kabul eder; bu yardımcı, seçilen susturucunun kritik banttaki IL değerini
              doğrudan o girdiye eşlemek için kullanılır — döngüyü kapatır.
    */
    public static double ApplyToNoiseBudget(SilencerModel silencer, int criticalBandHz = 500)
    {
        int bandIndex = Array.IndexOf(OctaveBands, criticalBandHz);
        if (bandIndex < 0) bandIndex = 3;
        return silencer.InsertionLossDb[bandIndex];
    }
}
