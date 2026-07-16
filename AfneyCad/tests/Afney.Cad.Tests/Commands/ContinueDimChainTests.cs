using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: DIMCONTINUE Zincir Yönü Testleri
   NEDEN: ContinueDimCommand önceden yalnızca yatay (Y sabit) zincirleme destekliyordu —
          isHorizontal parametresi hiç yoktu, her zaman point.X ile Y'yi sabit tutuyordu.
          Bu, ilk ölçü DİKEY (IsHorizontal=false) olduğunda zincirin yanlış eksende
          (X yerine Y sabit tutularak) çizilmesine yol açardı. Artık başlangıç ölçüsünün
          yönü parametre olarak alınıyor ve zincir gerçekten o eksende devam ediyor.
*/
public class ContinueDimChainTests
{
    [Fact]
    public void HorizontalChain_KeepsYCoordinateConstant()
    {
        var db = new CadDatabase();
        var cmd = new ContinueDimCommand(db, db.TransactionManager, new Vector3D(0, 100, 0), 100, isHorizontal: true);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(500, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));

        var dims = db.GetAllEntities().OfType<DimensionEntity>().ToList();
        Assert.Equal(2, dims.Count);
        Assert.All(dims, d => Assert.Equal(100, d.DimLinePoint.Y, precision: 6));
    }

    [Fact]
    public void VerticalChain_KeepsXCoordinateConstant()
    {
        var db = new CadDatabase();
        var cmd = new ContinueDimCommand(db, db.TransactionManager, new Vector3D(200, 0, 0), 200, isHorizontal: false);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 500, 0));
        cmd.OnPointerPressed(new Vector3D(0, 1000, 0));

        var dims = db.GetAllEntities().OfType<DimensionEntity>().ToList();
        Assert.Equal(2, dims.Count);
        Assert.All(dims, d => Assert.Equal(200, d.DimLinePoint.X, precision: 6));
    }

    [Fact]
    public void Chain_ContinuesFromPreviousEndpoint()
    {
        var db = new CadDatabase();
        var cmd = new ContinueDimCommand(db, db.TransactionManager, new Vector3D(0, 0, 0), 0, isHorizontal: true);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(500, 0, 0));
        cmd.OnPointerPressed(new Vector3D(900, 0, 0));

        var dims = db.GetAllEntities().OfType<DimensionEntity>().OrderBy(d => d.SecondPoint.X).ToList();
        Assert.Equal(500, dims[0].SecondPoint.X, precision: 6);
        Assert.Equal(500, dims[1].FirstPoint.X, precision: 6); // ikinci ölçü, birincinin bitişinden başlıyor
        Assert.Equal(900, dims[1].SecondPoint.X, precision: 6);
    }
}
