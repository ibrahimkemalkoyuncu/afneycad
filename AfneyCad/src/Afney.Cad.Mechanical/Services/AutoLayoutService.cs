using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Yerleşim Servisi (AutoLayoutService)
   NEDEN: Kullanıcının belirlediği odaya (Banyo, Mutfak vb.) uygun vitrifiyeleri kural tabanlı (Rule-Based) yerleştirmek.
   
   MÜHENDİSLİK DETAYI (FineSANI Algorithm):
   - Duvar tarama (Wall Scanning): En uzun boş duvarı bul.
   - Kapı çakışma kontrolü (Collision Check): Kapı açılış alanına cihaz koyma.
   - Gruplama: Klozet ve Lavabo yan yana olsun (Wet Wall).
*/
public class AutoLayoutService
{
    private readonly MechanicalTopologyGraph _graph;
    private readonly List<ArchitecturalObstacle> _obstacles;

    public AutoLayoutService(MechanicalTopologyGraph graph, List<ArchitecturalObstacle>? obstacles = null)
    {
        _graph = graph;
        _obstacles = obstacles ?? new List<ArchitecturalObstacle>();
    }

    public List<SanitaryFixtureEntity> AutoFurnishRoom(RoomEntity room)
    {
        var resultList = new List<SanitaryFixtureEntity>();
        if (room == null || room.BoundaryPoints.Count < 3) return resultList;

        var neededFixtures = GetRequiredFixtures(room.Type);
        var walls = GetWalls(room).OrderByDescending(w => w.Length).ToList();
        
        if (walls.Count == 0 || neededFixtures.Count == 0) return resultList;

        var mainWall = walls[0];
        Vector3D wallVec = mainWall.End - mainWall.Start;
        double wallLength = wallVec.Length();
        Vector3D wallDir = wallVec.Normalize();
        
        // Duvar normali (Odanın içine bakan)
        Vector3D normal = new Vector3D(-wallDir.Y, wallDir.X, 0); 
        Vector3D center = room.GetBoundingBox().Center;
        Vector3D midPoint = mainWall.Start + (wallDir * (wallLength/2));
        if (normal.Dot(center - midPoint) < 0) normal = -normal;

        // MÜHENDİSLİK STANDARDI (Kemal Bey'in Notu):
        // İlk cihazın yan duvardan uzaklığı (Side Clearance)
        string firstType = neededFixtures[0];
        double currentDist = Afney.Cad.Mechanical.Standards.FixtureLayoutStandards.GetSideClearance(firstType);

        string lastType = string.Empty;

        foreach (var type in neededFixtures)
        {
            var fixture = FixtureCatalog.Create(type, Vector3D.Zero);
            
            // Yerleşim Konumu
            double halfWidth = fixture.Width / 2.0;

            // Eğer bu ilk cihaz değilse, bir önceki cihazla arasındaki standart boşluğu ekle
            if (!string.IsNullOrEmpty(lastType))
            {
                currentDist += Afney.Cad.Mechanical.Standards.FixtureLayoutStandards.GetClearanceBetween(lastType, type);
            }

            currentDist += halfWidth; // Cihazın merkezine gel
            
            if (currentDist + halfWidth > wallLength) break; // Duvar bitti

            Vector3D pos = mainWall.Start + (wallDir * currentDist);
            pos += normal * (fixture.Depth / 2.0 + 10.0); // 10mm boşluk

            // ÇAKIŞMA KONTROLÜ (Collision Detection)
            if (IsOverlappingObstacle(pos, fixture.Width, fixture.Depth))
            {
                currentDist += halfWidth + 200; // Engel varsa atla ve bir miktar pay bırak
                continue;
            }

            fixture.Position = pos;
            
            // Phase 3: Associative Link
            fixture.WallOffset = currentDist;
            fixture.WallDistance = fixture.Depth / 2.0 + 10.0;
            
            // Bu duvarın hangi mimari engele (Obstacle) ait olduğunu bulalım
            var attachedObs = _obstacles.FirstOrDefault(o => o.Type == ObstacleType.Wall && 
                                                         o.Boundary.Any(p => p.DistanceTo(mainWall.Start) < 1.0 || p.DistanceTo(mainWall.End) < 1.0));
            if (attachedObs != null)
            {
                fixture.AttachedObstacleId = attachedObs.Id;
            }

            // Rotasyon: Cihazın önü (Face) normal yönüne bakmalı
            double angle = Math.Atan2(normal.Y, normal.X);
            fixture.Rotation = angle; 

            resultList.Add(fixture);
            
            currentDist += halfWidth; // Cihazın bitişine gel
            lastType = type;
        }
        
        return resultList;
    }

    private bool IsOverlappingObstacle(Vector3D pos, double width, double depth)
    {
        var buffer = 50.0; // Güvenlik mesafesi
        var fixtureBox = new CadBoundingBox(
            new Vector3D(pos.X - width/2 - buffer, pos.Y - depth/2 - buffer, 0),
            new Vector3D(pos.X + width/2 + buffer, pos.Y + depth/2 + buffer, 0)
        );

        foreach (var obs in _obstacles)
        {
            if (obs.Type == ObstacleType.Door || obs.Type == ObstacleType.Window)
            {
                if (obs.GetBoundingBox().Intersects(fixtureBox))
                    return true;
            }
        }
        return false;
    }

    private List<string> GetRequiredFixtures(RoomType type)
    {
        // Namespace çakışmasını önlemek için tam ad veya alias kullanalım
        // FixtureCatalog.FixtureTypes sınıfı public static
        return type switch
        {
            RoomType.Bathroom => new List<string> { FixtureCatalog.FixtureTypes.Washbasin, FixtureCatalog.FixtureTypes.WC_Reservoir, FixtureCatalog.FixtureTypes.Shower },
            RoomType.Toilet => new List<string> { FixtureCatalog.FixtureTypes.Washbasin, FixtureCatalog.FixtureTypes.WC_Reservoir },
            RoomType.Kitchen => new List<string> { FixtureCatalog.FixtureTypes.KitchenSink },
            RoomType.UtilityRoom => new List<string> { FixtureCatalog.FixtureTypes.WashingMachine },
            _ => new List<string>()
        };
    }

    private class WallSegment
    {
        public Vector3D Start;
        public Vector3D End;
        public double Length => Start.DistanceTo(End);
    }

    private List<WallSegment> GetWalls(RoomEntity room)
    {
        var list = new List<WallSegment>();
        for (int i = 0; i < room.BoundaryPoints.Count; i++)
        {
            var p1 = room.BoundaryPoints[i];
            var p2 = room.BoundaryPoints[(i + 1) % room.BoundaryPoints.Count];
            list.Add(new WallSegment { Start = p1, End = p2 });
        }
        return list;
    }
}
