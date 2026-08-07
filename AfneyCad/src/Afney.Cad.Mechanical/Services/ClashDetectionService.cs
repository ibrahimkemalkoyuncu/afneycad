using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.SpatialIndex.Core;
using Serilog;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Çakışma Analizi Servisi (ClashDetectionService)
   NEDEN: Boru hatlarının mimari engellerle (Duvar, Kolon) veya diğer tesisat borularıyla çakışmasını saptayıp mühendislik hatalarını önlemek için. (Suggestion 20)
   
   MÜHENDİSLİK DETAYI (Kemal & Mete):
   - 2D/3D ANALYSIS: Plan görünümünde kesişen hatları saptar.
   - AUTO-RESOLVE: Çakışan borulardan birini "Z-Atlaması" (By-pass) ile otomatik olarak engelin altından/üstünden geçirir.
*/
public class ClashDetectionService
{
    private readonly List<ArchitecturalObstacle> _obstacles;

    public ClashDetectionService(List<ArchitecturalObstacle> obstacles)
    {
        _obstacles = obstacles;
    }

    // NE: Devasa Sanal Çalışma Alanı (CadDatabase._spatialIndex ile aynı desen)
    // NEDEN: Broad-phase QuadTree'lerin, UTM/Global koordinatlar dahil her konumdaki
    //        varlığı kapsayabilmesi için (bkz. Afney.Cad.Database.Core.CadDatabase ctor).
    private static CadBoundingBox CreateWorldBounds() => new CadBoundingBox(
        new Vector3D(-1000000000000, -1000000000000, -100000000),
        new Vector3D(1000000000000, 1000000000000, 100000000));

    public List<ClashResult> DetectClashes(IEnumerable<MechanicalEntity> entities)
    {
        var results = new List<ClashResult>();
        var mechanicalEntities = entities.ToList();

        // Sadece fiziksel tesisat elemanlarını al (Oda vb. hariç)
        var physicalEntities = mechanicalEntities.Where(e => e is PipeEntity || e is ElbowEntity || e is TeeEntity).ToList();

        // 1. Tesisat vs Mimari Engel (Örn: Duvar, Kolon)
        // Broad-phase: physicalEntities için bir QuadTree kurulur, her engel için SADECE
        // bounding-box'ı kesişen aday varlıklar sorgulanır (O(n*m) yerine O(m log n)).
        // QuadTree.QueryRange zaten "Intersects(range, entBox)" testini uyguladığından
        // (CadBoundingBox.Intersects simetriktir), narrow-phase'te tekrar kontrol gerekmez —
        // eski davranışla birebir aynı sonuç kümesi üretilir.
        if (physicalEntities.Count > 0 && _obstacles.Count > 0)
        {
            var entityQuadTree = new QuadTree(CreateWorldBounds());
            foreach (var e in physicalEntities) entityQuadTree.Insert(e);

            // Eski kodun sırasını (entity dış döngü, _obstacles iç döngü, _obstacles sırasıyla)
            // korumak için önce eşleşmeleri entity Id'sine göre topluyoruz.
            var entityToObstacles = new Dictionary<Guid, List<ArchitecturalObstacle>>();
            foreach (var obs in _obstacles)
            {
                var candidates = new HashSet<CadEntity>();
                entityQuadTree.QueryRange(obs.GetBoundingBox(), candidates);
                foreach (var cand in candidates)
                {
                    if (!entityToObstacles.TryGetValue(cand.Id, out var list))
                    {
                        list = new List<ArchitecturalObstacle>();
                        entityToObstacles[cand.Id] = list;
                    }
                    list.Add(obs);
                }
            }

            foreach (var entity in physicalEntities)
            {
                if (!entityToObstacles.TryGetValue(entity.Id, out var matchedObstacles)) continue;

                var entityBox = entity.GetBoundingBox();
                foreach (var obs in matchedObstacles)
                {
                    results.Add(new ClashResult
                    {
                        Type = ClashType.MechanicalVsArchitectural,
                        EntityA_Id = entity.Id,
                        ObstacleId = obs.Id,
                        Position = entityBox.Center,
                        Severity = obs.Type == ObstacleType.Column ? ClashSeverity.Critical : ClashSeverity.Warning,
                        Message = $"{obs.Type} ile {entity.EntityType} çakışması tespit edildi."
                    });

                    if (entity is PipeEntity p) p.HasHydraulicViolation = true;
                    // Not: Diğer varlıklar için de görsel bir hata bayrağı eklenebilir.
                }
            }
        }

        // 2. Boru vs Boru — 3D segment mesafe kontrolü
        // Broad-phase: her boru için, olası en büyük minClearance kadar genişletilmiş
        // bounding-box'ı ile QuadTree'den aday komşular sorgulanır; hassas
        // SegmentToSegmentDistance hesabı (narrow-phase) SADECE bu adaylara uygulanır.
        var pipes = physicalEntities.OfType<PipeEntity>().ToList();
        if (pipes.Count > 1)
        {
            double maxDiameter = pipes.Max(p => p.InnerDiameter);
            var pipeQuadTree = new QuadTree(CreateWorldBounds());
            foreach (var p in pipes) pipeQuadTree.Insert(p);

            var pipeIndex = new Dictionary<Guid, int>();
            for (int idx = 0; idx < pipes.Count; idx++) pipeIndex[pipes[idx].Id] = idx;

            for (int i = 0; i < pipes.Count; i++)
            {
                var p1 = pipes[i];

                // En kötü durum: p1 en büyük çaplı boruyla eşleşirse minClearance = (d1+maxD)/2+25.
                // Bu marj kadar genişletilmiş kutu, gerçekten çakışabilecek TÜM adayları kapsar
                // (broad-phase'te kaçırma riski yok, sadece fazladan aday olabilir).
                double margin = (p1.InnerDiameter + maxDiameter) / 2.0 + 25.0;
                var queryBox = p1.GetBoundingBox().Expand(margin);

                var candidates = new HashSet<CadEntity>();
                pipeQuadTree.QueryRange(queryBox, candidates);

                var candidateIndices = candidates
                    .Select(c => pipeIndex[c.Id])
                    .Where(j => j > i)
                    .OrderBy(j => j);

                foreach (var j in candidateIndices)
                {
                    var p2 = pipes[j];
                    if (IsConnected(p1, p2)) continue;

                    double minClearance = (p1.InnerDiameter + p2.InnerDiameter) / 2.0 + 25.0; // +25mm boşluk
                    double dist = SegmentToSegmentDistance(p1.StartPoint, p1.EndPoint, p2.StartPoint, p2.EndPoint);

                    if (dist < minClearance)
                    {
                        bool isCrossing = LineIntersectsLine(p1.StartPoint, p1.EndPoint, p2.StartPoint, p2.EndPoint);
                        results.Add(new ClashResult
                        {
                            Type     = ClashType.MechanicalVsMechanical,
                            EntityA_Id = p1.Id,
                            EntityB_Id = p2.Id,
                            Position = CalculateIntersectionPoint(p1, p2),
                            Severity = isCrossing ? ClashSeverity.Critical : ClashSeverity.Warning,
                            Message  = $"{p1.SystemType}↔{p2.SystemType}: {(isCrossing ? "Kesişiyor" : $"Aralık {dist:F0}mm < {minClearance:F0}mm")}"
                        });
                        p1.HasHydraulicViolation = true;
                        p2.HasHydraulicViolation = true;
                    }
                }
            }
        }

        // 3. Vana vs Boru — ValveEntity BoundingBox kontrolü
        // Broad-phase: borular için kurulan QuadTree (varsa yeniden kullanılır) her vananın
        // kendi bbox'ı ile sorgulanır. QueryRange zaten "vBox.Intersects(pBox)" testini
        // uyguladığından, narrow-phase'te tekrar kontrol gerekmez.
        var valves = mechanicalEntities.OfType<ValveEntity>().ToList();
        if (valves.Count > 0 && pipes.Count > 0)
        {
            var valvePipeQuadTree = new QuadTree(CreateWorldBounds());
            foreach (var p in pipes) valvePipeQuadTree.Insert(p);

            var pipeIndexForValves = new Dictionary<Guid, int>();
            for (int idx = 0; idx < pipes.Count; idx++) pipeIndexForValves[pipes[idx].Id] = idx;

            foreach (var valve in valves)
            {
                var vBox = valve.GetBoundingBox();

                var candidates = new HashSet<CadEntity>();
                valvePipeQuadTree.QueryRange(vBox, candidates);

                var candidateIndices = candidates.Select(c => pipeIndexForValves[c.Id]).OrderBy(j => j);

                foreach (var j in candidateIndices)
                {
                    var pipe = pipes[j];
                    if (pipe.SystemType == valve.SystemType) continue; // Aynı sistemde bağlı olabilir

                    results.Add(new ClashResult
                    {
                        Type      = ClashType.MechanicalVsMechanical,
                        EntityA_Id = valve.Id,
                        EntityB_Id = pipe.Id,
                        Position  = valve.Position,
                        Severity  = ClashSeverity.Warning,
                        Message   = $"Vana ({valve.ValveType}) ↔ {pipe.SystemType} borusu — sınır kutusu çakışması."
                    });
                }
            }
        }

        return results;
    }

    /*
       NE: Otomatik Çakışma Çözümü (Auto-Resolve / By-pass)
       NEDEN: Mühendisin manuel olarak boru kesip kavis yapması yerine, sistemin standartlara uygun ofseti otomatik oluşturması için.
    */
    public List<MechanicalEntity> ResolveClash(ClashResult clash, IEnumerable<MechanicalEntity> entities)
    {
        var newEntities = new List<MechanicalEntity>();
        var allEntities = entities.ToList();

        if (clash.Type == ClashType.MechanicalVsMechanical)
        {
            var pipeA = allEntities.OfType<PipeEntity>().FirstOrDefault(p => p.Id == clash.EntityA_Id);
            var pipeB = allEntities.OfType<PipeEntity>().FirstOrDefault(p => p.Id == clash.EntityB_Id);

            if (pipeA != null && pipeB != null)
            {
                // pipeA üzerine bir "By-pass" (Kavis) oluşturalım.
                var dir = (pipeA.EndPoint - pipeA.StartPoint).Normalize();
                var p1 = clash.Position - dir * 150.0; // 15cm önce
                var p2 = clash.Position + dir * 150.0; // 15cm sonra

                // Z-Jump: Pis su ise aşağıdan, temiz su ise yukarıdan dolaş
                var offsetZ = (pipeA.SystemType == MechanicalSystemType.WasteWater) ? -200.0 : 200.0;
                var pMid1 = p1 + new Vector3D(0, 0, offsetZ);
                var pMid2 = p2 + new Vector3D(0, 0, offsetZ);

                // Yeni 5 segmentli kavisli hat
                newEntities.Add(CreateSegment(pipeA, pipeA.StartPoint, p1));
                newEntities.Add(CreateSegment(pipeA, p1, pMid1));
                newEntities.Add(CreateSegment(pipeA, pMid1, pMid2));
                newEntities.Add(CreateSegment(pipeA, pMid2, p2));
                newEntities.Add(CreateSegment(pipeA, p2, pipeA.EndPoint));
            }
        }
        return newEntities;
    }

    private PipeEntity CreateSegment(PipeEntity original, Vector3D start, Vector3D end)
    {
        return new PipeEntity(start, end, original.InnerDiameter)
        {
            SystemType = original.SystemType,
            Color = original.Color,
            PipeMaterialType = original.PipeMaterialType
        };
    }

    private bool IsConnected(PipeEntity p1, PipeEntity p2)
    {
        const double eps = 10.0; // 10mm tolerans
        return p1.StartPoint.DistanceTo(p2.StartPoint) < eps || 
               p1.StartPoint.DistanceTo(p2.EndPoint) < eps ||
               p1.EndPoint.DistanceTo(p2.StartPoint) < eps ||
               p1.EndPoint.DistanceTo(p2.EndPoint) < eps;
    }

    private bool Intersects(PipeEntity pipe, ArchitecturalObstacle obs)
    {
        // 3D BoundingBox Çakışma Kontrolü (Phase 23)
        var pipeBox = pipe.GetBoundingBox();
        var obsBox = obs.GetBoundingBox();

        if (pipeBox.Intersects(obsBox))
        {
            // Detaylı geometri kontrolü (Opsiyonel ama şimdilik BoundingBox yeterli görülebilir)
            return true;
        }
        
        return false;
    }

    // 3D segment-to-segment minimum mesafe (GCD algoritması)
    private static double SegmentToSegmentDistance(Vector3D p1, Vector3D p2, Vector3D p3, Vector3D p4)
    {
        var d1 = p2 - p1;
        var d2 = p4 - p3;
        var r  = p1 - p3;

        double a = d1.Dot(d1), e = d2.Dot(d2);
        double f = d2.Dot(r);

        double s, t;
        if (a <= 1e-10 && e <= 1e-10) return r.Length();
        if (a <= 1e-10) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            double c2 = d1.Dot(r);
            if (e <= 1e-10) { t = 0; s = Math.Clamp(-c2 / a, 0, 1); }
            else
            {
                double b2 = d1.Dot(d2);
                double denom = a * e - b2 * b2;
                s = denom != 0 ? Math.Clamp((b2 * f - c2 * e) / denom, 0, 1) : 0;
                t = (b2 * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c2 / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b2 - c2) / a, 0, 1); }
            }
        }
        return (p1 + d1 * s - (p3 + d2 * t)).Length();
    }

    private bool LineIntersectsLine(Vector3D a, Vector3D b, Vector3D c, Vector3D d)
    {
        // 2D Kesişim (Plan)
        double denominator = (d.Y - c.Y) * (b.X - a.X) - (d.X - c.X) * (b.Y - a.Y);
        if (Math.Abs(denominator) < 1e-6) return false;

        double ua = ((d.X - c.X) * (a.Y - c.Y) - (d.Y - c.Y) * (a.X - c.X)) / denominator;
        double ub = ((b.X - a.X) * (a.Y - c.Y) - (b.Y - a.Y) * (a.X - c.X)) / denominator;

        return (ua >= 0 && ua <= 1) && (ub >= 0 && ub <= 1);
    }

    private bool LineIntersectsPolygon(Vector3D start, Vector3D end, List<Vector3D> polygon)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            if (LineIntersectsLine(start, end, p1, p2)) return true;
        }
        return false;
    }

    private Vector3D CalculateIntersectionPoint(PipeEntity pipe, ArchitecturalObstacle obs)
    {
        return (pipe.StartPoint + pipe.EndPoint) * 0.5;
    }

    private Vector3D CalculateIntersectionPoint(PipeEntity p1, PipeEntity p2)
    {
        var a = p1.StartPoint;
        var b = p1.EndPoint;
        var c = p2.StartPoint;
        var d = p2.EndPoint;

        double denominator = (d.Y - c.Y) * (b.X - a.X) - (d.X - c.X) * (b.Y - a.Y);
        if (Math.Abs(denominator) < 1e-6) return (a + b) * 0.5;

        double ua = ((d.X - c.X) * (a.Y - c.Y) - (d.Y - c.Y) * (a.X - c.X)) / denominator;
        return a + (b - a) * ua;
    }
}

public enum ClashType
{
    MechanicalVsArchitectural,
    MechanicalVsMechanical
}

public class ClashResult
{
    public Guid Id { get; set; } = Guid.NewGuid(); // Çakışmanın tekil kimliği
    public ClashType Type { get; set; }
    public Guid EntityA_Id { get; set; }
    public Guid? EntityB_Id { get; set; }
    public Guid? ObstacleId { get; set; }
    public Vector3D Position { get; set; }
    public ClashSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false; // Kullanıcı bu çakışmayı onayladı/yoksaydı mı?
}

public enum ClashSeverity
{
    Warning,
    Critical
}
