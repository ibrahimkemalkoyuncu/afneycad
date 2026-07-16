using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: Circle/Arc TRIM/EXTEND/OFFSET Geometri Testleri
   NEDEN: Bu komutlar önceden sadece Line/Pipe destekliyordu; Circle/Arc desteği bu oturumda
          eklendi. Açı sarmalı (angle wraparound) hataya çok açık bir alan olduğu için ve
          canlı UI ile doğrulama bu ortamda güvenilir olmadığı için gerçek birim testleri
          şart — "derleniyor" ile "doğru çalışıyor" aynı şey değil.
*/
public class TrimExtendOffsetGeometryTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void TrimCircle_ClickedSegmentBetweenTwoIntersections_BecomesRemainingArc()
    {
        var db = new CadDatabase();
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 10);
        // Yatay çizgi çemberi (-10,0) ve (10,0) noktalarında (açı 180° ve 0°) kesiyor.
        var line = new LineEntity(new Vector3D(-20, 0, 0), new Vector3D(20, 0, 0));
        db.AddEntity(circle);
        db.AddEntity(line);

        var cmd = new TrimCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 10, 0)); // Üst yarıda tıkla (açı 90°)

        var circles = db.GetAllEntities().OfType<CircleEntity>().ToList();
        var arcs = db.GetAllEntities().OfType<ArcEntity>().ToList();

        Assert.Empty(circles); // Orijinal çember kaldırıldı
        Assert.Single(arcs);

        var result = arcs[0];
        Assert.Equal(10, result.Radius, precision: 6);
        // Kalan yay alt yarım daire olmalı: 180°(π) → 360°/0°
        Assert.Equal(System.Math.PI, result.StartAngle, precision: 5);
        Assert.Equal(0.0, result.EndAngle, precision: 5);
    }

    [Fact]
    public void TrimCircle_FewerThanTwoIntersections_DoesNothing()
    {
        var db = new CadDatabase();
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 10);
        db.AddEntity(circle);

        var cmd = new TrimCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.OnPointerPressed(new Vector3D(0, 10, 0));

        // Kesişim olmadığı için çember olduğu gibi kalmalı.
        Assert.Single(db.GetAllEntities().OfType<CircleEntity>());
    }

    [Fact]
    public void ExtendArc_ExtendsToTangentBoundary()
    {
        var db = new CadDatabase();
        // Çeyrek yay: 0° -> 90°, yarıçap 10, merkez orijin.
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 10, 0, System.Math.PI / 2);
        // x=-10 dikey çizgisi çembere (-10,0) noktasında (açı 180°) teğet.
        var boundary = new LineEntity(new Vector3D(-10, -20, 0), new Vector3D(-10, 20, 0));
        db.AddEntity(arc);
        db.AddEntity(boundary);

        var cmd = new ExtendCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        // Yayın "bitiş" ucuna (0,10) yakın bir noktaya tıkla -> ileri yönde uzat.
        cmd.OnPointerPressed(new Vector3D(0, 10, 0));

        var arcs = db.GetAllEntities().OfType<ArcEntity>().ToList();
        Assert.Single(arcs);
        var result = arcs[0];

        Assert.Equal(0.0, result.StartAngle, precision: 5);
        Assert.Equal(System.Math.PI, result.EndAngle, precision: 5); // 180°'ye kadar uzadı
    }

    [Fact]
    public void OffsetCircle_TargetOutside_GrowsRadiusToDistanceFromCenter()
    {
        var db = new CadDatabase();
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 5);
        db.AddEntity(circle);

        var cmd = new OffsetCommand(db, db.TransactionManager, new[] { circle });
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(20, 0, 0)); // Merkezden 20 birim uzakta bir nokta

        var circles = db.GetAllEntities().OfType<CircleEntity>().ToList();
        // Orijinal (r=5) + yeni ötelenmiş (r=20) — Offset orijinali silmez, kopya ekler.
        Assert.Equal(2, circles.Count);
        Assert.Contains(circles, c => System.Math.Abs(c.Radius - 20.0) < Tol);
    }

    [Fact]
    public void OffsetCircle_TargetInside_ShrinksRadiusToDistanceFromCenter()
    {
        var db = new CadDatabase();
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 10);
        db.AddEntity(circle);

        var cmd = new OffsetCommand(db, db.TransactionManager, new[] { circle });
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(3, 0, 0)); // Merkeze 3 birim uzaklıkta (içeride)

        var circles = db.GetAllEntities().OfType<CircleEntity>().ToList();
        Assert.Contains(circles, c => System.Math.Abs(c.Radius - 3.0) < Tol);
    }
}
