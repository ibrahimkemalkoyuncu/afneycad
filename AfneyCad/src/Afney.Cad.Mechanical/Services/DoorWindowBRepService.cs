using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Kapı/Pencere → B-Rep Katı Cisim Servisi (DoorWindowBRepService)
   NEDEN: Roadmap "3D Render Motoru Faz 2" — Wall/Duct/Pipe zaten B-Rep'e sahipti (WallBRepService/
          DuctBRepService/Pipe3DModelService), Door/Window/Fixture/Room hiç yoktu (sadece 2D Draw()).
          `WallBRepService` kapı/pencere boşluklarını duvardan OYUYOR (segment bölme) ama boşluğun
          İÇİNE hiçbir şey KOYMUYOR — bu servis o boşluğa basit bir kapı kanadı / cam paneli kutusu
          yerleştirir. Gerçekçi menteşe/kasa/cam çerçevesi kapsam dışı (düşük ROI, genel amaçlı 3D
          önizleme için basit hacim yeterli) — DoorEntity.Draw()'daki `Tr(x,y)` yerel eksen
          dönüşümüyle AYNI kural kullanılıyor (uAxis=genişlik yönü, vAxis=kalınlık yönü).

   VARSAYIM (dürüstçe belirtilmeli): DoorEntity/WindowEntity kalınlık (derinlik) taşımıyor —
   `DoorLeafThicknessMm`/`WindowPaneThicknessMm` sabitleri kullanılıyor (tipik kapı kanadı ~40mm,
   cam+çerçeve ~60mm). Gerçek üretici verisi eklenirse bu sabitler entity property'sine taşınabilir.
*/
public class DoorWindowBRepService
{
    private const double DoorLeafThicknessMm = 40.0;
    private const double WindowPaneThicknessMm = 60.0;

    private readonly CadDatabase _database;

    public DoorWindowBRepService(CadDatabase database)
    {
        _database = database;
    }

    public List<Solid> GenerateAllSolids()
    {
        var result = new List<Solid>();
        foreach (var door in _database.GetAllEntities().OfType<DoorEntity>())
            result.Add(GenerateDoorSolid(door));
        foreach (var window in _database.GetAllEntities().OfType<WindowEntity>())
            result.Add(GenerateWindowSolid(window));
        return result;
    }

    public Solid GenerateDoorSolid(DoorEntity door)
    {
        var (uAxis, vAxis) = LocalAxes(door.Rotation);
        var origin = door.Position - uAxis * (door.WidthMm / 2.0) - vAxis * (DoorLeafThicknessMm / 2.0);
        return BRepBuilder.ExtrudeBox(origin, uAxis, vAxis, Vector3D.ZAxis,
            door.WidthMm, DoorLeafThicknessMm, door.HeightMm, name: $"Door_{door.Id}");
    }

    public Solid GenerateWindowSolid(WindowEntity window)
    {
        var (uAxis, vAxis) = LocalAxes(window.Rotation);
        var origin = window.Position - uAxis * (window.WidthMm / 2.0) - vAxis * (WindowPaneThicknessMm / 2.0)
                     + Vector3D.ZAxis * window.SillHeightMm;
        return BRepBuilder.ExtrudeBox(origin, uAxis, vAxis, Vector3D.ZAxis,
            window.WidthMm, WindowPaneThicknessMm, window.HeightMm, name: $"Window_{window.Id}");
    }

    /// <summary>DoorEntity/WindowEntity.Draw()'daki Tr(x,y) yerel dönüşümüyle aynı kural: uAxis=genişlik, vAxis=kalınlık.</summary>
    private static (Vector3D UAxis, Vector3D VAxis) LocalAxes(double rotation)
    {
        double cos = Math.Cos(rotation), sin = Math.Sin(rotation);
        return (new Vector3D(cos, sin, 0), new Vector3D(-sin, cos, 0));
    }
}
