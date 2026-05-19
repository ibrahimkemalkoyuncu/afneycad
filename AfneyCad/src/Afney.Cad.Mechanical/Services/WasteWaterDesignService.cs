using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Pis Su ve Yağmur Suyu Tasarım Servisi (WasteWaterDesignService)
   NEDEN: FINE SANI'nin 2. ana modülü. TS EN 12056 ve DIN 1988 standartlarına göre
          cazibeli (gravitasyonel) pis su ve yağmur suyu sistemlerinin hesaplanması.

   MODÜLLER:
   1. Pis Su (Sewage/Waste): Evsel pis su debileri ve boru çaplandırması
   2. Yağmur Suyu (Rainwater): Çatı/teras yağmur suyu toplama kapasitesi
   3. Drenaj (Drainage): Zemin altı drenaj hesapları
   
   STANDARTLAR:
   - TS EN 12056-2: Cazibeli Pis Su Sistemleri (Bina İçi)
   - TS EN 12056-3: Çatı Drenajı (Yağmur Suyu)
   - DIN 1986: Bina Dışı Drenaj
*/
public class WasteWaterDesignService
{
    private readonly CadDatabase _database;

    public WasteWaterDesignService(CadDatabase database)
    {
        _database = database;
    }

    // TS EN 12056 Pis Su Debi Hesap Yöntemleri (Eş Zamanlılık Sistemleri)
    public enum DesignMethod
    {
        System_I,    // Her cihazın debisi ayrı (Hastane, Endüstriyel)
        System_II,   // Konut (Eş zamanlılık katsayılı) - K = 0.5
        System_III,  // Ticari (Düşük eş zamanlılık) - K = 0.7
        System_IV    // Özel (Tam kapasite) - K = 1.0
    }

    /*
       NE: Pis Su Toplam Debi Hesabı
       NEDEN: TS EN 12056-2 formülüne göre pis su debisini hesaplamak.
       FORMÜL: Q_ww = K * √(ΣDU)
         K: Frekans faktörü (Bina tipine göre)
         DU: Drainage Unit (Drenaj Birimi)
    */
    public WasteWaterResult CalculateWasteWaterFlow(List<DrainageUnit> units, DesignMethod method = DesignMethod.System_II)
    {
        double k = method switch
        {
            DesignMethod.System_I => 1.0,
            DesignMethod.System_II => 0.5,
            DesignMethod.System_III => 0.7,
            DesignMethod.System_IV => 1.0,
            _ => 0.5
        };

        double totalDU = units.Sum(u => u.DU * u.Count);
        double qWW = k * Math.Sqrt(totalDU);  // litre/saniye

        // Sürekli akış debileri eklenir (varsa)
        double qCont = units.Where(u => u.IsContinuous).Sum(u => u.ContinuousFlow * u.Count);
        double qTotal = qWW + qCont;

        // Boru çapı ve eğim hesabı
        var pipeResult = DeterminePipeSizeAndSlope(qTotal);

        return new WasteWaterResult
        {
            TotalDU = totalDU,
            FrequencyFactor = k,
            DesignFlow = qTotal,
            WasteWaterFlow = qWW,
            ContinuousFlow = qCont,
            RecommendedDN = pipeResult.DN,
            MinimumSlope = pipeResult.Slope,
            MaxCapacity = pipeResult.Capacity,
            FillingRatio = pipeResult.FillingRatio,
            Method = method,
            Standard = "TS EN 12056-2"
        };
    }

    /*
       NE: Yağmur Suyu Debi Hesabı
       NEDEN: TS EN 12056-3 standardına göre çatı/teras alanlarından toplanacak yağmur suyu debisini hesaplamak.
       FORMÜL: Q = r * C * A / 10000
         r: Yağış yoğunluğu (lt/s·ha) — Türkiye geneli 300 lt/s·ha (5 dk, 5 yıl tekerrür)
         C: Akış katsayısı (Çatı tipi — düz: 1.0, yeşil: 0.5)
         A: Toplama alanı (m²)
    */
    public RainwaterResult CalculateRainwaterFlow(List<CatchmentArea> areas, double rainfallIntensity = 300.0)
    {
        double totalFlow = 0;
        var details = new List<RainwaterAreaDetail>();

        foreach (var area in areas)
        {
            // Q = r * C * A / 10000 (lt/s)
            double q = rainfallIntensity * area.RunoffCoefficient * area.AreaM2 / 10000.0;
            totalFlow += q;

            details.Add(new RainwaterAreaDetail
            {
                AreaName = area.Name,
                AreaM2 = area.AreaM2,
                RunoffCoefficient = area.RunoffCoefficient,
                FlowRate = q
            });
        }

        var pipeResult = DeterminePipeSizeAndSlope(totalFlow);

        return new RainwaterResult
        {
            RainfallIntensity = rainfallIntensity,
            TotalCatchmentArea = areas.Sum(a => a.AreaM2),
            TotalFlow = totalFlow,
            RecommendedDN = pipeResult.DN,
            MinimumSlope = pipeResult.Slope,
            AreaDetails = details,
            Standard = "TS EN 12056-3"
        };
    }

    /*
       NE: Dikey Hat (Kolon) Kapasitesi Hesabı
       NEDEN: Dikey pis su kolonlarının taşıyacağı maksimum debiyi kontrol etmek.
       STANDART: TS EN 12056-2 Tablo 5 — Kolon DN ve Max Kapasite
    */
    public double GetVerticalStackCapacity(double dn)
    {
        // TS EN 12056 Tablo: DN → Max Q (lt/s) @ System II
        return dn switch
        {
            <= 50 => 0.5,
            <= 75 => 1.5,
            <= 100 => 4.0,
            <= 125 => 5.8,
            <= 150 => 9.5,
            <= 200 => 16.0,
            _ => 25.0
        };
    }

    /*
       NE: Minimum Eğim ve Çap Belirleme
       NEDEN: Yatay hatlarda cazibeli akışın sağlanması için minimum eğim ve çap seçmek.
       STANDART: TS EN 12056-2 Tablo 4
       
       DOLULUK ORANI: 
       - Yatay branşman: max %50
       - Kolektör: max %70
    */
    /*
       NE: Bölünmüş Kolon Çifti Oluştur (CreateSplitColumn)
       NEDEN: OtoNET eğitiminde anlatılan senaryo — alt katta teras olduğunda kolon
              alt segment (0-3 m) ve üst segment (3-6 m) olarak ayrı tanımlanır.
              İki segment dik nokta yakalama ile yatayda birbirine bağlanır.

       KULLANIM: Alt ve üst kolonların bağlantı noktaları (Vector3D) döner;
                 PipeRoutingEngine bu noktaları perpendicular boru ile birleştirir.
    */
    public SplitColumnResult CreateSplitColumn(
        Vector3D basePoint,
        double lowerBottomZ,
        double lowerTopZ,
        double upperBottomZ,
        double upperTopZ,
        double nominalDiameter,
        MechanicalSystemType systemType)
    {
        // Alt kolon: basePoint'ten lowerTopZ'ye dikey
        var lowerBottom = new Vector3D(basePoint.X, basePoint.Y, lowerBottomZ);
        var lowerTop    = new Vector3D(basePoint.X, basePoint.Y, lowerTopZ);

        // Üst kolon: ofset noktadan upperBottomZ'ye dikey (kullanıcı konum seçer)
        // Burada yatay bağlantı için üst kolonun alt ucu lowerTop ile aynı Z'de
        var upperBottom = new Vector3D(basePoint.X, basePoint.Y, upperBottomZ);
        var upperTop    = new Vector3D(basePoint.X, basePoint.Y, upperTopZ);

        return new SplitColumnResult
        {
            LowerColumnBottom  = lowerBottom,
            LowerColumnTop     = lowerTop,
            UpperColumnBottom  = upperBottom,
            UpperColumnTop     = upperTop,
            NominalDiameter    = nominalDiameter,
            SystemType         = systemType,
            // Yatay bağlantı noktaları — dik nokta snap ile birleştirilecek
            HorizontalJoinFrom = lowerTop,
            HorizontalJoinTo   = upperBottom
        };
    }

    /*
       NE: Tesisat Kopyalama Validasyonu (ValidateCopySelection)
       NEDEN: OtoNET eğitiminde kritik kural: tesisat kopyalanırken kolon boruları
              kesinlikle seçilmemelidir — aksi halde program hata verir.
              Bu metot seçilen entity listesini kontrol ederek kolon borularını filtreler.

       DÖNÜŞ:
       - IsValid: kolon borusu yoksa true
       - RiserPipeCount: bulunan kolon borusu sayısı
       - FilteredEntities: kolonlar çıkarılmış, kopyalamaya hazır liste
    */
    public CopyValidationResult ValidateCopySelection(IEnumerable<Domain.Abstractions.CadEntity> selectedEntities)
    {
        var all = selectedEntities.ToList();

        // Kolon borusu tespiti: dikey (Z yönünde) PipeEntity'ler kolon sayılır
        var riserPipes = all
            .OfType<PipeEntity>()
            .Where(p => IsVerticalPipe(p))
            .ToList();

        var filtered = all.Except(riserPipes).ToList();

        return new CopyValidationResult
        {
            IsValid        = riserPipes.Count == 0,
            RiserPipeCount = riserPipes.Count,
            RiserPipes     = riserPipes.Cast<Domain.Abstractions.CadEntity>().ToList(),
            FilteredEntities = filtered
        };
    }

    // Boru dikeyle 80° üzeri açı yapıyorsa kolon sayılır (Z bileşeni baskın)
    private static bool IsVerticalPipe(PipeEntity pipe)
    {
        var delta = pipe.EndPoint - pipe.StartPoint;
        double len = delta.Length();
        if (len < 0.001) return false;
        double verticalRatio = Math.Abs(delta.Z) / len;
        return verticalRatio > 0.8;
    }

    /*
       NE: Yağmur Düşme Alanlarından Toplam Debi (CalculateFromCatchments)
       NEDEN: RainfallCatchmentEntity listesini WasteWaterDesignService.CatchmentArea'ya
              dönüştürerek CalculateRainwaterFlow() metoduna besler.
    */
    public RainwaterResult CalculateFromCatchmentEntities(
        IEnumerable<Entities.RainfallCatchmentEntity> catchments,
        double rainfallIntensity = 300.0)
    {
        var areas = catchments.Select(c => new CatchmentArea
        {
            Name               = c.AreaName,
            AreaM2             = c.AreaM2,
            RunoffCoefficient  = c.RunoffCoefficient
        }).ToList();

        return CalculateRainwaterFlow(areas, rainfallIntensity);
    }

    private (double DN, double Slope, double Capacity, double FillingRatio) DeterminePipeSizeAndSlope(double flowLps)
    {
        // Standart pis su boru çapları ve minimum eğimleri
        var table = new (double DN, double MinSlope, double MaxCapacity, double FillRatio)[]
        {
            (50,  0.025, 0.8,  0.50), // DN50:  %2.5 eğim, max 0.8 lt/s
            (75,  0.020, 2.0,  0.50), // DN75:  %2.0 eğim, max 2.0 lt/s
            (100, 0.010, 5.2,  0.50), // DN100: %1.0 eğim, max 5.2 lt/s
            (125, 0.008, 8.0,  0.50), // DN125: %0.8 eğim, max 8.0 lt/s
            (150, 0.007, 12.8, 0.70), // DN150: %0.7 eğim, kolektör doluluk
            (200, 0.005, 25.0, 0.70), // DN200: %0.5 eğim
            (250, 0.005, 42.0, 0.70), // DN250: %0.5 eğim
            (300, 0.003, 65.0, 0.70), // DN300: %0.3 eğim
        };

        foreach (var row in table)
        {
            if (flowLps <= row.MaxCapacity)
            {
                return (row.DN, row.MinSlope, row.MaxCapacity, row.FillRatio);
            }
        }

        return (300, 0.003, 65.0, 0.70); // Fallback
    }
}

// --- VERI MODELLER ---

public class DrainageUnit
{
    public string FixtureName { get; set; } = "";
    public double DU { get; set; }       // Drainage Unit değeri
    public int Count { get; set; } = 1;
    public bool IsContinuous { get; set; } = false;
    public double ContinuousFlow { get; set; } = 0; // lt/s
}

public class CatchmentArea
{
    public string Name { get; set; } = "";
    public double AreaM2 { get; set; }
    public double RunoffCoefficient { get; set; } = 1.0; // Düz çatı=1.0, Yeşil=0.5, Toprak=0.3
}

public class WasteWaterResult
{
    public double TotalDU { get; set; }
    public double FrequencyFactor { get; set; }
    public double DesignFlow { get; set; }
    public double WasteWaterFlow { get; set; }
    public double ContinuousFlow { get; set; }
    public double RecommendedDN { get; set; }
    public double MinimumSlope { get; set; }
    public double MaxCapacity { get; set; }
    public double FillingRatio { get; set; }
    public WasteWaterDesignService.DesignMethod Method { get; set; }
    public string Standard { get; set; } = "";
}

public class RainwaterResult
{
    public double RainfallIntensity { get; set; }
    public double TotalCatchmentArea { get; set; }
    public double TotalFlow { get; set; }
    public double RecommendedDN { get; set; }
    public double MinimumSlope { get; set; }
    public List<RainwaterAreaDetail> AreaDetails { get; set; } = new();
    public string Standard { get; set; } = "";
}

public class RainwaterAreaDetail
{
    public string AreaName { get; set; } = "";
    public double AreaM2 { get; set; }
    public double RunoffCoefficient { get; set; }
    public double FlowRate { get; set; }
}

// Bölünmüş kolon senaryosu için sonuç modeli
// (Alt katta teras olduğunda iki ayrı dikey segment + yatay bağlantı)
public class SplitColumnResult
{
    public Vector3D LowerColumnBottom { get; set; }
    public Vector3D LowerColumnTop    { get; set; }
    public Vector3D UpperColumnBottom { get; set; }
    public Vector3D UpperColumnTop    { get; set; }
    public double NominalDiameter     { get; set; }
    public MechanicalSystemType SystemType { get; set; }
    // Dik nokta snap ile birleştirilecek yatay hat uç noktaları
    public Vector3D HorizontalJoinFrom { get; set; }
    public Vector3D HorizontalJoinTo   { get; set; }
}

// Tesisat kopyalama öncesi validasyon sonucu
// Kural: kolon boruları (dikey PipeEntity) seçime dahil edilmemeli
public class CopyValidationResult
{
    public bool IsValid { get; set; }
    public int RiserPipeCount { get; set; }
    public List<Domain.Abstractions.CadEntity> RiserPipes       { get; set; } = [];
    public List<Domain.Abstractions.CadEntity> FilteredEntities { get; set; } = [];
}
