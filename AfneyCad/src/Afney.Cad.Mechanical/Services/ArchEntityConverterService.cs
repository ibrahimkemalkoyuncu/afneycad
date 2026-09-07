using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

public class ArchEntityConvertResult
{
    public int WallsCreated   { get; set; }
    public int ColumnsCreated { get; set; }
    public int DoorsCreated   { get; set; }
    public int WindowsCreated { get; set; }
    public int BeamsCreated   { get; set; }

    /*
       NE: Kavisli Duvar Sayacı (CurvedWallsApproximated)
       NEDEN — GERÇEK BOŞLUK (denetim raporunda "kavisli duvar belirsiz" olarak işaretlenmişti):
              Önceden duvar dönüşümü SADECE `entity is LineEntity` kontrolü yapıyordu — bir
              duvar katmanındaki ArcEntity (DWG'de yaygın: yuvarlak cephe/köşe duvarları) hiçbir
              koşulla eşleşmediği için sessizce atlanıyordu (ne duvar oluşuyordu ne uyarı
              veriliyordu). Artık ArcEntity'ler N düz WallEntity kirişine (chord'a) yaklaşıklanıp
              gerçek duvar olarak ekleniyor — bu sayaç, kaç KAYNAK YAY'ın kaç segmente
              bölündüğünü (yaklaşıklandığını) kullanıcıya bildirmek için tutuluyor.
    */
    public int CurvedWallsApproximated { get; set; }

    public int Total => WallsCreated + ColumnsCreated + DoorsCreated + WindowsCreated + BeamsCreated;
}

public class ArchEntityConverterService
{
    private readonly CadDatabase _database;

    public ArchEntityConverterService(CadDatabase database)
    {
        _database = database;
    }

    public ArchEntityConvertResult ConvertFromLayers()
    {
        var result = new ArchEntityConvertResult();
        var entities = _database.GetAllEntities().ToList();

        foreach (var entity in entities)
        {
            if (entity is WallEntity || entity is ColumnEntity || entity is DoorEntity ||
                entity is WindowEntity || entity is BeamEntity)
                continue;

            var layer = (entity.Layer ?? "0").ToUpperInvariant();

            if (IsWallLayer(layer) && entity is LineEntity wallLine)
            {
                double thickness = EstimateWallThickness(wallLine, entities);
                var wall = new WallEntity(wallLine.StartPoint, wallLine.EndPoint, thickness)
                {
                    Color = 0xFFAAAAAA,
                    Material = layer.Contains("BETON") || layer.Contains("CONC")
                        ? WallMaterial.Concrete
                        : layer.Contains("GAZBETON") ? WallMaterial.AeratedConcrete
                        : WallMaterial.Brick
                };
                _database.AddEntity(wall);
                result.WallsCreated++;
            }
            else if (IsWallLayer(layer) && entity is ArcEntity wallArc)
            {
                // NE/NEDEN: bkz. ArchEntityConvertResult.CurvedWallsApproximated başlığı — kavisli
                // duvar kalınlığı, düz duvarlardaki gibi komşu paralel çizgi analiziyle DEĞİL
                // (bir yayın "paraleli" tanımsız), sabit varsayılan (200mm) ile atanıyor. Bu
                // bilinçli bir basitleştirme — tam çözüm (yay kalınlığını komşu iç/dış yay
                // çiftinden çıkarmak) ayrı bir iş.
                var material = layer.Contains("BETON") || layer.Contains("CONC")
                    ? WallMaterial.Concrete
                    : layer.Contains("GAZBETON") ? WallMaterial.AeratedConcrete
                    : WallMaterial.Brick;

                int segmentsCreated = 0;
                foreach (var (segStart, segEnd) in TessellateArcToChords(wallArc))
                {
                    var wall = new WallEntity(segStart, segEnd, 200)
                    {
                        Color = 0xFFAAAAAA,
                        Material = material
                    };
                    _database.AddEntity(wall);
                    result.WallsCreated++;
                    segmentsCreated++;
                }

                if (segmentsCreated > 0)
                    result.CurvedWallsApproximated++;
            }
            else if (IsColumnLayer(layer))
            {
                var bb = entity.GetBoundingBox();
                double w = bb.Max.X - bb.Min.X;
                double d = bb.Max.Y - bb.Min.Y;
                if (w < 10 || d < 10 || w > 2000 || d > 2000) continue;

                var center = bb.Center;
                bool isCircular = Math.Abs(w - d) < w * 0.2;

                var col = new ColumnEntity(center, w, d)
                {
                    Shape = isCircular ? ColumnShape.Circular : ColumnShape.Rectangular,
                    DiameterMm = isCircular ? Math.Max(w, d) : 0
                };
                _database.AddEntity(col);
                result.ColumnsCreated++;
            }
            else if (IsDoorLayer(layer))
            {
                var bb = entity.GetBoundingBox();
                double w = bb.Max.X - bb.Min.X;
                double h = bb.Max.Y - bb.Min.Y;
                double doorWidth = Math.Max(w, h);
                if (doorWidth < 300 || doorWidth > 3000) continue;

                var door = new DoorEntity(bb.Center, doorWidth, 2100)
                {
                    Type = doorWidth > 1400 ? DoorType.Double : DoorType.Single
                };
                _database.AddEntity(door);
                result.DoorsCreated++;
            }
            else if (IsWindowLayer(layer))
            {
                var bb = entity.GetBoundingBox();
                double w = bb.Max.X - bb.Min.X;
                double h = bb.Max.Y - bb.Min.Y;
                double winWidth = Math.Max(w, h);
                if (winWidth < 200 || winWidth > 5000) continue;

                var win = new WindowEntity(bb.Center, winWidth, 1500);
                _database.AddEntity(win);
                result.WindowsCreated++;
            }
            else if (IsBeamLayer(layer) && entity is LineEntity beamLine)
            {
                double len = beamLine.GetLength();
                if (len < 500 || len > 15000) continue;

                var beam = new BeamEntity(beamLine.StartPoint, beamLine.EndPoint, 250, 500);
                _database.AddEntity(beam);
                result.BeamsCreated++;
            }
        }

        return result;
    }

    private static bool IsWallLayer(string layer) =>
        layer.Contains("WALL") || layer.Contains("DUVAR") || layer.Contains("DUVAR1") ||
        layer.Contains("KABA") || layer.Contains("SIVA");

    private static bool IsColumnLayer(string layer) =>
        layer.Contains("KOLON") || layer.Contains("COLUMN") || layer.Contains("COL") ||
        layer.Contains("PILLAR") || layer.Contains("STUN");

    private static bool IsDoorLayer(string layer) =>
        layer.Contains("KAPI") || layer.Contains("DOOR") || layer.Contains("KAPILAR");

    private static bool IsWindowLayer(string layer) =>
        layer.Contains("PENCERE") || layer.Contains("WINDOW") || layer.Contains("WIN") ||
        layer.Contains("CAM") || layer.Contains("PENC");

    private static bool IsBeamLayer(string layer) =>
        layer.Contains("KIRIS") || layer.Contains("BEAM") || layer.Contains("HATIL");

    /*
       NE: Yayı Düz Kirişlere (Chord) Böl (TessellateArcToChords)
       NEDEN: WallEntity sadece düz (Start/End) bir doğru temsil eder — kod tabanında kavisli
              duvar için ayrı bir varlık tipi yok (bkz. denetim raporu). Bir yayı N düz segmente
              yaklaşıklamak, hiç duvar oluşturmamaktan (önceki davranış) her zaman daha doğru.
              Segment sayısı 10°'lik adımlarla ölçeklenir (4-32 arası clamp) — tipik bir mimari
              cephe yayı (örn. 90° dönüş) için 9 segment üretir, tam çember için 32'de kalır.
    */
    private static IEnumerable<(Vector3D start, Vector3D end)> TessellateArcToChords(ArcEntity arc)
    {
        double sweep = arc.EndAngle > arc.StartAngle
            ? arc.EndAngle - arc.StartAngle
            : (2 * Math.PI - arc.StartAngle) + arc.EndAngle;

        if (sweep < 1e-6) yield break;

        int segments = Math.Clamp((int)Math.Ceiling(sweep / (10.0 * Math.PI / 180.0)), 4, 32);
        double step = sweep / segments;

        Vector3D PointAt(double angle) => new(
            arc.Center.X + Math.Cos(angle) * arc.Radius,
            arc.Center.Y + Math.Sin(angle) * arc.Radius,
            arc.Center.Z);

        Vector3D prev = PointAt(arc.StartAngle);
        for (int i = 1; i <= segments; i++)
        {
            Vector3D curr = PointAt(arc.StartAngle + i * step);
            yield return (prev, curr);
            prev = curr;
        }
    }

    private double EstimateWallThickness(LineEntity wall, List<CadEntity> allEntities)
    {
        var mid = new Vector3D((wall.StartPoint.X + wall.EndPoint.X) / 2,
                                (wall.StartPoint.Y + wall.EndPoint.Y) / 2, 0);
        var dir = wall.EndPoint - wall.StartPoint;
        double len = dir.Length();
        if (len < 1e-9) return 200;
        var perp = new Vector3D(-dir.Y / len, dir.X / len, 0);

        double minDist = 500;
        foreach (var ent in allEntities)
        {
            if (ent.Id == wall.Id) continue;
            if (ent is not LineEntity other) continue;
            if ((ent.Layer ?? "").ToUpperInvariant() != (wall.Layer ?? "").ToUpperInvariant()) continue;

            var otherMid = new Vector3D((other.StartPoint.X + other.EndPoint.X) / 2,
                                        (other.StartPoint.Y + other.EndPoint.Y) / 2, 0);
            double dist = Math.Abs((otherMid.X - mid.X) * perp.X + (otherMid.Y - mid.Y) * perp.Y);
            if (dist > 50 && dist < minDist) minDist = dist;
        }

        return minDist < 500 ? minDist : 200;
    }
}
