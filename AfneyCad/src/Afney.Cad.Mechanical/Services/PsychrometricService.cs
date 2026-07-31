using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

// ASHRAE Fundamentals — Psikrometrik Hesaplar
// Referans: ASHRAE Handbook Fundamentals 2021, Ch. 1 Psychrometrics
public static class PsychrometricService
{
    private const double Patm = 101325.0; // Pa (deniz seviyesi)

    /*
       NE: Doyma Buhar Basıncı (Pa) — Sıvı: ASHRAE Hyland-Wexler; Buz: WMO/Magnus
       NEDEN: Buz dalı (0°C altı) daha önce yanlış sabitler taşıyordu — ör. -10°C'de
              gerçek değer ~260 Pa olması gerekirken (buhar tablosu/WMO CIMO Guide
              referansı) ~12.877 Pa (yaklaşık 50 KAT fazla) üretiyordu; bu, GERÇEK
              kullanımda hiç fark edilmeden duran, "10/10" etiketli bir hesap
              motorunda bulunan sessiz-yanlış-sonuç hatasıydı (bkz. denetim raporu —
              bağımsız buhar tablosu referans testleri eklenirken -10°C'de yakalandı).
              Buz dalı artık WMO/Magnus formülüyle (0°C'de sıvı dalıyla süreklilik
              sağlayan, -80°C'ye kadar geçerli, endüstri standardı) değiştirildi.
    */
    public static double SaturationPressure(double tempC)
    {
        if (tempC >= 0)
        {
            double T = tempC + 273.15;
            return Math.Exp(77.3450 + 0.0057 * T - 7235.0 / T) / Math.Pow(T, 8.2);
        }
        // Buz üzeri (0°C altı) — WMO/Magnus formülü (611.15 Pa × e^(22.452·Tc / (272.55+Tc)))
        return 611.15 * Math.Exp(22.452 * tempC / (272.55 + tempC));
    }

    // Nem oranı (kg_su/kg_kuru_hava)
    public static double HumidityRatio(double tempC, double relativeHumidity)
    {
        double pws = SaturationPressure(tempC);
        double pw = relativeHumidity * pws;
        return 0.62198 * pw / (Patm - pw);
    }

    // Entalpi (kJ/kg kuru hava)
    public static double Enthalpy(double tempC, double humidityRatio)
    {
        return 1.006 * tempC + humidityRatio * (2501.0 + 1.86 * tempC);
    }

    // Yaş termometre sıcaklığı (°C) — iteratif
    public static double WetBulbTemperature(double tempC, double relativeHumidity)
    {
        double w = HumidityRatio(tempC, relativeHumidity);
        double h = Enthalpy(tempC, w);

        double twLow = -20, twHigh = tempC;
        for (int i = 0; i < 50; i++)
        {
            double tw = (twLow + twHigh) / 2.0;
            double ws = HumidityRatio(tw, 1.0);
            double hs = Enthalpy(tw, ws);
            double hCalc = hs - (ws - w) * 4186.0 * tw / 2501000.0;
            // Yaklaşık: h ≈ h_s - (w_s - w) * (2501 - 2.326*tw)
            double hTest = 1.006 * tw + ws * (2501 + 1.86 * tw);
            if (hTest > h) twHigh = tw;
            else twLow = tw;
        }
        return (twLow + twHigh) / 2.0;
    }

    // Çiğ noktası sıcaklığı (°C)
    public static double DewPointTemperature(double tempC, double relativeHumidity)
    {
        double pw = relativeHumidity * SaturationPressure(tempC);
        if (pw <= 0) return -50;
        double alpha = Math.Log(pw / 610.78);
        return 237.3 * alpha / (17.269 - alpha);
    }

    // Özgül hacim (m³/kg kuru hava)
    public static double SpecificVolume(double tempC, double humidityRatio)
    {
        double T = tempC + 273.15;
        return 0.2871 * T * (1 + 1.6078 * humidityRatio) / (Patm / 1000.0);
    }

    // Yoğunluk (kg/m³)
    public static double AirDensity(double tempC, double relativeHumidity)
    {
        double w = HumidityRatio(tempC, relativeHumidity);
        double v = SpecificVolume(tempC, w);
        return (1 + w) / v;
    }

    // Tam durum noktası hesabı
    public static PsychrometricState CalculateState(double tempC, double relativeHumidity)
    {
        double w = HumidityRatio(tempC, relativeHumidity);
        return new PsychrometricState
        {
            DryBulbC = tempC,
            RelativeHumidity = relativeHumidity,
            HumidityRatio = w,
            EnthalpyKJkg = Enthalpy(tempC, w),
            WetBulbC = WetBulbTemperature(tempC, relativeHumidity),
            DewPointC = DewPointTemperature(tempC, relativeHumidity),
            SpecificVolumeM3kg = SpecificVolume(tempC, w),
            DensityKgM3 = AirDensity(tempC, relativeHumidity)
        };
    }

    // Karışım hesabı (iki hava akımı)
    public static PsychrometricState MixAirStreams(
        PsychrometricState air1, double massFlow1,
        PsychrometricState air2, double massFlow2)
    {
        double totalFlow = massFlow1 + massFlow2;
        if (totalFlow <= 0) return air1;

        double tMix = (air1.DryBulbC * massFlow1 + air2.DryBulbC * massFlow2) / totalFlow;
        double wMix = (air1.HumidityRatio * massFlow1 + air2.HumidityRatio * massFlow2) / totalFlow;

        double pws = SaturationPressure(tMix);
        double pw = wMix * Patm / (0.62198 + wMix);
        double rhMix = Math.Min(pw / pws, 1.0);

        return CalculateState(tMix, rhMix);
    }

    // Isıtma/Soğutma prosesi (sensible)
    public static PsychrometricState SensibleProcess(PsychrometricState inlet, double targetTempC)
    {
        return CalculateState(targetTempC, inlet.RelativeHumidity * SaturationPressure(inlet.DryBulbC) / SaturationPressure(targetTempC));
    }
}

public class PsychrometricState
{
    public double DryBulbC { get; set; }
    public double RelativeHumidity { get; set; }
    public double HumidityRatio { get; set; }
    public double EnthalpyKJkg { get; set; }
    public double WetBulbC { get; set; }
    public double DewPointC { get; set; }
    public double SpecificVolumeM3kg { get; set; }
    public double DensityKgM3 { get; set; }
}
