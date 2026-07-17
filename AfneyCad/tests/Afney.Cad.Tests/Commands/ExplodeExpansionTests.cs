using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: Explode Genişletme Testleri
   NEDEN: EXPLODE önceden sadece BlockReference ve LwPolyline'ı destekliyordu — Hatch/
          Dimension/Spline patlatılamıyordu. Bu testler üçünün de gerçekten temel bileşenlere
          ayrıldığını ve orijinal nesnenin veritabanından kaldırıldığını doğruluyor.
*/
public class ExplodeExpansionTests
{
    [Fact]
    public void Explode_Hatch_ProducesBoundaryLinesAndRemovesOriginal()
    {
        var db = new CadDatabase();
        var hatch = new HatchEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), new Vector3D(100, 100, 0), new Vector3D(0, 100, 0)
        }, 0xFFFFFFFF);
        db.AddEntity(hatch);

        var cmd = new ExplodeCommand(db, db.TransactionManager, new[] { hatch });
        cmd.Start();

        Assert.Empty(db.GetAllEntities().OfType<HatchEntity>());
        var lines = db.GetAllEntities().OfType<LineEntity>().ToList();
        Assert.Equal(4, lines.Count); // kapalı 4 köşeli sınır → 4 kenar
    }

    [Fact]
    public void Explode_LinearDimension_ProducesLinesAndTextRemovesOriginal()
    {
        var db = new CadDatabase();
        var dim = new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), new Vector3D(0, 200, 0), DimensionType.Linear);
        db.AddEntity(dim);

        var cmd = new ExplodeCommand(db, db.TransactionManager, new[] { dim });
        cmd.Start();

        Assert.Empty(db.GetAllEntities().OfType<DimensionEntity>());
        Assert.Equal(3, db.GetAllEntities().OfType<LineEntity>().Count()); // ölçü çizgisi + 2 uzatma çizgisi
        Assert.Single(db.GetAllEntities().OfType<TextEntity>());
    }

    [Fact]
    public void Explode_Spline_ProducesTessellatedPolylineAndRemovesOriginal()
    {
        var db = new CadDatabase();
        var spline = new SplineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(50, 100, 0), new Vector3D(100, 0, 0), new Vector3D(150, 100, 0)
        }, degree: 3);
        db.AddEntity(spline);

        var cmd = new ExplodeCommand(db, db.TransactionManager, new[] { spline });
        cmd.Start();

        Assert.Empty(db.GetAllEntities().OfType<SplineEntity>());
        var poly = db.GetAllEntities().OfType<LwPolylineEntity>().Single();
        Assert.True(poly.Vertices.Count > 10); // tessellate edilmiş, çok sayıda ara nokta
    }
}
