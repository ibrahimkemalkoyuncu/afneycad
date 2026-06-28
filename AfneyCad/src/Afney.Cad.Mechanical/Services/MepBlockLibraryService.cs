using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

// MEP Sembol Kütüphanesi — TS 7363 / ISO 4067 standart sembolleri
public static class MepBlockLibraryService
{
    private static readonly List<MepSymbol> Catalog = new()
    {
        // ── Tesisat Armatürleri ──
        new("Lavabo", "PLMB", "lavabo_plan", 550, 450, SymbolCategory.Fixture),
        new("Klozet", "PLMB", "klozet_plan", 400, 700, SymbolCategory.Fixture),
        new("Duş Teknesi", "PLMB", "dus_plan", 900, 900, SymbolCategory.Fixture),
        new("Küvet", "PLMB", "kuvet_plan", 1700, 700, SymbolCategory.Fixture),
        new("Eviye", "PLMB", "eviye_plan", 600, 500, SymbolCategory.Fixture),
        new("Pisuvar", "PLMB", "pisuvar_plan", 350, 400, SymbolCategory.Fixture),
        new("Çamaşır Makinesi", "PLMB", "camasir_plan", 600, 600, SymbolCategory.Fixture),
        new("Bulaşık Makinesi", "PLMB", "bulasik_plan", 600, 600, SymbolCategory.Fixture),

        // ── Vanalar ──
        new("Küresel Vana", "VALVE", "kuresel_vana", 80, 80, SymbolCategory.Valve),
        new("Sürgülü Vana", "VALVE", "surgulu_vana", 80, 80, SymbolCategory.Valve),
        new("Kelebek Vana", "VALVE", "kelebek_vana", 100, 100, SymbolCategory.Valve),
        new("Çekvalf", "VALVE", "cekvalf", 80, 80, SymbolCategory.Valve),
        new("Basınç Düşürücü", "VALVE", "basinc_dusurucu", 120, 80, SymbolCategory.Valve),
        new("Emniyet Ventili", "VALVE", "emniyet_ventili", 80, 80, SymbolCategory.Valve),

        // ── Ekipman ──
        new("Pompa", "EQUIP", "pompa", 200, 150, SymbolCategory.Equipment),
        new("Hidrofor", "EQUIP", "hidrofor", 400, 300, SymbolCategory.Equipment),
        new("Genleşme Tankı", "EQUIP", "genlesme_tanki", 200, 200, SymbolCategory.Equipment),
        new("Su Sayacı", "EQUIP", "su_sayaci", 150, 80, SymbolCategory.Equipment),
        new("Y Filtre", "EQUIP", "y_filtre", 120, 80, SymbolCategory.Equipment),
        new("Boyler", "EQUIP", "boyler", 500, 500, SymbolCategory.Equipment),
        new("Kombi", "EQUIP", "kombi", 400, 300, SymbolCategory.Equipment),

        // ── HVAC ──
        new("Anemostat (Dairesel)", "HVAC", "anemostat_daire", 300, 300, SymbolCategory.HVAC),
        new("Menfez (Dikdörtgen)", "HVAC", "menfez_dikd", 400, 200, SymbolCategory.HVAC),
        new("Fan Coil", "HVAC", "fan_coil", 800, 300, SymbolCategory.HVAC),
        new("Split Klima", "HVAC", "split_klima", 900, 250, SymbolCategory.HVAC),
        new("VRF İç Ünite", "HVAC", "vrf_ic", 800, 250, SymbolCategory.HVAC),
        new("Aspiratör", "HVAC", "aspirator", 300, 300, SymbolCategory.HVAC),
        new("Damper", "HVAC", "damper", 200, 100, SymbolCategory.HVAC),

        // ── Yangın ──
        new("Yangın Dolabı", "FIRE", "yangin_dolabi", 600, 200, SymbolCategory.Fire),
        new("Sprinkler", "FIRE", "sprinkler", 100, 100, SymbolCategory.Fire),
        new("Duman Dedektörü", "FIRE", "duman_ded", 100, 100, SymbolCategory.Fire),

        // ── Pis Su ──
        new("Yer Süzgeci", "DRAIN", "yer_suzgeci", 150, 150, SymbolCategory.Drainage),
        new("Fosseptik", "DRAIN", "fosseptik", 2000, 1500, SymbolCategory.Drainage),
        new("Yağ Tutucu", "DRAIN", "yag_tutucu", 800, 500, SymbolCategory.Drainage),

        // ── İzometrik Semboller ──
        new("Dirsek 90° (Iso)", "ISO", "dirsek90_iso", 50, 50, SymbolCategory.IsoSymbol),
        new("Te (Iso)", "ISO", "te_iso", 50, 50, SymbolCategory.IsoSymbol),
        new("Redüksiyon (Iso)", "ISO", "reduksiyon_iso", 50, 50, SymbolCategory.IsoSymbol),
        new("Vana (Iso)", "ISO", "vana_iso", 50, 50, SymbolCategory.IsoSymbol),
    };

    public static IReadOnlyList<MepSymbol> GetAll() => Catalog;

    public static IEnumerable<MepSymbol> GetByCategory(SymbolCategory category)
        => Catalog.FindAll(s => s.Category == category);

    public static MepSymbol? FindByName(string name)
        => Catalog.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<MepSymbol> Search(string keyword)
        => Catalog.FindAll(s => s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || s.BlockName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    // Basit dikdörtgen sembol geometrisi üret (plan görünümü)
    public static List<Vector3D> GenerateSymbolOutline(MepSymbol symbol, Vector3D insertPoint, double rotation = 0)
    {
        double w = symbol.WidthMm / 2.0;
        double d = symbol.DepthMm / 2.0;
        double cos = Math.Cos(rotation * Math.PI / 180);
        double sin = Math.Sin(rotation * Math.PI / 180);

        var corners = new List<Vector3D>
        {
            new(insertPoint.X + (-w * cos - (-d) * sin), insertPoint.Y + (-w * sin + (-d) * cos), 0),
            new(insertPoint.X + (w * cos - (-d) * sin), insertPoint.Y + (w * sin + (-d) * cos), 0),
            new(insertPoint.X + (w * cos - d * sin), insertPoint.Y + (w * sin + d * cos), 0),
            new(insertPoint.X + (-w * cos - d * sin), insertPoint.Y + (-w * sin + d * cos), 0),
        };
        return corners;
    }
}

public class MepSymbol
{
    public string Name { get; set; }
    public string Prefix { get; set; }
    public string BlockName { get; set; }
    public double WidthMm { get; set; }
    public double DepthMm { get; set; }
    public SymbolCategory Category { get; set; }

    public MepSymbol(string name, string prefix, string blockName, double width, double depth, SymbolCategory category)
    {
        Name = name; Prefix = prefix; BlockName = blockName;
        WidthMm = width; DepthMm = depth; Category = category;
    }
}

public enum SymbolCategory
{
    Fixture, Valve, Equipment, HVAC, Fire, Drainage, IsoSymbol
}
