using System;
using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: Polyline/Duct Offset Testleri
   NEDEN: OFFSET önceden LwPolyline'ı hiç desteklemiyordu (AutoCAD OFFSET'in en temel kullanım
          senaryolarından biri) ve DuctEntity de desteklenmiyordu. Bu testler her iki yeni
          desteğin gerçekten çalıştığını doğruluyor.
*/
public class PolylineDuctOffsetTests
{
    private static double DistancePointToInfiniteLine(Vector3D p, Vector3D a, Vector3D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        return Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / len;
    }

    [Fact]
    public void OffsetPolyline_OpenLShape_AllNewVerticesAreOffsetDistanceFromOriginalSegments()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0),
            new Vector3D(100, 0, 0),
            new Vector3D(100, 100, 0),
        }, isClosed: false);
        db.AddEntity(poly);

        var cmd = new OffsetCommand(db, db.TransactionManager, new CadEntity[] { poly });
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(110, 10, 0)); // dikey segmentin dışına tıkla

        var offsetPoly = db.GetAllEntities().OfType<LwPolylineEntity>().Single(p => p != poly);
        Assert.Equal(3, offsetPoly.Vertices.Count);

        // İlk ve son köşe, ilgili orijinal segmentten yaklaşık aynı mesafede olmalı.
        double d0 = DistancePointToInfiniteLine(offsetPoly.Vertices[0], poly.Vertices[0], poly.Vertices[1]);
        double d2 = DistancePointToInfiniteLine(offsetPoly.Vertices[2], poly.Vertices[1], poly.Vertices[2]);
        Assert.True(d0 > 5 && d0 < 15, $"d0={d0}");
        Assert.True(d2 > 5 && d2 < 15, $"d2={d2}");

        // Orta (köşe) nokta HER İKİ orijinal segmentten de yaklaşık aynı mesafede olmalı (miter join).
        double dMidToSeg0 = DistancePointToInfiniteLine(offsetPoly.Vertices[1], poly.Vertices[0], poly.Vertices[1]);
        double dMidToSeg1 = DistancePointToInfiniteLine(offsetPoly.Vertices[1], poly.Vertices[1], poly.Vertices[2]);
        Assert.True(Math.Abs(dMidToSeg0 - dMidToSeg1) < 1.0);
    }

    [Fact]
    public void OffsetPolyline_ClosedRectangle_GrowsOutwardUniformly()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0),
            new Vector3D(100, 0, 0),
            new Vector3D(100, 50, 0),
            new Vector3D(0, 50, 0),
        }, isClosed: true);
        db.AddEntity(poly);

        var cmd = new OffsetCommand(db, db.TransactionManager, new CadEntity[] { poly });
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(110, 25, 0)); // sağ kenarın dışına tıkla → dışa doğru büyümeli

        var offsetPoly = db.GetAllEntities().OfType<LwPolylineEntity>().Single(p => p != poly);
        Assert.Equal(4, offsetPoly.Vertices.Count);
        Assert.True(offsetPoly.IsClosed);

        // Dışa büyüyen bir dikdörtgen: X aralığı orijinalden daha genişlemiş olmalı.
        double minX = offsetPoly.Vertices.Min(v => v.X);
        double maxX = offsetPoly.Vertices.Max(v => v.X);
        Assert.True(minX < 0);
        Assert.True(maxX > 100);
    }

    [Fact]
    public void OffsetCommand_DuctEntity_OffsetsLikePipe()
    {
        var db = new CadDatabase();
        var duct = new DuctEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 400, 300);
        db.AddEntity(duct);

        var cmd = new OffsetCommand(db, db.TransactionManager, new CadEntity[] { duct });
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(500, 200, 0));

        var offsetDuct = db.GetAllEntities().OfType<DuctEntity>().Single(d => d != duct);
        Assert.Equal(200, offsetDuct.StartPoint.Y, precision: 1);
        Assert.Equal(200, offsetDuct.EndPoint.Y, precision: 1);
    }
}
