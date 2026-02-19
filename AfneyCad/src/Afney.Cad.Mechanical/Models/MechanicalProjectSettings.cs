using Afney.Cad.Mechanical.Enums;
using System;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Mekanik Proje Ayarları (MechanicalProjectSettings)
    NEDEN: Tüm tesisat hesaplamalarında kullanılan global parametreleri (K faktörü, malzeme pürüzlülüğü, limit hızlar) tek bir noktadan yönetmek için.
    
    NASIL (Mühendislik Modu):
    1. TS 1258 ve DIN 1988 standartlarına uygun varsayılan değerleri taşır.
    2. Bina tipine göre Eş Zamanlılık Faktörü (K) değişimini destekler.
    3. Hız ve basınç kaybı limitleri burada tanımlanır, validasyon motoru bu değerleri referans alır.
*/
public class MechanicalProjectSettings
{
    // NE: Bina Tipi
    // NEDEN: Hesaplama katsayılarını (a, b, c) doğrudan etkiler.
    public BuildingType BuildingType { get; set; } = BuildingType.Residential;

    // Eş Zamanlılık Faktörü (K) - Diversity Factor
    // Konut: 0.5, Hastane/Okul: 0.7, Kamusal Alan/Endüstri: 1.0 (TS EN 12056-2)
    public double FrequencyFactor { get; set; } = 0.5;

    // Maksimum Akış Hızı (m/s)
    // Boru gürültüsü ve aşınmayı önlemek için limit değer.
    public double MaxVelocity { get; set; } = 2.0;

    // Minimum Akış Hızı (m/s)
    // Çökelmeyi önlemek için (Özellikle pis su hatlarında kritik).
    public double MinVelocity { get; set; } = 0.5;

    // Boru Pürüzlülük Katsayısı (Roughness - mm)
    // PP-R: 0.007, Çelik: 0.045
    public double PipeRoughness { get; set; } = 0.007;

    // Yerel Kayıp Katsayısı (Zeta) (%)
    // Fittingslerden kaynaklanan ek direnç payı.
    public double LocalLossAllowance { get; set; } = 0.3; // %30 varsayılan

    // NE: Minimum İşletme Basıncı (mSS)
    // NEDEN: Armatür ucunda (muslukta) akışın sağlıklı olabilmesi için gereken minimum basınç.
    // Konutlar için genelde 5 mSS (0.5 bar) veya 10 mSS (1 bar) kabul edilir.
    public double RequiredResidualPressure { get; set; } = 5.0; 

    // NE: Fabrika Ayarlarına Dön
    public static MechanicalProjectSettings CreateDefault() => new MechanicalProjectSettings();
}
