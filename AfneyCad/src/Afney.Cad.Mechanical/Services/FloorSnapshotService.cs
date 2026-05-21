using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Blocks;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

public class FloorSnapshotService
{
    private const string SnapshotPrefix = "SNAP_";
    private const double FloorClusterTolerance = 500.0; // mm

    // ── Kat tespiti ──────────────────────────────────────────────────────────
    public List<FloorRange> DetectFloors(CadDatabase database)
    {
        var zValues = database.GetAllEntities()
            .Select(e =>
            {
                try { return e.GetBoundingBox().Center.Z; }
                catch { return double.NaN; }
            })
            .Where(z => !double.IsNaN(z) && z > -100_000 && z < 100_000)
            .OrderBy(z => z)
            .ToList();

        if (zValues.Count == 0)
            return [new FloorRange("Zemin Kat", -1, double.MaxValue)];

        var floors = new List<FloorRange>();
        double clusterStart = zValues[0];
        double clusterCenter = zValues[0];
        int clusterIndex = 0;

        for (int i = 1; i < zValues.Count; i++)
        {
            if (zValues[i] - clusterCenter > FloorClusterTolerance)
            {
                string name = clusterIndex == 0 ? "Zemin Kat" : $"{clusterIndex}. Kat";
                floors.Add(new FloorRange(name, clusterStart - 1, clusterCenter + FloorClusterTolerance / 2));
                clusterStart = zValues[i];
                clusterIndex++;
            }
            clusterCenter = zValues[i];
        }

        string lastName = clusterIndex == 0 ? "Zemin Kat" : $"{clusterIndex}. Kat";
        floors.Add(new FloorRange(lastName, clusterStart - 1, double.MaxValue));
        return floors;
    }

    // ── Ekran çizimi → CadBlockRecord ────────────────────────────────────────
    public CadBlockRecord CaptureToBlock(
        CadDatabase database,
        string name,
        MechanicalSystemType? systemFilter, // null = tümü
        double zMin = double.MinValue,
        double zMax = double.MaxValue)
    {
        string blockName = SnapshotPrefix + name.ToUpperInvariant().Replace(" ", "_");

        var entities = database.GetAllEntities().Where(e =>
        {
            double z;
            try { z = e.GetBoundingBox().Center.Z; }
            catch { z = 0; }

            if (z < zMin || z > zMax) return false;

            // Mekanik entity: sistem filtresine göre
            if (e is MechanicalEntity mech && systemFilter.HasValue)
            {
                var sysType = (mech as PipeEntity)?.SystemType
                           ?? (mech as SanitaryFixtureEntity)?.SystemType
                           ?? MechanicalSystemType.Undefined;

                return systemFilter.Value switch
                {
                    MechanicalSystemType.DomesticColdWater =>
                        sysType == MechanicalSystemType.DomesticColdWater ||
                        sysType == MechanicalSystemType.DomesticHotWater,
                    MechanicalSystemType.WasteWater =>
                        sysType == MechanicalSystemType.WasteWater ||
                        sysType == MechanicalSystemType.RainWater,
                    _ => true
                };
            }

            return true; // Mimari entity — her zaman dahil
        }).ToList();

        var block = new CadBlockRecord(blockName)
        {
            Entities = entities.Select(e => e.Clone()).ToList()
        };

        // Mevcut aynı isimli bloğu güncelle ya da yenisini ekle
        database.AddBlock(block);
        return block;
    }

    // ── Mevcut snapshot blokları listele ─────────────────────────────────────
    public List<SnapshotInfo> GetSnapshots(CadDatabase database)
    {
        var refs = database.GetAllEntities()
            .OfType<BlockReferenceEntity>()
            .GroupBy(r => r.BlockName)
            .ToDictionary(g => g.Key, g => g.Count());

        return database.GetBlocks()
            .Where(b => b.Name.StartsWith(SnapshotPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(b => new SnapshotInfo
            {
                BlockName   = b.Name,
                DisplayName = b.Name[SnapshotPrefix.Length..],
                EntityCount = b.Entities.Count,
                IsReferenced = refs.TryGetValue(b.Name, out int c) && c > 0,
                RefCount    = refs.GetValueOrDefault(b.Name)
            })
            .OrderBy(s => s.DisplayName)
            .ToList();
    }

    // ── Yardımcı veri sınıfları ───────────────────────────────────────────────
    public class FloorRange
    {
        public string Name { get; }
        public double ZMin { get; }
        public double ZMax { get; }
        public FloorRange(string name, double zMin, double zMax)
        { Name = name; ZMin = zMin; ZMax = zMax; }
        public override string ToString() => Name;
    }

    public class SnapshotInfo
    {
        public string BlockName   { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int    EntityCount { get; set; }
        public bool   IsReferenced { get; set; }
        public int    RefCount    { get; set; }
        public string StatusText => IsReferenced ? $"Bağlı ({RefCount}×)" : "Serbest";
    }
}
