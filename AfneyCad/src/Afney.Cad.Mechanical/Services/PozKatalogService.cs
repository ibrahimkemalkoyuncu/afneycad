using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Bayındırlık Birim Fiyat Katalogu (PozKatalogService)
   NEDEN: WasteWaterCalcSheetDialog keşif listesinde, BillOfMaterialsService'te
          ve TechnicalSpecificationService'te tutarlı, güncellenebilir poz no +
          birim fiyat kullanmak için.
   MODEL: PozApp'taki BirimFiyatKitabi / BirimFiyatKalemi mirasından ilham alındı.
          Snapshot fiyat: cetvel oluşturulduğu andaki fiyat korunur, katalog
          değişse de proje kaydı bozulmaz.
   KAYNAK: ÇŞİB (Çevre, Şehircilik ve İklim Değişikliği Bakanlığı) 2024 birim
           fiyat listesi — yaklaşık değerler, KDV hariç.
*/
public class PozKatalogService
{
    // ── Veri Modeli (PozApp.BirimFiyatKalemi uyumlu) ─────────────────────────
    public record PozKalemi(
        string PozNo,
        string Tanim,
        string Birim,
        decimal BirimFiyat,
        string IsGrubu);

    // ── Statik 2024 Katalogu ──────────────────────────────────────────────────
    // İş Grupları: 22=Temiz Su, 23=Sıcak Su, 27=Pis Su/Yağmur, 28=Yangın, 29=Gaz
    private static readonly List<PozKalemi> _builtinKatalog =
    [
        // ── GRUP 22: Temiz Su (Soğuk Su) Boruları ────────────────────────────
        new("22.001/1",  "Çelik boru Ø½\" (DN15) — sıhhi tesisat",              "m",    420m,  "22-Temiz Su"),
        new("22.001/2",  "Çelik boru Ø¾\" (DN20) — sıhhi tesisat",              "m",    520m,  "22-Temiz Su"),
        new("22.001/3",  "Çelik boru Ø1\" (DN25) — sıhhi tesisat",              "m",    650m,  "22-Temiz Su"),
        new("22.001/4",  "Çelik boru Ø1¼\" (DN32) — sıhhi tesisat",             "m",    820m,  "22-Temiz Su"),
        new("22.001/5",  "Çelik boru Ø1½\" (DN40) — sıhhi tesisat",             "m",   1020m,  "22-Temiz Su"),
        new("22.001/6",  "Çelik boru Ø2\" (DN50) — sıhhi tesisat",              "m",   1300m,  "22-Temiz Su"),
        new("22.001/7",  "Çelik boru Ø2½\" (DN65) — sıhhi tesisat",             "m",   1700m,  "22-Temiz Su"),
        new("22.001/8",  "Çelik boru Ø3\" (DN80) — sıhhi tesisat",              "m",   2100m,  "22-Temiz Su"),
        new("22.001/9",  "Çelik boru Ø4\" (DN100) — sıhhi tesisat",             "m",   2750m,  "22-Temiz Su"),

        // PPR (PP-R PN20)
        new("22.010/1",  "PP-R PN20 boru DN20 — içme/kullanma suyu",            "m",    180m,  "22-Temiz Su"),
        new("22.010/2",  "PP-R PN20 boru DN25 — içme/kullanma suyu",            "m",    240m,  "22-Temiz Su"),
        new("22.010/3",  "PP-R PN20 boru DN32 — içme/kullanma suyu",            "m",    320m,  "22-Temiz Su"),
        new("22.010/4",  "PP-R PN20 boru DN40 — içme/kullanma suyu",            "m",    430m,  "22-Temiz Su"),
        new("22.010/5",  "PP-R PN20 boru DN50 — içme/kullanma suyu",            "m",    580m,  "22-Temiz Su"),
        new("22.010/6",  "PP-R PN20 boru DN63 — içme/kullanma suyu",            "m",    780m,  "22-Temiz Su"),

        // ── GRUP 23: Sıcak Su Boruları ────────────────────────────────────────
        new("23.001/1",  "PP-R PN25 boru DN20 — sıcak su tesisatı",             "m",    210m,  "23-Sıcak Su"),
        new("23.001/2",  "PP-R PN25 boru DN25 — sıcak su tesisatı",             "m",    280m,  "23-Sıcak Su"),
        new("23.001/3",  "PP-R PN25 boru DN32 — sıcak su tesisatı",             "m",    380m,  "23-Sıcak Su"),
        new("23.001/4",  "PP-R PN25 boru DN40 — sıcak su tesisatı",             "m",    510m,  "23-Sıcak Su"),
        new("23.001/5",  "PP-R PN25 boru DN50 — sıcak su tesisatı",             "m",    680m,  "23-Sıcak Su"),
        new("23.001/6",  "PP-R PN25 boru DN63 — sıcak su tesisatı",             "m",    920m,  "23-Sıcak Su"),

        // ── GRUP 27: Pis Su / Yağmur Suyu (PVC-U) ────────────────────────────
        new("27.001/1",  "PVC-U SN4 pis su borusu DN50 — yatay/kolon",          "m",    280m,  "27-Pis Su"),
        new("27.001/2",  "PVC-U SN4 pis su borusu DN75 — yatay/kolon",          "m",    380m,  "27-Pis Su"),
        new("27.001/3",  "PVC-U SN4 pis su borusu DN100 — yatay/kolon",         "m",    520m,  "27-Pis Su"),
        new("27.001/4",  "PVC-U SN4 pis su borusu DN125 — yatay/kolon",         "m",    720m,  "27-Pis Su"),
        new("27.001/5",  "PVC-U SN4 pis su borusu DN150 — kolektör",            "m",    980m,  "27-Pis Su"),
        new("27.001/6",  "PVC-U SN4 pis su borusu DN200 — bina içi ana",        "m",   1480m,  "27-Pis Su"),

        new("27.005/1",  "PVC-U SN4 yağmur suyu borusu DN100",                  "m",    540m,  "27-Yağmur"),
        new("27.005/2",  "PVC-U SN4 yağmur suyu borusu DN125",                  "m",    750m,  "27-Yağmur"),
        new("27.005/3",  "PVC-U SN4 yağmur suyu borusu DN160",                  "m",   1050m,  "27-Yağmur"),

        // ── GRUP 27: Vitrifiyeler / Sıhhi Cihazlar ────────────────────────────
        new("27.101",    "Alaturka klozet — porselen, plastik kapak dahil",      "adet", 3800m, "27-Vitrifiye"),
        new("27.102",    "Alafranga klozet — seramik, yavaş kapanan kapak",      "adet", 5500m, "27-Vitrifiye"),
        new("27.103",    "Lavabo — seramik, batarya hariç, montaj dahil",        "adet", 2800m, "27-Vitrifiye"),
        new("27.104",    "Duş teknesi — akrilik 80×80, sifon dahil",             "adet", 3200m, "27-Vitrifiye"),
        new("27.105",    "Evye — paslanmaz çelik tek gözlü, sifon dahil",        "adet", 2200m, "27-Vitrifiye"),
        new("27.106",    "Pisuar — porselen, fotoselli valf dahil",              "adet", 4200m, "27-Vitrifiye"),
        new("27.107",    "Küvet — akrilik 150 cm, gider dahil",                  "adet", 5800m, "27-Vitrifiye"),
        new("27.108",    "Mutfak evyesi — paslanmaz çelik çift gözlü",          "adet", 3200m, "27-Vitrifiye"),

        // ── GRUP 28: Yangın Tesisatı ──────────────────────────────────────────
        new("28.001/1",  "Galvanizli çelik boru DN25 — yangın tesisatı",         "m",    720m,  "28-Yangın"),
        new("28.001/2",  "Galvanizli çelik boru DN32 — yangın tesisatı",         "m",    900m,  "28-Yangın"),
        new("28.001/3",  "Galvanizli çelik boru DN40 — yangın tesisatı",         "m",   1150m,  "28-Yangın"),
        new("28.001/4",  "Galvanizli çelik boru DN50 — yangın tesisatı",         "m",   1450m,  "28-Yangın"),
        new("28.001/5",  "Galvanizli çelik boru DN65 — yangın tesisatı",         "m",   1900m,  "28-Yangın"),
        new("28.001/6",  "Galvanizli çelik boru DN80 — yangın tesisatı",         "m",   2350m,  "28-Yangın"),
        new("28.001/7",  "Galvanizli çelik boru DN100 — yangın tesisatı",        "m",   3100m,  "28-Yangın"),

        // ── GRUP 29: Gaz Tesisatı ─────────────────────────────────────────────
        new("29.001/1",  "Çelik gaz borusu DN15 (1/2\") — doğalgaz",            "m",    480m,  "29-Gaz"),
        new("29.001/2",  "Çelik gaz borusu DN20 (3/4\") — doğalgaz",            "m",    600m,  "29-Gaz"),
        new("29.001/3",  "Çelik gaz borusu DN25 (1\") — doğalgaz",              "m",    750m,  "29-Gaz"),
        new("29.001/4",  "Çelik gaz borusu DN32 (1¼\") — doğalgaz",             "m",    950m,  "29-Gaz"),
        new("29.001/5",  "Çelik gaz borusu DN40 (1½\") — doğalgaz",             "m",   1200m,  "29-Gaz"),
        new("29.001/6",  "Çelik gaz borusu DN50 (2\") — doğalgaz",              "m",   1550m,  "29-Gaz"),
    ];

    private List<PozKalemi> _aktifKatalog = [.. _builtinKatalog];

    // ── Katalog Yükleme ───────────────────────────────────────────────────────

    /// <summary>JSON dosyasından kullanıcı kataloğunu yükle (built-in üzerine override).</summary>
    public void LoadFromJson(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<PozKalemiDto>>(
                File.ReadAllText(jsonPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list is null || list.Count == 0) return;

            var imported = list.Select(d => new PozKalemi(d.PozNo, d.Tanim, d.Birim, d.BirimFiyat, d.IsGrubu))
                               .ToList();
            // Merge: built-in + import (import wins on same PozNo)
            var merged = _builtinKatalog
                .Where(b => !imported.Any(i => i.PozNo == b.PozNo))
                .Concat(imported)
                .ToList();
            _aktifKatalog = merged;
        }
        catch { /* Bozuk JSON — built-in kullan */ }
    }

    public void SaveToJson(string jsonPath)
    {
        var dtos = _aktifKatalog.Select(k => new PozKalemiDto
        {
            PozNo = k.PozNo, Tanim = k.Tanim, Birim = k.Birim,
            BirimFiyat = k.BirimFiyat, IsGrubu = k.IsGrubu
        }).ToList();
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(dtos,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    // ── Sorgulama ─────────────────────────────────────────────────────────────

    public IReadOnlyList<PozKalemi> GetAll() => _aktifKatalog;

    public IReadOnlyList<PozKalemi> GetByIsGrubu(string isGrubu) =>
        _aktifKatalog.Where(k => k.IsGrubu.StartsWith(isGrubu, StringComparison.OrdinalIgnoreCase)).ToList();

    public PozKalemi? FindByPozNo(string pozNo) =>
        _aktifKatalog.FirstOrDefault(k => k.PozNo == pozNo);

    /// <summary>Sistem tipi + iç çap (mm) için en uygun poz kalemini döner.</summary>
    public PozKalemi? FindForPipe(MechanicalSystemType systemType, double innerDiamMm)
    {
        string prefix = systemType switch
        {
            MechanicalSystemType.DomesticColdWater => "22.",
            MechanicalSystemType.DomesticHotWater  => "23.",
            MechanicalSystemType.WasteWater        => "27.001",
            MechanicalSystemType.RainWater         => "27.005",
            MechanicalSystemType.FireProtection    => "28.",
            MechanicalSystemType.Gas               => "29.",
            _                                      => "22."
        };

        // Katalogdan ilgili grubu al, çapa en yakın birini seç
        var candidates = _aktifKatalog
            .Where(k => k.PozNo.StartsWith(prefix) && k.Birim == "m")
            .ToList();

        if (candidates.Count == 0) return null;

        // Tanımdan çap çıkar ve en yakını seç
        return candidates
            .OrderBy(k => Math.Abs(ExtractDiameterFromTanim(k.Tanim) - innerDiamMm))
            .FirstOrDefault();
    }

    public PozKalemi? FindForFixture(string fixtureType)
    {
        string s = fixtureType.ToLowerInvariant();
        string partial = s.Contains("klozet") || s.Contains("wc") ? "klozet"
                       : s.Contains("lavabo")                      ? "lavabo"
                       : s.Contains("duş") || s.Contains("banyo") ? "duş"
                       : s.Contains("evye") && s.Contains("mutfak") ? "mutfak"
                       : s.Contains("evye")                        ? "evye"
                       : s.Contains("pisuar")                      ? "pisuar"
                       : s.Contains("küvet")                       ? "küvet"
                       : "";

        if (string.IsNullOrEmpty(partial)) return null;

        return _aktifKatalog.FirstOrDefault(k =>
            k.Tanim.ToLowerInvariant().Contains(partial) && k.Birim == "adet");
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────────

    private static double ExtractDiameterFromTanim(string tanim)
    {
        // "DN100", "DN 100", "DN50" gibi ifadelerden çap çıkar
        var m = System.Text.RegularExpressions.Regex.Match(tanim, @"DN\s*(\d+)");
        return m.Success ? double.Parse(m.Groups[1].Value) : 50;
    }

    // ── DTO ───────────────────────────────────────────────────────────────────
    private class PozKalemiDto
    {
        public string  PozNo      { get; set; } = "";
        public string  Tanim      { get; set; } = "";
        public string  Birim      { get; set; } = "";
        public decimal BirimFiyat { get; set; }
        public string  IsGrubu    { get; set; } = "";
    }
}
