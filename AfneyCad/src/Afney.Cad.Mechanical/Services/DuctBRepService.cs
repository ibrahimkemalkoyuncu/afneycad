using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Kanal → B-Rep Katı Cisim Servisi (DuctBRepService)
   NEDEN: DuctEntity'ler (dikdörtgen veya dairesel kesit) 3D görünümde hiç temsil edilmiyordu —
          Pipe3DModelService sadece Pipe/Elbow/Tee/Reducer'ı kapsıyordu (grep ile doğrulandı,
          DuctEntity hiç geçmiyordu). Bu servis, WallBRepService ile aynı desende, BRepBuilder
          üzerinden gerçek bir B-Rep Solid üretir: dikdörtgen kanal → oriented box (ExtrudeBox),
          dairesel kanal → N-gon profil ekstrüzyonu (ExtrudePolygon).
*/
public class DuctBRepService
{
    private readonly CadDatabase _database;

    public DuctBRepService(CadDatabase database)
    {
        _database = database;
    }

    public List<Solid> GenerateAllDuctSolids()
    {
        return _database.GetAllEntities()
            .OfType<DuctEntity>()
            .Select(d => GenerateDuctSolid(d))
            .Where(s => s != null)
            .Select(s => s!)
            .ToList();
    }

    public Solid? GenerateDuctSolid(DuctEntity duct, int circularSegments = 16)
    {
        var direction = duct.EndPoint - duct.StartPoint;
        double length = direction.Length();
        if (length < 1e-6) return null;

        var axisW = direction / length;
        var (localX, localY) = ComputeLocalAxes(axisW);

        if (duct.Shape == DuctShape.Rectangular)
        {
            double halfW = duct.WidthMm / 2.0;
            double halfH = duct.HeightMm / 2.0;
            var origin = duct.StartPoint - localX * halfW - localY * halfH;

            return BRepBuilder.ExtrudeBox(origin, localX, localY, axisW,
                duct.WidthMm, duct.HeightMm, length, name: $"Duct_{duct.Id}");
        }

        // Dairesel: N-gon profil ile yaklaşıklama (Pipe3DModelService'in silindir üretimiyle
        // aynı segment mantığı — 16 dilim varsayılan LOD200 kalitesine denk).
        double radius = duct.DiameterMm / 2.0;
        var profile = new List<Vector3D>(circularSegments);
        for (int i = 0; i < circularSegments; i++)
        {
            double angle = 2.0 * Math.PI * i / circularSegments;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            profile.Add(duct.StartPoint + localX * (radius * cos) + localY * (radius * sin));
        }

        return BRepBuilder.ExtrudePolygon(profile, direction, name: $"Duct_{duct.Id}");
    }

    private static (Vector3D localX, Vector3D localY) ComputeLocalAxes(Vector3D zAxis)
    {
        var up = Math.Abs(zAxis.Z) < 0.99 ? Vector3D.ZAxis : Vector3D.XAxis;
        var localX = up.Cross(zAxis).Normalize();
        var localY = zAxis.Cross(localX).Normalize();
        return (localX, localY);
    }
}
