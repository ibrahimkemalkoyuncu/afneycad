using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Standards;

/*
    NE: Vitrifiye Yerleşim Standartları (FixtureLayoutStandards)
    NEDEN: TS 1258 ve DIN 1986 standartlarına göre vitrifiyeler arası minimum montaj mesafelerini yönetmek için.
    
    NASIL (Mühendislik Modu):
    - I-I, I-L gibi farklı yerleşim tipleri için mesafe matrisi sunar.
    - Cihazın duvara olan yan mesafesini (Side Clearance) belirler.
*/
public static class FixtureLayoutStandards
{
    // Minimum Yan Boşluklar (Duvar ile cihaz arası)
    private static readonly Dictionary<string, double> SideClearances = new()
    {
        { "WC_Reservoir", 250 }, // Klozet yan duvardan min 25cm
        { "Washbasin", 150 },    // Lavabo yan duvardan min 15cm
        { "Shower", 50 },        // Duş teknesi duvara sıfır veya 5cm
        { "KitchenSink", 100 }
    };

    // Cihazlar Arası Minimum Mesafeler (Merkezden merkeze değil, kenardan kenara)
    // Key: "TypeA-TypeB"
    private static readonly Dictionary<string, double> InterFixtureClearances = new()
    {
        { "WC_Reservoir-Washbasin", 200 },
        { "Washbasin-WC_Reservoir", 200 },
        { "WC_Reservoir-Shower", 200 },
        { "Shower-WC_Reservoir", 200 },
        { "Washbasin-Shower", 100 },
        { "Shower-Washbasin", 100 },
        { "Washbasin-Washbasin", 100 },
        { "WC_Reservoir-WC_Reservoir", 300 }
    };

    public static double GetSideClearance(string type)
    {
        return SideClearances.TryGetValue(type, out var val) ? val : 150;
    }

    public static double GetClearanceBetween(string typeA, string typeB)
    {
        string key = $"{typeA}-{typeB}";
        return InterFixtureClearances.TryGetValue(key, out var val) ? val : 150;
    }
}
