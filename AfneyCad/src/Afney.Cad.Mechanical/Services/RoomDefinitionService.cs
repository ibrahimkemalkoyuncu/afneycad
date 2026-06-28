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

    // Geometri bazlı cihaz tanıma boyut tablosu (mm)
    private static readonly List<FixtureGeometryProfile> _geometryProfiles = new()
    {
        new("WC", 1.0, 340, 400, 500, 700, 0.85),
        new("Washbasin", 0.5, 400, 550, 300, 450, 0.80),
        new("Shower", 0.8, 800, 1000, 800, 1000, 0.75),
        new("Bathtub", 1.5, 1500, 1800, 600, 800, 0.90),
        new("KitchenSink", 0.8, 500, 800, 400, 600, 0.75),
        new("Urinal", 0.5, 250, 400, 300, 450, 0.80),
        new("Bidet", 0.5, 350, 420, 550, 700, 0.80),
        new("WashingMachine", 0.8, 550, 650, 550, 650, 0.90),
        new("Dishwasher", 0.8, 550, 650, 550, 650, 0.85),
    };

    private (string Type, double FU)? MatchBlockToFixtureType(string blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName)) return null;

        string normalizedName = blockName.ToUpperInvariant()
            .Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G")
            .Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C");

        // 1. Tam eşleşme
        if (_fixtureLibrary.TryGetValue(normalizedName, out var exactMatch))
            return exactMatch;

        // 2. İçerik araması (Contains)
        foreach (var key in _fixtureLibrary.Keys)
        {
            if (normalizedName.Contains(key))
                return _fixtureLibrary[key];
        }

        // 3. Fuzzy matching (Levenshtein mesafesi)
        var fuzzyResult = FuzzyMatch(normalizedName);
        if (fuzzyResult != null)
            return fuzzyResult;

        return null;
    }

    // Geometri bazlı cihaz tanıma — blok boyutlarından cihaz tipi tahmin et
    public FixtureDetectionResult IdentifyFixtureByGeometry(CadBoundingBox bbox)
    {
        double width = Math.Abs(bbox.Max.X - bbox.Min.X);
        double depth = Math.Abs(bbox.Max.Y - bbox.Min.Y);

        // Küçük boyut → genişlik, büyük boyut → derinlik olarak normalize et
        double w = Math.Min(width, depth);
        double d = Math.Max(width, depth);

        FixtureDetectionResult? bestMatch = null;
        double bestConfidence = 0;

        foreach (var profile in _geometryProfiles)
        {
            double wScore = 1.0 - Math.Abs(w - (profile.MinWidth + profile.MaxWidth) / 2.0) / ((profile.MaxWidth - profile.MinWidth) / 2.0 + 100);
            double dScore = 1.0 - Math.Abs(d - (profile.MinDepth + profile.MaxDepth) / 2.0) / ((profile.MaxDepth - profile.MinDepth) / 2.0 + 100);

            wScore = Math.Max(0, Math.Min(1, wScore));
            dScore = Math.Max(0, Math.Min(1, dScore));

            bool wInRange = w >= profile.MinWidth * 0.8 && w <= profile.MaxWidth * 1.2;
            bool dInRange = d >= profile.MinDepth * 0.8 && d <= profile.MaxDepth * 1.2;

            if (wInRange && dInRange)
            {
                double confidence = (wScore + dScore) / 2.0 * profile.BaseConfidence;
                if (confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestMatch = new FixtureDetectionResult
                    {
                        DetectedType = profile.Type,
                        FixtureUnit = profile.FU,
                        Confidence = confidence,
                        Method = "Geometry",
                        MeasuredWidth = w,
                        MeasuredDepth = d
                    };
                }
            }
        }

        return bestMatch ?? new FixtureDetectionResult { DetectedType = "Unknown", Confidence = 0, Method = "None" };
    }

    // Hibrit tanıma — önce isim, sonra geometri
    public FixtureDetectionResult IdentifyFixtureHybrid(string? blockName, CadBoundingBox bbox)
    {
        // 1. İsim bazlı (yüksek güven)
        if (!string.IsNullOrEmpty(blockName))
        {
            var nameMatch = MatchBlockToFixtureType(blockName);
            if (nameMatch != null)
            {
                return new FixtureDetectionResult
                {
                    DetectedType = nameMatch.Value.Type,
                    FixtureUnit = nameMatch.Value.FU,
                    Confidence = 0.95,
                    Method = "Name"
                };
            }
        }

        // 2. Geometri bazlı (orta güven)
        var geoResult = IdentifyFixtureByGeometry(bbox);
        if (geoResult.Confidence > 0.5)
            return geoResult;

        // 3. TS EN 806-2 FU tablosundan çapraz kontrol
        if (!string.IsNullOrEmpty(blockName))
        {
            var fuEntry = FixtureUnitTable.GetEntry(blockName);
            if (fuEntry != null)
            {
                return new FixtureDetectionResult
                {
                    DetectedType = blockName,
                    FixtureUnit = fuEntry.LoadUnits,
                    Confidence = 0.85,
                    Method = "FU_Table"
                };
            }
        }

        return new FixtureDetectionResult { DetectedType = "Unknown", Confidence = 0, Method = "None" };
    }

    // Levenshtein mesafesi ile fuzzy matching
    private (string Type, double FU)? FuzzyMatch(string input)
    {
        int bestDistance = int.MaxValue;
        string? bestKey = null;

        foreach (var key in _fixtureLibrary.Keys)
        {
            int dist = LevenshteinDistance(input, key);
            if (dist < bestDistance && dist <= Math.Max(2, key.Length / 3))
            {
                bestDistance = dist;
                bestKey = key;
            }
        }

        if (bestKey != null)
            return _fixtureLibrary[bestKey];
        return null;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
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

public record FixtureGeometryProfile(
    string Type,
    double FU,
    double MinWidth,
    double MaxWidth,
    double MinDepth,
    double MaxDepth,
    double BaseConfidence
);

public class FixtureDetectionResult
{
    public string DetectedType { get; set; } = "Unknown";
    public double FixtureUnit { get; set; }
    public double Confidence { get; set; }
    public string Method { get; set; } = "None";
    public double MeasuredWidth { get; set; }
    public double MeasuredDepth { get; set; }
}
