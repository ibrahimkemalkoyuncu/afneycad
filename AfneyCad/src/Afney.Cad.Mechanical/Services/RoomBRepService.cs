using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Mahal → B-Rep Zemin Döşemesi Servisi (RoomBRepService)
   NEDEN: Roadmap "3D Render Motoru Faz 2" — genelleştirilmiş B-Rep adaptörü kapsamında.
          `MahalEntity` sadece 2D sınır poligonu (`BoundaryPoints`) + Alan/Çevre taşıyor,
          KAT YÜKSEKLİĞİ verisi YOK (dürüstçe belirtilmeli). Tam hacimli bir oda kutusu
          uydurmak (rastgele bir kat yüksekliği varsayıp duvarları İKİNCİ KEZ çizmek —
          WallBRepService zaten duvarları üretiyor) yanıltıcı olurdu; bunun yerine yaygın bir
          BIM görselleştirme deseni izlenip mahal sınırı İNCE bir ZEMİN DÖŞEMESİ (renk-kodlu,
          `SlabThicknessMm` — gerçek bir yapısal döşeme değil, sadece 3D'de oda sınırlarını
          ayırt etmeye yarayan görsel bir plaka) olarak render ediliyor.
*/
public class RoomBRepService
{
    private const double SlabThicknessMm = 50.0;

    private readonly CadDatabase _database;

    public RoomBRepService(CadDatabase database)
    {
        _database = database;
    }

    public List<Solid> GenerateAllSolids()
    {
        var result = new List<Solid>();
        foreach (var mahal in _database.GetAllEntities().OfType<MahalEntity>())
        {
            var solid = GenerateRoomSlab(mahal);
            if (solid != null) result.Add(solid);
        }
        return result;
    }

    public Solid? GenerateRoomSlab(MahalEntity mahal)
    {
        if (mahal.BoundaryPoints.Count < 3) return null;
        try
        {
            return BRepBuilder.ExtrudePolygon(mahal.BoundaryPoints, new Vector3D(0, 0, SlabThicknessMm), name: $"Room_{mahal.Id}");
        }
        catch (ArgumentException)
        {
            // Dejenere/self-intersecting poligon — 3D önizlemede sessizce atla (2D tarafta zaten
            // ayrı bir doğrulama var, bkz. GeomUtils.HasSelfIntersection).
            return null;
        }
    }
}
