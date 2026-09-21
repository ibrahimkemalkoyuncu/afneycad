using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   VAV/CAV terminal kutusu ön seçimi. Kutu performansı (Δp, NC) AHRI 880 ile derecelendirilir ve üreticiye
   özeldir; bu servis giriş çapı, minimum debi kuralı ve reheat yükünü verir.
*/
public static class VavCavBoxService
{
    public enum BoxKind { Cav, Vav }

    // ASHRAE 90.1 (G3.1.3.13 / 6.5.2.1): VAV minimum debi = max(zirve debinin %30'u, min. taze hava, yönetmelik minimumu).
    public const double MinFlowFractionOfPeak = 0.30;

    // EN 1506 / ISO 13350 tercihli yuvarlak kanal çapları (mm).
    public static readonly double[] StandardRoundDiametersMm = { 100, 125, 160, 200, 250, 315, 400, 500, 630 };

    private const double AirDensity = 1.2; // kg/m³
    private const double AirCp = 1.006;    // kJ/(kg·K)

    public class BoxResult
    {
        public BoxKind Kind { get; set; }
        public double MaxFlowM3h { get; set; }
        public double MinFlowM3h { get; set; }
        public double TurndownRatio { get; set; }
        public double InletDiameterMm { get; set; }
        public double InletVelocityAtMaxMs { get; set; }
        public double InletVelocityAtMinMs { get; set; }
        public double ReheatCapacityKw { get; set; }
        public bool IsValid { get; set; } = true;
        public List<string> Warnings { get; } = new();
    }

    // maxInletVelocityMs: tipik tasarım varsayımı — gürültü (NC) sınırı üreticinin AHRI 880 tablosuyla doğrulanmalı.
    public static BoxResult Select(
        BoxKind kind, double designMaxFlowM3h,
        double minOutdoorAirM3h = 0, double codeMinFlowM3h = 0, double? overrideMinFlowM3h = null,
        double maxInletVelocityMs = 8.0,
        double? coldSupplyC = null, double? reheatDischargeC = null)
    {
        var r = new BoxResult { Kind = kind, MaxFlowM3h = designMaxFlowM3h };

        if (designMaxFlowM3h <= 0 || maxInletVelocityMs <= 0)
        {
            r.IsValid = false;
            r.Warnings.Add("Girdi geçersiz: maksimum debi ve giriş hızı sınırı sıfırdan büyük olmalı.");
            return r;
        }

        if (kind == BoxKind.Cav)
        {
            r.MinFlowM3h = designMaxFlowM3h;
        }
        else
        {
            double rule = Math.Max(MinFlowFractionOfPeak * designMaxFlowM3h, Math.Max(minOutdoorAirM3h, codeMinFlowM3h));
            r.MinFlowM3h = overrideMinFlowM3h ?? rule;

            if (r.MinFlowM3h > designMaxFlowM3h)
            {
                r.IsValid = false;
                r.Warnings.Add("Minimum debi maksimum debiden büyük olamaz.");
                return r;
            }
            if (overrideMinFlowM3h.HasValue && overrideMinFlowM3h.Value + 1e-9 < rule)
                r.Warnings.Add($"Verilen minimum debi ({overrideMinFlowM3h.Value:F0} m³/h) kural değerinin ({rule:F0} m³/h) altında — reheat ile eşzamanlı ısıtma/soğutma ASHRAE 90.1 6.5.2.1 kapsamında kısıtlıdır.");
        }
        r.TurndownRatio = r.MinFlowM3h / designMaxFlowM3h;

        r.InletDiameterMm = SmallestStandardDiameterMm(designMaxFlowM3h, maxInletVelocityMs);
        r.InletVelocityAtMaxMs = VelocityMs(designMaxFlowM3h, r.InletDiameterMm);
        r.InletVelocityAtMinMs = VelocityMs(r.MinFlowM3h, r.InletDiameterMm);
        if (r.InletVelocityAtMaxMs > maxInletVelocityMs + 1e-9)
            r.Warnings.Add($"En büyük standart çap ({r.InletDiameterMm:F0} mm) bile {maxInletVelocityMs:F1} m/s sınırını aşıyor — kutuyu bölün.");

        if (coldSupplyC.HasValue && reheatDischargeC.HasValue)
        {
            if (reheatDischargeC.Value <= coldSupplyC.Value)
                r.Warnings.Add("Reheat çıkış sıcaklığı soğuk besleme sıcaklığından yüksek olmalı.");
            else
                r.ReheatCapacityKw = r.MinFlowM3h / 3600.0 * AirDensity * AirCp * (reheatDischargeC.Value - coldSupplyC.Value);
        }
        return r;
    }

    public static double SmallestStandardDiameterMm(double flowM3h, double maxVelocityMs)
    {
        foreach (double d in StandardRoundDiametersMm)
            if (VelocityMs(flowM3h, d) <= maxVelocityMs) return d;
        return StandardRoundDiametersMm[^1];
    }

    public static double VelocityMs(double flowM3h, double diameterMm)
    {
        double d = diameterMm / 1000.0;
        return d > 0 ? flowM3h / 3600.0 / (Math.PI * d * d / 4.0) : 0;
    }
}
