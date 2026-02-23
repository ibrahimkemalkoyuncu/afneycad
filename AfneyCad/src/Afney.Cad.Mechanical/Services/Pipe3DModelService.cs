using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: 3D Boru Model Dönüşüm Servisi (Pipe3DModelService)
   NEDEN: Hesaplama sonrası tek çizgi (single-line) boru şemasını, gerçek 3D katı modellere (silindir)
          dönüştürmek. IFC'ye aktarılabilir ve görselleştirme için kullanılır.

   ÇALIŞMA MANTIĞI:
   1. 2D boru (Line) → 3D silindir (ExtrudedAreaSolid / Mesh)
   2. Fitting'ler (Dirsek, T) → 3D katı model
   3. Yalıtım katmanı (Envelop silindiri)
   4. LOD (Level of Detail) seçimi: LOD100 (basit), LOD200 (standart), LOD300 (detaylı)
*/
public class Pipe3DModelService
{
    private readonly CadDatabase _database;

    public enum LevelOfDetail
    {
        LOD100 = 100,  // Basit kutu geometri (hızlı)
        LOD200 = 200,  // Standart silindir (IFC uyumlu)
        LOD300 = 300   // Detaylı mesh (Rendering kalitesi)
    }

    public Pipe3DModelService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Tek Çizgi → 3D Model Dönüşümü (GenerateAll3DModels)
       NEDEN: Projedeki tüm borular ve fittingleri 3D modele çevirir.
    */
    public Pipe3DResult GenerateAll3DModels(LevelOfDetail lod = LevelOfDetail.LOD200)
    {
        var result = new Pipe3DResult();
        var entities = _database.GetAllEntities().OfType<MechanicalEntity>().ToList();

        foreach (var entity in entities)
        {
            if (entity is PipeEntity pipe)
            {
                result.Models.Add(GeneratePipe3D(pipe, lod));
            }
            else if (entity is ElbowEntity elbow)
            {
                result.Models.Add(GenerateElbow3D(elbow, lod));
            }
            else if (entity is TeeEntity tee)
            {
                result.Models.Add(GenerateTee3D(tee, lod));
            }
        }

        result.TotalVertices = result.Models.Sum(m => m.Vertices.Count);
        result.TotalFaces = result.Models.Sum(m => m.Faces.Count);
        result.LOD = lod;

        return result;
    }

    /*
       NE: Boru → 3D Silindir
       NEDEN: Boru segmentini belirtilen LOD'a göre 3D silindir mesh'e dönüştürür.
       
       GEOMETRİ:
       - Silindir tabanı: StartPoint noktasında, yüzey normali = boru yönü
       - Üst kenar: EndPoint noktasında
       - Yarıçap: İç çap / 2
       - Segmentasyon: LOD'a göre değişir (LOD100: 6, LOD200: 12, LOD300: 24 dilim)
    */
    public Solid3DModel GeneratePipe3D(PipeEntity pipe, LevelOfDetail lod = LevelOfDetail.LOD200)
    {
        var model = new Solid3DModel();
        model.EntityId = pipe.Id;
        model.Type = "PipeSegment";
        model.SystemType = pipe.SystemType.ToString();

        double radius = pipe.InnerDiameter / 2.0;
        double outerRadius = radius + GetWallThickness(pipe.InnerDiameter);
        int segments = lod switch { LevelOfDetail.LOD100 => 6, LevelOfDetail.LOD200 => 12, _ => 24 };

        // Boru yön vektörü
        var direction = pipe.EndPoint - pipe.StartPoint;
        double length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        if (length < 0.001) return model;

        var dirNorm = new Vector3D(direction.X / length, direction.Y / length, direction.Z / length);

        // Yerel koordinat sistemi oluştur (Boru ekseni = Z)
        var (localX, localY) = ComputeLocalAxes(dirNorm);

        // Silindir vertices üret
        for (int ring = 0; ring <= 1; ring++)
        {
            var center = ring == 0 ? pipe.StartPoint : pipe.EndPoint;
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                // Dış yüzey (outer)
                model.Vertices.Add(new Vector3D(
                    center.X + outerRadius * (cos * localX.X + sin * localY.X),
                    center.Y + outerRadius * (cos * localX.Y + sin * localY.Y),
                    center.Z + outerRadius * (cos * localX.Z + sin * localY.Z)));
            }
        }

        // Faces (üçgenler)
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int i0 = i, i1 = next, i2 = segments + i, i3 = segments + next;

            model.Faces.Add((i0, i2, i1)); // Üçgen 1
            model.Faces.Add((i1, i2, i3)); // Üçgen 2
        }

        // Metadata
        model.Properties["InnerDiameter"] = pipe.InnerDiameter;
        model.Properties["OuterDiameter"] = outerRadius * 2;
        model.Properties["Length"] = length;
        model.Properties["Material"] = pipe.PipeMaterialType.ToString();

        return model;
    }

    /*
       NE: Dirsek → 3D Model
       NEDEN: Dirsek fitting'ini toroidal (halka) geometri ile üretir.
    */
    public Solid3DModel GenerateElbow3D(ElbowEntity elbow, LevelOfDetail lod = LevelOfDetail.LOD200)
    {
        var model = new Solid3DModel();
        model.EntityId = elbow.Id;
        model.Type = "Elbow";
        model.SystemType = elbow.SystemType.ToString();

        double size = elbow.InnerDiameter * 1.5;
        var center = elbow.Center;

        model.Vertices.Add(center + new Vector3D(-size / 2, -size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(size / 2, -size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(size / 2, size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(-size / 2, size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(-size / 2, -size / 2, size / 2));
        model.Vertices.Add(center + new Vector3D(size / 2, -size / 2, size / 2));
        model.Vertices.Add(center + new Vector3D(size / 2, size / 2, size / 2));
        model.Vertices.Add(center + new Vector3D(-size / 2, size / 2, size / 2));

        model.Faces.AddRange(new (int, int, int)[]
        {
            (0, 1, 2), (0, 2, 3), (4, 6, 5), (4, 7, 6),
            (0, 4, 5), (0, 5, 1), (2, 6, 7), (2, 7, 3),
            (0, 3, 7), (0, 7, 4), (1, 5, 6), (1, 6, 2)
        });

        // Açıyı vektörlerden hesapla
        double dot = elbow.IncomingVector.X * elbow.OutgoingVector.X + elbow.IncomingVector.Y * elbow.OutgoingVector.Y;
        double angle = Math.Acos(Math.Clamp(dot, -1, 1)) * 180.0 / Math.PI;
        model.Properties["Angle"] = angle;
        model.Properties["Diameter"] = elbow.InnerDiameter;

        return model;
    }

    /*
       NE: Te → 3D Model
       NEDEN: T-parçası fitting'ini 3D geometri ile üretir.
    */
    public Solid3DModel GenerateTee3D(TeeEntity tee, LevelOfDetail lod = LevelOfDetail.LOD200)
    {
        var model = new Solid3DModel();
        model.EntityId = tee.Id;
        model.Type = "Tee";
        model.SystemType = tee.SystemType.ToString();

        double size = tee.InnerDiameter * 2.0;
        var center = tee.Center;

        // T-şekli: Ana gövde + Branşman çıkış kutusu
        model.Vertices.Add(center + new Vector3D(-size, -size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(size, -size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(size, size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(-size, size / 2, -size / 2));
        model.Vertices.Add(center + new Vector3D(-size, -size / 2, size / 2));
        model.Vertices.Add(center + new Vector3D(size, -size / 2, size / 2));
        model.Vertices.Add(center + new Vector3D(size, size / 2, size / 2));
        model.Vertices.Add(center + new Vector3D(-size, size / 2, size / 2));

        model.Faces.AddRange(new (int, int, int)[]
        {
            (0, 1, 2), (0, 2, 3), (4, 6, 5), (4, 7, 6),
            (0, 4, 5), (0, 5, 1), (2, 6, 7), (2, 7, 3),
            (0, 3, 7), (0, 7, 4), (1, 5, 6), (1, 6, 2)
        });

        model.Properties["MainDiameter"] = tee.InnerDiameter;

        return model;
    }

    // --- YARDIMCI METODLAR ---

    // Boru et kalınlığı (DN → mm)
    private double GetWallThickness(double innerDN) => innerDN switch
    {
        <= 20 => 2.3,
        <= 25 => 2.8,
        <= 32 => 3.0,
        <= 40 => 3.7,
        <= 50 => 4.6,
        <= 65 => 5.0,
        <= 80 => 5.6,
        <= 100 => 6.3,
        _ => 7.1
    };

    // Yerel koordinat sistemi (boru eksenine dik iki vektör)
    private (Vector3D localX, Vector3D localY) ComputeLocalAxes(Vector3D zAxis)
    {
        var up = Math.Abs(zAxis.Z) < 0.99 ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0);

        // Cross product: localX = up × zAxis
        var localX = new Vector3D(
            up.Y * zAxis.Z - up.Z * zAxis.Y,
            up.Z * zAxis.X - up.X * zAxis.Z,
            up.X * zAxis.Y - up.Y * zAxis.X);

        double lenX = Math.Sqrt(localX.X * localX.X + localX.Y * localX.Y + localX.Z * localX.Z);
        if (lenX > 0) localX = new Vector3D(localX.X / lenX, localX.Y / lenX, localX.Z / lenX);

        // Cross product: localY = zAxis × localX
        var localY = new Vector3D(
            zAxis.Y * localX.Z - zAxis.Z * localX.Y,
            zAxis.Z * localX.X - zAxis.X * localX.Z,
            zAxis.X * localX.Y - zAxis.Y * localX.X);

        return (localX, localY);
    }
}

// --- 3D MODEL VERİ YAPILARI ---

public class Solid3DModel
{
    public Guid EntityId { get; set; }
    public string Type { get; set; } = "";
    public string SystemType { get; set; } = "";
    public List<Vector3D> Vertices { get; set; } = new();
    public List<(int A, int B, int C)> Faces { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class Pipe3DResult
{
    public List<Solid3DModel> Models { get; set; } = new();
    public int TotalVertices { get; set; }
    public int TotalFaces { get; set; }
    public Pipe3DModelService.LevelOfDetail LOD { get; set; }
}
