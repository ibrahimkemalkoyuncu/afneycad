using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Yangın Söndürme Tesisatı Servisi (FireFightingService)
   NEDEN: FINE SANI, sprinkler hesabı ve yerleşimini destekler.
          Bu servis NFPA 13 / TS EN 12845 standartlarına göre sprinkler tasarımı yapar.
   
   MÜHENDİSLİK DETAYI:
   - Tehlike sınıfı belirleme (Hafif, Orta, Yüksek)
   - Sprinkler aralık ve kapasite hesabı
   - Boru çapı ve basınç hesabı
   - Pompa kapasitesi hesabı
*/
public class FireFightingService
{
    public enum HazardClass
    {
        LightHazard,       // Hafif tehlike (Ofis, Konut)
        OrdinaryHazard_1,  // Orta tehlike Grup 1 (Otopark, Fabrika)
        OrdinaryHazard_2,  // Orta tehlike Grup 2 (Ağır depo)
        ExtraHazard        // Yüksek tehlike (Kimyasal depo)
    }

    public class SprinklerDesignInput
    {
        public double ProtectedAreaM2 { get; set; } = 500;
        public HazardClass Hazard { get; set; } = HazardClass.LightHazard;
        public double CeilingHeightM { get; set; } = 3.0;
        public bool IsWetSystem { get; set; } = true;
        public double FloorToSystemPressure { get; set; } = 0; // mSS (dış basınç)
    }

    public class SprinklerDesignResult
    {
        public int SprinklerCount { get; set; }
        public double CoverageAreaPerHead { get; set; }          // m²/sprinkler
        public double MaxSpacing { get; set; }                   // m
        public double DesignDensity { get; set; }                // mm/min
        public double DesignAreaM2 { get; set; }                 // operasyon alanı
        public double RequiredFlowLpm { get; set; }              // lt/dk
        public double RequiredPressureBar { get; set; }          // bar
        public double MainPipeDN { get; set; }                   // mm
        public double BranchPipeDN { get; set; }                 // mm
        public double PumpCapacityLpm { get; set; }              // lt/dk
        public double PumpHeadM { get; set; }                    // mSS
        public double WaterTankVolumeM3 { get; set; }            // m³
        public string Standard { get; set; } = "";
        public List<string> Notes { get; set; } = new();
    }

    /*
       NE: Sprinkler sistemi tasarımı
       NEDEN: Tehlike sınıfına göre sprinkler sayısı, debi, basınç ve pompa hesabı
       
       REFERANS: TS EN 12845 / NFPA 13
    */
    public SprinklerDesignResult DesignSprinklerSystem(SprinklerDesignInput input)
    {
        var result = new SprinklerDesignResult();

        // Tehlike sınıfına göre parametreler
        double density_mm_min, designArea_m2, maxSpacing_m, coveragePerHead_m2;

        switch (input.Hazard)
        {
            case HazardClass.LightHazard:
                density_mm_min = 2.25;
                designArea_m2 = 84;
                maxSpacing_m = 4.6;
                coveragePerHead_m2 = 21;
                break;
            case HazardClass.OrdinaryHazard_1:
                density_mm_min = 5.0;
                designArea_m2 = 72;
                maxSpacing_m = 4.0;
                coveragePerHead_m2 = 12;
                break;
            case HazardClass.OrdinaryHazard_2:
                density_mm_min = 5.0;
                designArea_m2 = 216;
                maxSpacing_m = 4.0;
                coveragePerHead_m2 = 12;
                break;
            case HazardClass.ExtraHazard:
                density_mm_min = 10.0;
                designArea_m2 = 260;
                maxSpacing_m = 3.7;
                coveragePerHead_m2 = 9.3;
                break;
            default:
                density_mm_min = 5.0;
                designArea_m2 = 72;
                maxSpacing_m = 4.0;
                coveragePerHead_m2 = 12;
                break;
        }

        // Sprinkler sayısı
        result.SprinklerCount = (int)Math.Ceiling(input.ProtectedAreaM2 / coveragePerHead_m2);
        result.CoverageAreaPerHead = coveragePerHead_m2;
        result.MaxSpacing = maxSpacing_m;
        result.DesignDensity = density_mm_min;
        result.DesignAreaM2 = designArea_m2;

        // Gerekli debi: Q = Density × OperationArea
        result.RequiredFlowLpm = density_mm_min * designArea_m2;

        // Gerekli basınç (basit hesap)
        double frictionLoss = 0.02 * input.CeilingHeightM * 10; // bar
        double sprinklerPressure = 0.5; // bar (min sprinkler çalışma basıncı)
        result.RequiredPressureBar = sprinklerPressure + frictionLoss + (input.CeilingHeightM / 10.0);

        // Boru çapları
        result.MainPipeDN = result.RequiredFlowLpm <= 500 ? 100 : (result.RequiredFlowLpm <= 1500 ? 150 : 200);
        result.BranchPipeDN = result.RequiredFlowLpm <= 200 ? 32 : (result.RequiredFlowLpm <= 500 ? 50 : 65);

        // Pompa kapasitesi (%20 güvenlik marji)
        result.PumpCapacityLpm = result.RequiredFlowLpm * 1.2;
        result.PumpHeadM = result.RequiredPressureBar * 10.2 + input.FloorToSystemPressure;

        // Su deposu (30 dakika operasyon + %10 yedek)
        result.WaterTankVolumeM3 = (result.RequiredFlowLpm * 30 / 1000.0) * 1.1;

        result.Standard = "TS EN 12845 / NFPA 13";

        result.Notes.Add($"Tehlike sınıfı: {input.Hazard}");
        result.Notes.Add($"Sistem tipi: {(input.IsWetSystem ? "Islak (Wet)" : "Kuru (Dry)")}");
        result.Notes.Add($"Koruma alanı: {input.ProtectedAreaM2:F0} m²");
        if (input.CeilingHeightM > 6.0)
            result.Notes.Add("⚠️ Tavan yüksekliği 6m üzeri — ESFR veya rakipleri değerlendirin.");
        if (result.WaterTankVolumeM3 > 100)
            result.Notes.Add("⚠️ 100 m³ üzeri su deposu — bölgesel su kaynağı değerlendirmesi gerekli.");

        return result;
    }

    // Sprinkler yerleşim noktaları üret
    public List<Vector3D> GenerateSprinklerLayout(double roomWidth, double roomLength, double spacing)
    {
        var positions = new List<Vector3D>();
        int cols = (int)Math.Ceiling(roomWidth / spacing);
        int rows = (int)Math.Ceiling(roomLength / spacing);
        double xOffset = (roomWidth - (cols - 1) * spacing) / 2.0;
        double yOffset = (roomLength - (rows - 1) * spacing) / 2.0;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                positions.Add(new Vector3D(xOffset + c * spacing, yOffset + r * spacing, 0));

        return positions;
    }

    // ── HİDRANT SİSTEMİ ────────────────────────────────────────────────────────

    public enum HydrantType { Indoor, Outdoor }

    public class HydrantSystemInput
    {
        public double BuildingAreaM2     { get; set; } = 1000;
        public int    NumberOfFloors     { get; set; } = 5;
        public double FloorHeightM       { get; set; } = 3.5;
        public HazardClass Hazard        { get; set; } = HazardClass.OrdinaryHazard_1;
        public HydrantType HydrantType   { get; set; } = HydrantType.Indoor;
        public double AvailablePressureBar { get; set; } = 3.5;
    }

    public class HydrantDesignResult
    {
        public int    HydrantCount         { get; set; }
        public double HydrantFlowLpm       { get; set; }   // lt/dk (tek hidrant)
        public double TotalFlowLpm         { get; set; }   // lt/dk (eş zamanlı)
        public double SimultaneousCount    { get; set; }   // eş zamanlı çalışan
        public double HoseDiameterMm       { get; set; }
        public double HoseReelFlowLpm      { get; set; }
        public double RequiredPressureBar  { get; set; }
        public double RisserPipeDn         { get; set; }
        public double PumpCapacityLpm      { get; set; }
        public double PumpHeadMss          { get; set; }
        public double WaterTankM3          { get; set; }   // 45 dk yedek
        public string Standard             { get; set; } = "";
        public List<string> Notes          { get; set; } = [];
    }

    /*
       NE: İç/dış hidrant sistemi tasarımı
       NEDEN: TS EN 671-1/2, NFPA 14, Binaların Yangından Korunması Yönetmeliği
       Eş zamanlı hidrant sayısı: <5 kat → 1 hidrant; 5-10 kat → 2; >10 kat → 3
    */
    public HydrantDesignResult DesignHydrantSystem(HydrantSystemInput input)
    {
        var result = new HydrantDesignResult
        {
            Standard = "TS EN 671-1/2 / NFPA 14 / BKY"
        };

        // Eş zamanlı hidrant sayısı (BKY Tablo)
        int simultaneous = input.NumberOfFloors switch
        {
            <= 4  => 1,
            <= 10 => 2,
            _     => 3
        };
        result.SimultaneousCount = simultaneous;

        // Her hidrant debisi (TS EN 671-1: DN52 hortum → 400 lt/dk; DN25 makara → 100 lt/dk)
        if (input.HydrantType == HydrantType.Indoor)
        {
            result.HoseDiameterMm     = 52;
            result.HydrantFlowLpm     = 400;
            result.HoseReelFlowLpm    = 100;
        }
        else
        {
            result.HoseDiameterMm     = 100;
            result.HydrantFlowLpm     = 1500;  // dış hidrant NFPA
            result.HoseReelFlowLpm    = 0;
        }

        // Toplam hidrant sayısı (bina katı başına 1, min 2)
        result.HydrantCount = Math.Max(2, input.NumberOfFloors);

        result.TotalFlowLpm = simultaneous * result.HydrantFlowLpm;

        // Boru çapı (Darcy-Weisbach basit yaklaşım)
        result.RisserPipeDn = result.TotalFlowLpm switch
        {
            <= 800  => 80,
            <= 1600 => 100,
            <= 3000 => 150,
            _       => 200
        };

        // Gerekli basınç (tavan + sürtünme + hidrant min. basıncı)
        double heightLoss = (input.NumberOfFloors * input.FloorHeightM) / 10.2;
        double frictionLoss = 0.5;  // bar (riser + dağıtım)
        double minHydrantPressure = input.HydrantType == HydrantType.Indoor ? 2.5 : 3.5;
        result.RequiredPressureBar = minHydrantPressure + heightLoss + frictionLoss;

        // Pompa
        result.PumpCapacityLpm = result.TotalFlowLpm * 1.15;
        result.PumpHeadMss     = result.RequiredPressureBar * 10.2;

        // Su deposu (45 dakika operasyon, TS EN 671)
        result.WaterTankM3 = result.TotalFlowLpm * 45.0 / 1000.0;

        if (input.AvailablePressureBar >= result.RequiredPressureBar)
            result.Notes.Add($"✓ Şebeke basıncı ({input.AvailablePressureBar:F1} bar) yeterli — pompa gerekmeyebilir.");
        else
            result.Notes.Add($"⚠ Şebeke basıncı yetersiz. Pompa gerekli: Hm ≥ {result.PumpHeadMss:F1} mSS");

        result.Notes.Add($"Eş zamanlı {simultaneous} hidrant çalışması varsayıldı.");
        result.Notes.Add($"Su deposu: min {result.WaterTankM3:F0} m³ (45 dk. işletme)");

        return result;
    }

    // ── YANGIN HORTUM MAKARASI ─────────────────────────────────────────────────

    public class HoseReelDesignResult
    {
        public int    ReelCount           { get; set; }
        public double HoseLength          { get; set; } = 30;  // m (standart)
        public double FlowPerReelLpm      { get; set; }
        public double WorkingPressureBar  { get; set; }
        public double CoverageRadiusM     { get; set; }
        public double PipeDn              { get; set; }
        public string Standard            { get; set; } = "";
        public List<string> Notes         { get; set; } = [];
    }

    /*
       NE: Yangın hortum makarası (First-Aid Hose Reel) tasarımı
       NEDEN: TS EN 671-1, her makaranın 100 lt/dk @ 2.5 bar min. sağlaması gerekir
              Yerleşim: 30m hortum + 5m jettle 35m çap, tam örtme için ≤35m aralık
    */
    public HoseReelDesignResult DesignHoseReels(double buildingFloorAreaM2, int floors)
    {
        var result = new HoseReelDesignResult
        {
            Standard          = "TS EN 671-1",
            FlowPerReelLpm    = 100,
            WorkingPressureBar = 2.5,
            HoseLength        = 30,
            CoverageRadiusM   = 35  // 30m hortum + 5m jet
        };

        // Her 500 m² için 1 makara, kat başına min. 1
        int perFloor   = (int)Math.Ceiling(buildingFloorAreaM2 / 500.0);
        result.ReelCount = Math.Max(floors, floors * perFloor);

        // Çekiş borusu çapı (her katta max 2 eş zamanlı makara)
        double totalFlowLpm = 2 * result.FlowPerReelLpm;
        result.PipeDn = totalFlowLpm <= 200 ? 32 : 50;

        result.Notes.Add($"Kat başına {perFloor} adet makara — toplam {result.ReelCount} adet.");
        result.Notes.Add($"Kapsama yarıçapı: {result.CoverageRadiusM} m (30m hortum + 5m jet).");
        result.Notes.Add("Her makarada otomatik yeniden sarım ve cam kırma aparatı önerilir.");

        return result;
    }

    // ── SU TEMİNİ ANALİZİ ─────────────────────────────────────────────────────

    public class WaterSupplyAnalysisInput
    {
        public double AvailableFlowLpm     { get; set; }   // şebeke kapasitesi
        public double AvailablePressureBar { get; set; }   // şebeke basıncı
        public double SprinklerFlowLpm     { get; set; }
        public double HydrantFlowLpm       { get; set; }
        public double HoseReelFlowLpm      { get; set; }
        public bool   HasBoosterPump       { get; set; }
        public double BoosterPumpCapLpm    { get; set; }
        public double BoosterPumpHeadBar   { get; set; }
    }

    public class WaterSupplyAnalysisResult
    {
        public double TotalDemandLpm       { get; set; }
        public double TotalSupplyLpm       { get; set; }
        public double FlowMarginLpm        { get; set; }
        public double FlowMarginPct        { get; set; }
        public bool   IsAdequate           { get; set; }
        public double ReservoirVolumeM3    { get; set; }  // 60 dk toplam depo
        public List<string> Recommendations { get; set; } = [];
    }

    /*
       NE: Yangın tesisat su talebi vs. arz analizi
       NEDEN: NFPA 1, TS 9811 — tüm sistemlerin eş zamanlı su ihtiyacının karşılanıp karşılanmadığını doğrular
    */
    public WaterSupplyAnalysisResult AnalyzeWaterSupply(WaterSupplyAnalysisInput input)
    {
        var result = new WaterSupplyAnalysisResult();

        result.TotalDemandLpm = input.SprinklerFlowLpm + input.HydrantFlowLpm + input.HoseReelFlowLpm;

        double supplyFlow = input.AvailableFlowLpm;
        if (input.HasBoosterPump)
            supplyFlow = Math.Max(supplyFlow, input.BoosterPumpCapLpm);

        result.TotalSupplyLpm  = supplyFlow;
        result.FlowMarginLpm   = supplyFlow - result.TotalDemandLpm;
        result.FlowMarginPct   = result.TotalDemandLpm > 0
            ? result.FlowMarginLpm / result.TotalDemandLpm * 100
            : 0;
        result.IsAdequate = result.FlowMarginLpm >= 0;

        // Depo hesabı: 60 dakika yedek (NFPA 13 / TS EN 12845)
        result.ReservoirVolumeM3 = result.TotalDemandLpm * 60.0 / 1000.0;

        if (!result.IsAdequate)
        {
            double deficit = -result.FlowMarginLpm;
            result.Recommendations.Add($"⚠ Su arzı {deficit:F0} lt/dk yetersiz. Ek pompa veya depo bağlantısı gerekli.");
        }
        else
        {
            result.Recommendations.Add($"✓ Su arzı yeterli: {result.FlowMarginPct:F0}% marj mevcut.");
        }

        result.Recommendations.Add($"Yangın suyu deposu: min {result.ReservoirVolumeM3:F0} m³ (60 dk. operasyon).");

        if (input.AvailablePressureBar < 3.5)
            result.Recommendations.Add($"⚠ Şebeke basıncı ({input.AvailablePressureBar:F1} bar) < 3.5 bar — güçlendirme pompası şart.");

        return result;
    }
}
