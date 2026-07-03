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
   NE: Çok Katlı Bina Yönetim Servisi (MultiStoryBuildingService)
   NEDEN: Birden fazla katı olan binalarda kat tanımı, katlar arası boru (kolon) bağlantısı,
          kat kopyalama ve dikey hizalama işlemlerini yönetmek için.
   
   ÇALIŞMA MANTIĞI:
   1. Kat Tanımı: Her kat bir Z yüksekliği, isim ve kat planı referansı taşır
   2. Kolon Yönetimi: Dikey borular (Riser) katlar arasında otomatik bağlanır
   3. Kat Kopyalama: Bir katın tüm tesisatı (vitrifiye + boru) başka bir kata kopyalanır
   4. Yükseklik Farkı: Basınç kaybı hesabında statik yükseklik farkı otomatik dahil edilir
*/
public class MultiStoryBuildingService
{
    private readonly CadDatabase _database;
    private readonly List<FloorDefinition> _floors = new();

    public MultiStoryBuildingService(CadDatabase database)
    {
        _database = database;
    }

    // --- KAT TANIMI ---

    /*
       NE: Yeni Kat Ekle (AddFloor)
       NEDEN: Bina kat planını tanımlamak ve Z koordinat yönetimini merkezi yapmak.
    */
    public FloorDefinition AddFloor(string name, double elevationMm, double heightMm = 3000, int? order = null)
    {
        var floor = new FloorDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Elevation = elevationMm,
            Height = heightMm,
            Order = order ?? _floors.Count,
            IsActive = false
        };
        _floors.Add(floor);
        _floors.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));
        // Order'ı yeniden numarala
        for (int i = 0; i < _floors.Count; i++) _floors[i].Order = i;
        return floor;
    }

    /*
       NE: Standart Kat Yapısı Oluştur (InitializeStandardBuilding)
       NEDEN: Tipik bir konut binası için Bodrum + Zemin + Normal Katlar + Çatı yapısını otomatik oluşturmak.
    */
    public List<FloorDefinition> InitializeStandardBuilding(int normalFloorCount, double floorHeight = 3000, bool hasBasement = true)
    {
        double currentElevation = 0;

        if (hasBasement)
        {
            currentElevation = -floorHeight;
            AddFloor("Bodrum Kat", currentElevation, floorHeight);
            currentElevation += floorHeight;
        }

        AddFloor("Zemin Kat", currentElevation, floorHeight);
        currentElevation += floorHeight;

        for (int i = 1; i <= normalFloorCount; i++)
        {
            AddFloor($"{i}. Normal Kat", currentElevation, floorHeight);
            currentElevation += floorHeight;
        }

        AddFloor("Çatı Katı", currentElevation, floorHeight * 0.5);

        return _floors.ToList();
    }

    public List<FloorDefinition> GetAllFloors() => _floors.OrderBy(f => f.Order).ToList();

    public FloorDefinition? GetFloorByName(string name) =>
        _floors.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public FloorDefinition? GetActiveFloor() => _floors.FirstOrDefault(f => f.IsActive);

    public void SetActiveFloor(Guid floorId)
    {
        foreach (var f in _floors) f.IsActive = false;
        var floor = _floors.FirstOrDefault(f => f.Id == floorId);
        if (floor != null) floor.IsActive = true;
    }

    // --- KOLON YÖNETİMİ ---

    /*
       NE: Dikey Kolon (Riser) Oluştur
       NEDEN: Belirtilen XY konumunda, tüm katları dikey olarak bağlayan bir boru kolonu oluşturmak.
    */
    public List<PipeEntity> CreateRiser(Vector3D xyPosition, double diameter, MechanicalSystemType systemType,
        string? fromFloor = null, string? toFloor = null)
    {
        var pipes = new List<PipeEntity>();
        var sortedFloors = _floors.OrderBy(f => f.Elevation).ToList();

        int startIdx = 0;
        int endIdx = sortedFloors.Count - 1;

        if (fromFloor != null)
        {
            var f = sortedFloors.FindIndex(fl => fl.Name.Equals(fromFloor, StringComparison.OrdinalIgnoreCase));
            if (f >= 0) startIdx = f;
        }
        if (toFloor != null)
        {
            var f = sortedFloors.FindIndex(fl => fl.Name.Equals(toFloor, StringComparison.OrdinalIgnoreCase));
            if (f >= 0) endIdx = f;
        }

        for (int i = startIdx; i < endIdx; i++)
        {
            var lower = sortedFloors[i];
            var upper = sortedFloors[i + 1];

            var pipe = new PipeEntity(
                new Vector3D(xyPosition.X, xyPosition.Y, lower.Elevation),
                new Vector3D(xyPosition.X, xyPosition.Y, upper.Elevation),
                diameter);
            pipe.SystemType = systemType;
            pipe.Layer = GetLayerForSystem(systemType);
            pipe.ApplySystemColor();

            pipes.Add(pipe);
            lower.RiserIds.Add(pipe.Id);
        }

        return pipes;
    }

    // --- KAT KOPYALAMA ---

    /*
       NE: Katı Kopyala (CopyFloorPlumbing)
       NEDEN: Tip katı tanımlanmış bir binada, aynı tesisat düzenini diğer katlara çoğaltmak.
       
       PARAMETRELER:
       - sourceFloorId: Kaynak kat
       - targetFloorId: Hedef kat
       - Sonuç: Kopyalanan entity sayısı
    */
    public int CopyFloorPlumbing(Guid sourceFloorId, Guid targetFloorId)
    {
        var source = _floors.FirstOrDefault(f => f.Id == sourceFloorId);
        var target = _floors.FirstOrDefault(f => f.Id == targetFloorId);
        if (source == null || target == null) return 0;

        double deltaZ = target.Elevation - source.Elevation;
        var allEntities = _database.GetAllEntities().OfType<MechanicalEntity>().ToList();

        // Kaynak kattaki entity'leri bul (Z koordinatına göre filtreleme)
        var sourceEntities = allEntities.Where(e =>
        {
            double ez = GetEntityZ(e);
            return ez >= source.Elevation && ez < source.Elevation + source.Height;
        }).ToList();

        int copied = 0;
        foreach (var entity in sourceEntities)
        {
            var clone = CloneEntityWithOffset(entity, new Vector3D(0, 0, deltaZ));
            if (clone != null)
            {
                _database.AddEntity(clone);
                target.EntityIds.Add(clone.Id);
                copied++;
            }
        }

        return copied;
    }

    // --- YÜKSEKLIK HESABI ---

    /*
       NE: İki Kat Arası Statik Yükseklik Farkı
       NEDEN: Basınç kaybı hesabında (Bernoulli denklemi) statik yükseklik bileşenini hesaplamak.
       FORMÜL: ΔP_static (mSS) = ΔH (m)  (1 metre su = 1 mSS)
    */
    public double GetStaticHeadBetweenFloors(string floorA, string floorB)
    {
        var a = GetFloorByName(floorA);
        var b = GetFloorByName(floorB);
        if (a == null || b == null) return 0;

        return Math.Abs(b.Elevation - a.Elevation) / 1000.0; // mm → m = mSS
    }

    /*
       NE: Toplam Bina Yüksekliği
       NEDEN: Kritik hat basınç hesabında bina toplam yüksekliğini bilmek gerekir.
    */
    public double GetTotalBuildingHeight()
    {
        if (!_floors.Any()) return 0;
        return (_floors.Max(f => f.Elevation + f.Height) - _floors.Min(f => f.Elevation)) / 1000.0;
    }

    // --- YARDIMCI ---

    private static string GetLayerForSystem(MechanicalSystemType sysType) => sysType switch
    {
        MechanicalSystemType.DomesticColdWater => "MEK_TEMIZ_SU",
        MechanicalSystemType.DomesticHotWater  => "MEK_SICAK_SU",
        MechanicalSystemType.WasteWater        => "MEK_PIS_SU",
        MechanicalSystemType.RainWater         => "MEK_YAGMUR",
        MechanicalSystemType.FireProtection    => "MEK_YANGIN",
        MechanicalSystemType.Gas               => "MEK_GAZ",
        MechanicalSystemType.Ventilation       => "MEK_HAVALAND",
        _                                      => "MEK_GENEL"
    };

    private double GetEntityZ(MechanicalEntity entity)
    {
        if (entity is PipeEntity pipe) return Math.Min(pipe.StartPoint.Z, pipe.EndPoint.Z);
        if (entity is SanitaryFixtureEntity fix) return fix.Position.Z;
        if (entity is ElbowEntity elbow) return elbow.Center.Z;
        if (entity is TeeEntity tee) return tee.Center.Z;
        return 0;
    }

    private MechanicalEntity? CloneEntityWithOffset(MechanicalEntity original, Vector3D offset)
    {
        if (original is PipeEntity pipe)
        {
            var clone = new PipeEntity(
                pipe.StartPoint + offset,
                pipe.EndPoint + offset,
                pipe.InnerDiameter);
            clone.SystemType = pipe.SystemType;
            clone.PipeMaterialType = pipe.PipeMaterialType;
            clone.Color = pipe.Color;
            clone.Layer = pipe.Layer;
            return clone;
        }
        else if (original is SanitaryFixtureEntity fixture)
        {
            var clone = new SanitaryFixtureEntity(fixture.Position + offset, fixture.FixtureType, fixture.LoadUnits);
            clone.SystemType = fixture.SystemType;
            clone.Color = fixture.Color;
            clone.Layer = fixture.Layer;
            return clone;
        }
        else if (original is ElbowEntity elbow)
        {
            var clone = new ElbowEntity(elbow.Center + offset, elbow.InnerDiameter, elbow.IncomingVector, elbow.OutgoingVector);
            clone.SystemType = elbow.SystemType;
            clone.Color = elbow.Color;
            clone.Layer = elbow.Layer;
            return clone;
        }
        return null;
    }
}

// --- KAT VERİ MODELİ ---

public class FloorDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public double Elevation { get; set; }       // Kat yüksekliği (mm cinsinden, 0 = Zemin)
    public double Height { get; set; }          // Kat brüt yüksekliği (mm)
    public int Order { get; set; }              // Sıra numarası (0 = en alt)
    public bool IsActive { get; set; }          // Aktif çalışma katı
    public List<Guid> EntityIds { get; set; } = new();  // Bu kattaki entity ID'leri
    public List<Guid> RiserIds { get; set; } = new();   // Bu kattan geçen kolon ID'leri
}
