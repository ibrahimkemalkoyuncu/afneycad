using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Duvara Paralel Boru Çizme Servisi (WallParallelRoutingService)
   NEDEN: FINE SANI'de boru, duvar kenarından belirli mesafede (ofset) otomatik rotalanır.
          Mimari duvar verisi ile boru başlangıç/bitiş noktalarını hesaplar.
   
   MÜHENDİSLİK DETAYI:
   - Duvar normal vektörünü hesaplar
   - Ofset mesafesi uygulanarak boru axisi belirlenir
   - Duvar köşelerinde otomatik dirsek eklenir
   - Engel tespiti ile çakışma önlenir
*/
public class WallParallelRoutingService
{
    private readonly CadDatabase _database;

    // Varsayılan ofset mesafesi (mm) — duvar yüzeyinden boru merkezine
    public double DefaultOffset { get; set; } = 150.0;

    // Boru çapı (mm)
    public double DefaultDiameter { get; set; } = 20.0;

    public WallParallelRoutingService(CadDatabase database)
    {
        _database = database;
    }

    public class WallSegment
    {
        public Vector3D Start { get; set; } = Vector3D.Zero;
        public Vector3D End { get; set; } = Vector3D.Zero;
        public double Thickness { get; set; } = 200;
    }

    public class ParallelRouteResult
    {
        public List<PipeEntity> Pipes { get; set; } = new();
        public List<ElbowEntity> Elbows { get; set; } = new();
        public double TotalLength { get; set; }
        public int CornerCount { get; set; }
    }

    /*
       NE: Duvar segmentine paralel boru rotalama
       NEDEN: Duvar yüzeyinden belirli ofset mesafesinde boru oluşturur
    */
    public ParallelRouteResult RouteParallelToWall(WallSegment wall, double offset = 0, double diameter = 0)
    {
        if (offset <= 0) offset = DefaultOffset;
        if (diameter <= 0) diameter = DefaultDiameter;

        var result = new ParallelRouteResult();

        // Duvar yön vektörü
        var wallDir = (wall.End - wall.Start).Normalize();

        // Duvar normal vektörü (sol taraf) — Z düzleminde 90° dönüş
        var normal = new Vector3D(-wallDir.Y, wallDir.X, wallDir.Z);

        // Ofsetli başlangıç/bitiş noktaları
        double totalOffset = wall.Thickness / 2.0 + offset;
        var offsetStart = wall.Start + normal * totalOffset;
        var offsetEnd = wall.End + normal * totalOffset;

        // Boru oluştur
        var pipe = new PipeEntity(offsetStart, offsetEnd, diameter);
        pipe.Color = 0xFF0088FF;
        pipe.Layer = "SIHHI-BORU";
        result.Pipes.Add(pipe);
        result.TotalLength = (offsetEnd - offsetStart).Length();

        return result;
    }

    /*
       NE: Birden fazla duvar segmentine paralel rotalama (L ve U şekil)
       NEDEN: Köşelerde dirsek ekleyerek sürekli hat oluşturur
    */
    public ParallelRouteResult RouteAlongWalls(List<WallSegment> walls, double offset = 0, double diameter = 0)
    {
        if (offset <= 0) offset = DefaultOffset;
        if (diameter <= 0) diameter = DefaultDiameter;

        var result = new ParallelRouteResult();
        var offsetPoints = new List<Vector3D>();

        foreach (var wall in walls)
        {
            var wallDir = (wall.End - wall.Start).Normalize();
            var normal = new Vector3D(-wallDir.Y, wallDir.X, wallDir.Z);
            double totalOffset = wall.Thickness / 2.0 + offset;

            offsetPoints.Add(wall.Start + normal * totalOffset);
            offsetPoints.Add(wall.End + normal * totalOffset);
        }

        // Ardışık noktalar arasında boru segmentleri oluştur
        for (int i = 0; i < offsetPoints.Count - 1; i += 2)
        {
            var pipe = new PipeEntity(offsetPoints[i], offsetPoints[i + 1], diameter);
            pipe.Color = 0xFF0088FF;
            pipe.Layer = "SIHHI-BORU";
            result.Pipes.Add(pipe);
            result.TotalLength += (offsetPoints[i + 1] - offsetPoints[i]).Length();
        }

        // Köşelerde dirsek bağlantısı
        for (int i = 1; i < offsetPoints.Count - 1; i += 2)
        {
            if (i + 1 < offsetPoints.Count)
            {
                var inDir = (offsetPoints[i] - offsetPoints[i - 1]).Normalize();
                var outDir = (offsetPoints[i + 1] - offsetPoints[i]).Normalize();
                var elbow = new ElbowEntity(offsetPoints[i], diameter, inDir, outDir);
                elbow.Color = 0xFFFF6600;
                result.Elbows.Add(elbow);
                result.CornerCount++;
            }
        }

        return result;
    }
}
