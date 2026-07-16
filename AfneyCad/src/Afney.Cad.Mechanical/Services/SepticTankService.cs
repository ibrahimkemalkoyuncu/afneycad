using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Fosseptik/Rögar Boyutlandırma Servisi (SepticTankService)
   NEDEN: FINE SANI, atık su sistemlerinde fosseptik tank ve rögarlarin hacim ve geometri hesabını yapar.
   
   MÜHENDİSLİK DETAYI:
   - Fosseptik hacmi: Kişi sayısı × Birim hacim × Bekletme süresi
   - Rögar boyutu: Debi ve yerleşim kurallarına göre
   - TS 2873 (Beton ve Plastik fosseptikler) referans
   - Ön arıtma, çamur kapasitesi, havalandırma alanı hesabı
*/
public class SepticTankService
{
    public enum TankType
    {
        SingleChamber,   // Tek hazne
        DoubleChamber,   // Çift hazne
        TripleChamber    // Üç hazne (büyük yapılar)
    }

    public enum ManholeMaterial
    {
        Concrete,        // Beton
        HDPE,            // Yüksek yoğunluklu polietilen
        Fiberglass       // Cam elyaf takviyeli
    }

    public class SepticTankInput
    {
        public int PersonCount { get; set; } = 50;
        public double UnitWaterConsumption { get; set; } = 150.0;  // lt/kişi/gün
        public double RetentionTime { get; set; } = 2.0;           // gün
        public TankType Type { get; set; } = TankType.DoubleChamber;

        // NE: Çamur payı oranı (SludgeMarginRatio)
        // NEDEN: Önceden %30 sabitti. WasteWaterCalcSheetService'in ayrı bir fosseptik
        //        hesabı vardı ve kullanıcı bu payı değiştirebiliyordu (SludgeFactor). İki servis
        //        tek motora (bu sınıfa) birleştirilirken kullanıcı ayarlanabilirliği kaybolmasın
        //        diye bu oran parametrik hale getirildi.
        public double SludgeMarginRatio { get; set; } = 0.30;
    }

    public class SepticTankResult
    {
        public double RequiredVolume { get; set; }    // m³
        public double SludgeVolume { get; set; }      // m³
        public double TotalVolume { get; set; }       // m³
        public double Length { get; set; }             // m
        public double Width { get; set; }              // m
        public double Depth { get; set; }              // m
        public TankType Type { get; set; }
        public int ChamberCount { get; set; }
        public string Standard { get; set; } = "";
        public List<string> Notes { get; set; } = new();
    }

    /*
       NE: Fosseptik hacim ve geometri hesabı
       NEDEN: Kişi sayısına ve bekletme süresine göre fosseptik boyutlandırması yapar
       
       FORMÜL: V = n × q × t / 1000 (m³)
       n: kişi sayısı, q: birim su tüketimi (lt/kişi/gün), t: bekletme süresi (gün)
    */
    public SepticTankResult CalculateSepticTank(SepticTankInput input)
    {
        var result = new SepticTankResult();

        // Ana hacim (m³)
        double dailyFlow = input.PersonCount * input.UnitWaterConsumption / 1000.0; // m³/gün
        result.RequiredVolume = dailyFlow * input.RetentionTime;

        // Çamur hacmi: parametrik ek kapasite (varsayılan %30)
        result.SludgeVolume = result.RequiredVolume * input.SludgeMarginRatio;
        result.TotalVolume = result.RequiredVolume + result.SludgeVolume;

        // Minimum hacim kontrolü
        if (result.TotalVolume < 2.0) result.TotalVolume = 2.0;
        if (input.PersonCount > 100 && result.TotalVolume < 10.0) result.TotalVolume = 10.0;

        // Geometri hesabı (Uzunluk:Genişlik = 3:1 oranı, Derinlik = min 1.5m)
        result.Depth = Math.Max(1.5, Math.Min(3.0, result.TotalVolume / 4.0));
        double surfaceArea = result.TotalVolume / result.Depth;
        result.Width = Math.Sqrt(surfaceArea / 3.0);
        result.Length = result.Width * 3.0;

        // Minimum boyut kontrolleri (TS 2873)
        if (result.Width < 0.8) result.Width = 0.8;
        if (result.Length < 2.0) result.Length = 2.0;
        if (result.Depth < 1.5) result.Depth = 1.5;

        result.Type = input.Type;
        result.ChamberCount = input.Type switch
        {
            TankType.SingleChamber => 1,
            TankType.DoubleChamber => 2,
            TankType.TripleChamber => 3,
            _ => 2
        };

        result.Standard = "TS 2873 / EN 12566-1";

        // Notlar
        result.Notes.Add($"Günlük atık su debisi: {dailyFlow:F2} m³/gün");
        result.Notes.Add($"Bekletme süresi: {input.RetentionTime} gün");
        result.Notes.Add($"Çamur boşaltma periyodu: 6-12 ay (önerilen)");
        if (input.PersonCount > 200)
            result.Notes.Add("⚠️ 200+ kişi için paket arıtma tesisi değerlendirilmelidir.");
        if (result.TotalVolume > 50)
            result.Notes.Add("⚠️ 50 m³ üzeri hacimler için özel projelendirme gereklidir.");

        return result;
    }

    // Rögar boyutlandırma
    public class ManholeResult
    {
        public double InternalDiameter { get; set; }  // mm
        public double Depth { get; set; }              // mm
        public ManholeMaterial Material { get; set; }
        public string CoverClass { get; set; } = "";   // D400, C250 vb.
        public string Standard { get; set; } = "";
    }

    public ManholeResult CalculateManhole(double pipeDN, double depth, bool isTrafficArea = false)
    {
        var result = new ManholeResult();

        // İç çap: Boru çapına göre
        if (pipeDN <= 200) result.InternalDiameter = 600;
        else if (pipeDN <= 400) result.InternalDiameter = 800;
        else if (pipeDN <= 600) result.InternalDiameter = 1000;
        else result.InternalDiameter = 1200;

        result.Depth = Math.Max(depth, 800);  // Min 800mm
        result.Material = pipeDN <= 300 ? ManholeMaterial.HDPE : ManholeMaterial.Concrete;
        result.CoverClass = isTrafficArea ? "D400" : "C250";
        result.Standard = "TS EN 124-2 / TS EN 13598";

        return result;
    }
}
