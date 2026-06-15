using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Eşzamanlı Su Talebi Servisi (SimultaneousDemandService)
   NEDEN: DIN 1988-300 / TS EN 806-3 — armatür birimlerinden gerçek pik debinin
          hesabı. FINE MEP sadece toplam armatür sayar; eşzamanlılık katsayısı
          uygulamaz. Bu da büyük binalarda %40 aşırı borulamaya yol açar.

   YÖNTEMLERİ:
   A) DIN 1988-300 Olasılık modeli: Pd = Σ(qi × pi) + k×√Σ(qi²×pi×(1-pi))
   B) TS EN 806-3 Tablo B.1 LU tabanlı
   C) Hunter eğrisi (ABD referansı — karşılaştırmalı)
*/
public class SimultaneousDemandService
{
    // ── Armatür Tanımı ───────────────────────────────────────────────────────────

    public class Fixture
    {
        public string Name         { get; set; } = "";
        public int    Count        { get; set; } = 1;
        public double FlowRateLps  { get; set; }  // Tekil debi (L/s)
        public double UseProbability { get; set; }  // Kullanım olasılığı 0-1
        public int    LU            { get; set; }   // Armatür birimi (DIN 1988-300)
    }

    // ── DIN 1988-300 Standart Armatür Listesi ────────────────────────────────────

    public static readonly List<Fixture> StandardFixtures =
    [
        new() { Name="Lavabo",               FlowRateLps=0.07, UseProbability=0.02, LU=1  },
        new() { Name="Klozet (Sifon)",       FlowRateLps=0.13, UseProbability=0.02, LU=2  },
        new() { Name="Klozet (Basın.Dep.)",  FlowRateLps=1.50, UseProbability=0.01, LU=5  },
        new() { Name="Banyo Küveti",         FlowRateLps=0.15, UseProbability=0.02, LU=3  },
        new() { Name="Duş",                  FlowRateLps=0.15, UseProbability=0.03, LU=2  },
        new() { Name="Mutfak Eviyesi",       FlowRateLps=0.07, UseProbability=0.04, LU=1  },
        new() { Name="Bulaşık Makinesi",     FlowRateLps=0.10, UseProbability=0.03, LU=2  },
        new() { Name="Çamaşır Makinesi",     FlowRateLps=0.25, UseProbability=0.03, LU=3  },
        new() { Name="Bahçe Musluğu (DN15)", FlowRateLps=0.20, UseProbability=0.04, LU=3  },
        new() { Name="Bahçe Musluğu (DN20)", FlowRateLps=0.30, UseProbability=0.04, LU=4  },
        new() { Name="Ofis Lavabo",          FlowRateLps=0.07, UseProbability=0.04, LU=1  },
        new() { Name="Ofis WC (Sifon)",      FlowRateLps=0.13, UseProbability=0.05, LU=2  },
        new() { Name="Yemekhane Eviyesi",    FlowRateLps=0.15, UseProbability=0.10, LU=2  },
        new() { Name="Hastane Lavabo",       FlowRateLps=0.07, UseProbability=0.07, LU=1  },
        new() { Name="Hastane WC",           FlowRateLps=0.13, UseProbability=0.05, LU=2  },
        new() { Name="Temizlik Eviyesi",     FlowRateLps=0.15, UseProbability=0.10, LU=2  },
        new() { Name="Tuvalet (Otel Odası)", FlowRateLps=0.15, UseProbability=0.06, LU=3  },
    ];

    // ── Hesap Sonucu ─────────────────────────────────────────────────────────────

    public class DemandResult
    {
        public double PeakFlowDIN_Lps    { get; set; }   // DIN 1988-300 pik debi (L/s)
        public double PeakFlowEN806_Lps  { get; set; }   // EN 806-3 LU tabanlı (L/s)
        public double PeakFlowHunter_Lps { get; set; }   // Hunter eğrisi (L/s) — karş.
        public int    TotalLU            { get; set; }   // Toplam armatür birimi
        public double TotalMaxFlowLps    { get; set; }   // Tüm armatürler aynı anda çalışsa
        public double SimultaneityFactor { get; set; }   // Eşzamanlılık katsayısı
        public string PipeRecommendation { get; set; } = "";
        public List<string> Notes        { get; set; } = [];
    }

    // ── DIN 1988-300 Hesabı ──────────────────────────────────────────────────────

    public static DemandResult Calculate(List<(Fixture fixture, int count)> fixtureList, double k = 1.8)
    {
        // k: güvenlik faktörü — konut=1.8, büro=2.0, otel=2.5 (DIN Tablo 1)
        double sumQP = 0, sumQ2PP = 0;
        int totalLU  = 0;
        double sumMaxFlow = 0;

        foreach (var (f, cnt) in fixtureList)
        {
            double qi = f.FlowRateLps;
            double pi = f.UseProbability;
            int    n  = cnt;

            sumQP    += n * qi * pi;
            sumQ2PP  += n * qi * qi * pi * (1 - pi);
            totalLU  += n * f.LU;
            sumMaxFlow += n * qi;
        }

        double peakDIN = sumQP + k * Math.Sqrt(sumQ2PP);
        peakDIN = Math.Max(peakDIN, fixtureList.Count > 0 ? fixtureList[0].fixture.FlowRateLps : 0.07);

        double peakEN806  = EN806FromLU(totalLU);
        double peakHunter = HunterCurve(totalLU);

        double factor = sumMaxFlow > 0 ? peakDIN / sumMaxFlow : 1.0;

        var result = new DemandResult
        {
            PeakFlowDIN_Lps    = peakDIN,
            PeakFlowEN806_Lps  = peakEN806,
            PeakFlowHunter_Lps = peakHunter,
            TotalLU            = totalLU,
            TotalMaxFlowLps    = sumMaxFlow,
            SimultaneityFactor = factor,
            PipeRecommendation = PipeSizeFromFlow(peakDIN)
        };

        result.Notes.Add($"DIN 1988-300 k faktörü: {k} (konut≤1.8 / ofis≤2.0 / otel≤2.5)");
        result.Notes.Add($"Eşzamanlılık katsayısı: {factor:P0} — gerçek pik / toplam max");
        if (Math.Abs(peakDIN - peakEN806) / peakDIN > 0.25)
            result.Notes.Add($"DIN ve EN 806-3 sonuçları >%25 fark gösteriyor — iki yöntemi karşılaştırın.");
        return result;
    }

    // ── EN 806-3 LU → Debi (Tablo B.1 interpolasyon) ───────────────────────────

    private static double EN806FromLU(int lu)
    {
        // Tablo B.1 — LU ve karşılık gelen Qd (L/s)
        var table = new (int lu, double q)[]
        {
            (1,0.10),(2,0.15),(3,0.18),(5,0.23),(10,0.33),(15,0.41),
            (20,0.47),(30,0.58),(40,0.67),(50,0.75),(75,0.92),(100,1.06),
            (150,1.30),(200,1.50),(300,1.84),(500,2.37),(750,2.91),(1000,3.36)
        };
        if (lu <= table[0].lu) return table[0].q;
        for (int i = 1; i < table.Length; i++)
        {
            if (lu <= table[i].lu)
            {
                double t = (double)(lu - table[i - 1].lu) / (table[i].lu - table[i - 1].lu);
                return table[i - 1].q + t * (table[i].q - table[i - 1].q);
            }
        }
        return 3.36 + (lu - 1000) * 0.001;
    }

    // ── Hunter eğrisi (yaklaşık polinom) ─────────────────────────────────────────

    private static double HunterCurve(int lu)
    {
        if (lu <= 0)   return 0;
        if (lu <= 10)  return 0.18 + 0.012 * lu;
        if (lu <= 100) return 0.30 + 0.008 * lu;
        if (lu <= 500) return 0.80 + 0.004 * lu;
        return 2.80 + 0.002 * (lu - 500);
    }

    // ── Boru Boyutu Önerisi ──────────────────────────────────────────────────────

    private static string PipeSizeFromFlow(double qLps) =>
        qLps <= 0.15 ? "DN15 (½\")" :
        qLps <= 0.30 ? "DN20 (¾\")" :
        qLps <= 0.60 ? "DN25 (1\")" :
        qLps <= 1.10 ? "DN32 (1¼\")" :
        qLps <= 1.80 ? "DN40 (1½\")" :
        qLps <= 3.00 ? "DN50 (2\")" :
        qLps <= 5.00 ? "DN65"        :
                       "DN80+";
}
