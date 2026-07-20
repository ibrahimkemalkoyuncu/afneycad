using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: MultiStoryBuildingService Testleri
   NEDEN: Çok Katlı Bina kategorisindeki servisler daha önce hiç test edilmiyordu (0 test
          dosyası, önceki denetimde alt-özellik puanı 2/10). Bu testler, kat tanımının
          (elevation'a göre otomatik sıralama) ve dikey kolon (riser) oluşturmanın —
          "kat başına 1 dikey boru, ardışık kat kotları arasında" — gerçekten doğru
          çalıştığını kanıtlar.
*/
public class MultiStoryBuildingServiceTests
{
    [Fact]
    public void InitializeStandardBuilding_WithBasement_ProducesCorrectFloorCountAndOrder()
    {
        var service = new MultiStoryBuildingService(new CadDatabase());

        var floors = service.InitializeStandardBuilding(normalFloorCount: 3, floorHeight: 3000, hasBasement: true);

        // Bodrum + Zemin + 3 Normal + Çatı = 6 kat
        Assert.Equal(6, floors.Count);
        Assert.Equal("Bodrum Kat", floors[0].Name);
        Assert.Equal(-3000, floors[0].Elevation);
        Assert.Equal("Zemin Kat", floors[1].Name);
        Assert.Equal(0, floors[1].Elevation);
        Assert.Equal("Çatı Katı", floors[^1].Name);

        // Order, elevation'a göre artan sırada olmalı (0'dan başlayarak)
        for (int i = 0; i < floors.Count; i++)
            Assert.Equal(i, floors[i].Order);
    }

    [Fact]
    public void AddFloor_OutOfOrderInsertion_ResortsAndRenumbersByElevation()
    {
        var service = new MultiStoryBuildingService(new CadDatabase());

        service.AddFloor("Kat 2", elevationMm: 6000);
        service.AddFloor("Kat 1", elevationMm: 3000); // eklenme sırası ters, elevation sırası doğru olmalı
        service.AddFloor("Zemin", elevationMm: 0);

        var floors = service.GetAllFloors();

        Assert.Equal("Zemin", floors[0].Name);
        Assert.Equal("Kat 1", floors[1].Name);
        Assert.Equal("Kat 2", floors[2].Name);
        Assert.Equal(0, floors[0].Order);
        Assert.Equal(1, floors[1].Order);
        Assert.Equal(2, floors[2].Order);
    }

    [Fact]
    public void CreateRiser_ForNFloors_ProducesNMinus1PipesSpanningConsecutiveElevations()
    {
        var service = new MultiStoryBuildingService(new CadDatabase());
        service.InitializeStandardBuilding(normalFloorCount: 4, floorHeight: 3000, hasBasement: false);
        // Zemin + 4 Normal + Çatı = 6 kat → 5 riser segmenti

        var pipes = service.CreateRiser(new Vector3D(1000, 2000, 0), diameter: 50,
            systemType: MechanicalSystemType.DomesticColdWater);

        Assert.Equal(5, pipes.Count);
        foreach (var pipe in pipes)
        {
            Assert.Equal(1000, pipe.StartPoint.X);
            Assert.Equal(2000, pipe.StartPoint.Y);
            Assert.True(pipe.EndPoint.Z > pipe.StartPoint.Z, "Riser segmenti yukarı doğru gitmeli");
            Assert.Equal(MechanicalSystemType.DomesticColdWater, pipe.SystemType);
        }

        // Segmentler ardışık olmalı: pipe[i].EndPoint.Z == pipe[i+1].StartPoint.Z
        for (int i = 0; i < pipes.Count - 1; i++)
            Assert.Equal(pipes[i].EndPoint.Z, pipes[i + 1].StartPoint.Z, precision: 6);
    }

    [Fact]
    public void SetActiveFloor_OnlyOneFloorIsActiveAtATime()
    {
        var service = new MultiStoryBuildingService(new CadDatabase());
        var floors = service.InitializeStandardBuilding(normalFloorCount: 2, floorHeight: 3000, hasBasement: false);

        service.SetActiveFloor(floors[0].Id);
        Assert.Equal(floors[0].Id, service.GetActiveFloor()!.Id);

        service.SetActiveFloor(floors[2].Id);
        Assert.Equal(floors[2].Id, service.GetActiveFloor()!.Id);
        Assert.All(service.GetAllFloors(), f => Assert.Equal(f.Id == floors[2].Id, f.IsActive));
    }
}
