using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Sıhhi Tesisat Cihazı → B-Rep Katı Cisim Servisi (FixtureBRepService)
   NEDEN: Roadmap "3D Render Motoru Faz 2" — genelleştirilmiş B-Rep adaptörü kapsamında.
          `SanitaryFixtureEntity` sadece plan-görünüm boyutları (Width/Depth) taşıyor,
          YÜKSEKLİK verisi YOK (dürüstçe belirtilmeli — veri şemasında eksik). Gerçek
          üretici kataloğuna göre tip-bazlı yükseklik (WC ~400mm, Lavabo ~850mm, Duş ~150mm
          taban teknesi) eklemek ayrı bir oturum gerektirir; burada TÜM tipler için tek,
          genel bir "kutu" yer tutucu yüksekliği (`DefaultHeightMm`) kullanılıyor — 3D
          önizlemede cihazın KONUMUNU/AYAK İZİNİ doğru göstermek yeterli, gerçekçi katalog
          modeli değil.
*/
public class FixtureBRepService
{
    private const double DefaultHeightMm = 450.0;

    private readonly CadDatabase _database;

    public FixtureBRepService(CadDatabase database)
    {
        _database = database;
    }

    public List<Solid> GenerateAllSolids()
    {
        var result = new List<Solid>();
        foreach (var fixture in _database.GetAllEntities().OfType<SanitaryFixtureEntity>())
            result.Add(GenerateFixtureSolid(fixture));
        return result;
    }

    public Solid GenerateFixtureSolid(SanitaryFixtureEntity fixture)
    {
        double cos = Math.Cos(fixture.Rotation), sin = Math.Sin(fixture.Rotation);
        var uAxis = new Vector3D(cos, sin, 0);
        var vAxis = new Vector3D(-sin, cos, 0);
        var origin = fixture.Position - uAxis * (fixture.Width / 2.0) - vAxis * (fixture.Depth / 2.0);

        return BRepBuilder.ExtrudeBox(origin, uAxis, vAxis, Vector3D.ZAxis,
            fixture.Width, fixture.Depth, DefaultHeightMm, name: $"Fixture_{fixture.Id}");
    }
}
