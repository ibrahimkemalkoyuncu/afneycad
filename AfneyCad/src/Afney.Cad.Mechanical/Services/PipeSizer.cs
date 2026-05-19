using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Hidrolik Çap Tayini Servisi (PipeSizer)
    NEDEN: Mahallerden gelen toplam yük birimine (LU) göre, standartlara uygun (DIN 1988 / TS 1258
           / ASPE / BS 6700 / ASHRAE / TS EN 806) hesap debisini bulmak ve bu debiyi taşıyacak
           en uygun boru çapını seçmek için.

    DESTEKLENEn STANDARTLAR:
      - TS 1258 / DIN 1988 — Türkiye ve Almanya (K=0.25-0.7, LU bazlı)
      - TS EN 806-3         — Avrupa (K=0.5 konut, 0.7 ticari)
      - ASPE               — Amerika (WSFUs, Hunter Curve)
      - BS 6700            — İngiltere (LU, K=0.2 konut, 0.5 ticari)
      - ASHRAE 90.1        — Enerji verimliliği odaklı

    FORMÜLLER:
    1. Hesap Debisi (Q): Q = k * sqrt(Sum LU)   [l/s]
    2. Süreklilik Denklemi: Q = A * V => d = sqrt(4Q / (PI * V))
    3. ASPE Hunter Curve: ters-log eğri fit (WSFU bazlı)
*/
public static class PipeSizer
{
    // ── STANDART ENUMERATİON ────────────────────────────────────────────────────

    public enum PlumbingStandard
    {
        TS1258_DIN1988,   // Türkiye / Almanya — varsayılan
        TSEN806_3,        // Avrupa (EN 806 Part 3)
        ASPE_Hunter,      // Amerika (ASPE Hunter Curve, WSFU)
        BS6700,           // İngiltere
        ASHRAE_90_1       // Enerji verimliliği (aynı formül, farklı K)
    }

    public enum BuildingCategory
    {
        Residential,      // Konut / Daire
        Commercial,       // Ofis / Ticari
        Hotel,            // Otel / Hastane (yüksek eş zamanlılık)
        Industrial        // Endüstriyel / İşyeri
    }

    // ── STANDART KATSAYI TABLOSU ───────────────────────────────────────────────

    private static readonly Dictionary<(PlumbingStandard, BuildingCategory), double> KFactors = new()
    {
        // TS 1258 / DIN 1988
        { (PlumbingStandard.TS1258_DIN1988, BuildingCategory.Residential),  0.25 },
        { (PlumbingStandard.TS1258_DIN1988, BuildingCategory.Commercial),   0.50 },
        { (PlumbingStandard.TS1258_DIN1988, BuildingCategory.Hotel),        0.70 },
        { (PlumbingStandard.TS1258_DIN1988, BuildingCategory.Industrial),   1.00 },

        // EN 806-3
        { (PlumbingStandard.TSEN806_3, BuildingCategory.Residential),       0.50 },
        { (PlumbingStandard.TSEN806_3, BuildingCategory.Commercial),        0.70 },
        { (PlumbingStandard.TSEN806_3, BuildingCategory.Hotel),             1.00 },
        { (PlumbingStandard.TSEN806_3, BuildingCategory.Industrial),        1.00 },

        // BS 6700
        { (PlumbingStandard.BS6700, BuildingCategory.Residential),          0.20 },
        { (PlumbingStandard.BS6700, BuildingCategory.Commercial),           0.50 },
        { (PlumbingStandard.BS6700, BuildingCategory.Hotel),                0.70 },
        { (PlumbingStandard.BS6700, BuildingCategory.Industrial),           1.00 },

        // ASHRAE 90.1 (benzer EN806 ama enerji odaklı kısıtlamalar)
        { (PlumbingStandard.ASHRAE_90_1, BuildingCategory.Residential),     0.45 },
        { (PlumbingStandard.ASHRAE_90_1, BuildingCategory.Commercial),      0.65 },
        { (PlumbingStandard.ASHRAE_90_1, BuildingCategory.Hotel),           0.90 },
        { (PlumbingStandard.ASHRAE_90_1, BuildingCategory.Industrial),      1.00 },
    };

    // Maksimum önerilen akış hızı (m/s) — standarda göre
    // (maxSupplyVelocity, maxReturnVelocity) — named via Item1/Item2
    private static readonly Dictionary<PlumbingStandard, (double MaxSupply, double MaxReturn)> VelocityLimits = new()
    {
        { PlumbingStandard.TS1258_DIN1988, (2.0, 1.5) },
        { PlumbingStandard.TSEN806_3,      (2.0, 1.5) },
        { PlumbingStandard.ASPE_Hunter,    (2.4, 1.8) },
        { PlumbingStandard.BS6700,         (1.5, 1.0) },
        { PlumbingStandard.ASHRAE_90_1,    (2.0, 1.5) },
    };

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
        NE: Standarda Göre Hesap Debisi (CalculateDesignFlowByStandard)
        NEDEN: Farklı ülke normlarına uygun debi hesabı — K katsayısı otomatik seçilir.
    */
    public static double CalculateDesignFlowByStandard(
        double sumLoadUnits,
        PlumbingStandard standard = PlumbingStandard.TS1258_DIN1988,
        BuildingCategory category = BuildingCategory.Residential)
    {
        if (sumLoadUnits <= 0) return 0;

        if (standard == PlumbingStandard.ASPE_Hunter)
            return HunterCurveLookup(sumLoadUnits);

        double k = KFactors.TryGetValue((standard, category), out var kf) ? kf : 0.25;
        return k * Math.Sqrt(sumLoadUnits);
    }

    /*
        NE: ASPE Hunter Eğrisi (Hunter Curve Lookup)
        NEDEN: ABD'de WFU (Water Fixture Units) bazlı debi belirleme — logaritmik fit.
        NOT: Bu bir eğri fit yaklaşımıdır; gerçek Hunter tablosu ile doğrulayın.
    */
    public static double HunterCurveLookup(double wsfu)
    {
        // Parçalı lineer interpolasyon — ASPE tablosu (WSFU → lt/s yaklaşım)
        // Kaynak: Uniform Plumbing Code Table A-2
        (double wsfu, double lts)[] table =
        {
            (1,   0.063), (2,   0.126), (3,   0.189), (4,   0.220),
            (6,   0.284), (8,   0.315), (10,  0.379), (20,  0.600),
            (40,  0.820), (60,  1.010), (80,  1.200), (100, 1.380),
            (200, 2.270), (400, 3.470), (600, 4.540), (1000,6.310)
        };

        if (wsfu <= table[0].wsfu) return table[0].lts;
        if (wsfu >= table[^1].wsfu) return table[^1].lts;

        for (int i = 1; i < table.Length; i++)
        {
            if (wsfu <= table[i].wsfu)
            {
                double t = (wsfu - table[i - 1].wsfu) / (table[i].wsfu - table[i - 1].wsfu);
                return table[i - 1].lts + t * (table[i].lts - table[i - 1].lts);
            }
        }
        return 0;
    }

    /*
        NE: Standarda Göre Hız Limiti Al
        NEDEN: Gürültü, aşınma ve enerji verimliliği için her norma farklı hız sınırı uygulanır.
    */
    public static double GetMaxVelocity(PlumbingStandard standard, bool isSupply = true)
    {
        var limits = VelocityLimits.GetValueOrDefault(standard, (MaxSupply: 2.0, MaxReturn: 1.5));
        return isSupply ? limits.MaxSupply : limits.MaxReturn;
    }

    /*
        NE: Standart Bilgi Metni
        NEDEN: UI'da kullanıcıya seçili normun açıklamasını göstermek için.
    */
    public static string GetStandardDescription(PlumbingStandard standard) => standard switch
    {
        PlumbingStandard.TS1258_DIN1988 => "TS 1258 / DIN 1988 — Türkiye & Almanya (K=0.25-1.0, LU)",
        PlumbingStandard.TSEN806_3      => "TS EN 806-3 — Avrupa Normu (K=0.5-1.0, LU)",
        PlumbingStandard.ASPE_Hunter    => "ASPE Hunter Curve — ABD (WSFU bazlı logaritmik)",
        PlumbingStandard.BS6700         => "BS 6700 — İngiltere (K=0.2-1.0, LU)",
        PlumbingStandard.ASHRAE_90_1    => "ASHRAE 90.1 — Enerji Verimliliği (K=0.45-1.0)",
        _                               => "Bilinmeyen standart"
    };

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
