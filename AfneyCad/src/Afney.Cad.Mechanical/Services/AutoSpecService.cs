using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı Teknik Şartname ve Keşif Özeti Jeneratörü (AutoSpecService)
   NEDEN: Projedeki tüm imalatları analiz ederek, Bayındırlık Poz Nolarına uygun keşif özeti,
          mühendislik şartnamesi ve standart referansları oluşturmak için.
   
   STANDART REFERANSLARİ:
   - TS 1258: Binalardaki Sıhhi Tesisat Hesap Kuralları
   - TS EN 12056: Binalar İçi Cazibeli Pis Su Sistemleri
   - TS EN 806: Bina İçi Su Tesisatları
   - DIN 1988: İçme ve Kullanma Suyu Tesisatları
   - TS 11382: Plastik (PVC-U) Borular
   - TS 12090: PP-R Borular
*/
public class AutoSpecService
{
    private readonly IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> _entities;

    public AutoSpecService(IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> entities)
    {
        _entities = entities;
    }

    /*
       NE: Keşif Özeti Üret (GenerateBoMReport)
       AMACI: Boru metrajları, armatür adetleri ve fittings listesini Bayındırlık Poz Nolarına uygun profesyonel tablo olarak sunmak.
    */
    public List<SpecItem> GenerateBoMReport()
    {
        var items = new List<SpecItem>();
        var mechanicalEntities = _entities.OfType<MechanicalEntity>().ToList();

        // 1. Boru Metrajları (Çap, Malzeme ve Sistem bazlı gruplama + Poz No)
        var pipes = mechanicalEntities.OfType<PipeEntity>()
            .GroupBy(p => new { Material = p.PipeMaterialType, p.InnerDiameter, p.SystemType })
            .Select(g => new SpecItem
            {
                Code = GetPozNo(g.Key.Material, g.Key.InnerDiameter),
                Description = GetPipeDescription(g.Key.Material, g.Key.InnerDiameter, g.Key.SystemType),
                Unit = "mt",
                Quantity = g.Sum(p => (p.EndPoint - p.StartPoint).Length() / 1000.0),
                Standard = GetPipeStandard(g.Key.Material),
                Category = "Boru Tesisatı"
            });
        items.AddRange(pipes);

        // 2. Vitrifiye ve Cihaz Adetleri
        var fixtures = mechanicalEntities.OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType)
            .Select(g => new SpecItem
            {
                Code = GetFixturePozNo(g.Key),
                Description = GetFixtureDescription(g.Key),
                Unit = "Ad.",
                Quantity = g.Count(),
                Standard = "TS 1258",
                Category = "Vitrifiye ve Armatürler"
            });
        items.AddRange(fixtures);

        // 3. Ek Parçalar (Dirsek, T, Vana)
        var elbows = mechanicalEntities.OfType<ElbowEntity>()
            .GroupBy(e => e.InnerDiameter)
            .Select(g => new SpecItem
            {
                Code = $"25.352.{(int)g.Key:D4}",
                Description = $"DN {g.Key:F0} Dirsek (90° / 45°)",
                Unit = "Ad.",
                Quantity = g.Count(),
                Standard = GetFittingStandard(g.First()),
                Category = "Ek Parçalar"
            });
        items.AddRange(elbows);

        var tees = mechanicalEntities.OfType<TeeEntity>()
            .GroupBy(e => e.InnerDiameter)
            .Select(g => new SpecItem
            {
                Code = $"25.353.{(int)g.Key:D4}",
                Description = $"DN {g.Key:F0} Te Parçası",
                Unit = "Ad.",
                Quantity = g.Count(),
                Standard = GetFittingStandard(g.First()),
                Category = "Ek Parçalar"
            });
        items.AddRange(tees);

        var valves = mechanicalEntities.OfType<Valve>()
            .GroupBy(v => v.InnerDiameter)
            .Select(g => new SpecItem
            {
                Code = $"25.360.{(int)g.Key:D4}",
                Description = $"DN {g.Key:F0} Küresel Vana (PN16)",
                Unit = "Ad.",
                Quantity = g.Count(),
                Standard = "TS EN 13828",
                Category = "Vanalar"
            });
        items.AddRange(valves);

        return items;
    }

    /*
       NE: Teknik Şartname Taslağı Üret (GenerateSpecificationText)
       AMACI: Tüm malzeme, standart ve montaj kurallarını kapsayan profesyonel mühendislik şartnamesi.
    */
    public string GenerateSpecificationText()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("      AFNEYCAD MEKANİK TESİSAT TEKNİK ŞARTNAMESİ     ");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy}");
        sb.AppendLine($"Proje Mühendislik Yazılımı: AfneyCAD Engine v2.0");
        sb.AppendLine();

        // 1. GENEL HÜKÜMLER
        sb.AppendLine("1. GENEL HÜKÜMLER VE STANDARTLAR");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine("1.1 Tesisat tasarımı aşağıdaki standartlara uygun olarak yapılmıştır:");
        sb.AppendLine("    - TS 1258  : Binalardaki Sıhhi Tesisat Hesap Kuralları");
        sb.AppendLine("    - TS EN 806: Bina İçi Su Tesisatları (Bölüm 1-5)");
        sb.AppendLine("    - DIN 1988 : İçme ve Kullanma Suyu Tesisatları");
        sb.AppendLine("    - TS EN 12056: Binalar İçi Cazibeli Pis Su Sistemleri");
        sb.AppendLine("    - TS 12514 : Binalardaki Yangın Söndürme Tesisatı");
        sb.AppendLine("1.2 Tesisat imalatı, Çevre, Şehircilik ve İklim Değişikliği Bakanlığı");
        sb.AppendLine("    Birim Fiyat Tarifleri ve Genel Teknik Şartnamelere uygun yapılacaktır.");
        sb.AppendLine("1.3 Tüm borulama sistemleri montaj sonrası basınç testine tabi tutulacaktır.");
        sb.AppendLine("    - Temiz su hatları: 10 bar (24 saat), düşüş toleransı max 0.5 bar");
        sb.AppendLine("    - Pis su hatları : Su dolum testi (Gözle muayene)");
        sb.AppendLine();

        // 2. BORU SİSTEMLERİ
        sb.AppendLine("2. BORU SİSTEMLERİ");
        sb.AppendLine("────────────────────────────────────");
        var materials = _entities.OfType<PipeEntity>().Select(p => p.PipeMaterialType).Distinct();
        foreach (var mat in materials)
        {
            sb.AppendLine($"2.{(int)mat + 1} {GetMaterialFullName(mat)}");
            sb.AppendLine($"    Standart   : {GetMaterialStandard(mat)}");
            sb.AppendLine($"    Basınç Sınıfı: {GetMaterialPressureClass(mat)}");
            sb.AppendLine($"    Birleştirme : {GetMaterialJoiningMethod(mat)}");
            sb.AppendLine($"    Borular üretici kataloglarına ve TSE belgelerine uygun olacaktır.");
            sb.AppendLine($"    Montaj, üretici firma teknik şartnamesine uygun yapılacaktır.");
            sb.AppendLine();
        }

        // 3. HİDROLİK KRİTERLER
        sb.AppendLine("3. HİDROLİK HESAP KRİTERLERİ");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine("3.1 Temiz su hatlarında kritik akış hızı 2.0 m/s değerini aşmayacaktır.");
        sb.AppendLine("    Konut binalarında konfor hız limiti 1.5 m/s olarak uygulanmıştır.");
        sb.AppendLine("3.2 Pis su hatları min %1 - %2 eğimle döşenecektir.");
        sb.AppendLine("    - DN50 hatlar: min %2.5 eğim");
        sb.AppendLine("    - DN75 hatlar: min %2.0 eğim");
        sb.AppendLine("    - DN100 hatlar: min %1.0 eğim");
        sb.AppendLine("3.3 Eş zamanlılık katsayısı TS 1258'e göre bina tipine uygun seçilmiştir.");
        sb.AppendLine("3.4 Basınç kaybı hesaplamaları Darcy-Weisbach formülüne göre yapılmıştır.");
        sb.AppendLine("    Sürtünme katsayısı (f) Colebrook-White denklemi ile belirlenmiştir.");
        sb.AppendLine();

        // 4. YALITIM
        sb.AppendLine("4. BORU YALITIMI");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine("4.1 Sıcak su hatları TS 825'e uygun olarak yalıtılacaktır.");
        sb.AppendLine("4.2 Yalıtım malzemesi: Elastomerik kauçuk (Kapalı hücre) veya Cam yünü boru kabuğu");
        sb.AppendLine("    - DN15-32 hatlar: 19mm kalınlık");
        sb.AppendLine("    - DN40-65 hatlar: 25mm kalınlık");
        sb.AppendLine("    - DN80+ hatlar : 32mm kalınlık");
        sb.AppendLine("4.3 Soğuk su hatları terlemeyi önlemek için min 9mm kauçuk yalıtım ile kaplanacaktır.");
        sb.AppendLine();

        // 5. VİTRİFİYE
        sb.AppendLine("5. VİTRİFİYE VE ARMATÜRLER");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine("5.1 Tüm vitrifiyeler 1. kalite birinci sınıf porselen olacaktır.");
        sb.AppendLine("5.2 Armatürler TS EN 200 / TS EN 816 standartlarına uygun olacaktır.");
        sb.AppendLine("5.3 Bataryalar su tasarruflu (Perlatörlü, max 9 lt/dk) tipte olacaktır.");
        sb.AppendLine("5.4 WC Rezervuarları çift kademeli (3/6 lt) su tasarruflu olacaktır.");
        sb.AppendLine();

        // 6. GENEL NOTLAR
        sb.AppendLine("6. GENEL MONTAJ VE İŞÇİLİK");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine("6.1 Boru tesisatı bina taşıyıcı elemanlarına zarar vermeyecek şekilde döşenecektir.");
        sb.AppendLine("6.2 Boru geçişlerinde yangın durdurucu (Fire Stop) kullanılacaktır.");
        sb.AppendLine("6.3 Tüm gider hatlarına kolay erişilebilir temizleme tıkaçları konulacaktır.");
        sb.AppendLine("6.4 Ana giriş hattında çekvalf, küresel vana ve su sayacı düzeni kurulacaktır.");

        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("Bu şartname AfneyCAD Mühendislik Yazılımı tarafından");
        sb.AppendLine("otomatik olarak üretilmiştir.");
        sb.AppendLine("───────────────────────────────────────────────────────");

        return sb.ToString();
    }

    // --- Poz No Üretimi (Bayındırlık Standart) ---

    private string GetPozNo(PipeMaterial material, double diameter)
    {
        string matCode = material switch
        {
            PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => "PP",
            PipeMaterial.PVC_SN4 => "PV",
            PipeMaterial.PEX_b => "PX",
            PipeMaterial.Steel_Galvanized => "CL",
            PipeMaterial.Silent_PP => "SP",
            _ => "XX"
        };
        return $"25.305.{matCode}{(int)diameter:D3}";
    }

    private string GetFixturePozNo(string type)
    {
        if (type.Contains("WC", StringComparison.OrdinalIgnoreCase) || type.Contains("Toilet", StringComparison.OrdinalIgnoreCase)) return "25.370.1101";
        if (type.Contains("Lavabo", StringComparison.OrdinalIgnoreCase) || type.Contains("Washbasin", StringComparison.OrdinalIgnoreCase)) return "25.385.1101";
        if (type.Contains("Eviye", StringComparison.OrdinalIgnoreCase) || type.Contains("Sink", StringComparison.OrdinalIgnoreCase)) return "25.390.1101";
        if (type.Contains("Duş", StringComparison.OrdinalIgnoreCase) || type.Contains("Shower", StringComparison.OrdinalIgnoreCase)) return "25.405.1101";
        if (type.Contains("Küvet", StringComparison.OrdinalIgnoreCase) || type.Contains("Bathtub", StringComparison.OrdinalIgnoreCase)) return "25.408.1101";
        if (type.Contains("Pisuvar", StringComparison.OrdinalIgnoreCase) || type.Contains("Urinal", StringComparison.OrdinalIgnoreCase)) return "25.375.1101";
        return "25.400.0001";
    }

    // --- Açıklama Üretimi ---

    private string GetPipeDescription(PipeMaterial material, double diameter, MechanicalSystemType systemType)
    {
        string mat = GetMaterialShortName(material);
        string sys = GetSystemName(systemType);
        return $"{mat} {sys} Borusu (DN {diameter:F0}), TS {GetMaterialStandard(material)} uygun";
    }

    private string GetFixtureDescription(string type)
    {
        if (type.Contains("WC", StringComparison.OrdinalIgnoreCase)) return "Klozet (Rezervuarlı, Çift Kademeli 3/6L) Komple Montaj";
        if (type.Contains("Lavabo", StringComparison.OrdinalIgnoreCase)) return "Lavabo (Yarım Ayak, Perlatörlü Batarya dahil) Komple Montaj";
        if (type.Contains("Eviye", StringComparison.OrdinalIgnoreCase)) return "Eviye (Çelik, Batarya dahil) Komple Montaj";
        if (type.Contains("Duş", StringComparison.OrdinalIgnoreCase)) return "Duş Teknesi/Kabin (Süzgeç ve Batarya dahil) Komple Montaj";
        if (type.Contains("Küvet", StringComparison.OrdinalIgnoreCase)) return "Banyo Küveti (Panel, Batarya dahil) Komple Montaj";
        return $"{type} Komple Montaj";
    }

    // --- Malzeme Bilgileri ---

    private string GetMaterialShortName(PipeMaterial m) => m switch
    {
        PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => "PP-R",
        PipeMaterial.PVC_SN4 => "PVC-U",
        PipeMaterial.PEX_b => "PEX-Al-PEX",
        PipeMaterial.Steel_Galvanized => "Çelik (Galv.)",
        PipeMaterial.Silent_PP => "Sessiz PP",
        _ => "Genel"
    };

    private string GetMaterialFullName(PipeMaterial m) => m switch
    {
        PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => "PP-R (Polipropilen Random Kopolimer) Borular",
        PipeMaterial.PVC_SN4 => "PVC-U (Sert Polivinil Klorür) Borular",
        PipeMaterial.PEX_b => "PEX-Al-PEX (Alüminyum Kompozit) Borular",
        PipeMaterial.Steel_Galvanized => "Galvanizli Çelik Borular",
        PipeMaterial.Silent_PP => "Sessiz PP (3 Katmanlı Mineral Takviyeli) Borular",
        _ => "Genel Boru Sistemi"
    };

    private string GetMaterialStandard(PipeMaterial m) => m switch
    {
        PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => "TS 12090 / ISO 15874",
        PipeMaterial.PVC_SN4 => "TS 11382 / ISO 3633",
        PipeMaterial.PEX_b => "TS EN ISO 21003",
        PipeMaterial.Steel_Galvanized => "TS 301 / DIN 2440",
        PipeMaterial.Silent_PP => "TS EN 14366",
        _ => "TS Genel"
    };

    private string GetPipeStandard(PipeMaterial m) => GetMaterialStandard(m);

    private string GetMaterialPressureClass(PipeMaterial m) => m switch
    {
        PipeMaterial.PPRC_PN20 => "PN20 (20°C, 25 yıl) / PN10 (70°C, 25 yıl)",
        PipeMaterial.PPRC_PN25 => "PN25 (20°C, 25 yıl) / PN12.5 (70°C, 25 yıl)",
        PipeMaterial.PVC_SN4 => "PN6 (Cazibeli Pis Su)",
        PipeMaterial.PEX_b => "PN16 (70°C, 50 yıl)",
        PipeMaterial.Steel_Galvanized => "PN16 / PN25",
        PipeMaterial.Silent_PP => "PN10 (Ses Azaltıcı)",
        _ => "Üretici kataloğuna göre"
    };

    private string GetMaterialJoiningMethod(PipeMaterial m) => m switch
    {
        PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => "Soket Füzyon Kaynağı (Polifüzyon Pens ile)",
        PipeMaterial.PVC_SN4 => "Yapıştırma (Solvent Cement) veya Conta (O-Ring)",
        PipeMaterial.PEX_b => "Press-Fitting (Sıkma Bağlantı)",
        PipeMaterial.Steel_Galvanized => "Dişli Bağlantı veya Kaynak",
        PipeMaterial.Silent_PP => "Push-Fit (Kelepçe + O-Ring)",
        _ => "Üretici talimatlarına göre"
    };

    private string GetFittingStandard(MechanicalEntity entity)
    {
        return "TS EN 1452 / ISO 3633";
    }

    private string GetSystemName(MechanicalSystemType type) => type switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater => "Sıcak Su",
        MechanicalSystemType.WasteWater => "Pis Su",
        _ => "Genel"
    };
}

public class SpecItem
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Standard { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
