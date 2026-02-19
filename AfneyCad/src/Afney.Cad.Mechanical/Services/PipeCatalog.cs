using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Boru Katalog Servisi (PipeCatalog)
    NEDEN: Farklı malzeme standartlarına (DIN, TS, EN) göre nominal dış çap (DN/OD) ile hidrolik iç çap (ID) arasındaki ilişkiyi sağlamak için.
    
    KAYNAKLAR:
    - PPRC: DIN 8077 (SDR 11, SDR 6)
    - PVC: TS EN 1329
    - PEX: TS EN ISO 15875
*/
public static class PipeCatalog
{
    // Malzeme -> (Dış Çap -> İç Çap)
    private static readonly Dictionary<PipeMaterial, Dictionary<double, double>> _catalog = new();

    static PipeCatalog()
    {
        InitializePPRC_PN20(); // SDR 11 (Soğuk Su)
        InitializePPRC_PN25(); // SDR 6 (Sıcak Su / Kompozit)
        InitializePVC();       // Pis Su
        InitializePEX();       // Mobil
    }

    public static double GetInnerDiameter(PipeMaterial material, double outerDiameter)
    {
        // Eğer malzeme tanımlıysa ve çap listede varsa döndür
        if (_catalog.TryGetValue(material, out var sizes))
        {
            if (sizes.TryGetValue(outerDiameter, out double id))
                return id;

            // Tam eşleşme yoksa en yakın alt/üst değeri mi interpolasyon mu? 
            // Mühendislikte "seçilen boru" kullanılır, interpolasyon olmaz.
            // Fakat listede olmayan bir OD gelirse (örn: kullanıcı manuel girdi),
            // Standart SDR oranına göre tahmini ID verelim.
            return EstimateInnerDiameter(material, outerDiameter);
        }

        // Generic: Et kalınlığı yok sayılır (veya %10 düşülür)
        return outerDiameter * 0.9;
    }
    
    public static List<double> GetStandardDiameters(PipeMaterial material)
    {
        if (_catalog.TryGetValue(material, out var sizes))
        {
            return sizes.Keys.OrderBy(d => d).ToList();
        }
        return new List<double> { 20, 25, 32, 40, 50, 63, 75, 90, 110 }; // Default
    }

    private static double EstimateInnerDiameter(PipeMaterial material, double od)
    {
        // Basit SDR (Standard Dimension Ratio) Hesabı: OD / WallThickness
        // ID = OD - 2*WT = OD - 2*(OD/SDR) = OD * (1 - 2/SDR)
        
        return material switch
        {
            PipeMaterial.PPRC_PN20 => od * (1.0 - 2.0/11.0), // SDR 11
            PipeMaterial.PPRC_PN25 => od * (1.0 - 2.0/6.0),  // SDR 6
            PipeMaterial.PVC_SN4   => od * 0.94,             // Yaklaşık
            _ => od * 0.9
        };
    }

    private static void InitializePPRC_PN20() // SDR 11
    {
        // DN 20 -> 1.9mm et -> 16.2mm ID
        var map = new Dictionary<double, double>
        {
            { 20, 16.2 },
            { 25, 20.4 },
            { 32, 26.0 },
            { 40, 32.6 },
            { 50, 40.8 },
            { 63, 51.4 },
            { 75, 61.2 },
            { 90, 73.6 },
            { 110, 90.0 }
        };
        _catalog[PipeMaterial.PPRC_PN20] = map;
    }

    private static void InitializePPRC_PN25() // SDR 6 (Kalın Etli)
    {
        // DN 20 -> 3.4mm et -> 13.2mm ID
        var map = new Dictionary<double, double>
        {
            { 20, 13.2 },
            { 25, 16.6 },
            { 32, 21.2 },
            { 40, 26.6 },
            { 50, 33.2 },
            { 63, 42.0 },
            { 75, 50.0 },
            { 90, 60.0 },
            { 110, 73.2 }
        };
        _catalog[PipeMaterial.PPRC_PN25] = map;
    }

    private static void InitializePVC() // Pis Su (SN4 / Tip 1)
    {
        // DN 50 -> 3.0mm -> 44mm (Yaklaşık)
        // Piyasada: 50, 70, 100, 125, 150, 200...
        var map = new Dictionary<double, double>
        {
            { 50, 46.4 }, // 1.8mm et
            { 75, 71.2 }, // 1.9mm et (Q70 olarak geçer ama OD75'tir genelde, Q70 pimaş standardı farklı olabilir, burada ISO OD baz alıyoruz)
            { 110, 103.6 }, // 3.2mm
            { 125, 118.6 }, // 3.2mm
            { 160, 152.0 }, // 4.0mm
            { 200, 190.2 }  // 4.9mm
        };
        // Not: Türkiye piyasasında "70'lik pimaş" aslında 75mm OD olabilir veya eski standart 70mm olabilir. 
        // Modern PVC-U standartlarında 75mm yaygındır. Biz 75 ekledik.
        
        _catalog[PipeMaterial.PVC_SN4] = map;
    }
    
    private static void InitializePEX()
    {
        // PEX-b (Kılıflı) - Genelde 16x2.0, 16x2.2
        var map = new Dictionary<double, double>
        {
            { 16, 12.0 }, // 16x2.0
            { 20, 16.0 }, // 20x2.0
            { 25, 20.4 }, // 25x2.3
            { 32, 26.2 }  // 32x2.9
        };
        _catalog[PipeMaterial.PEX_b] = map;
    }
}
