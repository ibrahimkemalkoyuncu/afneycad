using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: CadEntity.NotifyGeometryChanged Testleri
   NEDEN: PropertiesPanel artık Circle/Line/Arc/Text gibi temel çizim varlıklarının
          geometrisini (Center/Radius/StartPoint/vb.) doğrudan dışarıdan set edebiliyor.
          InvalidateCache() korumalı olduğu için dış çağıranlar için NotifyGeometryChanged()
          adında genel bir kapı eklendi — bu, eski (bayat) sınır kutusu önbelleğinin
          gerçekten temizlendiğini doğruluyor. Aksi halde Properties panelinden çap
          büyütülen bir dairenin seçim/hit-testing kutusu eski (küçük) boyutta kalırdı.
*/
public class CadEntityGeometryChangeTests
{
    [Fact]
    public void NotifyGeometryChanged_AfterDirectRadiusMutation_BoundingBoxReflectsNewSize()
    {
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 5);
        var originalBox = circle.GetBoundingBox();

        circle.Radius = 50;
        circle.NotifyGeometryChanged();

        var updatedBox = circle.GetBoundingBox();

        Assert.NotEqual(originalBox.Max.X, updatedBox.Max.X);
        Assert.Equal(50, updatedBox.Max.X, precision: 6);
    }

    [Fact]
    public void WithoutNotifyGeometryChanged_BoundingBoxStaysStale()
    {
        var circle = new CircleEntity(new Vector3D(0, 0, 0), 5);
        _ = circle.GetBoundingBox(); // önbelleği doldur

        circle.Radius = 50; // NotifyGeometryChanged çağrılmadı

        var staleBox = circle.GetBoundingBox();
        Assert.Equal(5, staleBox.Max.X, precision: 6); // hâlâ eski değer — cache temizlenmedi
    }
}
