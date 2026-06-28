using System;

namespace Afney.Cad.Mechanical.Services;

// TS EN 806 / VDI 2073 referans su fiziksel özellikleri
public static class WaterPropertiesService
{
    // Kinematik viskozite (m²/s) — sıcaklık bağımlı (Vogel denklemi interpolasyonu)
    // Kaynak: CRC Handbook of Chemistry and Physics, IAPWS-IF97
    private static readonly (double TempC, double NuM2s)[] _viscosityTable =
    {
        (  4, 1.568e-6),
        (  5, 1.519e-6),
        ( 10, 1.307e-6),
        ( 15, 1.139e-6),
        ( 20, 1.004e-6),
        ( 25, 0.893e-6),
        ( 30, 0.801e-6),
        ( 35, 0.724e-6),
        ( 40, 0.658e-6),
        ( 45, 0.602e-6),
        ( 50, 0.554e-6),
        ( 55, 0.511e-6),
        ( 60, 0.475e-6),
        ( 65, 0.443e-6),
        ( 70, 0.415e-6),
        ( 75, 0.390e-6),
        ( 80, 0.367e-6),
        ( 85, 0.347e-6),
        ( 90, 0.328e-6),
        ( 95, 0.311e-6),
    };

    public static double GetKinematicViscosity(double temperatureC)
    {
        temperatureC = Math.Clamp(temperatureC, 4, 95);

        for (int i = 0; i < _viscosityTable.Length - 1; i++)
        {
            var (t0, nu0) = _viscosityTable[i];
            var (t1, nu1) = _viscosityTable[i + 1];
            if (temperatureC >= t0 && temperatureC <= t1)
            {
                double ratio = (temperatureC - t0) / (t1 - t0);
                return nu0 + ratio * (nu1 - nu0);
            }
        }
        return 1.004e-6; // fallback 20°C
    }

    // Yoğunluk (kg/m³) — sıcaklık bağımlı (IFC 97 basitleştirilmiş)
    public static double GetDensity(double temperatureC)
    {
        temperatureC = Math.Clamp(temperatureC, 4, 95);
        return 1000.6 - 0.0128 * Math.Pow(temperatureC - 4.0, 2) + 0.000068 * Math.Pow(temperatureC - 4.0, 3);
    }

    // Dinamik viskozite (Pa·s)
    public static double GetDynamicViscosity(double temperatureC)
    {
        return GetKinematicViscosity(temperatureC) * GetDensity(temperatureC);
    }

    // Özgül ısı kapasitesi (J/kg·K) — sıcaklık bağımlı (basitleştirilmiş)
    public static double GetSpecificHeat(double temperatureC)
    {
        temperatureC = Math.Clamp(temperatureC, 4, 95);
        return 4217.0 - 2.0 * (temperatureC - 15.0) + 0.03 * Math.Pow(temperatureC - 15.0, 2);
    }

    // Prandtl sayısı
    public static double GetPrandtl(double temperatureC)
    {
        double cp = GetSpecificHeat(temperatureC);
        double mu = GetDynamicViscosity(temperatureC);
        double k = GetThermalConductivity(temperatureC);
        return cp * mu / k;
    }

    // Isıl iletkenlik (W/m·K)
    public static double GetThermalConductivity(double temperatureC)
    {
        temperatureC = Math.Clamp(temperatureC, 4, 95);
        return 0.569 + 0.0019 * temperatureC - 0.000008 * temperatureC * temperatureC;
    }
}
