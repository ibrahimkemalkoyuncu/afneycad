namespace Afney.Cad.Mechanical.Services;

/*
NE:
Mekanik tesisat hesaplama servisleri.

NE İÇİN:
Numan Bey'in domain bilgisi - boru boyutlandırma, basınç kaybı, akış hesaplamaları.

NEREDE:
Mechanical Module - Application/Services katmanı.

NE ZAMAN:
Kullanıcı otomatik boyutlandırma veya doğrulama yaptığında.

AMAÇ:
ISO/TS/DIN standartlarına uygun mekanik hesaplamalar yapmak.
*/
public class MechanicalCalculations
{
    // Pürüzlülük katsayıları (Darcy-Weisbach için)
    private static readonly Dictionary<string, double> RoughnessCoefficients = new()
    {
        { "Steel", 0.045 },      // mm
        { "Copper", 0.0015 },    // mm
        { "PVC", 0.0015 },       // mm
        { "PE", 0.007 }          // mm
    };

    /*
    METOD ADI:
    CalculatePressureDrop

    AMACI:
    Darcy-Weisbach denklemi ile basınç kaybını hesaplamak.

    GİRDİLER:
    - length: Boru uzunluğu (m)
    - diameter: İç çap (mm)
    - flowRate: Debi (m³/h)
    - material: Malzeme tipi
    - temperature: Sıcaklık (°C)

    ÇIKTILAR:
    double - Basınç kaybı (bar).

    KULLANIM SENARYOSU:
    Sistem tasarımında pompa seçimi.

    PERFORMANS NOTU:
    İterativ Reynolds hesabı içerir, O(log n).
    */
    public static double CalculatePressureDrop(
        double length, 
        double diameter, 
        double flowRate, 
        string material, 
        double temperature)
    {
        if (diameter <= 0 || flowRate <= 0 || length <= 0)
            return 0;

        // Parametreleri SI birimine çevir
        double diameterM = diameter / 1000.0;  // mm -> m
        double flowRateM3S = flowRate / 3600.0; // m³/h -> m³/s

        // Hız hesabı
        double area = Math.PI * Math.Pow(diameterM / 2, 2);
        double velocity = flowRateM3S / area;

        // Su yoğunluğu ve viskozite — sıcaklığa GERÇEKTEN bağımlı (IAPWS-IF97, WaterPropertiesService).
        // NOT: Önceden burada sabit 20°C değerleri (998.0 kg/m³, 0.001 Pa·s) kullanılıyordu ve
        // `temperature` parametresi hesaba hiç katılmıyordu (bkz. denetim raporu). WaterPropertiesService
        // 20°C'de bu sabitlere çok yakın (~997.6 kg/m³, ~0.0010 Pa·s) sonuç verdiği için mevcut testler
        // (20°C varsayımıyla yazılmış) regresyon vermeden geçmeye devam eder.
        double density = WaterPropertiesService.GetDensity(temperature);
        double viscosity = WaterPropertiesService.GetDynamicViscosity(temperature);

        // Reynolds sayısı
        double reynolds = (density * velocity * diameterM) / viscosity;

        // Sürtünme faktörü (Colebrook-White)
        //
        // NE/NEDEN — GERÇEK KOD TEKRARI (denetim raporunda "belgede Newton-Raphson/10 iterasyon
        // yanlış tanımlanmış" olarak işaretlenmişti — araştırma bunun aslında İKİ AYRI Colebrook-White
        // implementasyonu olduğunu ortaya çıkardı): Bu metod kendi basit fixed-point (Picard) iterasyonunu
        // (doğrusal yakınsama, 50 adıma kadar) kullanıyordu; `AdvancedHydraulicsService.ColebrookWhiteFriction`
        // ise (PressureDropService'in kullandığı) GERÇEK Newton-Raphson'du (ikinci dereceden yakınsama,
        // türevli düzeltme, 10 iterasyonda 1e-8 hassasiyete ulaşıyor). Aynı fiziği iki farklı kalitede
        // çözen iki kod parçası tutmak yerine, HardyCrossSolver'ın da (bu metodun tek çağıranı) aynı
        // doğrulanmış Newton-Raphson çözücüsünü kullanması sağlandı — sonuç aynı denklemi çözdüğü için
        // pratikte aynı sayısal değeri üretir (ikisi de aynı toleransta yakınsıyor), ama artık TEK bir
        // doğrulanmış implementasyon var.
        double roughnessMm = RoughnessCoefficients.GetValueOrDefault(material, 0.045);

        double frictionFactor = AdvancedHydraulicsService.ColebrookWhiteFriction(reynolds, roughnessMm, diameter);

        // Darcy-Weisbach: ΔP = f * (L/D) * (ρ*v²/2)
        double pressureLossPa = frictionFactor * (length / diameterM) * (density * velocity * velocity / 2.0);
        
        // Pa -> bar
        return pressureLossPa / 100000.0;
    }

    /*
    METOD ADI:
    OptimizePipeDiameter

    AMACI:
    Ekonomik boru çapını hesaplamak (hız 0.5 - 2.0 m/s arasında).

    GİRDİLER:
    - flowRate: Debi (m³/h)

    ÇIKTILAR:
    double - Önerilen çap (mm).

    KULLANIM SENARYOSU:
    Kullanıcı "otomatik boyutlandır" dediğinde.

    PERFORMANS NOTU:
    O(1) - Direkt formül.
    */
    public static double OptimizePipeDiameter(double flowRate)
    {
        // Hedef hız: 1.5 m/s (optimum)
        const double targetVelocity = 1.5;

        double flowRateM3S = flowRate / 3600.0;
        
        // A = Q / v  →  πr² = Q/v  →  r = sqrt(Q/(π*v))
        double radiusM = Math.Sqrt(flowRateM3S / (Math.PI * targetVelocity));
        double diameterMm = radiusM * 2.0 * 1000.0;

        // Standart boru çaplarına yuvarla (ISO)
        double[] standardDiameters = { 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300 };
        
        return standardDiameters.FirstOrDefault(d => d >= diameterMm, standardDiameters.Last());
    }

    /*
    METOD ADI:
    ValidateFlow

    AMACI:
    Akış hızının güvenli sınırlarda olduğunu kontrol etmek.

    GİRDİLER:
    - velocity: Akış hızı (m/s)

    ÇIKTILAR:
    - (isValid, errorMessage)

    KULLANIM SENARYOSU:
    Kullanıcı uyarıları (hız >2 m/s ise uyar).

    PERFORMANS NOTU:
    O(1) - Basit karşılaştırma.
    */
    public static (bool IsValid, string ErrorMessage) ValidateFlow(double velocity)
    {
        const double minVelocity = 0.3; // m/s
        const double maxVelocity = 2.0; // m/s

        if (velocity < minVelocity)
            return (false, $"Hız çok düşük ({velocity:F2} m/s). Minimum {minVelocity} m/s olmalı.");
        
        if (velocity > maxVelocity)
            return (false, $"Hız çok yüksek ({velocity:F2} m/s). Maksimum {maxVelocity} m/s olmalı.");

        return (true, string.Empty);
    }
}
