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

        // Su yoğunluğu ve viskozite (sıcaklığa göre)
        double density = 998.0;  // kg/m³ (20°C için basitleştirme)
        double viscosity = 0.001; // Pa·s (20°C için)

        // Reynolds sayısı
        double reynolds = (density * velocity * diameterM) / viscosity;

        // Sürtünme faktörü (Colebrook-White - basitleştirilmiş)
        double roughness = RoughnessCoefficients.GetValueOrDefault(material, 0.045) / 1000.0; // mm -> m
        double relativeRoughness = roughness / diameterM;
        
        double frictionFactor;

        // 1. LAMINAR AKIŞ (Re < 2300)
        // Hagen-Poiseuille Yasası: f = 64/Re (Analitik ve kesindir)
        if (reynolds < 2300)
        {
            frictionFactor = 64.0 / reynolds;
        }
        // 2. GEÇİŞ BÖLGESİ (2300 <= Re <= 4000)
        // Kritik Bölge: Akış kararsızdır. Mühendislik güvenlik payı için Churchill denklemi veya enterpolasyon kullanılır.
        else if (reynolds <= 4000)
        {
            // Basit lineer enterpolasyon yerine, güvenli tarafta kalmak için türbülanslı başlangıç değerine yakınsaması sağlanır.
            // Bu aralıkta kesin bir formül yoktur, ancak tasarımda risk almamak için yüksek katsayı tercih edilir.
            double fLaminar = 64.0 / 2300.0;
            // Re=4000 için Colebrook-White tahmini (~0.04)
            double fTurbulent = 0.04; 
            double t = (reynolds - 2300) / (4000 - 2300);
            frictionFactor = fLaminar + t * (fTurbulent - fLaminar);
        }
        // 3. TÜRBÜLANSLI AKIŞ (Re > 4000)
        // Colebrook-White Denklemi (İteratif Çözüm)
        // 1 / sqrt(f) = -2 * log10( (ε/D)/3.7 + 2.51 / (Re * sqrt(f)) )
        else
        {
            // Başlangıç tahmini (Swamee-Jain ile iyi bir başlangıç noktası seçelim)
            double fInitial = 0.25 / Math.Pow(Math.Log10(relativeRoughness / 3.7 + 5.74 / Math.Pow(reynolds, 0.9)), 2);
            
            frictionFactor = fInitial;
            double tolerance = 1e-6;
            int maxIter = 50;

            for (int i = 0; i < maxIter; i++)
            {
                // Colebrook: 1/√f = -2 * log((ε/D)/3.7 + 2.51/(Re * √f))
                double term = -2.0 * Math.Log10((relativeRoughness / 3.7) + (2.51 / (reynolds * Math.Sqrt(frictionFactor))));
                double fNew = 1.0 / (term * term); // f = 1 / term^2
                
                if (Math.Abs(fNew - frictionFactor) < tolerance)
                {
                    frictionFactor = fNew;
                    break;
                }
                frictionFactor = fNew;
            }
        }

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
