using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: DIN 1988-300 Sıhhi Tesisat Standardı Servisi
   NEDEN: Almanya / AB projelerinde DIN 1988-300 zorunlu. FINE MEP'te yoktur.
          Armatür birimi yöntemi (LU/TU) ile boru boyutlandırma, hız kontrolü,
          eşzamanlılık katsayısı hesabı.

   YÖNTEMİ:
   - Tüm armatür birimlerini (LU) topla
   - Pik debi: Qd = tablodan LU'ya karşılık
   - Boru: v ≤ 2.5 m/s (soğuk su) / 2.0 m/s (sıcak su) — DN seçim
   - Düşey besleme borusu: Qd stacking faktörü ile
*/
public class DIN1988300Service
{
    // ── Armatür Birimleri Tablosu (DIN 1988-300 Tablo 2) ────────────────────────

    public record FixtureUnit(string Name, int ColdLU, int HotLU, double QnLps, string Standard)
    {
        public int TotalLU => ColdLU + HotLU;
    }

    public static readonly List<FixtureUnit> FixtureTable =
    [
        new("Lavabo (DN15)",              1, 1, 0.07, "DIN 1988-300 T2"),
        new("Lavabo (DN20)",              2, 2, 0.10, "DIN 1988-300 T2"),
        new("Klozet sifon (DN15)",        2, 0, 0.13, "DIN 1988-300 T2"),
        new("Klozet basınç deposu",       5, 0, 1.50, "DIN 1988-300 T2"),
        new("Pisuvar (sifon)",            1, 0, 0.07, "DIN 1988-300 T2"),
        new("Duş (DN15)",                 2, 2, 0.15, "DIN 1988-300 T2"),
        new("Küvet (DN15)",               2, 2, 0.15, "DIN 1988-300 T2"),
        new("Mutfak eviyesi (DN15)",      1, 1, 0.07, "DIN 1988-300 T2"),
        new("Bulaşık makinesi (DN15)",    1, 1, 0.10, "DIN 1988-300 T2"),
        new("Çamaşır makinesi (DN15)",    2, 1, 0.25, "DIN 1988-300 T2"),
        new("Bahçe musluğu (DN15)",       3, 0, 0.20, "DIN 1988-300 T2"),
        new("Bahçe musluğu (DN20)",       4, 0, 0.30, "DIN 1988-300 T2"),
        new("Temizlik eviyesi (DN15)",    2, 2, 0.15, "DIN 1988-300 T2"),
        new("Yemekhane eviyesi (DN20)",   3, 3, 0.20, "DIN 1988-300 T2"),
        new("Endüstriyel duş (DN20)",     3, 3, 0.20, "DIN 1988-300 T2"),
        new("Hastane lavabo (DN15)",      1, 1, 0.07, "DIN 1988-300 T2"),
        new("Ofis lavabo (DN15)",         1, 1, 0.07, "DIN 1988-300 T2"),
        new("Sprinkler başlık (15mm)",    1, 0, 0.05, "DIN 1988-300 T2"),
    ];

    // ── Debi Tablosu LU → Qd (DIN 1988-300 Tablo 3) ─────────────────────────────

    private static readonly (int lu, double qdLps)[] QdTable =
    [
        (1,0.07),(2,0.10),(3,0.13),(4,0.15),(5,0.17),(6,0.19),(8,0.22),(10,0.25),
        (15,0.30),(20,0.35),(25,0.40),(30,0.44),(40,0.52),(50,0.59),(60,0.65),
        (70,0.71),(80,0.76),(90,0.81),(100,0.85),(120,0.93),(150,1.03),(200,1.18),
        (250,1.30),(300,1.41),(400,1.60),(500,1.76),(750,2.15),(1000,2.48),
        (1500,3.03),(2000,3.50),(3000,4.28),(5000,5.52)
    ];

    public static double GetQdFromLU(int totalLU)
    {
        if (totalLU <= 0) return 0;
        if (totalLU <= QdTable[0].lu) return QdTable[0].qdLps;
        for (int i = 1; i < QdTable.Length; i++)
        {
            if (totalLU <= QdTable[i].lu)
            {
                double t = (double)(totalLU - QdTable[i-1].lu) / (QdTable[i].lu - QdTable[i-1].lu);
                return QdTable[i-1].qdLps + t * (QdTable[i].qdLps - QdTable[i-1].qdLps);
            }
        }
        return 5.52 + (totalLU - 5000) * 0.0005;
    }

    // ── Boru Boyutu (Hız ≤ 2.0 m/s) ─────────────────────────────────────────────

    public static string SelectPipeDN(double qdLps, bool hotWater = false)
    {
        double vMax = hotWater ? 2.0 : 2.5;
        // A = Q/v → d = √(4A/π) = √(4Q/(π×v))
        double aM2  = qdLps / vMax;
        double dM   = Math.Sqrt(4.0 * aM2 / Math.PI);
        double dMm  = dM * 1000;

        // Standart DN serileri iç çap yaklaşımları
        if (dMm <= 10) return "DN10";
        if (dMm <= 12) return "DN12";
        if (dMm <= 15) return "DN15";
        if (dMm <= 20) return "DN20";
        if (dMm <= 25) return "DN25";
        if (dMm <= 32) return "DN32";
        if (dMm <= 40) return "DN40";
        if (dMm <= 50) return "DN50";
        if (dMm <= 65) return "DN65";
        if (dMm <= 80) return "DN80";
        if (dMm <= 100) return "DN100";
        return "DN125+";
    }

    // ── Girdi / Sonuç ────────────────────────────────────────────────────────────

    public class DIN1988Input
    {
        public List<(FixtureUnit fixture, int count)> Fixtures { get; set; } = [];
        public bool IsHotWater { get; set; } = false;
        public string BuildingType { get; set; } = "Konut";
    }

    public class DIN1988Result
    {
        public int    TotalColdLU  { get; set; }
        public int    TotalHotLU   { get; set; }
        public double QdColdLps    { get; set; }
        public double QdHotLps     { get; set; }
        public string ColdPipeDN   { get; set; } = "";
        public string HotPipeDN    { get; set; } = "";
        public double TotalMaxFlow { get; set; }  // Teorik max (tüm armatür birden)
        public double SimFactor    { get; set; }  // Eşzamanlılık katsayısı
        public List<string> Notes  { get; set; } = [];
    }

    public static DIN1988Result Calculate(DIN1988Input inp)
    {
        int coldLU = 0, hotLU = 0;
        double maxFlow = 0;
        foreach (var (f, cnt) in inp.Fixtures)
        {
            coldLU  += f.ColdLU * cnt;
            hotLU   += f.HotLU  * cnt;
            maxFlow += f.QnLps  * cnt;
        }

        double qdCold = GetQdFromLU(coldLU);
        double qdHot  = GetQdFromLU(hotLU);

        var result = new DIN1988Result
        {
            TotalColdLU  = coldLU,
            TotalHotLU   = hotLU,
            QdColdLps    = qdCold,
            QdHotLps     = qdHot,
            ColdPipeDN   = SelectPipeDN(qdCold, false),
            HotPipeDN    = SelectPipeDN(qdHot,  true),
            TotalMaxFlow = maxFlow,
            SimFactor    = maxFlow > 0 ? qdCold / maxFlow : 1.0
        };

        result.Notes.Add($"Soğuk su: {coldLU} LU → Qd={qdCold:F3} L/s → {result.ColdPipeDN}");
        result.Notes.Add($"Sıcak su: {hotLU} LU → Qd={qdHot:F3} L/s → {result.HotPipeDN}");
        result.Notes.Add($"Eşzamanlılık katsayısı: {result.SimFactor:P0}");

        if (coldLU > 1000)
            result.Notes.Add("⚠ Büyük bina: düşey besleme borusunu kat gruplarına bölerek hesaplayın.");

        return result;
    }
}
