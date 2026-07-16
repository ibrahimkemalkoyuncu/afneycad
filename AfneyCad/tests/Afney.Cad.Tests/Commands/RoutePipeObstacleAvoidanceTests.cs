using System.Linq;
using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: RoutePipeCommand Engel Kaçınma Testleri
   NEDEN: Bu oturumda RoutePipeCommand'a AutoRouteService (A*) bağlandı — önceden manuel
          rotalama her zaman düz çizgi çekiyordu ("Pathfinding şimdilik devre dışı" yorumu).
          Artık tıklanan segment bir mimari engeli (WALL/BUILD katmanlı) kesiyorsa otomatik
          olarak etrafından dolaşan bir rota kullanılmalı.
*/
public class RoutePipeObstacleAvoidanceTests
{
    [Fact]
    public void OnPointerPressed_StraightSegmentCrossesWall_RoutesAroundObstacle()
    {
        var db = new CadDatabase();
        var kernel = new MechanicalKernel();

        // Başlangıç ve hedef noktalar arasında dik bir duvar (WALL katmanı).
        var wall = new LineEntity(new Vector3D(500, -1000, 0), new Vector3D(500, 1000, 0)) { Layer = "A-WALL" };
        db.AddEntity(wall);

        var cmd = new RoutePipeCommand(db, kernel);
        cmd.OnEntityPlaced += e => db.AddEntity(e);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));      // Başlangıç (duvarın solunda)
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));   // Hedef (duvarın sağında) — düz çizgi duvarı keser

        var pipes = db.GetAllEntities().OfType<PipeEntity>().ToList();

        // Düz bir tek segment olsaydı tam olarak 1 boru olurdu; kaçınma rotası birden çok segment üretir.
        Assert.True(pipes.Count > 1, "Engelden kaçınma rotası birden fazla boru segmenti üretmeli.");
    }

    [Fact]
    public void OnPointerPressed_NoObstacle_DrawsDirectSegment()
    {
        var db = new CadDatabase();
        var kernel = new MechanicalKernel();

        var cmd = new RoutePipeCommand(db, kernel);
        cmd.OnEntityPlaced += db.AddEntity;
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));

        var pipes = db.GetAllEntities().OfType<PipeEntity>().ToList();
        Assert.Single(pipes);
    }
}
