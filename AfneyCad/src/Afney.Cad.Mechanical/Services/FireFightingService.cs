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
}
