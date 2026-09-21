using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   Su beslemeli ısıtma/soğutma bobini ön seçimi (yük, su debisi, yüz alanı). Sıra sayısı/kanat aralığı
   üretici seçim yazılımı işidir (AHRI 410 derecelendirmesi); bu servis fiziksel enerji dengesini ve
   yüz hızı kontrolünü verir.
*/
public static class AirCoilSelectionService
{
    private const double AirDensity = 1.2;      // kg/m³ (ısıtma, standart hava)
    private const double AirCp = 1.006;         // kJ/(kg·K)
    private const double WaterCp = 4.186;       // kJ/(kg·K)
    private const double WaterDensity = 1000.0; // kg/m³

    // ASHRAE (Fundamentals 2021 / Systems & Equipment 2020): soğutma bobini 2,0–2,5 m/s, nem taşınmasını önlemek için ≤ 2,8 m/s.
    public const double CoolingFaceVelocityMinMs = 2.0;
    public const double CoolingFaceVelocityMaxRecommendedMs = 2.5;
    public const double CoolingFaceVelocityLimitMs = 2.8;

    // Isıtma bobininde nem taşınması yok; tipik tasarım aralığı 3,0–4,0 m/s (ACCA Manual D).
    public const double HeatingFaceVelocityMinMs = 3.0;
    public const double HeatingFaceVelocityMaxMs = 4.0;

    public class CoilResult
    {
        public double CapacityKw { get; set; }
        public double SensibleKw { get; set; }
        public double LatentKw { get; set; }
        public double WaterFlowLps { get; set; }
        public double FaceAreaM2 { get; set; }
        public double FaceVelocityMs { get; set; }
        public double CondensateKgPerH { get; set; }
        public List<string> Warnings { get; } = new();
        public bool IsValid { get; set; } = true;
    }

    public static CoilResult SelectHeating(
        double airFlowM3h, double airInC, double airOutC, double waterInC, double waterOutC,
        double targetFaceVelocityMs = 3.0)
    {
        var r = new CoilResult();

        if (airFlowM3h <= 0 || airOutC <= airInC || waterInC <= waterOutC)
        {
            r.IsValid = false;
            r.Warnings.Add("Girdi geçersiz: debi > 0, çıkış havası > giriş havası ve su gidiş > dönüş olmalı.");
            return r;
        }
        if (waterInC <= airOutC)
            r.Warnings.Add($"Su gidiş sıcaklığı ({waterInC:F0} °C) hedef hava çıkışının ({airOutC:F0} °C) üzerinde olmalı.");

        double massAir = airFlowM3h / 3600.0 * AirDensity;
        r.CapacityKw = massAir * AirCp * (airOutC - airInC);
        r.SensibleKw = r.CapacityKw;
        FillWaterAndFace(r, airFlowM3h, waterInC - waterOutC, targetFaceVelocityMs);

        if (r.FaceVelocityMs < HeatingFaceVelocityMinMs || r.FaceVelocityMs > HeatingFaceVelocityMaxMs)
            r.Warnings.Add($"Yüz hızı {r.FaceVelocityMs:F2} m/s, tipik ısıtma aralığı 3,0–4,0 m/s dışında.");
        return r;
    }

    // rh değerleri 0–1 arası oran.
    public static CoilResult SelectCooling(
        double airFlowM3h, double airInC, double airInRh, double airOutC, double airOutRh,
        double waterInC, double waterOutC, double targetFaceVelocityMs = 2.5)
    {
        var r = new CoilResult();

        if (airFlowM3h <= 0 || airOutC >= airInC || waterOutC <= waterInC)
        {
            r.IsValid = false;
            r.Warnings.Add("Girdi geçersiz: debi > 0, çıkış havası < giriş havası ve su dönüş > gidiş olmalı.");
            return r;
        }

        var inState = PsychrometricService.CalculateState(airInC, airInRh);
        var outState = PsychrometricService.CalculateState(airOutC, airOutRh);

        if (outState.HumidityRatio > inState.HumidityRatio + 1e-9)
        {
            r.IsValid = false;
            r.Warnings.Add("Çıkış nem oranı giriş nem oranından yüksek — soğutma bobini nem ekleyemez.");
            return r;
        }

        double massDryAir = airFlowM3h / 3600.0 / inState.SpecificVolumeM3kg;

        // Toplam yük: giriş→çıkış entalpi farkı; duyulur kısım: nem oranı sabit (çıkış W) tutularak Tgiriş→Tçıkış.
        double hSensibleStart = PsychrometricService.Enthalpy(airInC, outState.HumidityRatio);
        r.CapacityKw = massDryAir * (inState.EnthalpyKJkg - outState.EnthalpyKJkg);
        r.SensibleKw = massDryAir * (hSensibleStart - outState.EnthalpyKJkg);
        r.LatentKw = r.CapacityKw - r.SensibleKw;
        r.CondensateKgPerH = massDryAir * (inState.HumidityRatio - outState.HumidityRatio) * 3600.0;
        FillWaterAndFace(r, airFlowM3h, waterOutC - waterInC, targetFaceVelocityMs);

        if (r.LatentKw > 1e-6 && waterInC >= inState.DewPointC)
            r.Warnings.Add($"Nem alınıyor ama su gidiş sıcaklığı ({waterInC:F1} °C) giriş çiy noktasının ({inState.DewPointC:F1} °C) altında değil.");
        if (r.FaceVelocityMs > CoolingFaceVelocityLimitMs)
            r.Warnings.Add($"Yüz hızı {r.FaceVelocityMs:F2} m/s > {CoolingFaceVelocityLimitMs:F1} m/s — kondens damlacık taşınması riski (ASHRAE).");
        else if (r.FaceVelocityMs > CoolingFaceVelocityMaxRecommendedMs)
            r.Warnings.Add($"Yüz hızı {r.FaceVelocityMs:F2} m/s, önerilen 2,0–2,5 m/s üstünde (sınır 2,8 m/s).");
        else if (r.FaceVelocityMs < CoolingFaceVelocityMinMs)
            r.Warnings.Add($"Yüz hızı {r.FaceVelocityMs:F2} m/s, önerilen 2,0–2,5 m/s altında (bobin gereğinden büyük).");
        return r;
    }

    private static void FillWaterAndFace(CoilResult r, double airFlowM3h, double waterDeltaT, double targetFaceVelocityMs)
    {
        double waterKgS = r.CapacityKw / (WaterCp * waterDeltaT);
        r.WaterFlowLps = waterKgS / WaterDensity * 1000.0;
        r.FaceVelocityMs = targetFaceVelocityMs;
        r.FaceAreaM2 = targetFaceVelocityMs > 0 ? airFlowM3h / 3600.0 / targetFaceVelocityMs : 0;
    }

    public static double FaceVelocityMs(double airFlowM3h, double faceAreaM2)
        => faceAreaM2 > 0 ? airFlowM3h / 3600.0 / faceAreaM2 : 0;
}
