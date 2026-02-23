using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Hidrolik Hesap ve Çap Tayin Motoru (HydraulicCalculationService)
   NEDEN: TS 1258 ve DIN 1988 standartlarına göre, toplam yük biriminden (LU) yola çıkarak boru çaplarını otomatik belirlemek için.
   
   DESTEKLENEN BİNA TİPLERİ:
   - Konut (Residential): TS 1258 Konut formülü
   - Ofis/Ticari (Commercial): DIN 1988 Ofis formülü
   - Otel (Hotel): Yüksek eşzamanlılık katsayısı
   - Hastane (Hospital): Maksimum güvenlik (düşük hız limiti)
   - Fabrika/Endüstriyel (Industrial): Yüksek debi kapasitesi

   MÜHENDİSLİK FORMÜLLERİ:
   - Tasarım Debisi: Q = a * (Sum LU)^b + c  (TS 1258 polinom yaklaşımı)
   - Çap Hesabı: d = sqrt((4 * Q) / (π * V * 3600))
   - Her bina tipi için farklı a, b, c katsayıları ve hız limitleri
*/
public class HydraulicCalculationService
{
    // Bina Tip Enum
    public enum BuildingType
    {
        Residential,   // Konut
        Commercial,    // Ofis / İşyeri
        Hotel,         // Otel
        Hospital,      // Hastane
        Industrial     // Endüstriyel / Fabrika
    }

    // Bina tipine göre hidrolik parametreler
    private static readonly Dictionary<BuildingType, BuildingHydraulicParams> _params = new()
    {
        [BuildingType.Residential] = new()
        {
            Name = "Konut",
            Standard = "TS 1258",
            CoefficientA = 0.25,     // Eş zamanlılık katsayısı
            CoefficientB = 0.50,     // Üs katsayısı (sqrt)
            CoefficientC = 0.0,      // Sabit ofset
            MaxVelocity = 1.5,       // m/s (Konfor sınırı)
            MinVelocity = 0.3,       // m/s (Durgunluk önlemi)
            DiversityFactor = 0.70,  // Eş zamanlılık düzeltme çarpanı
            Description = "TS 1258'e göre normal konut tesisatı. Eş zamanlılık formülü: Q = 0.25·√(ΣLU)"
        },
        [BuildingType.Commercial] = new()
        {
            Name = "Ofis / Ticari",
            Standard = "DIN 1988",
            CoefficientA = 0.20,
            CoefficientB = 0.50,
            CoefficientC = 0.04,
            MaxVelocity = 2.0,       // Ofislerde biraz daha yüksek hız kabul edilir
            MinVelocity = 0.3,
            DiversityFactor = 0.55,
            Description = "DIN 1988 ofis/ticari bina. Q = 0.20·√(ΣLU) + 0.04. Düşük eş zamanlılık."
        },
        [BuildingType.Hotel] = new()
        {
            Name = "Otel",
            Standard = "TS 1258 / EN 806",
            CoefficientA = 0.30,
            CoefficientB = 0.50,
            CoefficientC = 0.05,
            MaxVelocity = 1.5,
            MinVelocity = 0.3,
            DiversityFactor = 0.80,  // Otellerde yüksek eş zamanlılık
            Description = "Otel tesisatı. Yüksek pik saatlerde %80 eş zamanlılık. Q = 0.30·√(ΣLU) + 0.05"
        },
        [BuildingType.Hospital] = new()
        {
            Name = "Hastane",
            Standard = "TS 1258 / EN 806",
            CoefficientA = 0.35,
            CoefficientB = 0.50,
            CoefficientC = 0.10,
            MaxVelocity = 1.2,       // Hastanelerde düşük hız (Gürültü kontrolü + Güvenlik)
            MinVelocity = 0.3,
            DiversityFactor = 0.90,  // Çok yüksek eş zamanlılık (kritik hizmet)
            Description = "Hastane tesisatı. Güvenlik kritik. Düşük hız (1.2 m/s), %90 eş zamanlılık."
        },
        [BuildingType.Industrial] = new()
        {
            Name = "Endüstriyel / Fabrika",
            Standard = "DIN 1988 / NFPA",
            CoefficientA = 0.40,
            CoefficientB = 0.55,
            CoefficientC = 0.15,
            MaxVelocity = 2.5,       // Endüstriyel: Yüksek hız kabul edilir
            MinVelocity = 0.5,
            DiversityFactor = 0.95,  // Neredeyse tam kapasite
            Description = "Endüstriyel tesisat. Yüksek debi, geniş çaplar. Q = 0.40·(ΣLU)^0.55 + 0.15"
        }
    };

    // Çelik/Plastik borular için standart ticari çaplar (İç Çap mm)
    private readonly double[] _standardDiameters = { 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150 };

    private BuildingType _activeBuildingType = BuildingType.Residential;

    /*
       NE: Aktif Bina Tipini Ayarla
       NEDEN: Tüm hesaplamaların seçilen bina tipine göre yapılması.
    */
    public void SetBuildingType(BuildingType type) => _activeBuildingType = type;
    public BuildingType GetBuildingType() => _activeBuildingType;
    public BuildingHydraulicParams GetActiveParams() => _params[_activeBuildingType];

    /*
       NE: Tüm Desteklenen Bina Tiplerini Listele
       NEDEN: UI ComboBox için
    */
    public List<(BuildingType Type, string Name, string Standard)> GetSupportedBuildingTypes()
    {
        return _params.Select(kv => (kv.Key, kv.Value.Name, kv.Value.Standard)).ToList();
    }

    /*
       NE: Tasarım Debisi Hesapla (CalculateDesignFlow)
       NEDEN: Bina tipine göre TS 1258 / DIN 1988 eş zamanlılık formülünü uygular.
    */
    public double CalculateDesignFlow(double totalLU, BuildingType? overrideType = null)
    {
        if (totalLU <= 0) return 0;

        var p = _params[overrideType ?? _activeBuildingType];

        // Q = a * (LU)^b + c   (Litre/saniye)
        double flowLps = p.CoefficientA * Math.Pow(totalLU, p.CoefficientB) + p.CoefficientC;

        // Eş zamanlılık düzeltmesi
        flowLps *= p.DiversityFactor;

        // m³/h dönüşümü
        return flowLps * 3.6;
    }

    /*
       NE: Boru Çapı Belirle (DeterminePipeDiameter)
       NEDEN: Hesaplanan debiye ve bina tipinin hız limitine göre en uygun standart çapı seçer.
    */
    public double DeterminePipeDiameter(double flowM3h, BuildingType? overrideType = null)
    {
        if (flowM3h <= 0) return _standardDiameters[0];

        var p = _params[overrideType ?? _activeBuildingType];

        // Alan = Q / V  (m²)
        double area = (flowM3h / 3600.0) / p.MaxVelocity;
        
        // d = sqrt(4A / π) * 1000  (mm)
        double theoreticalDiameter = Math.Sqrt((4 * area) / Math.PI) * 1000;

        // En yakın ÜST standart çapı seç
        double selected = _standardDiameters.FirstOrDefault(d => d >= theoreticalDiameter);
        if (selected <= 0) selected = _standardDiameters.Last(); // Fallback

        return selected;
    }

    /*
       NE: Mahal Bazlı Hidrolik Analiz
       AMACI: Bir odanın tüm vitrifiyelerini toplayıp giriş çapını ve debisini hesaplar.
    */
    public MahalHydraulicResult AnalyzeMahalHydraulics(MahalEntity mahal, BuildingType? overrideType = null)
    {
        double totalLU = mahal.Fixtures.Sum(f => f.LoadUnits);
        double flow = CalculateDesignFlow(totalLU, overrideType);
        double diameter = DeterminePipeDiameter(flow, overrideType);
        var p = _params[overrideType ?? _activeBuildingType];
        
        // Seçilen çapta gerçek hız hesabı
        double area = Math.PI * Math.Pow((diameter / 1000.0) / 2.0, 2);
        double velocity = area > 0 ? (flow / 3600.0) / area : 0;

        return new MahalHydraulicResult
        {
            TotalLU = totalLU,
            DesignFlow = flow,
            RecommendedDiameter = diameter,
            ActualVelocity = velocity,
            MaxAllowedVelocity = p.MaxVelocity,
            IsVelocityOk = velocity <= p.MaxVelocity,
            BuildingType = overrideType ?? _activeBuildingType,
            Standard = p.Standard
        };
    }

    /*
       NE: Sirkülasyon Pompası Debi Hesabı
       AMACI: Sıcak su dönüş hattındaki ısı kaybını karşılayacak sirkülasyon pompası debisini hesaplamak.
       FORMÜL: P(Watt) = m(kg/s) * c * ΔT 
               Debi(m³/h) = (P / (c * ΔT)) * 3.6
    */
    public double CalculateCirculationPumpFlow(double heatLossWatt, double deltaT = 5.0)
    {
        if (heatLossWatt <= 0 || deltaT <= 0) return 0;
        
        double massFlowKgPerSec = heatLossWatt / (4180.0 * deltaT);
        return massFlowKgPerSec * 3.6;
    }

    /*
       NE: Çoklu Bina Tipi Karşılaştırma Raporu
       NEDEN: Mühendise tüm bina tiplerindeki sonuçları yan yana göstermek.
    */
    public List<MahalHydraulicResult> CompareAllBuildingTypes(MahalEntity mahal)
    {
        return _params.Keys.Select(type => AnalyzeMahalHydraulics(mahal, type)).ToList();
    }
}

// Bina Hidrolik Parametreleri
public class BuildingHydraulicParams
{
    public string Name { get; set; } = "";
    public string Standard { get; set; } = "";
    public double CoefficientA { get; set; }
    public double CoefficientB { get; set; }
    public double CoefficientC { get; set; }
    public double MaxVelocity { get; set; }
    public double MinVelocity { get; set; }
    public double DiversityFactor { get; set; }
    public string Description { get; set; } = "";
}

// Mahal Hidrolik Analiz Sonucu
public class MahalHydraulicResult
{
    public double TotalLU { get; set; }
    public double DesignFlow { get; set; }
    public double RecommendedDiameter { get; set; }
    public double ActualVelocity { get; set; }
    public double MaxAllowedVelocity { get; set; }
    public bool IsVelocityOk { get; set; }
    public HydraulicCalculationService.BuildingType BuildingType { get; set; }
    public string Standard { get; set; } = "";
}
