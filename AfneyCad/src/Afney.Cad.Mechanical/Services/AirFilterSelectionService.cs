using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   EN ISO 16890 sınıflı filtre seçimi — Eurovent Rec. 4/23 (4. baskı, 2022) Tablo 3 ve Bölüm 4.6/5.
   ODA: dış hava kalitesi (WHO 2021 eşiklerine göre), SUP: besleme havası kategorisi.
*/
public static class AirFilterSelectionService
{
    public enum OutdoorAirCategory { Oda1 = 1, Oda2 = 2, Oda3 = 3 }

    public enum SupplyAirCategory { Sup1 = 1, Sup2 = 2, Sup3 = 3, Sup4 = 4, Sup5 = 5 }

    public enum PmFraction { ePM1, ePM2_5, ePM10 }

    public record FilterRequirement(PmFraction Fraction, int MinEfficiencyPct, bool FinalStageOnly);

    // Bölüm 4.6: dış hava girişindeki ilk kademe ≥ ePM10 %50; nemlendirici sonrası filtre ≥ ePM2,5 %65.
    public const int MinFirstStageEpm10Pct = 50;
    public const int MinPostHumidifierEpm25Pct = 65;

    private static readonly PmFraction[] ColumnFraction =
        { PmFraction.ePM1, PmFraction.ePM1, PmFraction.ePM2_5, PmFraction.ePM10, PmFraction.ePM10 };

    // Satır: ODA1..3, sütun: SUP1..5 (Tablo 3).
    private static readonly int[,] MinEfficiency =
    {
        { 70, 50, 50, 50, 50 },
        { 80, 70, 70, 80, 50 },
        { 90, 80, 80, 90, 80 }
    };

    public static FilterRequirement GetRecommendedMinimum(OutdoorAirCategory oda, SupplyAirCategory sup)
    {
        int r = (int)oda - 1, c = (int)sup - 1;
        int pct = MinEfficiency[r, c];
        var fraction = ColumnFraction[c];

        // Tablo 3 dipnotları: SUP1/SUP2'de ePM1 %50 ve SUP3'te ePM2,5 %50 son filtre kademesi için geçerli.
        bool finalStageOnly = pct == 50 && (c <= 2);
        return new FilterRequirement(fraction, pct, finalStageOnly);
    }

    // Bölüm 5: çok kademeli birikimli verim, ePMx,cum = 100·(1 − Π(1 − ePMx,i/100)).
    public static double CumulativeEfficiencyPct(IEnumerable<double> stageEfficiencyPct)
    {
        double passing = 1.0;
        foreach (double e in stageEfficiencyPct)
            passing *= 1.0 - Math.Clamp(e, 0, 100) / 100.0;
        return 100.0 * (1.0 - passing);
    }

    public static bool MeetsRequirement(FilterRequirement requirement, double achievedEfficiencyPct)
        => achievedEfficiencyPct + 1e-9 >= requirement.MinEfficiencyPct;

    public static double RequiredFaceAreaM2(double airFlowM3h, double faceVelocityMs)
        => faceVelocityMs > 0 ? airFlowM3h / 3600.0 / faceVelocityMs : 0;

    public static double FaceVelocityMs(double airFlowM3h, double faceAreaM2)
        => faceAreaM2 > 0 ? airFlowM3h / 3600.0 / faceAreaM2 : 0;

    // Filtre direncinin fan gücüne etkisi: P = Q·Δp/η.
    public static double FanPowerForPressureDropW(double airFlowM3h, double deltaPPa, double fanEfficiency)
        => fanEfficiency > 0 ? airFlowM3h / 3600.0 * deltaPPa / fanEfficiency : 0;

    public static string FormatRequirement(FilterRequirement r)
    {
        string frac = r.Fraction switch
        {
            PmFraction.ePM1 => "ePM1",
            PmFraction.ePM2_5 => "ePM2,5",
            _ => "ePM10"
        };
        return $"{frac} ≥ %{r.MinEfficiencyPct}" + (r.FinalStageOnly ? " (son filtre kademesi)" : "");
    }
}
