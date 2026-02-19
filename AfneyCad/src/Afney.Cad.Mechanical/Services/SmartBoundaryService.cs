using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Akıllı Mahal Sınırı Tespit Servisi (SmartBoundaryService)
    NEDEN: FINE SANI / 4M standardında, odanın içinde bir noktaya tıklandığında duvarları bularak otomatik mahal oluşturmak için.
    
    NASIL (Mühendislik Modu - Ray Casting & Boundary Following):
    1. Tıklanan noktadan 360 derece ışınlar gönderilir (Ray casting).
    2. En yakın duvarlar (Line, LwPolyline) tespit edilir.
    3. Tespit edilen duvarların kesişim noktaları ve köşe uçları birleştirilerek kapalı bir poligon (Mahal) oluşturulur.
    4. Küçük çizim hataları (açık uçlar) 10-20mm toleransla otomatik kapatılır.
*/
public class SmartBoundaryService
{
    private readonly CadDatabase _database;
    private const double Tolerance = 50.0; // 5cm tolerans (çizim hataları için)

    public SmartBoundaryService(CadDatabase database)
    {
        _database = database;
    }

    /*
        NE: Mahal Sınırlarını Bul (FindBoundary)
        AMACI: Verilen bir iç noktadan yola çıkarak odayı çevreleyen poligonu üretmek.
    */
    /*
        NE: Mahal Sınırlarını Bul (FindBoundary)
        AMACI: Verilen bir iç noktadan yola çıkarak odayı çevreleyen poligonu üretmek.
    */
    public List<Vector3D>? FindBoundary(Vector3D startPoint)
    {
        // Merkezi Mimari Servisi Kullan (Code Reusability)
        var archService = new ArchitecturalRecognitionService(_database);
        var boundaryPoints = archService.FindEnclosedArea(startPoint);

        if (boundaryPoints == null || boundaryPoints.Count < 3) return null;

        // 3. Noktaları saat yönünde sırala (Poligon geçerliliği için)
        // Lidar taraması zaten açısal sıralı gelir ama emin olalım.
        var center = new Vector3D(boundaryPoints.Average(p => p.X), boundaryPoints.Average(p => p.Y), 0);
        return boundaryPoints.OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToList();
    }

    /*
        NE: Mahal İçindeki Cihazları Tespit Et (GetFixturesInBoundary)
        NEDEN: Odanın içindeki vitrifiyeleri otomatik bulup hesaplamaya dahil etmek için.
    */
    public List<Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity> GetFixturesInBoundary(List<Vector3D> boundary)
    {
        var fixtures = _database.GetAllEntities().OfType<Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity>().ToList();
        var insideFixtures = new List<Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity>();

        foreach (var fix in fixtures)
        {
            if (IsPointInPolygon(fix.Position, boundary))
            {
                insideFixtures.Add(fix);
            }
        }

        return insideFixtures;
    }

    private bool IsPointInPolygon(Vector3D p, List<Vector3D> poly)
    {
        bool isInside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                isInside = !isInside;
            }
        }
        return isInside;
    }

    private Vector3D? FindNearestHit(Vector3D origin, Vector3D direction, List<CadEntity> walls)
    {
        Vector3D? nearestHit = null;
        double minDistance = double.MaxValue;

        foreach (var wall in walls)
        {
            if (wall is LineEntity line)
            {
                var hit = IntersectRayLine(origin, direction, line.StartPoint, line.EndPoint);
                if (hit != null)
                {
                    double d = origin.DistanceTo(hit.Value);
                    if (d < minDistance) { minDistance = d; nearestHit = hit; }
                }
            }
            else if (wall is LwPolylineEntity poly)
            {
                for (int i = 0; i < poly.Vertices.Count - 1; i++)
                {
                    var hit = IntersectRayLine(origin, direction, poly.Vertices[i], poly.Vertices[i+1]);
                    if (hit != null)
                    {
                        double d = origin.DistanceTo(hit.Value);
                        if (d < minDistance) { minDistance = d; nearestHit = hit; }
                    }
                }
                if (poly.IsClosed)
                {
                    var hit = IntersectRayLine(origin, direction, poly.Vertices.Last(), poly.Vertices.First());
                    if (hit != null)
                    {
                        double d = origin.DistanceTo(hit.Value);
                        if (d < minDistance) { minDistance = d; nearestHit = hit; }
                    }
                }
            }
        }

        return nearestHit;
    }

    private Vector3D? IntersectRayLine(Vector3D rayOrigin, Vector3D rayDir, Vector3D p1, Vector3D p2)
    {
        // 2D Kesişim (Ray: O + tD, LineSegment: A + u(B-A))
        double x1 = p1.X, y1 = p1.Y, x2 = p2.X, y2 = p2.Y;
        double x3 = rayOrigin.X, y3 = rayOrigin.Y, x4 = rayOrigin.X + rayDir.X, y4 = rayOrigin.Y + rayDir.Y;

        double den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(den) < 1e-9) return null;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / den;

        if (t >= 0 && t <= 1 && u >= 0)
        {
            return new Vector3D(x1 + t * (x2 - x1), y1 + t * (y2 - y1), 0);
        }
        return null;
    }
}
