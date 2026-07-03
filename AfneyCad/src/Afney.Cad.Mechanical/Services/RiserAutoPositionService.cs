using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Kolon Konumlandırma Servisi (RiserAutoPositionService)
   NEDEN: Vitrifiye gruplarının geometrik merkezinden (centroid) optimum riser XY
          konumunu hesaplayarak mühendise başlangıç noktası önermek için.

   NASIL:
   - Vitrifiyeler sistem tipine göre gruplandırılır (soğuk/sıcak/pis su vb.)
   - Her grup için ağırlıklı centroid hesaplanır (LU ağırlığıyla)
   - Mevcut borulara mesafe kontrolü yapılır (yakın riser varsa atlanır)
   - Sonuç: sistem bazlı önerilen XY konumu listesi
*/
public class RiserAutoPositionService
{
    private readonly CadDatabase _database;

    // Mevcut risere bu mesafeden yakın öneri üretme (mm)
    private const double MinDistanceToExistingRiser = 500.0;

    public RiserAutoPositionService(CadDatabase database)
    {
        _database = database;
    }

    public class RiserSuggestion
    {
        public MechanicalSystemType SystemType { get; init; }
        public Vector3D Position         { get; init; }
        public double   WeightedLU       { get; init; }
        public int      FixtureCount     { get; init; }
        public string   Label            { get; init; } = "";
        public bool     HasNearbyRiser   { get; init; }
    }

    // ── Ana Metot ─────────────────────────────────────────────────────────────

    public List<RiserSuggestion> SuggestRiserPositions()
    {
        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
        if (fixtures.Count == 0) return [];

        // Mevcut dikey borular (riser) — çakışma tespiti için
        var existingRisers = _database.GetAllEntities().OfType<PipeEntity>()
            .Where(p => IsVertical(p))
            .ToList();

        // Sistem tipine göre grupla
        var groups = fixtures
            .GroupBy(f => ResolveSystem(f))
            .ToList();

        var results = new List<RiserSuggestion>();

        foreach (var group in groups)
        {
            var sysType = group.Key;
            var members = group.ToList();

            // LU ağırlıklı centroid
            double totalLU = members.Sum(f => f.LoadUnits);
            if (totalLU <= 0) totalLU = members.Count;

            double cx = members.Sum(f => f.Position.X * (f.LoadUnits > 0 ? f.LoadUnits : 1.0)) / totalLU;
            double cy = members.Sum(f => f.Position.Y * (f.LoadUnits > 0 ? f.LoadUnits : 1.0)) / totalLU;

            var proposed = new Vector3D(cx, cy, 0);

            // Yakın riser var mı?
            bool hasNearby = existingRisers.Any(r =>
            {
                var rXY = new Vector3D(r.StartPoint.X, r.StartPoint.Y, 0);
                return proposed.DistanceTo(rXY) < MinDistanceToExistingRiser;
            });

            results.Add(new RiserSuggestion
            {
                SystemType     = sysType,
                Position       = proposed,
                WeightedLU     = Math.Round(totalLU, 2),
                FixtureCount   = members.Count,
                Label          = SystemLabel(sysType),
                HasNearbyRiser = hasNearby
            });
        }

        return results.OrderBy(r => r.SystemType).ToList();
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private static bool IsVertical(PipeEntity p)
    {
        var dir = (p.EndPoint - p.StartPoint).Normalize();
        return Math.Abs(dir.Z) > 0.8;
    }

    // Vitrifiye port adından sistem tipini çıkar
    private static MechanicalSystemType ResolveSystem(SanitaryFixtureEntity f)
    {
        // Pis su porta sahip cihazlar (WC, duş, lavabo, küvet, eviye) → WasteWater
        bool hasDrainage = f.GetPorts().Any(p =>
            p.Name.Contains("Drain", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Drainage", StringComparison.OrdinalIgnoreCase));
        if (hasDrainage) return MechanicalSystemType.WasteWater;
        return MechanicalSystemType.DomesticColdWater;
    }

    private static string SystemLabel(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater  => "Sıcak Su",
        MechanicalSystemType.WasteWater        => "Pis Su",
        MechanicalSystemType.RainWater         => "Yağmur",
        MechanicalSystemType.FireProtection    => "Yangın",
        MechanicalSystemType.Gas               => "Gaz",
        _                                      => "Genel"
    };
}
