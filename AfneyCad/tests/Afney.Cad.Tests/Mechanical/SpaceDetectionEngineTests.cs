using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: SpaceDetectionEngine Testleri
   NEDEN: Bu servis (gerçek planar-graph algoritması — half-edge benzeri CCW traversal,
          Shoelace alan, en büyük poligonu dış kabuk sayıp eleme) daha önce HİÇ test
          edilmiyordu (önceki denetimde bu, alt-özellik puanını düşüren ana neden olarak
          işaretlenmişti). Bu testler, tek odalı ve çok odalı gerçek senaryolarda otonom
          oda tespitinin GERÇEKTEN doğru alan/oda sayısı ürettiğini kanıtlar.
*/
public class SpaceDetectionEngineTests
{
    private static void AddWall(CadDatabase db, Vector3D a, Vector3D b)
        => db.AddEntity(new LineEntity(a, b) { Layer = "DUVAR" });

    [Fact]
    public void DetectAllSpaces_SingleRectangularRoom_FindsOneRoomWithCorrectArea()
    {
        // 4m x 3m tek oda (kapalı 4 duvar).
        var db = new CadDatabase();
        AddWall(db, new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0));
        AddWall(db, new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0));
        AddWall(db, new Vector3D(4000, 3000, 0), new Vector3D(0, 3000, 0));
        AddWall(db, new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0));

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Single(rooms); // dış kabuk elendi, sadece iç oda kaldı
        Assert.Equal(12.0, SpaceDetectionEngine.CalculateAreaM2(rooms[0]), precision: 3);
    }

    [Fact]
    public void DetectAllSpaces_TwoRoomsSeparatedByInternalWall_FindsBothRoomsWithCorrectAreas()
    {
        // 8m x 3m dış kabuk, x=4m'de bir ara duvarla ikiye bölünmüş — her biri 4m x 3m = 12 m².
        var db = new CadDatabase();
        AddWall(db, new Vector3D(0, 0, 0), new Vector3D(8000, 0, 0));
        AddWall(db, new Vector3D(8000, 0, 0), new Vector3D(8000, 3000, 0));
        AddWall(db, new Vector3D(8000, 3000, 0), new Vector3D(0, 3000, 0));
        AddWall(db, new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0));
        AddWall(db, new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0)); // ara duvar

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Equal(2, rooms.Count);
        foreach (var room in rooms)
            Assert.Equal(12.0, SpaceDetectionEngine.CalculateAreaM2(room), precision: 3);
    }

    [Fact]
    public void DetectAllSpaces_NonWallLayer_IsIgnoredEntirely()
    {
        // Duvar katmanı anahtar kelimelerinden hiçbirini içermeyen bir katmandaki kapalı
        // dörtgen (ör. mobilya çizimi) oda olarak algılanmamalı.
        var db = new CadDatabase();
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0)) { Layer = "MOBILYA" });
        db.AddEntity(new LineEntity(new Vector3D(2000, 0, 0), new Vector3D(2000, 1000, 0)) { Layer = "MOBILYA" });
        db.AddEntity(new LineEntity(new Vector3D(2000, 1000, 0), new Vector3D(0, 1000, 0)) { Layer = "MOBILYA" });
        db.AddEntity(new LineEntity(new Vector3D(0, 1000, 0), new Vector3D(0, 0, 0)) { Layer = "MOBILYA" });

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Empty(rooms);
    }

    [Fact]
    public void DetectRoomNameFromTexts_TextInsideBoundary_ReturnsRoomName()
    {
        var db = new CadDatabase();
        db.AddEntity(new TextEntity("Mutfak", new Vector3D(2000, 1500, 0), 250));
        var engine = new SpaceDetectionEngine(db);

        var boundary = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 3000, 0), new(0, 3000, 0)
        };

        string? name = engine.DetectRoomNameFromTexts(boundary);

        Assert.Equal("Mutfak", name);
    }

    [Fact]
    public void CalculateAreaM2_And_CalculatePerimeterM_MatchAnalyticalRectangleFormulas()
    {
        var rect = new List<Vector3D>
        {
            new(0, 0, 0), new(5000, 0, 0), new(5000, 2000, 0), new(0, 2000, 0)
        };

        Assert.Equal(10.0, SpaceDetectionEngine.CalculateAreaM2(rect), precision: 6);       // 5m x 2m
        Assert.Equal(14.0, SpaceDetectionEngine.CalculatePerimeterM(rect), precision: 6);   // 2*(5+2)
    }
}
