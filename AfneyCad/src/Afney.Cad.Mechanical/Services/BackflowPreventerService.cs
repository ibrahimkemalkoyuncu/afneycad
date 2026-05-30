using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Geri Akış Önleyici Seçim Servisi (BackflowPreventerService)
   NEDEN: TS EN 1717 kapsamında sistemin kirlenme risk sınıfına göre uygun geri akış önleyici tipini seçmek için.

   TS EN 1717 Sınıflandırması:
   - Sınıf 1: İçme suyu (kirlenme riski yok)
   - Sınıf 2: Düşük kirlenme riski (renk/koku değişimi)
   - Sınıf 3: Orta kirlenme riski (insan sağlığı için zararlı)
   - Sınıf 4: Yüksek kirlenme riski (zehirli maddeler)
   - Sınıf 5: Radyoaktif / mikrobiyolojik risk

   Cihaz Tipleri:
   - AA: Hava boşluğu (Sınıf 1–5, basınçsız)
   - AB: Hava boşluğu (Sınıf 1–5, basınçlı)
   - BA: Kontrol edilebilir çek valf (Sınıf 4 dahil)
   - CA: Çek valfl, boşaltmalı (Sınıf 3)
   - DC: Çift çek valf (Sınıf 2)
   - EC: Tek çek valf (Sınıf 2)
*/
public class BackflowPreventerService
{
    public class BackflowResult
    {
        public string DeviceType        { get; set; } = "";
        public string DeviceName        { get; set; } = "";
        public string Description       { get; set; } = "";
        public string Standard          { get; set; } = "TS EN 1717";
        public int    RiskClass         { get; set; }
        public int    RecommendedDN     { get; set; }
        public double PressureLossBar   { get; set; }
        public List<string> Applications { get; set; } = [];
    }

    public BackflowResult Select(int riskClass, double peakFlowLs, int systemDN)
    {
        var result = new BackflowResult
        {
            RiskClass       = riskClass,
            RecommendedDN   = systemDN,
            PressureLossBar = EstimatePressureLoss(riskClass, peakFlowLs)
        };

        switch (riskClass)
        {
            case 1:
            case 2:
                result.DeviceType  = "DC";
                result.DeviceName  = "Çift Çek Valf (Double Check Valve)";
                result.Description = "Sınıf 1-2 — Soğuk/sıcak su dağıtım sistemleri için";
                result.Applications.AddRange(["Konut sıhhi tesisat", "Ofis binaları"]);
                break;
            case 3:
                result.DeviceType  = "CA";
                result.DeviceName  = "Kontrol Valflı Çek Valf — Boşaltmalı";
                result.Description = "Sınıf 3 — Otomatik sulama, yangın söndürme bağlantısı";
                result.Applications.AddRange(["Sulama sistemleri", "Yüzme havuzu bağlantısı"]);
                break;
            case 4:
                result.DeviceType  = "BA";
                result.DeviceName  = "Kontrol Edilebilir Basınç Azaltmalı Çek Valf";
                result.Description = "Sınıf 4 — Endüstriyel ve kimyasal sistemler";
                result.Applications.AddRange(["Endüstriyel proses suyu", "Kimyasal dozaj sistemleri"]);
                break;
            case 5:
                result.DeviceType  = "AA";
                result.DeviceName  = "Hava Boşluğu (Air Gap) — TS EN 1717 Tip AA";
                result.Description = "Sınıf 5 — Radyoaktif veya mikrobiyolojik risk";
                result.Applications.AddRange(["Hastane/laboratuvar", "Nükleer tesis"]);
                break;
            default:
                result.DeviceType  = "DC";
                result.DeviceName  = "Çift Çek Valf";
                result.Description = "Genel amaçlı seçim";
                break;
        }

        result.Standard = "TS EN 1717 / TS EN 13959";
        return result;
    }

    // Kayıp basınç tahmini (bar) — tip ve debiye göre
    private static double EstimatePressureLoss(int riskClass, double qLs)
    {
        double kv = riskClass switch
        {
            1 or 2 => 8.0,
            3      => 5.0,
            4      => 3.5,
            _      => 0.0 // hava boşluğu — basınç kaybı yoktur
        };
        if (kv <= 0) return 0;
        double qM3h = qLs * 3.6;
        double dpBar = Math.Pow(qM3h / kv, 2) * 0.1; // bar
        return Math.Round(dpBar, 3);
    }
}
