using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: Polyline TRIM/EXTEND Testleri
   NEDEN: TRIM/EXTEND önceden LwPolyline'ı hiç desteklemiyordu (kodda da açıkça "kapsam dışı"
          olarak işaretliydi) — rakip yazılımlarda evrensel olan bir senaryo. Bu testler,
          ORTADAKİ bir segment budandığında polyline'ın gerçekten İKİYE bölündüğünü, UÇTAKİ
          bir segment budandığında tek parça kaldığını, kapalı polyline budandığında halkanın
          açıldığını, ve uç segmentin gerçekten uzatılabildiğini doğruluyor.
*/
public class PolylineTrimExtendTests
{
    [Fact]
    public void TrimPolyline_MiddleSegmentCut_SplitsIntoTwoPolylines()
    {
        var db = new CadDatabase();
        // Açık polyline: (0,0)-(100,0)-(200,0)-(300,0). Ortadaki segmenti (100,0)-(200,0) kesen
        // dikey bir çizgi x=150'de var.
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), new Vector3D(200, 0, 0), new Vector3D(300, 0, 0)
        }, isClosed: false);
        var cutter = new LineEntity(new Vector3D(150, -50, 0), new Vector3D(150, 50, 0));
        db.AddEntity(poly);
        db.AddEntity(cutter);

        var cmd = new TrimCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(120, 0, 0)); // orta segmentte, kesiciden önceki kısma tıkla

        var resultPolys = db.GetAllEntities().OfType<LwPolylineEntity>().ToList();
        Assert.Equal(2, resultPolys.Count);
    }

    [Fact]
    public void TrimPolyline_EndSegmentCut_StaysAsSinglePiece()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), new Vector3D(200, 0, 0)
        }, isClosed: false);
        var cutter = new LineEntity(new Vector3D(50, -50, 0), new Vector3D(50, 50, 0));
        db.AddEntity(poly);
        db.AddEntity(cutter);

        var cmd = new TrimCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(20, 0, 0)); // ilk segmentte, kesicinin solunda

        var resultPolys = db.GetAllEntities().OfType<LwPolylineEntity>().ToList();
        Assert.Single(resultPolys);
        // Tıklanan kısım (polyline başlangıcı ile kesici arasındaki [0,50] aralığı) silinir;
        // kalan tek parça kesim noktasından (50,0) başlayıp geri kalan köşelerden geçer.
        Assert.Equal(50, resultPolys[0].Vertices.First().X, precision: 1);
        Assert.Equal(200, resultPolys[0].Vertices.Last().X, precision: 1);
    }

    [Fact]
    public void TrimPolyline_ClosedRectangle_OpensIntoSingleOpenPolyline()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), new Vector3D(100, 100, 0), new Vector3D(0, 100, 0)
        }, isClosed: true);
        var cutter = new LineEntity(new Vector3D(50, -50, 0), new Vector3D(50, 50, 0)); // alt kenarı keser
        db.AddEntity(poly);
        db.AddEntity(cutter);

        var cmd = new TrimCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(20, 0, 0)); // alt kenarda, kesicinin solunda

        var resultPolys = db.GetAllEntities().OfType<LwPolylineEntity>().ToList();
        Assert.Single(resultPolys);
        Assert.False(resultPolys[0].IsClosed);
    }

    [Fact]
    public void ExtendPolyline_LastSegmentExtendsToBoundary()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0)
        }, isClosed: false);
        var boundary = new LineEntity(new Vector3D(200, -50, 0), new Vector3D(200, 50, 0));
        db.AddEntity(poly);
        db.AddEntity(boundary);

        var cmd = new ExtendCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(100, 0, 0)); // son uca yakın tıkla

        var result = db.GetAllEntities().OfType<LwPolylineEntity>().Single();
        Assert.Equal(200, result.Vertices.Last().X, precision: 1);
        Assert.Equal(0, result.Vertices.First().X, precision: 1); // ilk uç değişmemeli
    }

    [Fact]
    public void ExtendPolyline_ClosedPolyline_DoesNothing()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), new Vector3D(100, 100, 0), new Vector3D(0, 100, 0)
        }, isClosed: true);
        db.AddEntity(poly);

        var cmd = new ExtendCommand(db, db.TransactionManager, currentZoom: 1.0);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));

        // Kapalı polyline uzatılamaz — orijinal, değişmeden kalmalı.
        var result = db.GetAllEntities().OfType<LwPolylineEntity>().Single();
        Assert.Same(poly, result);
    }
}
