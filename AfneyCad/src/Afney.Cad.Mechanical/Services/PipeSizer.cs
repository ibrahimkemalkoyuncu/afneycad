using System;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Hidrolik Çap Tayini Servisi (PipeSizer)
    NEDEN: Mahallerden gelen toplam yük birimine (LU) göre, standartlara uygun (DIN 1988 / TS 1258) 
           hesap debisini bulmak ve bu debiyi taşıyacak en uygun boru çapını seçmek için.
    
    FORMÜLLER:
    1. Hesap Debisi (Q): Q = k * sqrt(Sum LU)  [l/s]
    2. Süreklilik Denklemi: Q = A * V => d = sqrt(4Q / (PI * V))
*/
public static class PipeSizer
{
    /*
        NE: Hesap Debisi Hesapla (CalculateDesignFlow)
        NEDEN: Anlık kullanım faktörünü (eş zamanlılık) hesaba katar.
        PARAMETRE: k = Bina tipi katsayısı (Konutlar için genelde 0.25)
    */
    public static double CalculateDesignFlow(double sumLoadUnits, double k = 0.25)
    {
        if (sumLoadUnits <= 0) return 0;
        
        // Q = k * √ΣLU
        return k * Math.Sqrt(sumLoadUnits); // Birim: l/s (Litre/Saniye)
    }

    /*
        NE: Teorik İç Çapı Bul (CalculateRequiredInnerDiameter)
        NEDEN: Verilen debi ve hedeflenen akış hızı için gereken minimum çapı bulmak için.
        PARAMETRE: targetVelocity = Hedef hız (m/s). Gürültü ve aşınma için genelde 1.5 - 2.0 m/s seçilir.
    */
    public static double CalculateRequiredInnerDiameter(double flowRateLs, double targetVelocity = 1.5)
    {
        if (flowRateLs <= 0) return 0;

        // l/s -> m³/s dönüşümü
        double q_m3s = flowRateLs / 1000.0;

        // d = √(4 * Q / (π * v))
        double d_meter = Math.Sqrt((4.0 * q_m3s) / (Math.PI * targetVelocity));

        return d_meter * 1000.0; // Birim: mm
    }

    /*
        NE: Standart Çapa Yuvarla (GetStandardSize)
        NEDEN: Teorik çapı piyasada bulunan hazır boru çaplarına (DN) yükseltmek için.
    */
    public static double GetStandardSize(double requiredInnerDiameterMm, Afney.Cad.Mechanical.Enums.PipeMaterial material)
    {
        // Katalogdan o malzeme için mevcut tüm dış çapları (DN) al
        var availableSizes = PipeCatalog.GetStandardDiameters(material);
        
        foreach (var dn in availableSizes)
        {
            // O DN çapının iç çapını bul
            double actualID = PipeCatalog.GetInnerDiameter(material, dn);
            
            // Eğer gerçek iç çap ihtiyacı karşılıyorsa, bu DN'i seç
            if (actualID >= requiredInnerDiameterMm)
                return dn;
        }

        return availableSizes.Last(); // En büyüğü seç
    }
    
    /*
        Deprecated: Eski metod (Geriye uyumluluk için, Generic malzeme varsayar)
    */
    public static int GetStandardSize(double innerDiameterMm)
    {
        return (int)GetStandardSize(innerDiameterMm, Enums.PipeMaterial.Generic);
    }
}
