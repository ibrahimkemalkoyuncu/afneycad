using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

public class NorthArrowService
{
    private const string Layer = "KUZEY";
    private const uint Color = 0xFFFFFFFF;

    public List<CadEntity> Generate(Vector3D center, double size = 5.0)
    {
        var entities = new List<CadEntity>();
        double h = size;
        double w = size * 0.4;

        var top    = new Vector3D(center.X, center.Y + h, 0);
        var left   = new Vector3D(center.X - w / 2, center.Y, 0);
        var right  = new Vector3D(center.X + w / 2, center.Y, 0);
        var bottom = new Vector3D(center.X, center.Y - h * 0.3, 0);

        entities.Add(new LineEntity(left, top) { Color = Color, Layer = Layer });
        entities.Add(new LineEntity(top, right) { Color = Color, Layer = Layer });
        entities.Add(new LineEntity(right, left) { Color = Color, Layer = Layer });

        entities.Add(new LineEntity(left, bottom) { Color = Color, Layer = Layer });
        entities.Add(new LineEntity(right, bottom) { Color = Color, Layer = Layer });

        entities.Add(new TextEntity("N", new Vector3D(center.X, top.Y + size * 0.15, 0), size * 0.4, 0)
            { Color = Color, Layer = Layer });

        return entities;
    }
}
