using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Isı Köprüsü Servisi (ThermalBridgeService)
   NEDEN: TS EN ISO 14683 — lineer ısı köprüsü ψ (psi) katsayıları.
          Köşe, pencere, döşeme gibi detaylar net ısı kaybını %20-30 artırır.
          FINE MEP ve pek çok araçta yok. Binalarda zorunlu EKB hesabında gerekir.
*/
public class ThermalBridgeService
{
    // ── Isı Köprüsü Bileşeni ────────────────────────────────────────────────────

    public class ThermalBridgeElement
    {
        public string Name           { get; set; } = "";
        public string Category       { get; set; } = "";   // Duvar/Pencere/Döşeme...
        public double PsiWpmK        { get; set; }   // ψ değeri W/(m·K)
        public double LengthM        { get; set; }   // Uzunluk (m)
        public double DesignDeltaT   { get; set; } = 25;  // ΔT tasarım (K)
        // Türetilen
        public double HeatLossW      => PsiWpmK * LengthM * DesignDeltaT;
        public double AnnualLossKwh  => HeatLossW * 1800 / 1000.0;  // Türkiye ort. 1800 saat ısıtma
    }

    // ── TS EN ISO 14683 Standart ψ Tablosu ───────────────────────────────────────

    public static readonly List<(string category, string description, double psiMin, double psiMax, string note)> PsiTable =
    [
        // ─── Çatı-Duvar Bileşkeleri ──────────────────────────────────────────
        ("Çatı-Duvar", "Düz çatı — iyi yalıtım",               0.00, 0.10, "EN ISO 14683 Tablo A.3"),
        ("Çatı-Duvar", "Düz çatı — standart detay",             0.10, 0.30, "Detaya göre seçin"),
        ("Çatı-Duvar", "Eğik çatı — saçak",                     0.04, 0.20, ""),

        // ─── Döşeme-Duvar Bileşkeleri ─────────────────────────────────────────
        ("Döşeme-Duvar", "Aralık kat döşemesi (IC-ısı köprüsüz)", 0.00, 0.05, ""),
        ("Döşeme-Duvar", "Aralık kat döşemesi (standart)",        0.10, 0.40, ""),
        ("Döşeme-Duvar", "Zemin kat döşemesi — ısıtılmamış bodrum",0.15, 0.60, "EN ISO 13370"),
        ("Döşeme-Duvar", "Balkon döşemesi konsolu",               0.50, 1.00, "Yapısal köprü — en kritik"),

        // ─── Pencere / Kapı Çerçeveleri ───────────────────────────────────────
        ("Pencere",     "Pencere altı (denizlik)",               0.00, 0.04, ""),
        ("Pencere",     "Pencere yanı (montaj boşluğu)",         0.00, 0.07, ""),
        ("Pencere",     "Pencere üstü (lento)",                  0.00, 0.07, ""),
        ("Pencere",     "Dış kapı çerçevesi",                    0.00, 0.15, ""),

        // ─── Köşe Detayları ───────────────────────────────────────────────────
        ("Köşe",        "Dış köşe (iç yalıtım)",                -0.15, -0.05, "Negatif — kazanım"),
        ("Köşe",        "Dış köşe (dış yalıtım)",               -0.05, 0.00, ""),
        ("Köşe",        "İç köşe",                               0.05, 0.10, ""),
        ("Köşe",        "T-kavşak / Ara bölme-Dış duvar",        0.00, 0.15, ""),

        // ─── Çelik Strüktür ───────────────────────────────────────────────────
        ("Metal",       "Çelik kolon — yalıtımsız",             0.50, 2.00, "Kritik — mutlaka izolatör eklenmeli"),
        ("Metal",       "Çelik kolon — kısmi yalıtım",          0.20, 0.80, "Termal kırıcı önerilir"),

        // ─── Sıhhi Tesisat Penetrasyonları ───────────────────────────────────
        ("Penetrasyon", "Boru geçişi duvarda (plastik)",         0.01, 0.03, ""),
        ("Penetrasyon", "Boru geçişi duvarda (çelik)",           0.03, 0.15, "Hava sızdırmazlık ve yalıtım zorunlu"),
    ];

    // ── Hesap ─────────────────────────────────────────────────────────────────────

    public class ThermalBridgeResult
    {
        public List<ThermalBridgeElement> Elements   { get; set; } = [];
        public double TotalHeatLossW                 { get; set; }
        public double TotalAnnualKwh                 { get; set; }
        public double H_TB                           { get; set; }  // Isı köprüsü etkisi W/K
        public string Assessment                     { get; set; } = "";
    }

    public static ThermalBridgeResult Calculate(List<ThermalBridgeElement> elements)
    {
        var result = new ThermalBridgeResult { Elements = elements };
        foreach (var el in elements)
        {
            result.TotalHeatLossW  += el.HeatLossW;
            result.TotalAnnualKwh  += el.AnnualLossKwh;
            result.H_TB            += el.PsiWpmK * el.LengthM;
        }

        result.Assessment = result.H_TB switch
        {
            <= 2   => "✓ İyi — köprü etkisi düşük",
            <= 5   => "⚠ Orta — iyileştirme önerilir",
            <= 10  => "⚠ Yüksek — EKB sertifikasını etkiler",
            _      => "⛔ Çok yüksek — ısı köprüsü analizi ve detay revizyonu gerekli"
        };
        return result;
    }
}
