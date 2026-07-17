using System;
using System.Linq;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: Grip Sistemi Genişletme Testleri
   NEDEN: Grip sistemi entity tiplerinin küçük bir alt kümesinde çalışıyordu. Bu testler
          bu oturumda giderilen üç gerçek eksikliği doğruluyor:
          1. CircleEntity: GetGripPoints vardı ama MoveGripPointAt HİÇ yoktu — grip'ler
             görünüyor ama sürükleme hiçbir şey yapmıyordu (taban sınıfın boş varsayılanı
             çalışıyordu). Artık merkez taşıma ve kuadrant sürükleyerek yarıçap değiştirme
             gerçekten çalışıyor.
          2. ArcEntity: Hiç grip desteği yoktu (GetGripPoints/MoveGripPointAt). Artık
             merkez/başlangıç açısı/bitiş açısı/orta-nokta grip'leri var.
          3. TextEntity: Hiç grip desteği yoktu. Artık yerleşim noktası + yükseklik
             tutamacı var.
*/
public class GripSystemExpansionTests
{
    [Fact]
    public void CircleEntity_MoveCenterGrip_MovesWholeCircle()
    {
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 10);
        circle.MoveGripPointAt(0, new Vector3D(50, 50, 0));

        Assert.Equal(50, circle.Center.X, precision: 6);
        Assert.Equal(50, circle.Center.Y, precision: 6);
        Assert.Equal(10, circle.Radius, precision: 6); // yarıçap değişmemeli
    }

    [Fact]
    public void CircleEntity_MoveQuadrantGrip_ChangesRadius()
    {
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 10);
        circle.MoveGripPointAt(1, new Vector3D(30, 0, 0)); // sağ kuadrant, merkezden 30 birim

        Assert.Equal(30, circle.Radius, precision: 6);
        Assert.Equal(0, circle.Center.X, precision: 6); // merkez sabit kalmalı
    }

    [Fact]
    public void ArcEntity_HasFourGripPoints_CenterStartEndMid()
    {
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 10, 0, Math.PI / 2);
        var grips = arc.GetGripPoints().ToList();

        Assert.Equal(4, grips.Count);
        Assert.Equal(new Vector3D(0, 0, 0), grips[0]); // merkez
    }

    [Fact]
    public void ArcEntity_MoveCenterGrip_MovesWholeArc()
    {
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 10, 0, Math.PI / 2);
        arc.MoveGripPointAt(0, new Vector3D(100, 0, 0));

        Assert.Equal(100, arc.Center.X, precision: 6);
        Assert.Equal(10, arc.Radius, precision: 6);
    }

    [Fact]
    public void ArcEntity_MoveStartAngleGrip_ChangesStartAngleKeepsRadius()
    {
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 10, 0, Math.PI / 2);
        // Yeni başlangıç noktasını 90°'ye taşı (merkeze göre (0,10))
        arc.MoveGripPointAt(1, new Vector3D(0, 10, 0));

        Assert.Equal(Math.PI / 2, arc.StartAngle, precision: 5);
        Assert.Equal(10, arc.Radius, precision: 6); // yarıçap sabit kalmalı
    }

    [Fact]
    public void ArcEntity_MoveMidpointGrip_MovesWholeArc()
    {
        var arc = new ArcEntity(new Vector3D(0, 0, 0), 10, 0, Math.PI / 2);
        var oldMid = arc.GetGripPoints().Last();

        arc.MoveGripPointAt(3, new Vector3D(oldMid.X + 20, oldMid.Y + 5, 0));

        Assert.Equal(20, arc.Center.X, precision: 6);
        Assert.Equal(5, arc.Center.Y, precision: 6);
    }

    [Fact]
    public void TextEntity_HasTwoGripPoints_PositionAndHeightHandle()
    {
        var text = new TextEntity("Test", new Vector3D(0, 0, 0), 250);
        var grips = text.GetGripPoints().ToList();

        Assert.Equal(2, grips.Count);
        Assert.Equal(new Vector3D(0, 0, 0), grips[0]);
        Assert.Equal(new Vector3D(0, 250, 0), grips[1]);
    }

    [Fact]
    public void TextEntity_MoveHeightGrip_ChangesHeight()
    {
        var text = new TextEntity("Test", new Vector3D(0, 0, 0), 250);
        text.MoveGripPointAt(1, new Vector3D(0, 500, 0));

        Assert.Equal(500, text.Height, precision: 6);
        Assert.Equal(new Vector3D(0, 0, 0), text.Position); // konum değişmemeli
    }

    [Fact]
    public void TextEntity_MovePositionGrip_MovesText()
    {
        var text = new TextEntity("Test", new Vector3D(0, 0, 0), 250);
        text.MoveGripPointAt(0, new Vector3D(100, 200, 0));

        Assert.Equal(new Vector3D(100, 200, 0), text.Position);
    }
}
