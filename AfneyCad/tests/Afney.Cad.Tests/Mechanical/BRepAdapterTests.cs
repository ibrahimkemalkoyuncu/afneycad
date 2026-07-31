using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Faz 2 "Genelleştirilmiş B-Rep Adaptörü" Testleri (Door/Window/Fixture/Room)
   NEDEN: docs/Roadmap_3D_Render_Motoru.md Faz 2 — Wall/Duct/Pipe zaten B-Rep'e sahipti,
          Fixture/Door/Window/Room hiç yoktu. Bu testler yeni servislerin (DoorWindowBRepService,
          FixtureBRepService, RoomBRepService) geçerli, tutarlı-boyutlu Solid'ler ürettiğini
          kilitliyor — 3D render motoruna (Direct3DViewportControl.LoadFromDatabase) veri
          besleyen katman, GPU pipeline'ından bağımsız olarak burada doğrulanıyor.
*/
public class BRepAdapterTests
{
    [Fact]
    public void DoorWindowBRepService_Door_ProducesSolidWithCorrectBoundingBox()
    {
        var db = new CadDatabase();
        var door = new DoorEntity(new Vector3D(1000, 2000, 0), width: 900, height: 2100);
        db.AddEntity(door);

        var solid = new DoorWindowBRepService(db).GenerateDoorSolid(door);
        var (verts, faces) = BRepTessellator.Tessellate(solid);

        Assert.True(verts.Count >= 8); // en az bir kutu (8 köşe)
        Assert.True(faces.Count > 0);

        double minZ = verts.Min(v => v.Z), maxZ = verts.Max(v => v.Z);
        Assert.Equal(0.0, minZ, precision: 1);
        Assert.Equal(2100.0, maxZ, precision: 1); // HeightMm

        double minX = verts.Min(v => v.X), maxX = verts.Max(v => v.X);
        Assert.Equal(900.0, maxX - minX, precision: 1); // WidthMm (Rotation=0, uAxis=X ekseni)
    }

    [Fact]
    public void DoorWindowBRepService_Window_SitsAtSillHeight()
    {
        var db = new CadDatabase();
        var window = new WindowEntity(new Vector3D(0, 0, 0), width: 1200, height: 1500) { SillHeightMm = 900 };
        db.AddEntity(window);

        var solid = new DoorWindowBRepService(db).GenerateWindowSolid(window);
        var (verts, _) = BRepTessellator.Tessellate(solid);

        double minZ = verts.Min(v => v.Z), maxZ = verts.Max(v => v.Z);
        Assert.Equal(900.0, minZ, precision: 1);   // SillHeightMm
        Assert.Equal(2400.0, maxZ, precision: 1);  // SillHeightMm + HeightMm
    }

    [Fact]
    public void FixtureBRepService_GeneratesBoxMatchingWidthDepth()
    {
        var db = new CadDatabase();
        var fixture = new SanitaryFixtureEntity(new Vector3D(500, 500, 0), "Washbasin", 1.0) { Width = 600, Depth = 400 };
        db.AddEntity(fixture);

        var solid = new FixtureBRepService(db).GenerateFixtureSolid(fixture);
        var (verts, faces) = BRepTessellator.Tessellate(solid);

        Assert.True(faces.Count > 0);
        double minX = verts.Min(v => v.X), maxX = verts.Max(v => v.X);
        double minY = verts.Min(v => v.Y), maxY = verts.Max(v => v.Y);
        Assert.Equal(600.0, maxX - minX, precision: 1);
        Assert.Equal(400.0, maxY - minY, precision: 1);
    }

    [Fact]
    public void RoomBRepService_GeneratesThinSlabMatchingBoundary()
    {
        var db = new CadDatabase();
        var mahal = new MahalEntity(new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 3000, 0), new(0, 3000, 0)
        }, "Test Oda");
        db.AddEntity(mahal);

        var solid = new RoomBRepService(db).GenerateRoomSlab(mahal);
        Assert.NotNull(solid);

        var (verts, faces) = BRepTessellator.Tessellate(solid!);
        Assert.True(faces.Count > 0);

        double minX = verts.Min(v => v.X), maxX = verts.Max(v => v.X);
        double minY = verts.Min(v => v.Y), maxY = verts.Max(v => v.Y);
        double minZ = verts.Min(v => v.Z), maxZ = verts.Max(v => v.Z);
        Assert.Equal(4000.0, maxX - minX, precision: 1);
        Assert.Equal(3000.0, maxY - minY, precision: 1);
        Assert.True(maxZ - minZ > 0 && maxZ - minZ < 200); // ince döşeme, tam oda yüksekliği DEĞİL
    }

    [Fact]
    public void RoomBRepService_DegenerateBoundary_ReturnsNullInsteadOfThrowing()
    {
        var db = new CadDatabase();
        var mahal = new MahalEntity(new List<Vector3D> { new(0, 0, 0), new(0, 0, 0) }, "Bozuk"); // < 3 nokta

        var solid = new RoomBRepService(db).GenerateRoomSlab(mahal);
        Assert.Null(solid);
    }
}
