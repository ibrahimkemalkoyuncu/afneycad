using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// TS 1258, TS EN 806, DIN 1988, Yönetmelik referanslarıyla oda tipi kütüphanesi
public static class RoomStandardsLibrary
{
    private static readonly List<RoomStandard> _standards = new()
    {
        // ── KONUT ──
        new("Banyo", "Konut", 4.0, 8.0, 22, 5.0, new[] {
            new FixtureRequirement("Lavabo", 1, 0.1, 1.0),
            new FixtureRequirement("Duş/Küvet", 1, 0.2, 3.0),
            new FixtureRequirement("Klozet", 1, 0.1, 5.0),
        }),
        new("WC", "Konut", 1.5, 3.0, 22, 5.0, new[] {
            new FixtureRequirement("Lavabo", 1, 0.1, 1.0),
            new FixtureRequirement("Klozet", 1, 0.1, 5.0),
        }),
        new("Mutfak", "Konut", 8.0, 15.0, 22, 4.0, new[] {
            new FixtureRequirement("Evye", 1, 0.2, 3.0),
            new FixtureRequirement("Bulaşık Makinesi", 1, 0.15, 2.0),
        }),
        new("Yatak Odası", "Konut", 10.0, 20.0, 22, 0.5, System.Array.Empty<FixtureRequirement>()),
        new("Salon", "Konut", 18.0, 35.0, 22, 0.7, System.Array.Empty<FixtureRequirement>()),
        new("Çamaşır Odası", "Konut", 3.0, 6.0, 20, 3.0, new[] {
            new FixtureRequirement("Çamaşır Makinesi", 1, 0.15, 2.0),
            new FixtureRequirement("Çamaşır Teknesi", 1, 0.2, 3.0),
        }),

        // ── OFİS ──
        new("Ofis (Açık Plan)", "Ofis", 20.0, 200.0, 22, 1.5, System.Array.Empty<FixtureRequirement>()),
        new("Ofis WC (Erkek)", "Ofis", 6.0, 15.0, 22, 5.0, new[] {
            new FixtureRequirement("Lavabo", 2, 0.1, 1.0),
            new FixtureRequirement("Klozet", 1, 0.1, 5.0),
            new FixtureRequirement("Pisuvar", 2, 0.1, 2.0),
        }),
        new("Ofis WC (Kadın)", "Ofis", 6.0, 15.0, 22, 5.0, new[] {
            new FixtureRequirement("Lavabo", 2, 0.1, 1.0),
            new FixtureRequirement("Klozet", 2, 0.1, 5.0),
        }),
        new("Toplantı Odası", "Ofis", 15.0, 50.0, 22, 4.0, System.Array.Empty<FixtureRequirement>()),

        // ── TİCARİ ──
        new("Restoran Mutfak", "Ticari", 20.0, 60.0, 22, 6.0, new[] {
            new FixtureRequirement("Endüstriyel Evye", 2, 0.3, 3.0),
            new FixtureRequirement("Bulaşık Makinesi", 1, 0.3, 4.0),
            new FixtureRequirement("Gider", 1, 0.0, 0.0),
        }),
        new("Otel Odası Banyo", "Ticari", 5.0, 10.0, 24, 5.0, new[] {
            new FixtureRequirement("Lavabo", 1, 0.1, 1.0),
            new FixtureRequirement("Duş/Küvet", 1, 0.2, 3.0),
            new FixtureRequirement("Klozet", 1, 0.1, 5.0),
        }),

        // ── SAĞLIK ──
        new("Hasta Odası", "Hastane", 12.0, 25.0, 24, 2.0, new[] {
            new FixtureRequirement("Lavabo", 1, 0.1, 1.0),
        }),
        new("Ameliyathane", "Hastane", 25.0, 45.0, 22, 8.0, new[] {
            new FixtureRequirement("Cerrahi Lavabo", 2, 0.2, 2.0),
        }),
        new("Hastane WC", "Hastane", 4.0, 8.0, 22, 6.0, new[] {
            new FixtureRequirement("Lavabo", 1, 0.1, 1.0),
            new FixtureRequirement("Klozet (Engelli)", 1, 0.1, 5.0),
        }),

        // ── EĞİTİM ──
        new("Sınıf", "Eğitim", 30.0, 70.0, 22, 3.0, System.Array.Empty<FixtureRequirement>()),
        new("Okul WC", "Eğitim", 8.0, 20.0, 22, 5.0, new[] {
            new FixtureRequirement("Lavabo", 3, 0.1, 1.0),
            new FixtureRequirement("Klozet", 2, 0.1, 5.0),
            new FixtureRequirement("Pisuvar", 2, 0.1, 2.0),
        }),
        new("Laboratuvar", "Eğitim", 20.0, 50.0, 22, 6.0, new[] {
            new FixtureRequirement("Lab Evye", 2, 0.15, 2.0),
        }),

        // ── ENDÜSTRİ ──
        new("Soyunma/Duş", "Endüstri", 15.0, 40.0, 22, 5.0, new[] {
            new FixtureRequirement("Duş", 4, 0.2, 3.0),
            new FixtureRequirement("Lavabo", 3, 0.1, 1.0),
        }),
    };

    public static IReadOnlyList<RoomStandard> GetAll() => _standards;

    public static IEnumerable<RoomStandard> GetByBuildingType(string buildingType)
        => _standards.Where(s => s.BuildingType.Equals(buildingType, System.StringComparison.OrdinalIgnoreCase));

    public static RoomStandard? FindByName(string roomName)
        => _standards.FirstOrDefault(s => s.RoomName.Equals(roomName, System.StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> GetBuildingTypes()
        => _standards.Select(s => s.BuildingType).Distinct();

    public static double GetTotalFixtureUnits(RoomStandard room)
        => room.Fixtures.Sum(f => f.Quantity * f.LoadUnits);
}

public record RoomStandard(
    string RoomName,
    string BuildingType,
    double MinAreaM2,
    double MaxAreaM2,
    double DesignTempC,
    double AirChangesPerHour,
    FixtureRequirement[] Fixtures
);

public record FixtureRequirement(
    string Name,
    int Quantity,
    double FlowRateLs,
    double LoadUnits
);
