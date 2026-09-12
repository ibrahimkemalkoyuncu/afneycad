using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Models;

/*
   NE: Kat/Seviye Tanımı (MepLevel)
   NEDEN: Binadaki kat yüksekliklerini (Elevation) ve kat planlarını MEP ağında ayrıştırmak için. (Suggestion 17)
   
   BIM-Lite: FineSANI ve AutoCAD MEP mantığında, katlar sadece çizim düzlemi değil, nesnelerin ait olduğu uzamsal konteynerlardır.
*/
public class MepLevel
{
    public string Name { get; set; } = "Floor 0";
    public double Elevation { get; set; }
    public double Height { get; set; } = 3000.0;

    // --- BIM-Lite Hiyerarşisi ---
    // NE/NEDEN: Bu iki liste canlı entity referansı taşıyor (uygulamada hiç doldurulmuyor) —
    // LevelPersistenceService kat listesini JSON'a yazarken bunları hariç tutuyor.
    [JsonIgnore] public List<MahalEntity> Rooms { get; } = new();
    [JsonIgnore] public List<MechanicalEntity> Entities { get; } = new();

    /*
       NE: Id/Order/IsActive/EntityIds/RiserIds
       NEDEN — Session #75 iş akışı denetiminde bulunan veri-modeli parçalanmasının kapatılması:
              bu alanlar önceden sadece `MultiStoryBuildingService.FloorDefinition`'da vardı;
              `MultiStoryManagerDialog` ile `LevelManagerDialog` iki ayrı, elle senkronize
              edilen kat listesine bakıyordu. Artık `FloorDefinition` kaldırıldı ve
              `MultiStoryBuildingService` doğrudan `LevelManager`/`MepLevel` üzerinde çalışıyor
              — bu yüzden `MepLevel`'in GUID kimliği (Id), gösterim sırası (Order), aktif kat
              bayrağı (IsActive) ve kat-başına entity/riser takibi (EntityIds/RiserIds) taşıması
              gerekiyor.
    */
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Order { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> EntityIds { get; set; } = new();
    public List<Guid> RiserIds { get; set; } = new();

    public MepLevel(string name, double elevation, double height = 3000.0)
    {
        Name = name;
        Elevation = elevation;
        Height = height;
    }
}
