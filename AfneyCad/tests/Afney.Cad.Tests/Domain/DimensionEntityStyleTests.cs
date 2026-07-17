using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: Ok Başı Stili ve Dinamik Girdi Testleri
   NEDEN: DimensionEntity.DrawArrow önceden tek bir sabit dolu üçgen çiziyordu (Open/Dot/
          Architectural seçenekleri yoktu) ve ölçü metni her zaman otomatik hesaplanıyordu
          (kullanıcı klavyeden değer override edemiyordu — "dinamik girdi" özelliği yoktu).
          Bu testler her iki eksikliğin gerçekten giderildiğini doğruluyor.
*/
public class DimensionEntityStyleTests
{
    private static DimensionEntity MakeLinearDim(double length = 1000)
    {
        return new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(length, 0, 0), new Vector3D(0, 200, 0), DimensionType.Linear);
    }

    [Fact]
    public void Draw_FilledArrowStyle_ProducesFilledPolygon()
    {
        var dim = MakeLinearDim();
        dim.ArrowStyle = DimensionArrowStyle.Filled;
        var ctx = new FakeRenderContext();

        dim.Draw(ctx);

        Assert.True(ctx.FilledPolygons.Count >= 2); // iki ok ucu (her iki taraf)
    }

    [Fact]
    public void Draw_OpenArrowStyle_ProducesNoFilledPolygon()
    {
        var dim = MakeLinearDim();
        dim.ArrowStyle = DimensionArrowStyle.Open;
        var ctx = new FakeRenderContext();

        dim.Draw(ctx);

        Assert.Empty(ctx.FilledPolygons); // açık ok sadece çizgi, dolgu yok
    }

    [Fact]
    public void Draw_DotArrowStyle_ProducesFilledPolygonForDots()
    {
        var dim = MakeLinearDim();
        dim.ArrowStyle = DimensionArrowStyle.Dot;
        var ctx = new FakeRenderContext();

        dim.Draw(ctx);

        Assert.True(ctx.FilledPolygons.Count >= 2);
        // Dot poligonları 10 kenarlı — üçgenden (Filled, 3 köşe) ayırt edilebilir.
        Assert.All(ctx.FilledPolygons, p => Assert.Equal(10, p.vertices.Count));
    }

    [Fact]
    public void Draw_ArchitecturalTickStyle_ProducesObliqueLineNoPolygon()
    {
        var dim = MakeLinearDim();
        dim.ArrowStyle = DimensionArrowStyle.Architectural;
        var ctx = new FakeRenderContext();

        dim.Draw(ctx);

        Assert.Empty(ctx.FilledPolygons);
    }

    [Fact]
    public void GetText_WithoutOverride_ShowsComputedMeasurement()
    {
        var dim = MakeLinearDim(500); // <1000mm → "mm" formatında kalır (1000+ otomatik "m"ye geçer)
        var ctx = new FakeRenderContext();

        dim.Draw(ctx);

        Assert.Contains(ctx.Texts, t => t.text.Contains("500"));
    }

    [Fact]
    public void GetText_WithOverride_ShowsOverrideInsteadOfComputedValue()
    {
        var dim = MakeLinearDim(1000);
        dim.OverrideText = "1200 (nominal)";
        var ctx = new FakeRenderContext();

        dim.Draw(ctx);

        Assert.Contains(ctx.Texts, t => t.text == "1200 (nominal)");
        Assert.DoesNotContain(ctx.Texts, t => t.text.Contains("1000"));
    }
}
