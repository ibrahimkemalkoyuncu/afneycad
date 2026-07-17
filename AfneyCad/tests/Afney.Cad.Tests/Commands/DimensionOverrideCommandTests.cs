using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: Dinamik Girdi (Ölçü Değeri Override) Komut Testleri
   NEDEN: Dim* komutları önceden sadece fare tıklamasıyla nokta seçimini destekliyordu —
          klavyeden ölçü değeri girme (AutoCAD'in dinamik girdisi) hiç yoktu. Bu testler,
          IDimensionOverridable.SetTextOverride çağrıldığında yerleştirilen ölçünün gerçekten
          override metnini taşıdığını doğruluyor.
*/
public class DimensionOverrideCommandTests
{
    [Fact]
    public void LinearDimCommand_SetTextOverride_AppliesToPlacedDimension()
    {
        var db = new CadDatabase();
        var cmd = new LinearDimCommand(db, db.TransactionManager);
        Assert.IsAssignableFrom<IDimensionOverridable>(cmd);

        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));
        ((IDimensionOverridable)cmd).SetTextOverride("1200 (nominal)");
        cmd.OnPointerPressed(new Vector3D(500, 300, 0));

        var dim = db.GetAllEntities().OfType<DimensionEntity>().Single();
        Assert.Equal("1200 (nominal)", dim.OverrideText);
    }

    [Fact]
    public void RadiusDimCommand_SetTextOverride_AppliesToPlacedDimension()
    {
        var db = new CadDatabase();
        var cmd = new RadiusDimCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        ((IDimensionOverridable)cmd).SetTextOverride("R500");
        cmd.OnPointerPressed(new Vector3D(300, 0, 0));

        var dim = db.GetAllEntities().OfType<DimensionEntity>().Single();
        Assert.Equal("R500", dim.OverrideText);
    }

    [Fact]
    public void ContinueDimCommand_OverrideText_ConsumedByNextSegmentOnly()
    {
        var db = new CadDatabase();
        var cmd = new ContinueDimCommand(db, db.TransactionManager, new Vector3D(0, 0, 0), 0, isHorizontal: true);
        cmd.Start();
        ((IDimensionOverridable)cmd).SetTextOverride("999");
        cmd.OnPointerPressed(new Vector3D(500, 0, 0));  // bu segment override'ı tüketir
        cmd.OnPointerPressed(new Vector3D(900, 0, 0));  // bu segment normal hesaplanır

        var dims = db.GetAllEntities().OfType<DimensionEntity>()
            .OrderBy(d => d.SecondPoint.X).ToList();

        Assert.Equal("999", dims[0].OverrideText);
        Assert.Null(dims[1].OverrideText);
    }

    [Fact]
    public void LinearDimCommand_NoOverride_OverrideTextIsNull()
    {
        var db = new CadDatabase();
        var cmd = new LinearDimCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));
        cmd.OnPointerPressed(new Vector3D(500, 300, 0));

        var dim = db.GetAllEntities().OfType<DimensionEntity>().Single();
        Assert.Null(dim.OverrideText);
    }
}
