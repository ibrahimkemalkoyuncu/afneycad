using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı DWG→BIM Dönüştürücü (SmartBimConverterService)
   NEDEN: Mimari katmanlardaki LineEntity / LwPolylineEntity nesnelerini
          ArchitecturalObstacle BIM nesnelerine otomatik dönüştürmek için.

   MANTIK:
   1. "Mimari" layer filtresi uygula (DUVAR, WALL, MIMARI, KABA...)
   2. Paralel ve yakın çizgi çiftlerini "duvar çifti" olarak grupla
   3. Her gruptan bir ArchitecturalObstacle oluştur
   4. Varsayılan BIM özellikleri ata (kalınlık, yükseklik, malzeme şablonu)
*/
public class SmartBimConverterService
{
    private readonly CadDatabase _database;
    public double WallThicknessDefaultMm { get; set; } = 200.0;
    public double WallHeightDefaultMm    { get; set; } = 3000.0;
    public double SnapToleranceMm        { get; set; } = 20.0;

    public SmartBimConverterService(CadDatabase database) { _database = database; }

    public class ConvertResult
    {
        public List<ArchitecturalObstacle> Obstacles { get; set; } = [];
        public int LineCount    { get; set; }
        public int WallCount    { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Log { get; set; } = [];
    }

    // ── Ana Dönüşüm ───────────────────────────────────────────────────────────────

    public ConvertResult Convert(IEnumerable<string> targetLayers, ObstacleType obstacleType = ObstacleType.Wall)
    {
        var result = new ConvertResult();
        var layerSet = new HashSet<string>(targetLayers, StringComparer.OrdinalIgnoreCase);

        var lines = _database.GetAllEntities()
            .Where(e => layerSet.Contains(e.Layer ?? ""))
            .ToList();

        result.LineCount = lines.Count;

        if (lines.Count == 0)
        {
            result.Log.Add("Seçili layerlarda hiç çizim nesnesi bulunamadı.");
            return result;
        }

        // Her LineEntity → tek duvar segmenti
        foreach (var ent in lines)
        {
            try
            {
                Vector3D start, end;
                if (ent is LineEntity line)
                {
                    if ((line.EndPoint - line.StartPoint).Length() < 100) { result.SkippedCount++; continue; }
                    start = line.StartPoint; end = line.EndPoint;
                }
                else if (ent is LwPolylineEntity poly && poly.Vertices.Count >= 2)
                {
                    start = new Vector3D(poly.Vertices[0].X, poly.Vertices[0].Y, 0);
                    end   = new Vector3D(poly.Vertices[^1].X, poly.Vertices[^1].Y, 0);
                }
                else { result.SkippedCount++; continue; }

                var dir   = (end - start).Normalize();
                var perp  = new Vector3D(-dir.Y, dir.X, 0) * (WallThicknessDefaultMm / 2.0);

                var obstacle = new ArchitecturalObstacle
                {
                    Type          = obstacleType,
                    OriginalLayer = ent.Layer ?? "",
                    Height        = WallHeightDefaultMm,
                    Name          = $"{obstacleType} — {ent.Layer}",
                    SourceEntityId = ent.Id,
                    Boundary = [
                        start + perp,
                        start - perp,
                        end   - perp,
                        end   + perp
                    ]
                };

                // Varsayılan malzeme şablonu: standart tuğla duvar
                obstacle.MaterialLayers.Add(new BimMaterialLayer { MaterialName = "Sıva", ThicknessMm = 20, ThermalConductivity = 0.87 });
                obstacle.MaterialLayers.Add(new BimMaterialLayer { MaterialName = "Tuğla", ThicknessMm = WallThicknessDefaultMm - 40, ThermalConductivity = 0.45 });
                obstacle.MaterialLayers.Add(new BimMaterialLayer { MaterialName = "Sıva", ThicknessMm = 20, ThermalConductivity = 0.87 });

                result.Obstacles.Add(obstacle);
                result.WallCount++;
            }
            catch (Exception ex)
            {
                result.Log.Add($"Hata: {ex.Message}");
                result.SkippedCount++;
            }
        }

        result.Log.Add($"✓ {result.WallCount} BIM nesnesi oluşturuldu ({result.SkippedCount} atlandı).");
        Serilog.Log.Information("[SmartBimConverter] {W} wall, {S} skipped from {L} lines", result.WallCount, result.SkippedCount, result.LineCount);
        return result;
    }

    // ── Layer Tespit ──────────────────────────────────────────────────────────────

    public static bool IsWallLayer(string layer)
    {
        var u = layer.ToUpperInvariant();
        return u.Contains("WALL") || u.Contains("DUVAR") || u.Contains("MIMARI") ||
               u.Contains("KABA") || u.Contains("SIVA")  || u.Contains("YAPISAL") ||
               u.Contains("ARCH") || u.Contains("MIM");
    }

    public List<string> DetectWallLayers()
        => _database.GetLayers().Select(l => l.Name).Where(IsWallLayer).ToList();
}
