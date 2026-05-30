using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Su Sayacı Seçim Servisi (WaterMeterService)
   NEDEN: TS EN 14154 kapsamında nominal debiye göre DN ve kayıp basınç hesabı yapmak için.

   HESAP MANTIĞI:
   - Qnom (nominal debi) → DB'deki toplam LU üzerinden Walther formülü ile hesaplanır.
   - Seçilen sayaç DN'sine göre Δp = k × Q² formülüyle kayıp basınç hesaplanır.
   - Sayaç DN katalog değerleri: DN15/20/25/32/40/50 (TS EN 14154).
*/
public class WaterMeterService
{
    private readonly CadDatabase _database;

    public WaterMeterService(CadDatabase database) { _database = database; }

    public class MeterResult
    {
        public double PeakFlowLs    { get; set; }
        public double PeakFlowM3h   { get; set; }
        public int    RecommendedDN { get; set; }
        public double PressureLossM { get; set; }   // mSS
        public string MeterModel    { get; set; } = "";
        public List<MeterOption> Options { get; set; } = [];
    }

    public class MeterOption
    {
        public int    DN             { get; set; }
        public double QnomM3h        { get; set; }
        public double QmaxM3h        { get; set; }
        public double PressureLossM  { get; set; }
        public bool   Suitable       { get; set; }
    }

    // TS EN 14154 — sayaç Kv değerleri (yaklaşık): Δp [mSS] = (Q/Kv)²
    private static readonly (int DN, double Qnom, double Qmax, double Kv)[] Catalog =
    [
        (15, 1.5, 3.0, 1.4),
        (20, 2.5, 5.0, 2.0),
        (25, 3.5, 7.0, 3.0),
        (32, 6.0, 12.0, 5.2),
        (40, 10.0, 20.0, 8.5),
        (50, 16.0, 32.0, 14.0)
    ];

    public MeterResult Calculate()
    {
        double totalLU = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .Sum(f => f.LoadUnits);

        double peakLs = totalLU > 0 ? WaltherFlow(totalLU) : 0.5;
        double peakM3h = peakLs * 3.6;

        var result = new MeterResult { PeakFlowLs = Math.Round(peakLs, 3), PeakFlowM3h = Math.Round(peakM3h, 2) };

        int recommended = 0;
        double recommendedDp = 0;

        foreach (var (dn, qnom, qmax, kv) in Catalog)
        {
            double dp = Math.Pow(peakM3h / kv, 2); // mSS
            bool suitable = peakM3h <= qmax && dp <= 10.0; // ≤10 mSS kayıp şartı (TS EN)
            result.Options.Add(new MeterOption { DN = dn, QnomM3h = qnom, QmaxM3h = qmax, PressureLossM = Math.Round(dp, 2), Suitable = suitable });

            if (suitable && recommended == 0) { recommended = dn; recommendedDp = dp; }
        }

        result.RecommendedDN = recommended > 0 ? recommended : 50;
        result.PressureLossM = Math.Round(recommendedDp, 2);
        result.MeterModel    = $"DN {result.RecommendedDN} Woltmann / Kombine sayaç (TS EN 14154)";
        return result;
    }

    private static double WaltherFlow(double sumLU)
    {
        double q = 0.682 * Math.Pow(sumLU, 0.45) - 0.14;
        return Math.Max(q, 0.1);
    }
}
