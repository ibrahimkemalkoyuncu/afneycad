using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

// Çok Katlı Bina Gelişmiş Servisi — tüm eksikleri kapatır
public class MultiStoryEnhancementService
{
    private readonly CadDatabase _database;
    private readonly LevelManager _levelManager;

    public MultiStoryEnhancementService(CadDatabase database, LevelManager levelManager)
    {
        _database = database;
        _levelManager = levelManager;
    }

    // ═══ 1. KAT YÖNETİMİ GELİŞMİŞ ═══

    // Kat silme (entity'leri de siler)
    public int RemoveLevel(string levelName)
    {
        var level = _levelManager.GetLevels().FirstOrDefault(l => l.Name == levelName);
        if (level == null) return 0;

        var entities = GetEntitiesOnLevel(level).ToList();
        foreach (var ent in entities)
            _database.RemoveEntity(ent.Id);

        _levelManager.RemoveLevel(levelName);
        return entities.Count;
    }

    // Kat sırasını değiştir (elevation güncelle)
    public void ReorderLevel(string levelName, int newIndex)
    {
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        var target = levels.FirstOrDefault(l => l.Name == levelName);
        if (target == null) return;

        levels.Remove(target);
        newIndex = Math.Clamp(newIndex, 0, levels.Count);
        levels.Insert(newIndex, target);

        // Yükseklikleri yeniden hesapla
        double currentElev = levels.First().Elevation;
        foreach (var level in levels)
        {
            double oldElev = level.Elevation;
            level.Elevation = currentElev;

            // Entity'leri yeni yüksekliğe taşı
            if (Math.Abs(oldElev - currentElev) > 1.0)
            {
                double dz = currentElev - oldElev;
                foreach (var ent in GetEntitiesOnLevel(level, oldElev))
                    ent.Transform(Matrix4x4.TranslationMatrix(0, 0, dz));
            }

            currentElev += level.Height;
        }
    }

    // Elevation gap kontrolü
    public List<string> ValidateLevelGaps()
    {
        var warnings = new List<string>();
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();

        for (int i = 1; i < levels.Count; i++)
        {
            double gap = levels[i].Elevation - (levels[i - 1].Elevation + levels[i - 1].Height);
            if (Math.Abs(gap) > 10) // 10mm tolerans
                warnings.Add($"{levels[i - 1].Name} ↔ {levels[i].Name}: {gap:F0}mm boşluk/çakışma");
        }
        return warnings;
    }

    // ═══ 2. KAT KOPYALAMA GELİŞMİŞ ═══

    // MEP bağlantı korumalı kat kopyalama
    public FloorCopyWithConnectionResult CopyFloorWithConnections(MepLevel source, MepLevel target)
    {
        var result = new FloorCopyWithConnectionResult();
        double dz = target.Elevation - source.Elevation;

        var sourceEntities = GetEntitiesOnLevel(source).ToList();
        var idMap = new Dictionary<Guid, Guid>(); // eski → yeni ID eşleme

        // 1. Entity'leri kopyala
        foreach (var entity in sourceEntities)
        {
            var clone = entity.Clone();
            var newId = Guid.NewGuid();
            idMap[entity.Id] = newId;
            clone.Id = newId;
            clone.Transform(Matrix4x4.TranslationMatrix(0, 0, dz));
            _database.AddEntity(clone);
            result.CopiedCount++;
        }

        // 2. Boru bağlantılarını koru (start/end point eşleştirme)
        var copiedPipes = _database.GetAllEntities()
            .OfType<PipeEntity>()
            .Where(p => idMap.ContainsValue(p.Id))
            .ToList();

        foreach (var pipe in copiedPipes)
        {
            // Yakın uçları bağla
            foreach (var other in copiedPipes.Where(o => o.Id != pipe.Id))
            {
                if ((pipe.EndPoint - other.StartPoint).Length() < 50)
                {
                    pipe.EndPoint = other.StartPoint; // Snap
                    result.ConnectionsPreserved++;
                }
            }
        }

        // 3. Riser bağlantısı — alt kattan üst kata dikey boru ekle
        var sourceRisers = sourceEntities.OfType<PipeEntity>()
            .Where(p => Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) > 0.9)
            .ToList();

        foreach (var riser in sourceRisers)
        {
            var topPoint = riser.EndPoint.Z > riser.StartPoint.Z ? riser.EndPoint : riser.StartPoint;
            var bottomOfNew = topPoint + new Vector3D(0, 0, dz - source.Height);
            var topOfNew = topPoint + new Vector3D(0, 0, dz);

            // Kat arası bağlantı borusu
            if (Math.Abs(dz) > 100)
            {
                var connectionPipe = new PipeEntity(topPoint, bottomOfNew, riser.InnerDiameter)
                {
                    SystemType = riser.SystemType,
                    PipeMaterialType = riser.PipeMaterialType,
                    Layer = riser.Layer
                };
                connectionPipe.ApplySystemColor();
                _database.AddEntity(connectionPipe);
                result.RiserConnectionsCreated++;
            }
        }

        return result;
    }

    // Ayna kopyalama (X veya Y ekseninde)
    public int MirrorFloor(MepLevel source, MepLevel target, bool mirrorX = true)
    {
        double dz = target.Elevation - source.Elevation;
        var entities = GetEntitiesOnLevel(source).ToList();
        int copied = 0;

        foreach (var entity in entities)
        {
            var clone = entity.Clone();
            clone.Id = Guid.NewGuid();

            // Aynalama matrisi
            var mirrorMat = mirrorX
                ? Matrix4x4.Scaling(-1, 1, 1)
                : Matrix4x4.Scaling(1, -1, 1);
            var translateMat = Matrix4x4.TranslationMatrix(0, 0, dz);

            clone.Transform(mirrorMat);
            clone.Transform(translateMat);
            _database.AddEntity(clone);
            copied++;
        }
        return copied;
    }

    // ═══ 3. 3D MONTAJ GELİŞMİŞ ═══

    // Kat arası otomatik riser bağlantısı
    public int AutoConnectInterFloorRisers(double toleranceMm = 100)
    {
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        int connections = 0;

        for (int i = 0; i < levels.Count - 1; i++)
        {
            var lowerPipes = GetEntitiesOnLevel(levels[i]).OfType<PipeEntity>()
                .Where(p => Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) > 0.9)
                .ToList();

            var upperPipes = GetEntitiesOnLevel(levels[i + 1]).OfType<PipeEntity>()
                .Where(p => Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) > 0.9)
                .ToList();

            foreach (var lower in lowerPipes)
            {
                var topEnd = lower.EndPoint.Z > lower.StartPoint.Z ? lower.EndPoint : lower.StartPoint;

                var matchingUpper = upperPipes.FirstOrDefault(u =>
                {
                    var bottomEnd = u.EndPoint.Z < u.StartPoint.Z ? u.EndPoint : u.StartPoint;
                    double xyDist = Math.Sqrt(Math.Pow(topEnd.X - bottomEnd.X, 2) + Math.Pow(topEnd.Y - bottomEnd.Y, 2));
                    return xyDist < toleranceMm && u.SystemType == lower.SystemType;
                });

                if (matchingUpper != null)
                {
                    var bottomEnd = matchingUpper.EndPoint.Z < matchingUpper.StartPoint.Z
                        ? matchingUpper.EndPoint : matchingUpper.StartPoint;

                    // Aradaki boşluğu boru ile doldur
                    double gapZ = Math.Abs(bottomEnd.Z - topEnd.Z);
                    if (gapZ > 10 && gapZ < levels[i].Height * 2)
                    {
                        var connector = new PipeEntity(topEnd, bottomEnd, lower.InnerDiameter)
                        {
                            SystemType = lower.SystemType,
                            PipeMaterialType = lower.PipeMaterialType,
                            Layer = lower.Layer
                        };
                        connector.ApplySystemColor();
                        _database.AddEntity(connector);
                        connections++;
                    }
                }
            }
        }
        return connections;
    }

    // Kesit görünümü entity üretimi (Section View)
    public List<CadEntity> GenerateSectionView(Vector3D sectionStart, Vector3D sectionEnd, Vector3D insertPoint, double scale = 1.0)
    {
        var entities = new List<CadEntity>();
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        if (levels.Count == 0) return entities;

        var sectionDir = (sectionEnd - sectionStart).Normalize();
        var sectionNormal = new Vector3D(-sectionDir.Y, sectionDir.X, 0);
        double sectionLength = (sectionEnd - sectionStart).Length();

        double y0 = insertPoint.Y;

        // Kat çizgileri
        foreach (var level in levels)
        {
            double yLevel = y0 + level.Elevation * scale / 1000.0;

            // Kat çizgisi
            entities.Add(new Domain.Entities.Basic.LineEntity(
                new Vector3D(insertPoint.X - 500 * scale, yLevel, 0),
                new Vector3D(insertPoint.X + sectionLength * scale / 1000.0 + 500 * scale, yLevel, 0))
            { Color = 0xFF666666, Layer = "SECTION_VIEW" });

            // Kat etiketi
            entities.Add(new Domain.Entities.Basic.TextEntity(
                $"{level.Name} (+{level.Elevation / 1000.0:F2})",
                new Vector3D(insertPoint.X - 2000 * scale, yLevel + 100 * scale, 0),
                200 * scale)
            { Color = 0xFFFFFFFF, Layer = "SECTION_VIEW" });

            // Kesit çizgisi boyunca entity'leri kontrol et
            foreach (var ent in GetEntitiesOnLevel(level))
            {
                if (ent is PipeEntity pipe)
                {
                    var pipeCenter = (pipe.StartPoint + pipe.EndPoint) * 0.5;
                    double projDist = ProjectOntoSection(pipeCenter, sectionStart, sectionDir);

                    if (projDist >= 0 && projDist <= sectionLength)
                    {
                        double normalDist = Math.Abs(DistanceFromSection(pipeCenter, sectionStart, sectionNormal));
                        if (normalDist < 1000) // 1m tolerans
                        {
                            double xSection = insertPoint.X + projDist * scale / 1000.0;
                            double ySection = yLevel + (pipeCenter.Z - level.Elevation) * scale / 1000.0;

                            // Boru kesit dairesi
                            entities.Add(new Domain.Entities.Basic.CircleEntity(
                                new Vector3D(xSection, ySection, 0),
                                pipe.InnerDiameter * scale / 2000.0)
                            { Color = pipe.Color, Layer = "SECTION_VIEW" });
                        }
                    }
                }
            }
        }

        // Başlık
        entities.Add(new Domain.Entities.Basic.TextEntity(
            "KESİT GÖRÜNÜMÜ (SECTION VIEW)",
            new Vector3D(insertPoint.X, y0 + (levels.Last().Elevation + levels.Last().Height) * scale / 1000.0 + 500 * scale, 0),
            300 * scale)
        { Color = 0xFFFFFFFF, Layer = "SECTION_VIEW" });

        return entities;
    }

    // ═══ 4. BASINÇ BÖLGESİ ÖNERİSİ ═══

    // Basınç bölgesi analizi ve otomatik öneri
    public PressureZoneReport AnalyzePressureZones(double maxAllowedPressureBar = 6.0, double minResidualMSS = 5.0)
    {
        var report = new PressureZoneReport();
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();
        if (levels.Count == 0) return report;

        double baseElevation = levels.First().Elevation;
        double buildingHeight = (levels.Last().Elevation + levels.Last().Height - baseElevation) / 1000.0; // m

        // Her kat için basınç hesapla
        foreach (var level in levels)
        {
            double heightM = (level.Elevation - baseElevation) / 1000.0;
            double staticHeadMSS = buildingHeight - heightM; // Üstten besleme varsayımı
            double pressureBar = staticHeadMSS * 0.0981;

            var zone = new PressureZoneEntry
            {
                LevelName = level.Name,
                ElevationM = level.Elevation / 1000.0,
                StaticPressureMSS = staticHeadMSS,
                StaticPressureBar = pressureBar,
                NeedsReducer = pressureBar > maxAllowedPressureBar,
                IsUnderpressure = staticHeadMSS < minResidualMSS
            };

            if (zone.NeedsReducer)
            {
                zone.ReducerSetPoint = maxAllowedPressureBar;
                zone.Recommendation = $"Basınç düşürücü vana gerekli — giriş {pressureBar:F1} bar → çıkış {maxAllowedPressureBar:F1} bar";
            }
            else if (zone.IsUnderpressure)
            {
                zone.Recommendation = $"Yetersiz basınç ({staticHeadMSS:F1} mSS < {minResidualMSS:F1} mSS) — hidrofor/pompa gerekli";
            }
            else
            {
                zone.Recommendation = $"Basınç uygun: {pressureBar:F1} bar ({staticHeadMSS:F1} mSS)";
            }

            report.Zones.Add(zone);
        }

        // Basınç bölgesi önerisi (6 bar sınırı aşıldığında)
        double maxPressure = report.Zones.Max(z => z.StaticPressureBar);
        if (maxPressure > maxAllowedPressureBar)
        {
            int zonesNeeded = (int)Math.Ceiling(maxPressure / maxAllowedPressureBar);
            report.RecommendedZoneCount = zonesNeeded;
            report.ZoneBoundaryFloors = new List<string>();

            int floorsPerZone = levels.Count / zonesNeeded;
            for (int i = 1; i < zonesNeeded; i++)
            {
                int boundaryIdx = Math.Min(i * floorsPerZone, levels.Count - 1);
                report.ZoneBoundaryFloors.Add(levels[boundaryIdx].Name);
            }

            report.Summary = $"Bina yüksekliği {buildingHeight:F1}m — {zonesNeeded} basınç bölgesi önerilir (sınır: {maxAllowedPressureBar:F0} bar)";
        }
        else
        {
            report.RecommendedZoneCount = 1;
            report.Summary = $"Tek basınç bölgesi yeterli — max {maxPressure:F1} bar ≤ {maxAllowedPressureBar:F0} bar";
        }

        report.BuildingHeightM = buildingHeight;
        report.MaxPressureBar = maxPressure;

        return report;
    }

    // Post-assembly validasyon
    public List<string> ValidateAssembly()
    {
        var issues = new List<string>();
        var levels = _levelManager.GetLevels().OrderBy(l => l.Elevation).ToList();

        foreach (var level in levels)
        {
            var entities = GetEntitiesOnLevel(level).ToList();
            if (entities.Count == 0)
                issues.Add($"{level.Name}: Boş kat — entity yok");

            var pipes = entities.OfType<PipeEntity>().ToList();
            var openEnds = pipes.Where(p =>
            {
                var end = p.EndPoint;
                return !pipes.Any(o => o != p && ((o.StartPoint - end).Length() < 50 || (o.EndPoint - end).Length() < 50));
            }).Count();

            if (openEnds > 0)
                issues.Add($"{level.Name}: {openEnds} açık uçlu boru — bağlantı eksik");
        }

        return issues;
    }

    // ═══ Yardımcılar ═══

    private IEnumerable<CadEntity> GetEntitiesOnLevel(MepLevel level, double? overrideElevation = null)
    {
        double elev = overrideElevation ?? level.Elevation;
        return _database.GetAllEntities().Where(e =>
        {
            var z = e.GetBoundingBox().Center.Z;
            return z >= elev && z < elev + level.Height;
        });
    }

    private double ProjectOntoSection(Vector3D point, Vector3D sectionStart, Vector3D sectionDir)
    {
        return (point.X - sectionStart.X) * sectionDir.X + (point.Y - sectionStart.Y) * sectionDir.Y;
    }

    private double DistanceFromSection(Vector3D point, Vector3D sectionStart, Vector3D sectionNormal)
    {
        return (point.X - sectionStart.X) * sectionNormal.X + (point.Y - sectionStart.Y) * sectionNormal.Y;
    }
}

public class FloorCopyWithConnectionResult
{
    public int CopiedCount { get; set; }
    public int ConnectionsPreserved { get; set; }
    public int RiserConnectionsCreated { get; set; }
}

public class PressureZoneReport
{
    public double BuildingHeightM { get; set; }
    public double MaxPressureBar { get; set; }
    public int RecommendedZoneCount { get; set; }
    public List<string> ZoneBoundaryFloors { get; set; } = new();
    public string Summary { get; set; } = "";
    public List<PressureZoneEntry> Zones { get; set; } = new();
}

public class PressureZoneEntry
{
    public string LevelName { get; set; } = "";
    public double ElevationM { get; set; }
    public double StaticPressureMSS { get; set; }
    public double StaticPressureBar { get; set; }
    public bool NeedsReducer { get; set; }
    public bool IsUnderpressure { get; set; }
    public double ReducerSetPoint { get; set; }
    public string Recommendation { get; set; } = "";
}
