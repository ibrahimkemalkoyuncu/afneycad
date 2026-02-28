using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Enums;
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

    public List<ClashResult> DetectClashes(IEnumerable<MechanicalEntity> entities)
    {
        var results = new List<ClashResult>();
        var mechanicalEntities = entities.ToList();
        
        // Sadece fiziksel tesisat elemanlarını al (Oda vb. hariç)
        var physicalEntities = mechanicalEntities.Where(e => e is PipeEntity || e is ElbowEntity || e is TeeEntity).ToList();

        // 1. Tesisat vs Mimari Engel (Örn: Duvar, Kolon)
        foreach (var entity in physicalEntities)
        {
            foreach (var obs in _obstacles)
            {
                var entityBox = entity.GetBoundingBox();
                var obsBox = obs.GetBoundingBox();

                if (entityBox.Intersects(obsBox))
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

        // 2. Boru vs Boru (FineSANI Professional Özelliği)
        var pipes = physicalEntities.OfType<PipeEntity>().ToList();
        for (int i = 0; i < pipes.Count; i++)
        {
            for (int j = i + 1; j < pipes.Count; j++)
            {
                var p1 = pipes[i];
                var p2 = pipes[j];

                // Eğer borular zaten birbirine bağlıysa (Tee vb.) çakışma değildir
                if (IsConnected(p1, p2)) continue;

                if (LineIntersectsLine(p1.StartPoint, p1.EndPoint, p2.StartPoint, p2.EndPoint))
                {
                    results.Add(new ClashResult
                    {
                        Type = ClashType.MechanicalVsMechanical,
                        EntityA_Id = p1.Id,
                        EntityB_Id = p2.Id,
                        Position = CalculateIntersectionPoint(p1, p2),
                        Severity = ClashSeverity.Warning,
                        Message = $"{p1.SystemType} ve {p2.SystemType} hatları çakışıyor."
                    });
                    p1.HasHydraulicViolation = true;
                    p2.HasHydraulicViolation = true;
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
