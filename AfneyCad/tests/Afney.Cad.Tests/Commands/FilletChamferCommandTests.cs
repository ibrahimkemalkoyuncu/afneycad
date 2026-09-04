using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: FILLET/CHAMFER Komut Testleri (FilletChamferCommandTests)
   NEDEN: FilletCommand/ChamferCommand önceden kod tabanında hiç yoktu (TRIM/EXTEND'in aksine).
          Bu testler, iki tıklamalık seçim akışının (birinci doğru -> ikinci doğru) veritabanını
          doğru şekilde güncellediğini (iki orijinal Line kaldırılır, iki kısaltılmış Line +
          bir Arc/Line eklenir) uçtan uca doğrular.
*/
public class FilletChamferCommandTests
{
    [Fact]
    public void FilletCommand_TwoPerpendicularLines_RemovesOriginalsAndAddsTrimmedLinesPlusArc()
    {
        var db = new CadDatabase();
        var lineA = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
        var lineB = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(0, 10, 0));
        db.AddEntity(lineA);
        db.AddEntity(lineB);

        var cmd = new FilletCommand(db, db.TransactionManager, currentZoom: 1.0, radius: 2.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(8, 0, 0));  // Birinci doğruyu (yatay) seç
        cmd.OnPointerPressed(new Vector3D(0, 8, 0));  // İkinci doğruyu (dikey) seç

        var lines = db.GetAllEntities().OfType<LineEntity>().ToList();
        var arcs = db.GetAllEntities().OfType<ArcEntity>().ToList();

        Assert.Equal(2, lines.Count); // Orijinal 2 çizgi kaldırıldı, 2 kısaltılmış çizgi eklendi
        Assert.Single(arcs);
        Assert.Equal(2.0, arcs[0].Radius, precision: 6);

        // Kısaltılmış çizgilerden biri (2,0)-(10,0), diğeri (0,2)-(0,10) olmalı.
        Assert.Contains(lines, l => PointsMatch(l, new Vector3D(2, 0, 0), new Vector3D(10, 0, 0)));
        Assert.Contains(lines, l => PointsMatch(l, new Vector3D(0, 2, 0), new Vector3D(0, 10, 0)));
    }

    [Fact]
    public void FilletCommand_ParallelLines_LeavesDatabaseUnchangedAndReportsError()
    {
        var db = new CadDatabase();
        var lineA = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
        var lineB = new LineEntity(new Vector3D(0, 5, 0), new Vector3D(10, 5, 0));
        db.AddEntity(lineA);
        db.AddEntity(lineB);

        string? feedback = null;
        var cmd = new FilletCommand(db, db.TransactionManager, currentZoom: 1.0, radius: 2.0);
        cmd.OnFeedback += msg => feedback = msg;
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(5, 0, 0));
        cmd.OnPointerPressed(new Vector3D(5, 5, 0));

        Assert.Equal(2, db.GetAllEntities().OfType<LineEntity>().Count()); // Değişiklik yok
        Assert.Contains("paralel", feedback ?? "", System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChamferCommand_TwoPerpendicularLines_RemovesOriginalsAndAddsTrimmedLinesPlusChamferLine()
    {
        var db = new CadDatabase();
        var lineA = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
        var lineB = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(0, 10, 0));
        db.AddEntity(lineA);
        db.AddEntity(lineB);

        var cmd = new ChamferCommand(db, db.TransactionManager, currentZoom: 1.0, dist1: 3.0, dist2: 4.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(8, 0, 0));
        cmd.OnPointerPressed(new Vector3D(0, 8, 0));

        var lines = db.GetAllEntities().OfType<LineEntity>().ToList();
        // 2 kısaltılmış + 1 pah çizgisi = 3 (orijinal 2 kaldırıldı)
        Assert.Equal(3, lines.Count);

        Assert.Contains(lines, l => PointsMatch(l, new Vector3D(3, 0, 0), new Vector3D(10, 0, 0)));
        Assert.Contains(lines, l => PointsMatch(l, new Vector3D(0, 4, 0), new Vector3D(0, 10, 0)));
        Assert.Contains(lines, l => PointsMatch(l, new Vector3D(3, 0, 0), new Vector3D(0, 4, 0)));
    }

    private static bool PointsMatch(LineEntity l, Vector3D p1, Vector3D p2)
    {
        const double tol = 1e-6;
        bool forward = l.StartPoint.DistanceTo(p1) < tol && l.EndPoint.DistanceTo(p2) < tol;
        bool reverse = l.StartPoint.DistanceTo(p2) < tol && l.EndPoint.DistanceTo(p1) < tol;
        return forward || reverse;
    }
}
