using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Mahal Tanımlama Servisi (RoomDefinitionService)
    NEDEN: Kullanıcının belirlediği kapalı alan (Mahal) içerisindeki mimari blokları (Vitrifiye)
           otomatik olarak tespit edip akıllı tesisat nesnelerine (SanitaryFixture) dönüştürmek için.

    NASIL (Mühendislik Detayı):
    1. Poligon İçinde Nokta (Point-In-Polygon) algoritması ile mahal sınırları içindeki nesneleri bulur.
    2. Blok isimleri (ParentBlockName) üzerinden "Dictionary Matching" yaparak cihaz tipini belirler.
    3. TS 1258 / DIN 1988 standartlarına göre varsayılan Fixture Unit (FU) değerlerini atar.
*/
public class RoomDefinitionService
{
    private readonly CadDatabase _database;

    public RoomDefinitionService(CadDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Mahal sınırları içindeki armatürleri tespit eder ve akıllı nesnelere dönüştürür.
    /// </summary>
    /// <param name="boundaryPoints">Mahal sınırlarını oluşturan kapalı poligon noktaları.</param>
    /// <returns>Tespit edilen ve oluşturulan SanitaryFixtureEntity listesi.</returns>
    public List<SanitaryFixtureEntity> IdentifyFixturesInRoom(List<Vector3D> boundaryPoints)
    {
        var result = new List<SanitaryFixtureEntity>();

        // 1. Adayları Bul (Performans için önce Bounding Box sorgusu)
        var bbox = GetPolygonBoundingBox(boundaryPoints);
        var candidates = _database.QueryEntities(bbox);

        // 2. Blok Gruplaması Yap (Aynı bloğa ait çizgileri tek cihaz say)
        var blockGroups = candidates
            .Where(e => e.IsFromBlock && !string.IsNullOrEmpty(e.ParentBlockName))
            .GroupBy(e => e.ParentBlockId);

        foreach (var group in blockGroups)
        {
            var firstEntity = group.First();
            var blockName = firstEntity.ParentBlockName?.ToUpperInvariant() ?? "";
            
            // Konum Belirleme: Eğer elimizde Insert Point varsa onu kullan, yoksa BBox merkezi
            Vector3D fixturePos = firstEntity.SourceBlockPosition ?? group.GetBoundingBox().Center;

            // 3. Poligon İçinde mi? (Hassas Kontrol)
            if (IsPointInPolygon(fixturePos, boundaryPoints))
            {
                // 4. Blok İsmine Göre Tiplendirme (Rule-Based Matching)
                var matchedType = MatchBlockToFixtureType(blockName);
                
                if (matchedType != null)
                {
                    // Cihaz oluştur
                    var fixture = new SanitaryFixtureEntity(fixturePos, matchedType.Value.Type, matchedType.Value.FU);
                    
                    // Rotasyon (Eğer okunabildiyse)
                    if (firstEntity.SourceBlockRotation.HasValue)
                    {
                        fixture.Rotation = firstEntity.SourceBlockRotation.Value;
                    }

                    // Sisteme Ekle
                    result.Add(fixture);
                }
            }
        }

        return result;
    }

    // GENİŞLETİLEBİLİR CİHAZ KÜTÜPHANESİ (Fixture Library)
    // Blok isimlerinden (AutoCAD Block Name) standart cihaz tiplerine eşleme yapar.
    // Büyük/küçük harf duyarlılığı olmadan çalışır.
    private static readonly Dictionary<string, (string Type, double FU)> _fixtureLibrary = new()
    {
        // LAVABOLAR (Washbasins)
        { "LAVABO", ("Washbasin", 0.5) },
        { "WASHBASIN", ("Washbasin", 0.5) },
        { "SINK", ("Washbasin", 0.5) }, // Genel
        { "AYAKLI_LAVABO", ("Washbasin", 0.5) },
        { "YARIM_AYAK_LAVABO", ("Washbasin", 0.5) },
        { "CANAK_LAVABO", ("Washbasin", 0.5) },
        { "WB", ("Washbasin", 0.5) },
        { "LB", ("Washbasin", 0.5) }, // Kısaltma

        // KLOZETLER (WC)
        { "KLOZET", ("WC", 1.0) }, // Rezervuarlı
        { "WC", ("WC", 1.0) },
        { "TOILET", ("WC", 1.0) },
        { "GOMME_REZERVUAR", ("WC", 1.0) },
        { "ASMA_KLOZET", ("WC", 1.0) },
        { "HELA_TASI", ("SquatToilet", 1.0) },
        { "ALATURKA", ("SquatToilet", 1.0) },

        // DUŞ VE KÜVETLER (Showers & Bathtubs)
        { "DUS", ("Shower", 0.8) },
        { "DUŞ", ("Shower", 0.8) },
        { "SHOWER", ("Shower", 0.8) },
        { "DUS_TEKNESI", ("Shower", 0.8) },
        { "KUVET", ("Bathtub", 1.5) },
        { "KÜVET", ("Bathtub", 1.5) },
        { "BATHTUB", ("Bathtub", 1.5) },
        { "JACUZZI", ("Bathtub", 2.0) }, // Jakuzi

        // MUTFAK (Kitchen)
        { "EVIYE", ("KitchenSink", 0.8) },
        { "EVİYE", ("KitchenSink", 0.8) },
        { "KITCHEN_SINK", ("KitchenSink", 0.8) },
        { "KS", ("KitchenSink", 0.8) },
        { "BULASIK", ("Dishwasher", 0.8) },
        { "BULAŞIK", ("Dishwasher", 0.8) },
        { "DISHWASHER", ("Dishwasher", 0.8) },
        { "DW", ("Dishwasher", 0.8) },

        // ÇAMAŞIR (Laundry)
        { "CAMASIR", ("WashingMachine", 0.8) },
        { "ÇAMAŞIR", ("WashingMachine", 0.8) },
        { "WASHING_MACHINE", ("WashingMachine", 0.8) },
        { "WM", ("WashingMachine", 0.8) },

        // DİĞER (Other)
        { "PISUVAR", ("Urinal", 0.5) },
        { "URINAL", ("Urinal", 0.5) },
        { "SUZGEC", ("FloorDrain", 0.5) }, // Yer süzgeci
        { "SÜZGEÇ", ("FloorDrain", 0.5) },
        { "FD", ("FloorDrain", 0.5) }
    };

    private (string Type, double FU)? MatchBlockToFixtureType(string blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName)) return null;

        string normalizedName = blockName.ToUpperInvariant().Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G").Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C");

        // 1. Tam Eşleşme Kontrolü
        if (_fixtureLibrary.TryGetValue(normalizedName, out var exactMatch))
            return exactMatch;

        // 2. İçerik Araması (Contains)
        foreach (var key in _fixtureLibrary.Keys)
        {
            if (normalizedName.Contains(key))
            {
                return _fixtureLibrary[key];
            }
        }

        return null;
    }

    // Geometri Yardımcıları
    private CadBoundingBox GetPolygonBoundingBox(List<Vector3D> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new CadBoundingBox(new Vector3D(minX, minY, -1000), new Vector3D(maxX, maxY, 1000));
    }

    private bool IsPointInPolygon(Vector3D p, List<Vector3D> polygon)
    {
        // Ray-Casting Algoritması
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }
}

// Extention Helper for Group of Entities
public static class CadGroupExtensions 
{
    public static CadBoundingBox GetBoundingBox(this IGrouping<Guid?, CadEntity> group)
    {
        // Basit implementation
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var ent in group) {
            var box = ent.GetBoundingBox();
            if(box.Min.X < minX) minX = box.Min.X;
            if(box.Min.Y < minY) minY = box.Min.Y;
            if(box.Max.X > maxX) maxX = box.Max.X;
            if(box.Max.Y > maxY) maxY = box.Max.Y;
        }
        return new CadBoundingBox(new Vector3D(minX, minY, 0), new Vector3D(maxX, maxY, 0));
    }
}
