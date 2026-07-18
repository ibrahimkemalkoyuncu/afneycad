using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Domain;

/*
   NE: LwPolylineEntity Grip Sürükleme Hata Düzeltmesi Testi
   NEDEN: GetGripPoints köşeleri gösteriyordu ama MoveGripPointAt override'ı HİÇ YOKTU —
          grip sürüklendiğinde taban sınıfın boş varsayılanı çalışıyordu, yani polyline
          grip'leri görünüyor ama sürüklemek hiçbir şey yapmıyordu. Bu, CircleEntity'de
          bulunanla aynı sınıf bir hatanın çok daha yaygın kullanılan bir sınıftaki (Mahal/
          Room sınırları, OFFSET/TRIM sonuçları) versiyonuydu.
*/
public class LwPolylineGripFixTests
{
    [Fact]
    public void MoveGripPointAt_UpdatesCorrectVertexOnly()
    {
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), new Vector3D(1000, 1000, 0)
        });

        poly.MoveGripPointAt(1, new Vector3D(5000, 5000, 0));

        Assert.Equal(new Vector3D(0, 0, 0), poly.Vertices[0]);
        Assert.Equal(new Vector3D(5000, 5000, 0), poly.Vertices[1]);
        Assert.Equal(new Vector3D(1000, 1000, 0), poly.Vertices[2]);
    }

    [Fact]
    public void MoveGripPointAt_InvalidatesBoundingBoxCache()
    {
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0)
        });
        var originalBox = poly.GetBoundingBox();

        poly.MoveGripPointAt(1, new Vector3D(9000, 9000, 0));
        var updatedBox = poly.GetBoundingBox();

        Assert.NotEqual(originalBox.Max.X, updatedBox.Max.X);
        Assert.Equal(9000, updatedBox.Max.X, precision: 6);
    }
}
