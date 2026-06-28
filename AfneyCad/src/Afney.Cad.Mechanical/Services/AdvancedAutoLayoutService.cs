using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// Gelişmiş Otomatik Vitrifiye Yerleşim — duvar algılama, minimum mesafe, erişilebilirlik
public class AdvancedAutoLayoutService
{
    // TS 9111 — Engelli erişim minimum mesafeleri (mm)
    private static readonly Dictionary<string, PlacementRule> PlacementRules = new()
    {
        ["WC"] = new(400, 800, 200, 600, true, "Duvar yanında, karşısında min 600mm boşluk"),
        ["Lavabo"] = new(550, 450, 150, 500, false, "Duvar üstünde, önünde min 500mm boşluk"),
        ["Duş"] = new(900, 900, 0, 800, true, "Köşe veya duvar kenarı, min 800mm giriş"),
        ["Küvet"] = new(1700, 700, 0, 600, true, "Duvar boyunca, min 600mm yan boşluk"),
        ["Eviye"] = new(600, 500, 150, 500, false, "Tezgah altı, duvar üstü"),
        ["Çamaşır Makinesi"] = new(600, 600, 50, 600, false, "Duvar kenarı, su bağlantısı yakını"),
        ["Bulaşık Makinesi"] = new(600, 600, 50, 400, false, "Tezgah altı"),
    };

    public List<SanitaryFixtureEntity> AutoFurnishRoom(
        RoomEntity room,
        IEnumerable<RoomStandard>? standards = null)
    {
        var result = new List<SanitaryFixtureEntity>();
        var boundary = room.BoundaryPoints;
        if (boundary.Count < 3) return result;

        // Oda tipine göre standart kütüphanesinden gereksinimleri al
        var roomStd = standards?.FirstOrDefault() ?? RoomStandardsLibrary.FindByName(room.RoomName ?? "Banyo");
        if (roomStd == null) return result;

        var walls = ExtractWalls(boundary);
        double usedLength = 0;

        foreach (var fixture in roomStd.Fixtures)
        {
            var rule = PlacementRules.GetValueOrDefault(fixture.Name, PlacementRules.Values.First());

            for (int q = 0; q < fixture.Quantity; q++)
            {
                var placement = FindBestPlacement(walls, rule, boundary, result, usedLength);
                if (placement == null) continue;

                var entity = new SanitaryFixtureEntity(placement.Value, fixture.Name, fixture.LoadUnits)
                {
                    Layer = "MEP_FIXTURES",
                    Color = 0xFF00CCFF
                };

                result.Add(entity);
                usedLength += rule.Width + 200; // 200mm boşluk
            }
        }

        return result;
    }

    private List<(Vector3D Start, Vector3D End, double Length)> ExtractWalls(List<Vector3D> boundary)
    {
        var walls = new List<(Vector3D, Vector3D, double)>();
        for (int i = 0; i < boundary.Count; i++)
        {
            var start = boundary[i];
            var end = boundary[(i + 1) % boundary.Count];
            double len = (end - start).Length();
            if (len > 100) walls.Add((start, end, len));
        }
        return walls.OrderByDescending(w => w.Item3).ToList();
    }

    private Vector3D? FindBestPlacement(
        List<(Vector3D Start, Vector3D End, double Length)> walls,
        PlacementRule rule,
        List<Vector3D> boundary,
        List<SanitaryFixtureEntity> existing,
        double offset)
    {
        foreach (var wall in walls)
        {
            double wallLen = wall.Item3;
            var start = wall.Item1;
            var end = wall.Item2;
            if (wallLen < rule.Width + offset) continue;

            var dir = (end - start).Normalize();
            var wallNormal = new Vector3D(-dir.Y, dir.X, 0);

            double placement = offset + rule.Width / 2.0 + 200;
            if (placement > wallLen - 200) continue;

            var position = start + dir * placement + wallNormal * (rule.WallOffset + rule.Depth / 2.0);

            // Mevcut cihazlarla çakışma kontrolü
            bool conflicts = existing.Any(e => (e.Position - position).Length() < 400);
            if (conflicts) continue;

            return position;
        }
        return null;
    }
}

public record PlacementRule(
    double Width,        // mm
    double Depth,        // mm
    double WallOffset,   // Duvardan mesafe (mm)
    double FrontClear,   // Ön boşluk (mm)
    bool NeedsWall,      // Duvara yaslanmalı mı?
    string Description
);
