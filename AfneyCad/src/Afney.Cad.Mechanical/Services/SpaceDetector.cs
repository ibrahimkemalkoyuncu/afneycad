using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Algorithms;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Akıllı Mahal Tespit Servisi (SpaceDetector)
    NEDEN: Mimari plan üzerinde, çizgilerin tam kapalı olup olmadığına bakmaksızın (Gap Tolerance) 
           kullanıcının seçtiği bölgeyi çevreleyen odayı bulmak için.
    
    YÖNTEM: Ray Casting + Wall Following (Işın Atma ve Duvar Takibi).
*/
public class SpaceDetector
{
    private readonly CadDatabase _database;
    public double GapTolerance { get; set; } = 50.0; // 50mm (5cm) boşluk toleransı
    public double SearchRadius { get; set; } = 50000.0; // 50 metre yarıçapında arama

    public SpaceDetector(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Odayı Bul (DetectRoom)
       GİRDİ: Tıklanan Nokta (PickPoint)
       ÇIKTI: Oluşturulan Oda Poligonu (RoomEntity) veya null (Bulunamazsa)
    */
    public RoomEntity? DetectRoom(Vector3D pickPoint)
    {
        // 1. Bölgedeki Adayları Getir (Spatial Index ile Hızlandırma)
        var searchBox = new CadBoundingBox(
            new Vector3D(pickPoint.X - SearchRadius, pickPoint.Y - SearchRadius, 0),
            new Vector3D(pickPoint.X + SearchRadius, pickPoint.Y + SearchRadius, 0)
        );
        
        var rawEntities = _database.QueryEntities(searchBox);
        var segments = ConvertToSegments(rawEntities);

        if (!segments.Any()) return null;

        // 2. Başlangıç Duvarını Bul (Ray Casting)
        var rayDir = new Vector3D(1, 0, 0);
        var hit = GeomUtils.RayCast(pickPoint, rayDir, segments);
        
        if (hit.HitSegment == null) return null; 

        // 3. Duvar Takibi (Boundary Tracing)
        var loop = TraceBoundary(hit.HitSegment.Value, hit.HitPoint!.Value, segments, rayDir);
        
        if (loop == null || loop.Count < 3) return null;

        // 4. Poligon Oluştur
        var room = new RoomEntity(loop.ToList(), "Otomatik Mahal");
        
        // 5. İçerideki Tefrişleri (Vitrifiye) Tespit Et
        FindFixturesInRoom(room, rawEntities);
        
        return room;
    }

    /*
       NE: Mahal İçindeki Cihazları Bul (FindFixturesInRoom)
       NEDEN: Odanın içindeki lavabo, klozet gibi blokları tespit etmek için.
    */
    private void FindFixturesInRoom(RoomEntity room, IEnumerable<CadEntity> entities)
    {
        var blockGroups = entities
            .Where(e => !string.IsNullOrEmpty(e.ParentBlockName) && e.ParentBlockId != Guid.Empty)
            .GroupBy(e => e.ParentBlockId);

        foreach (var group in blockGroups)
        {
            var firstEntity = group.First();
            var pos = firstEntity.TransformMatrix.Translation;

            if (GeomUtils.IsPointInPolygon(pos, room.BoundaryPoints))
            {
                var type = FixtureDatabase.GetTypeFromBlockName(firstEntity.ParentBlockName!);
                if (type != SanitaryFixtureType.Unknown)
                {
                    var fix = new SanitaryFixtureEntity(pos, type.ToString(), FixtureDatabase.GetDefaultLoadUnit(type))
                    {
                        BlockName = firstEntity.ParentBlockName!,
                        // Type -> FixtureType (Constructor sets this)
                        // Location -> Position (Constructor sets this)
                        // LoadUnit -> FixtureUnit (Constructor sets this)
                    };
                    room.Fixtures.Add(fix);
                }
            }
        }
    }

    private IEnumerable<LineSegment> ConvertToSegments(IEnumerable<CadEntity> entities)
    {
        var list = new List<LineSegment>();
        foreach (var ent in entities)
        {
            if (ent is LineEntity l)
            {
                list.Add(new LineSegment(l.StartPoint, l.EndPoint));
            }
            else if (ent is LwPolylineEntity poly)
            {
                var vertices = poly.Vertices;
                for (int i = 0; i < vertices.Count - 1; i++)
                {
                    list.Add(new LineSegment(vertices[i], vertices[i+1]));
                }
                if (poly.IsClosed)
                {
                    list.Add(new LineSegment(vertices.Last(), vertices.First()));
                }
            }
        }
        return list;
    }

    private List<Vector3D>? TraceBoundary(LineSegment startSeg, Vector3D hitPoint, IEnumerable<LineSegment> allSegments, Vector3D incomingDir)
    {
        var loop = new List<Vector3D>();
        var segments = allSegments.ToList();
        double gapTolerance = 50.0;

        Vector3D currentPoint = hitPoint;
        LineSegment currentSeg = startSeg;
        
        Vector3D toStart = new Vector3D(startSeg.Start.X - hitPoint.X, startSeg.Start.Y - hitPoint.Y, 0);
        Vector3D toEnd = new Vector3D(startSeg.End.X - hitPoint.X, startSeg.End.Y - hitPoint.Y, 0);
        
        Vector3D selectedDir;
        Vector3D nextPoint;
        
        double crossStart = incomingDir.X * toStart.Y - incomingDir.Y * toStart.X;
        double crossEnd = incomingDir.X * toEnd.Y - incomingDir.Y * toEnd.X;
        
        if (crossStart < crossEnd) 
        {
            nextPoint = startSeg.Start;
            selectedDir = toStart;
        }
        else
        {
            nextPoint = startSeg.End;
            selectedDir = toEnd;
        }
        
        loop.Add(currentPoint);
        
        for (int i = 0; i < 500; i++)
        {
            loop.Add(nextPoint);
            
            if (i > 2 && GeomUtils.ArePointsConnected(nextPoint, hitPoint, gapTolerance))
            {
                 return loop;
            }
            
            currentPoint = nextPoint;
            Vector3D currentDir = new Vector3D(selectedDir.X, selectedDir.Y, 0); 
            
            var candidates = GeomUtils.FindNearbySegments(currentPoint, segments, currentSeg, gapTolerance);
            
            if (candidates.Count == 0) return null; 
            
            double bestAngle = -1.0;
            var bestCandidate = candidates[0];
            bool found = false;
            
            foreach (var cand in candidates)
            {
                Vector3D candDir = new Vector3D(cand.EndPoint.X - currentPoint.X, cand.EndPoint.Y - currentPoint.Y, 0);
                double angle = GeomUtils.CalculateClockwiseAngle(currentDir, candDir);
                
                if (angle > bestAngle)
                {
                    bestAngle = angle;
                    bestCandidate = cand;
                    found = true;
                }
            }
            
            if (!found) return null;
            
            currentSeg = bestCandidate.Segment;
            nextPoint = bestCandidate.EndPoint; 
            selectedDir = new Vector3D(nextPoint.X - currentPoint.X, nextPoint.Y - currentPoint.Y, 0);
        }
        
        return null; 
    }
}
