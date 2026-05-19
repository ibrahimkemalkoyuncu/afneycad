using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Standart Seçim Servisi (StandardSelectionService)
   NEDEN: FINE SANI'de kullanıcı, hangi uluslararası norma göre hesap yapacağını seçer.
          Bu servis aktif normu yönetir ve tüm hesap servislerine doğru katsayıları sağlar.
   
   DESTEKLENEN STANDARTLAR:
   - Temiz Su: TS EN 806 / DIN 1988
   - Pis Su:   TS EN 12056 / DIN 1986
   - Genel:    TS 1258 (Türk ulusal)
*/
public class StandardSelectionService
{
    public enum DesignStandard
    {
        TS_1258,        // Türk ulusal standardı — sıhhi tesisat
        EN_806,         // Avrupa — temiz su tesisatı
        DIN_1988,       // Alman — temiz su tesisatı
        EN_12056,       // Avrupa — pis su ve drenaj
        DIN_1986,       // Alman — pis su ve drenaj
        EN_1717,        // Avrupa — geri akış önleme
        ASPE_UPC,       // Amerika — ASPE / Uniform Plumbing Code (Hunter Curve, WSFU)
        BS_6700,        // İngiltere — BS 6700 (LU, K=0.20-0.50)
        ASHRAE_90_1,    // ABD Enerji Verimliliği — ASHRAE 90.1
        IPC_2021        // ABD — International Plumbing Code 2021
    }

    public DesignStandard ActiveCleanWaterStandard { get; set; } = DesignStandard.TS_1258;
    public DesignStandard ActiveWasteWaterStandard { get; set; } = DesignStandard.EN_12056;

    // Eşzamanlılık faktörü (Diversified Demand Factor)
    public double GetDiversityFactor(DesignStandard std, int fixtureCount)
    {
        return std switch
        {
            DesignStandard.TS_1258   => 1.0 / Math.Sqrt(fixtureCount),
            DesignStandard.EN_806    => 0.698 * Math.Pow(fixtureCount, -0.5),
            DesignStandard.DIN_1988  => 0.682 * Math.Pow(fixtureCount, -0.45),
            DesignStandard.BS_6700   => 0.6   * Math.Pow(fixtureCount, -0.5),
            DesignStandard.ASHRAE_90_1 => 0.65 * Math.Pow(fixtureCount, -0.5),
            DesignStandard.ASPE_UPC  => 1.0,  // ASPE Hunter Curve direkt tablo kullanır
            DesignStandard.IPC_2021  => 1.0 / Math.Sqrt(fixtureCount),
            _                        => 1.0 / Math.Sqrt(fixtureCount)
        };
    }

    // Minimum hız limiti (m/s)
    public double GetMinVelocity(DesignStandard std)
    {
        return std switch
        {
            DesignStandard.BS_6700   => 0.3,
            DesignStandard.ASPE_UPC  => 0.6,
            _                        => 0.5
        };
    }

    // Maksimum hız limiti (m/s)
    public double GetMaxVelocity(DesignStandard std)
    {
        return std switch
        {
            DesignStandard.TS_1258   => 2.0,
            DesignStandard.EN_806    => 2.0,
            DesignStandard.DIN_1988  => 2.5,
            DesignStandard.BS_6700   => 1.5,
            DesignStandard.ASPE_UPC  => 2.4,
            DesignStandard.ASHRAE_90_1 => 2.0,
            DesignStandard.IPC_2021  => 2.4,
            _                        => 2.0
        };
    }

    // Müsaade edilen basınç düşümü (mbar/m)
    public double GetAllowablePressureLoss(DesignStandard std)
    {
        return std switch
        {
            DesignStandard.TS_1258   => 4.0,
            DesignStandard.EN_806    => 5.0,
            DesignStandard.DIN_1988  => 3.5,
            DesignStandard.BS_6700   => 3.0,
            DesignStandard.ASPE_UPC  => 5.0,
            DesignStandard.ASHRAE_90_1 => 4.0,
            DesignStandard.IPC_2021  => 5.0,
            _                        => 4.0
        };
    }

    // Minimum pis su eğimi değerleri (DN bazlı)
    public double GetMinWasteSlope(DesignStandard std, double dn)
    {
        if (std == DesignStandard.EN_12056 || std == DesignStandard.DIN_1986)
        {
            if (dn <= 50)  return 0.030;
            if (dn <= 75)  return 0.025;
            if (dn <= 100) return 0.020;
            if (dn <= 150) return 0.015;
            return 0.010;
        }
        if (std == DesignStandard.ASPE_UPC || std == DesignStandard.IPC_2021)
        {
            // IPC/UPC: 1/4" per foot = 2.083%
            if (dn <= 75)  return 0.0208;
            if (dn <= 150) return 0.0104;  // 1/8" per foot
            return 0.0104;
        }
        if (std == DesignStandard.BS_6700)
        {
            if (dn <= 75)  return 0.025;
            if (dn <= 150) return 0.018;
            return 0.010;
        }
        // TS 1258 varsayılan
        if (dn <= 50)  return 0.025;
        if (dn <= 100) return 0.020;
        return 0.010;
    }

    // Tüm desteklenen standartları listele
    public static List<StandardInfo> GetAvailableStandards()
    {
        return new List<StandardInfo>
        {
            new() { Standard = DesignStandard.TS_1258,    Name = "TS 1258",    Description = "Türk Standardı — Sıhhi Tesisat Genel Kuralları",   Country = "TR" },
            new() { Standard = DesignStandard.EN_806,     Name = "EN 806",     Description = "İçme Suyu Tesisatı — Avrupa Standardı",            Country = "EU" },
            new() { Standard = DesignStandard.DIN_1988,   Name = "DIN 1988",   Description = "İçme Suyu Tesisatı — Alman Standardı",             Country = "DE" },
            new() { Standard = DesignStandard.BS_6700,    Name = "BS 6700",    Description = "Su Hizmetleri — İngiltere Standardı (K=0.20)",      Country = "GB" },
            new() { Standard = DesignStandard.ASPE_UPC,   Name = "ASPE/UPC",   Description = "Hunter Curve WSFU Yöntemi — ABD",                  Country = "US" },
            new() { Standard = DesignStandard.ASHRAE_90_1,Name = "ASHRAE 90.1",Description = "Enerji Verimliliği — ABD HVAC/Plumbing",           Country = "US" },
            new() { Standard = DesignStandard.IPC_2021,   Name = "IPC 2021",   Description = "International Plumbing Code 2021 — ABD",           Country = "US" },
            new() { Standard = DesignStandard.EN_12056,   Name = "EN 12056",   Description = "Bina İçi Pis Su Tahliye Sistemi",                  Country = "EU" },
            new() { Standard = DesignStandard.DIN_1986,   Name = "DIN 1986",   Description = "Atık Su Tesisatı — Alman Standardı",               Country = "DE" },
            new() { Standard = DesignStandard.EN_1717,    Name = "EN 1717",    Description = "İçme Suyu Geri Akış Önleme",                       Country = "EU" },
        };
    }

    public class StandardInfo
    {
        public DesignStandard Standard { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Country { get; set; } = "";
    }
}
