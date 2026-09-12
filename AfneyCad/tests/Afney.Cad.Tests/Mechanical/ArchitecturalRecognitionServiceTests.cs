using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: ArchitecturalRecognitionService — Yay (Arc) Duvar Testleri
   NEDEN — GERÇEK BOŞLUK (Session #75 iş akışı denetiminde bulundu): Bu servis, duvar
          katmanındaki bir ArcEntity'yi önceden sadece kaba bir bounding-box dikdörtgeniyle
          (4 köşe) temsil ediyordu — ArchEntityConverterService'in (DWG-import yolu) aynı
          soruna Session #75'te bulduğu chord-tessellation çözümü buraya (tanıma/çakışma
          yolu) hiç uygulanmamıştı. Bu testler artık bir yayın gerçek eğrisine yakın çok
          sayıda sınır noktasıyla temsil edildiğini (4 sabit köşeden FAZLA nokta) kilitler.
*/
public class ArchitecturalRecognitionServiceTests
{
    [Fact]
    public void RecognizeObstacles_ArcOnWallLayer_BoundaryApproximatesArcNotJustBoundingBox()
    {
        var db = new CadDatabase();
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 3000, 0, System.Math.PI / 2) { Layer = "A-DUVAR" };
        db.AddEntity(arc);

        var obstacles = new ArchitecturalRecognitionService(db).RecognizeObstacles();

        var obstacle = Assert.Single(obstacles);
        Assert.Equal(ObstacleType.Wall, obstacle.Type);

        // Kaba bir bounding-box temsili sadece 4 köşe üretirdi — chord-tessellation ile
        // çok daha fazla sınır noktası (10°'lik adımlarla en az 4, tipik olarak daha fazla) beklenir.
        Assert.True(obstacle.Boundary.Count > 4,
            $"Beklenen: bounding-box'tan (4 köşe) fazla sınır noktası, bulunan: {obstacle.Boundary.Count}");
    }

    [Fact]
    public void RecognizeObstacles_ArcOnWallLayer_BoundaryPointsStayWithinArcRadius()
    {
        var db = new CadDatabase();
        var arc = new ArcEntity(new Vector3D(1000, 500, 0), 2000, 0, System.Math.PI) { Layer = "DUVAR" };
        db.AddEntity(arc);

        var obstacles = new ArchitecturalRecognitionService(db).RecognizeObstacles();
        var obstacle = Assert.Single(obstacles);

        foreach (var point in obstacle.Boundary)
        {
            double distToCenter = point.DistanceTo(arc.Center);
            Assert.True(distToCenter <= arc.Radius + 0.01,
                $"Sınır noktası yarıçapın dışına taşıyor: {distToCenter} > {arc.Radius}");
        }
    }
}
