using Afney.Cad.Mechanical.Models;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Tefriş Veritabanı ve Eşleştirme Servisi (FixtureDatabase)
    NEDEN: Mimari plandaki blok isimlerini (Örn: "LAVABO-100") bizim mekanik cihaz tiplerimize (SanitaryFixtureType.Lavatory) çevirmek için.
    DETAY: Basit bir Dictionary yapısı ve "Contains" mantığı ile çalışır.
*/
public static class FixtureDatabase
{
    // Blok Adı -> Fixture Type Mapping (Sözlük)
    private static readonly Dictionary<string, SanitaryFixtureType> _mapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Türkçeler
        { "LAVABO", SanitaryFixtureType.Lavatory },
        { "LAV", SanitaryFixtureType.Lavatory },
        { "KLOZET", SanitaryFixtureType.WC },
        { "ALAFRANGA", SanitaryFixtureType.WC },
        { "GOMME", SanitaryFixtureType.WC }, // Gömme rezervuar
        { "HELATASI", SanitaryFixtureType.WC }, // Hela taşı
        { "ALATURKA", SanitaryFixtureType.WC }, // Alaturka
        { "DUS", SanitaryFixtureType.Shower },
        { "DUSH", SanitaryFixtureType.Shower },
        { "KUVET", SanitaryFixtureType.Bathtub },
        { "EVIYE", SanitaryFixtureType.Sink },
        { "MUTFAK", SanitaryFixtureType.Sink },
        { "CAMASIR", SanitaryFixtureType.WashingMachine },
        { "BULASIK", SanitaryFixtureType.DishWasher },
        { "SUZGEC", SanitaryFixtureType.FloorDrain },
        { "PISUVAR", SanitaryFixtureType.Urinal },
        { "BIDE", SanitaryFixtureType.Bidet },

        // İngilizceler (Yaygın CAD Blok Adları)
        { "WB", SanitaryFixtureType.Lavatory }, // Wash Basin
        { "SINK", SanitaryFixtureType.Sink },
        { "WC", SanitaryFixtureType.WC },
        { "TOILET", SanitaryFixtureType.WC },
        { "SHOWER", SanitaryFixtureType.Shower },
        { "BATHTUB", SanitaryFixtureType.Bathtub },
        { "URINAL", SanitaryFixtureType.Urinal },
        { "FD", SanitaryFixtureType.FloorDrain },
        { "WM", SanitaryFixtureType.WashingMachine },
        { "DW", SanitaryFixtureType.DishWasher }
    };

    /*
        NE: Blok Adından Tip Bul (GetTypeFromBlockName)
        NASIL: Önce tam eşleşme arar, bulamazsa içerik araması yapar (Örn: "LAVABO_YENI" -> "LAVABO" içerir).
    */
    public static SanitaryFixtureType GetTypeFromBlockName(string blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName)) return SanitaryFixtureType.Unknown;
        
        // 1. Tam Eşleşme
        if (_mapping.TryGetValue(blockName, out var type)) return type;
        
        // 2. İçerik Araması (Key içeren blok adı)
        foreach(var kvp in _mapping)
        {
            // Örn: kvp.Key="LAVABO", blk="LAVABO_YENI" -> TRUE
            if (blockName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)) return kvp.Value;
        }
        
        return SanitaryFixtureType.Unknown;
    }
    
    /*
        NE: Varsayılan Yük Birimi Getir (GetDefaultLoadUnit)
        NEDEN: Tesisat hesabı için başlangıç değerleri. (FINE SANI mantığı)
    */
    public static double GetDefaultLoadUnit(SanitaryFixtureType type)
    {
        return type switch
        {
            SanitaryFixtureType.Lavatory => 1.0,
            SanitaryFixtureType.Bidet => 1.0,
            SanitaryFixtureType.WC => 2.5, // Rezervuarlı (Flatörlü ise 0.25 l/s)
            SanitaryFixtureType.Shower => 2.0,
            SanitaryFixtureType.Bathtub => 4.0,
            SanitaryFixtureType.Sink => 2.0, // Mutfak Eviyesi
            SanitaryFixtureType.WashingMachine => 2.0, 
            SanitaryFixtureType.DishWasher => 2.0,
            SanitaryFixtureType.Urinal => 0.5,
            SanitaryFixtureType.FloorDrain => 2.0, 
            _ => 0.0
        };
    }
}
