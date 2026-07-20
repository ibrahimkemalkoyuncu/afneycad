using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: DuctBRepService Testleri
   NEDEN: DuctEntity'ler (dikdörtgen/dairesel kanal) 3D görünümde hiç temsil edilmiyordu —
          Pipe3DModelService bunları hiç kapsamıyordu. Bu testler, hem dikdörtgen (ExtrudeBox)
          hem dairesel (N-gon ExtrudePolygon) kanal geometrisinin B-Rep olarak Euler-geçerli
          ve hacmen analitik formülle tutarlı olduğunu kanıtlar.
*/
public class DuctBRepServiceTests
{
    [Fact]
    public void GenerateDuctSolid_Rectangular_VolumeMatchesAnalyticalFormula()
    {
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0), width: 400, height: 300);
        var service = new DuctBRepService(new CadDatabase());

        var solid = service.GenerateDuctSolid(duct);

        Assert.NotNull(solid);
        Assert.True(solid!.IsValid());

        double expected = 400.0 * 300.0 * 3000.0;
        Assert.Equal(expected, solid.GetVolume(), precision: 0);
    }

    [Fact]
    public void GenerateDuctSolid_Circular_VolumeApproximatesCylinderFormula()
    {
        // N-gon yaklaşıklaması gerçek daireye yakınsar ama tam eşit değildir (poligon < çember
        // alanı) — 16 segmentte %5'ten düşük sapma beklenir.
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), diameter: 315);
        var service = new DuctBRepService(new CadDatabase());

        var solid = service.GenerateDuctSolid(duct, circularSegments: 16);

        Assert.NotNull(solid);
        Assert.True(solid!.IsValid());

        double radius = 315.0 / 2.0;
        double cylinderVolume = Math.PI * radius * radius * 2000.0;
        double relativeError = Math.Abs(solid.GetVolume() - cylinderVolume) / cylinderVolume;
        Assert.True(relativeError < 0.05, $"Relative error too high: {relativeError}");
    }

    [Fact]
    public void GenerateDuctSolid_DiagonalRun_VolumeStillMatches()
    {
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 2000, 1000), width: 250, height: 200);
        var service = new DuctBRepService(new CadDatabase());

        var solid = service.GenerateDuctSolid(duct);

        Assert.NotNull(solid);
        double length = new Vector3D(2000, 2000, 1000).Length();
        double expected = 250.0 * 200.0 * length;
        double relativeError = Math.Abs(solid!.GetVolume() - expected) / expected;
        Assert.True(relativeError < 1e-6, $"Relative error too high: {relativeError}");
    }

    [Fact]
    public void GenerateAllDuctSolids_ReturnsOneSolidPerDuctEntity()
    {
        var db = new CadDatabase();
        db.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(3000, 0, 0), width: 400, height: 300));
        db.AddEntity(new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(0, 3000, 0), diameter: 250));

        var service = new DuctBRepService(db);
        var solids = service.GenerateAllDuctSolids();

        Assert.Equal(2, solids.Count);
        Assert.All(solids, s => Assert.True(s.IsValid()));
    }
}
