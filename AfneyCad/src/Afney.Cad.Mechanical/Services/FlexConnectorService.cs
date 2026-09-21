using System;

namespace Afney.Cad.Mechanical.Services;

public static class FlexConnectorService
{
    // IMC 603.6.2: esnek hava bağlantı elemanı (flexible air connector) en fazla 14 ft. Esnek kanal (603.6.1) için sınır yoktur.
    public const double ImcMaxConnectorLengthMm = 4267.2;

    // Bazı yerel yönetmeliklerdeki daha muhafazakâr 5 ft sınırı.
    public const double StrictLocalLimitMm = 1524.0;

    // Tasarım varsayımı (tam gerilmiş esnek kanal); üretici verisi öncelikli.
    public const double DefaultRoughnessMm = 3.0;

    private const double AirDensity = 1.2;            // kg/m³
    private const double KinematicViscosity = 1.5e-5; // m²/s

    public record FlexCheckResult(bool IsCompliant, double LimitMm, string Message);

    public static FlexCheckResult CheckLength(double lengthMm, bool strictLocalLimit = false)
    {
        double limit = strictLocalLimit ? StrictLocalLimitMm : ImcMaxConnectorLengthMm;
        string source = strictLocalLimit ? "yerel sınır 5 ft" : "IMC 603.6.2 (14 ft)";

        if (lengthMm <= 0)
            return new FlexCheckResult(false, limit, "Bağlantı uzunluğu sıfırdan büyük olmalı.");

        return lengthMm <= limit
            ? new FlexCheckResult(true, limit, $"Uzunluk {lengthMm:F0} mm ≤ {limit:F0} mm — {source} uygun.")
            : new FlexCheckResult(false, limit,
                $"Uzunluk {lengthMm:F0} mm, {source} sınırı olan {limit:F0} mm'yi aşıyor. Bağlantıyı kısaltın veya sert kanala çevirin.");
    }

    public static double EstimatePressureDropPa(double diameterMm, double lengthMm, double airFlowM3h, double roughnessMm = DefaultRoughnessMm)
    {
        if (diameterMm <= 0 || lengthMm <= 0 || airFlowM3h <= 0) return 0;

        double d = diameterMm / 1000.0;
        double area = Math.PI * d * d / 4.0;
        double v = airFlowM3h / 3600.0 / area;
        double re = v * d / KinematicViscosity;
        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(re, roughnessMm, diameterMm);

        return f * (lengthMm / 1000.0 / d) * AirDensity * v * v / 2.0;
    }
}
