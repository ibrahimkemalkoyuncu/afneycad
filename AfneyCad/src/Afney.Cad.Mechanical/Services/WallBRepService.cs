using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Duvar → B-Rep Katı Cisim Servisi (WallBRepService)
   NEDEN: WallEntity'ler (StartPoint/EndPoint/ThicknessMm/HeightMm) bu oturuma kadar sadece 2D
          çiziliyordu (Draw() metodu — 4 çizgi, katı model yok). Bu servis, mevcut ölü
          Topology.Solid kernel'ini gerçek bir domain varlığına bağlayan ilk kullanım noktası:
          her duvarı, Euler açısından geçerli, gerçek dikdörtgen prizma bir B-Rep Solid'e
          dönüştürür (BRepBuilder.ExtrudeBox ile).

   GEOMETRİ:
   - uAxis = duvar ekseni yönü (StartPoint→EndPoint, birim)
   - vAxis = kalınlık yönü (uAxis'e dik, XY düzleminde — WallEntity.Draw()'daki `norm` ile aynı
     kural: (-dir.Y, dir.X, 0))
   - wAxis = dünya yukarı (0,0,1)
   - Origin = duvar ekseninin kalınlık yönünde -yarım kalınlık kaydırılmış başlangıç noktası
     (böylece kutu, duvarın gerçek eksenini ortalar — Draw()'daki p1/p2/p3/p4 dörtgenine denk).
*/
public class WallBRepService
{
    private readonly CadDatabase _database;

    public WallBRepService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Kapı/Pencere Boşluğu (WallOpening) — duvar ekseni üzerinde parametrik aralık.
       NEDEN: Gerçek boolean (CSG subtract) bu oturumun kapsamı dışında bırakıldı (BRepBuilder.cs
              başlığında dokümante edildi). Bunun yerine, çoğu gerçek CAD çekirdeğinin de kullandığı
              BOOLEAN GEREKTİRMEYEN bir teknik: duvarı boşluğun ETRAFINDA birden fazla ExtrudeBox
              PARÇASINA bölmek (yanlar + varsa lento + varsa denizlik altı). Her parça ayrı ayrı
              Euler-geçerli bir Solid'dir; toplam hacimleri analitik olarak
              (duvar_hacmi - boşluk_hacmi) ile TAM eşleşir — bu ölçülebilir/test edilebilir bir
              doğruluk kanıtıdır (bkz. WallBRepServiceTests.cs).
    */
    private readonly record struct WallOpening(double Start, double End, double SillHeight, double HeadHeight);

    public List<Solid> GenerateAllWallSolids()
    {
        var walls = _database.GetAllEntities().OfType<WallEntity>().ToList();
        var doors = _database.GetAllEntities().OfType<DoorEntity>().ToList();
        var windows = _database.GetAllEntities().OfType<WindowEntity>().ToList();

        var result = new List<Solid>();
        foreach (var wall in walls)
            result.AddRange(GenerateWallSolids(wall, doors, windows));
        return result;
    }

    /*
       NE: Tek bir duvarı, üzerindeki kapı/pencerelerin boşluklarını çıkararak B-Rep parçalarına ayırır.
       TOLERANS: Bir kapı/pencere "bu duvarın üzerinde" sayılır eğer Position, duvar eksenine
                 dik uzaklığı duvar kalınlığının yarısı + 50mm toleransı içindeyse (yerleştirme
                 sırasındaki küçük snap sapmalarını tolere etmek için).
    */
    public List<Solid> GenerateWallSolids(WallEntity wall, IEnumerable<DoorEntity>? doors = null, IEnumerable<WindowEntity>? windows = null)
    {
        var dir = wall.EndPoint - wall.StartPoint;
        double length = dir.Length();
        if (length < 1e-6) return new List<Solid>();

        var uAxis = dir / length;
        var vAxis = new Vector3D(-uAxis.Y, uAxis.X, 0);
        if (vAxis.LengthSquared() < 1e-9) vAxis = new Vector3D(1, 0, 0);
        var wAxis = Vector3D.ZAxis;

        double halfThickness = wall.ThicknessMm / 2.0;
        var baseOrigin = wall.StartPoint - vAxis * halfThickness;

        var openings = CollectOpenings(wall, uAxis, vAxis, length, halfThickness, doors, windows);

        Solid MakeSegment(double startT, double endT, double baseZ, double topZ, int index, string tag)
        {
            double segLen = endT - startT;
            var origin = baseOrigin + uAxis * startT + wAxis * baseZ;
            return BRepBuilder.ExtrudeBox(origin, uAxis, vAxis, wAxis, segLen, wall.ThicknessMm, topZ - baseZ, name: $"Wall_{wall.Id}_{tag}{index}");
        }

        if (openings.Count == 0)
            return new List<Solid> { MakeSegment(0, length, 0, wall.HeightMm, 0, "Full") };

        var solids = new List<Solid>();
        double cursor = 0;
        int i = 0;
        foreach (var op in openings)
        {
            if (op.Start > cursor + 1e-6)
                solids.Add(MakeSegment(cursor, op.Start, 0, wall.HeightMm, i, "Side")); // boşluğun solundaki tam-yükseklik dilim

            if (op.SillHeight > 1e-6)
                solids.Add(MakeSegment(op.Start, op.End, 0, op.SillHeight, i, "Sill")); // denizlik altı (pencere)

            if (op.HeadHeight < wall.HeightMm - 1e-6)
                solids.Add(MakeSegment(op.Start, op.End, op.HeadHeight, wall.HeightMm, i, "Lintel")); // lento üstü

            cursor = op.End;
            i++;
        }
        if (cursor < length - 1e-6)
            solids.Add(MakeSegment(cursor, length, 0, wall.HeightMm, i, "Side"));

        return solids;
    }

    /// <summary>Geriye uyumluluk: tek boşluksuz Solid döner (var olan çağıranlar için).</summary>
    public Solid? GenerateWallSolid(WallEntity wall) => GenerateWallSolids(wall).FirstOrDefault();

    private static List<WallOpening> CollectOpenings(WallEntity wall, Vector3D uAxis, Vector3D vAxis, double length, double halfThickness,
        IEnumerable<DoorEntity>? doors, IEnumerable<WindowEntity>? windows)
    {
        var raw = new List<WallOpening>();
        double tolerance = halfThickness + 50;

        void TryAdd(Vector3D position, double width, double sillHeight, double headHeight)
        {
            var rel = position - wall.StartPoint;
            double t = rel.Dot(uAxis);
            double perpDist = Math.Abs(rel.Dot(vAxis));
            if (perpDist > tolerance) return;

            double start = Math.Max(0, t - width / 2.0);
            double end = Math.Min(length, t + width / 2.0);
            if (end - start > 1e-6)
                raw.Add(new WallOpening(start, end, sillHeight, headHeight));
        }

        if (doors != null)
            foreach (var d in doors)
                TryAdd(d.Position, d.WidthMm, 0, d.HeightMm);

        if (windows != null)
            foreach (var w in windows)
                TryAdd(w.Position, w.WidthMm, w.SillHeightMm, w.SillHeightMm + w.HeightMm);

        raw.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Çakışan/bitişik boşlukları birleştir (ör. bir kapının hemen yanındaki pencere).
        var merged = new List<WallOpening>();
        foreach (var op in raw)
        {
            if (merged.Count > 0 && op.Start <= merged[^1].End + 1e-6)
            {
                var last = merged[^1];
                merged[^1] = new WallOpening(
                    last.Start, Math.Max(last.End, op.End),
                    Math.Min(last.SillHeight, op.SillHeight),
                    Math.Max(last.HeadHeight, op.HeadHeight));
            }
            else
            {
                merged.Add(op);
            }
        }

        return merged;
    }
}
