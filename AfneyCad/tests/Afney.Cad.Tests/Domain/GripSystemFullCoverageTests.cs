using System;
using System.Linq;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: Grip Sistemi Tam Kapsam Testleri (GripSystemFullCoverageTests)
   NEDEN: Grip sistemi entity tiplerinin küçük bir alt kümesindeydi — BlockReferenceEntity
          (en sık kullanılan tiplerden biri), SplineEntity, ValveEntity, SanitaryFixtureEntity,
          MahalEntity, RoomEntity, ReducerEntity, RainfallCatchmentEntity, PipeLabelEntity,
          DrainageOutletEntity ve TableEntity'nin HİÇBİRİNDE grip desteği yoktu. Bu testler
          her birinin artık gerçekten grip ile taşınabildiğini/düzenlenebildiğini doğruluyor.
*/
public class GripSystemFullCoverageTests
{
    [Fact]
    public void BlockReferenceEntity_PositionGrip_MovesBlock()
    {
        var blk = new BlockReferenceEntity("TestBlock", new Vector3D(0, 0, 0));
        blk.MoveGripPointAt(0, new Vector3D(500, 300, 0));
        Assert.Equal(new Vector3D(500, 300, 0), blk.Position);
    }

    [Fact]
    public void BlockReferenceEntity_RotationGrip_UpdatesRotation()
    {
        var blk = new BlockReferenceEntity("TestBlock", new Vector3D(0, 0, 0));
        blk.MoveGripPointAt(1, new Vector3D(0, 100, 0)); // doğrudan yukarı → 90°
        Assert.Equal(90.0, blk.Rotation, precision: 3);
    }

    [Fact]
    public void SplineEntity_ControlPointGrip_MovesThatControlPointOnly()
    {
        var spline = new SplineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 100, 0), new Vector3D(200, 0, 0), new Vector3D(300, 100, 0)
        });
        spline.MoveGripPointAt(1, new Vector3D(999, 999, 0));

        Assert.Equal(new Vector3D(999, 999, 0), spline.ControlPoints[1]);
        Assert.Equal(new Vector3D(0, 0, 0), spline.ControlPoints[0]); // diğerleri etkilenmemeli
    }

    [Fact]
    public void ValveEntity_Grip_MovesValve()
    {
        var valve = new ValveEntity(new Vector3D(0, 0, 0), ValveType.BallValve, 50);
        valve.MoveGripPointAt(0, new Vector3D(750, 0, 0));
        Assert.Equal(new Vector3D(750, 0, 0), valve.Position);
    }

    [Fact]
    public void SanitaryFixtureEntity_Grip_MovesFixture()
    {
        var fixture = SanitaryFixtureEntity.CreateWashbasin(new Vector3D(0, 0, 0));
        fixture.MoveGripPointAt(0, new Vector3D(1200, 400, 0));
        Assert.Equal(new Vector3D(1200, 400, 0), fixture.Position);
    }

    [Fact]
    public void MahalEntity_VertexGrip_MovesOnlyThatVertexAndRecalculatesArea()
    {
        var mahal = new MahalEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), new Vector3D(1000, 1000, 0), new Vector3D(0, 1000, 0)
        }, "Test Oda");
        double originalArea = mahal.Area;

        mahal.MoveGripPointAt(2, new Vector3D(2000, 2000, 0));

        Assert.Equal(new Vector3D(2000, 2000, 0), mahal.BoundaryPoints[2]);
        Assert.NotEqual(originalArea, mahal.Area);
    }

    [Fact]
    public void RoomEntity_VertexGrip_DelegatesToLwPolylineBoundary()
    {
        var boundary = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), new Vector3D(1000, 1000, 0), new Vector3D(0, 1000, 0)
        }, isClosed: true);
        var room = new RoomEntity(boundary);

        room.MoveGripPointAt(1, new Vector3D(1500, 500, 0));

        Assert.Equal(new Vector3D(1500, 500, 0), boundary.Vertices[1]);
    }

    [Fact]
    public void ReducerEntity_Grip_MovesReducer()
    {
        var reducer = new ReducerEntity(new Vector3D(0, 0, 0), 50, 32);
        reducer.MoveGripPointAt(0, new Vector3D(300, 0, 0));
        Assert.Equal(new Vector3D(300, 0, 0), reducer.Position);
    }

    [Fact]
    public void RainfallCatchmentEntity_VertexGrip_MovesVertexAndUpdatesArea()
    {
        var catchment = new RainfallCatchmentEntity();
        catchment.AddVertex(new Vector3D(0, 0, 0));
        catchment.AddVertex(new Vector3D(1000, 0, 0));
        catchment.AddVertex(new Vector3D(1000, 1000, 0));
        catchment.AddVertex(new Vector3D(0, 1000, 0));
        double originalArea = catchment.AreaM2;

        catchment.MoveGripPointAt(2, new Vector3D(3000, 3000, 0));

        Assert.Equal(new Vector3D(3000, 3000, 0), catchment.Vertices[2]);
        Assert.NotEqual(originalArea, catchment.AreaM2);
    }

    [Fact]
    public void DrainageOutletEntity_Grip_MovesOutlet()
    {
        var outlet = new DrainageOutletEntity(new Vector3D(0, 0, 0));
        outlet.MoveGripPointAt(0, new Vector3D(400, 400, 0));
        Assert.Equal(new Vector3D(400, 400, 0), outlet.Position);
    }

    [Fact]
    public void PipeLabelEntity_Grip_MovesLabelManually()
    {
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 50);
        var label = new PipeLabelEntity(pipe);
        label.MoveGripPointAt(0, new Vector3D(500, 300, 0));
        Assert.Equal(new Vector3D(500, 300, 0), label.Position);
    }

    [Fact]
    public void TableEntity_CornerGrip_ResizesColumnsAndRows()
    {
        var table = new TableEntity(new Vector3D(0, 0, 0), rows: 2, cols: 3);
        double originalColWidth = table.ColumnWidth;

        // Sağ-alt köşeyi çok daha büyük bir konuma sürükle → hücreler büyümeli.
        table.MoveGripPointAt(1, new Vector3D(9000, -6000, 0));

        Assert.True(table.ColumnWidth > originalColWidth);
        Assert.Equal(3000, table.ColumnWidth, precision: 3); // 9000 / 3 sütun
    }
}
