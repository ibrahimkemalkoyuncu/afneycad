using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Manuel Mahal — Kapı/Pencere Pervaz-Snap Testleri
   NEDEN: `ManualMahalCommand.OnPointerPressed`, bir duvara denk gelmeyen tıklamaları önceden
          HAM imleç koordinatı olarak ekliyordu — kılavuz çizgisi ve dolayısıyla mahal alanı
          kullanıcının ne kadar hassas tıkladığına bağlıydı. Artık yakındaki bir `DoorEntity`/
          `WindowEntity` varsa, tıklama ANALİTİK pervaz (jamb) noktasına snap ediyor
          (Position ± WidthMm/2, Rotation yönünde) — bu testler snap'in gerçekten doğru
          noktaya (yaklaşık tıklamaya değil) gittiğini kanıtlıyor.
*/
public class ManualMahalOpeningSnapTests
{
    [Fact]
    public void OnPointerPressed_ClickNearDoorJamb_SnapsToExactAnalyticalJambPoint()
    {
        var db = new CadDatabase();
        // Yatay bir duvar üzerinde, X ekseni boyunca 900mm genişliğinde bir kapı — merkez (1000,0,0).
        var door = new DoorEntity(new Vector3D(1000, 0, 0), width: 900) { Rotation = 0 };
        db.AddEntity(door);

        var cmd = new ManualMahalCommand(db, _ => { });

        cmd.Start();
        // Tıklama, sağ pervaz noktasından (1450,0,0) 40mm uzakta — tolerans (500mm) içinde.
        cmd.OnPointerPressed(new Vector3D(1410, 15, 0));

        // Kılavuz/serbest nokta olarak ham tıklama DEĞİL, tam pervaz noktası (1450,0,0) eklenmeli.
        var lastPoint = GetLastFreePoint(cmd);
        Assert.NotNull(lastPoint);
        Assert.Equal(1450.0, lastPoint.Value.X, precision: 3);
        Assert.Equal(0.0, lastPoint.Value.Y, precision: 3);
    }

    [Fact]
    public void OnPointerPressed_ClickFarFromAnyOpening_KeepsRawClickPosition()
    {
        var db = new CadDatabase();
        var door = new DoorEntity(new Vector3D(1000, 0, 0), width: 900) { Rotation = 0 };
        db.AddEntity(door);

        var cmd = new ManualMahalCommand(db, _ => { });
        cmd.Start();

        // Tıklama en yakın pervaz noktasından (1450,0,0) 5000mm uzakta — tolerans dışı.
        cmd.OnPointerPressed(new Vector3D(6450, 0, 0));

        var lastPoint = GetLastFreePoint(cmd);
        Assert.NotNull(lastPoint);
        Assert.Equal(6450.0, lastPoint.Value.X, precision: 3);
    }

    [Fact]
    public void OnPointerPressed_ClickNearRotatedWindowJamb_UsesRotationForJambDirection()
    {
        var db = new CadDatabase();
        // Dikey bir duvar üzerinde (90° döndürülmüş), 1200mm genişliğinde pencere, merkez (0,2000,0).
        var window = new WindowEntity(new Vector3D(0, 2000, 0), width: 1200) { Rotation = Math.PI / 2 };
        db.AddEntity(window);

        var cmd = new ManualMahalCommand(db, _ => { });
        cmd.Start();

        // Üst pervaz noktası analitik olarak (0, 2600, 0) olmalı (hw=600, dir=(cos90,sin90)=(0,1)).
        cmd.OnPointerPressed(new Vector3D(10, 2580, 0));

        var lastPoint = GetLastFreePoint(cmd);
        Assert.NotNull(lastPoint);
        Assert.Equal(0.0, lastPoint.Value.X, precision: 2);
        Assert.Equal(2600.0, lastPoint.Value.Y, precision: 2);
    }

    private static Vector3D? GetLastFreePoint(ManualMahalCommand cmd)
    {
        var field = typeof(ManualMahalCommand).GetField("_freePoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var list = (System.Collections.Generic.List<Vector3D>)field.GetValue(cmd)!;
        return list.Count > 0 ? list[^1] : null;
    }
}
