using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;

namespace Afney.Cad.Domain.Entities.Basic;

/*
   NE: 3D Katı Cisim Varlığı (SolidEntity)
   NEDEN: Denetim raporu bulgusu — `Afney.Cad.Geometry.Topology.Solid` (B-Rep kernel,
          CSG Boolean UNION/SUBTRACT/INTERSECT dahil, 506 testle doğrulanmış) çizim
          veritabanında (CadDatabase) HİÇBİR temsile sahip değildi: sadece render
          amacıyla anlık (WallBRepService/DuctBRepService vb. tarafından her seferinde
          yeniden üretilen, KALICI OLMAYAN) bir ara veri yapısıydı. Bu sınıf, bir Solid'i
          gerçek, seçilebilir, taşınabilir, Undo/Redo'lu bir CadEntity'ye dönüştüren İLK
          köprüdür — CSG Boolean komutlarının (bkz. Afney.Cad.Commands.BasicCommands.
          SolidUnionCommand/SolidSubtractCommand/SolidIntersectCommand) çalışabilmesi
          için önkoşuldur.

   KAPSAM (v1 — bilinçli sınırlamalar, bkz. rapor):
   - Draw(): Solid'in kenarlarını (wireframe, üstten/plan görünüm) 2D Skia viewport'unda
     çizer. Gerçek gölgelendirilmiş/dolu 3D render Direct3DViewportControl.RebuildMeshesFromDatabase
     üzerinden yapılır — SolidEntity, BRepTessellator.Tessellate(solid) ile diğer tüm entity
     tipleriyle (Wall/Duct/Fixture/Room) AYNI önbelleklenmiş mesh pipeline'ına bağlıdır
     (bkz. Session #75 denetimi — tek satırlık foreach ile eklendi).
   - GetGripPoints()/MoveGripPointAt() — GERÇEK BOŞLUK (bu turda kapatıldı, "4M FineSANI
     karşılaştırma raporu"nun kendi bulgusu üzerine): Önceden hiç override edilmiyordu
     ("tekil vertex'leri sürükleyerek serbestçe deforme etmek Euler/manifold geçerliliğini
     bozabilir" gerekçesiyle — bu risk genel vertex-sürükleme için hâlâ geçerli). Ama şu an
     veritabanındaki TÜM SolidEntity'ler (BOX komutu + IFC duvar/döşeme/pencere/kapı importu)
     sadece eksene-hizalı veya döndürülmüş basit kutu/prizmalar — bunlar için DAR ve GÜVENLİ
     bir alt-küme mümkün: vertex'leri TEK TEK sürüklemek yerine, sadece GERÇEKTEN eksene-hizalı
     (rotasyonsuz) bir kutuysa 6 yüz-merkezi grip'i sunuluyor ve sürükleme SADECE ilgili
     eksende ölçekleme (Transform ile — zaten kanıtlanmış güvenli yol, Move/Transform'la
     birebir aynı mekanizma) uyguluyor. Topoloji HİÇ değişmiyor (vertex sayısı/bağlantısı
     aynı kalıyor, sadece pozisyonlar ölçekleniyor) — bu yüzden Solid.IsValid() ihlali
     riski YOK. Döndürülmüş kutular (ör. döndürülmüş IFC duvarları) veya CSG boolean
     sonucu/karmaşık profilli Solid'ler (IFCARBITRARYCLOSEDPROFILEDEF vb.) bu tespitten
     GEÇMEZ — onlar için grip listesi boş kalır (önceki davranışla aynı, güvenli geri düşüş).
   - DXF/IFC export/import'a BAĞLANDI (Session #75 denetiminde doğrulandı): DxfWriterService
     her SolidEntity'yi ayrı 3DFACE'ler olarak yazar, DxfImportService (Layer,Color) paylaşan
     3DFACE'leri BRepBuilder.FromTriangleSoup ile tek Solid'e kaynaştırır; IfcExportService/
     IfcImportService IFCCARTESIANPOINTLIST3D+IFCPOLYGONALFACESET ile tam 1:1 round-trip yapar.
*/
public class SolidEntity : CadEntity
{
    public Solid Solid { get; private set; }

    public SolidEntity(Solid solid)
    {
        Solid = solid;
    }

    /*
       NE: Çiz (Draw)
       NEDEN: Solid'in tüm kenarlarını (winged-edge topolojisinden) tek bir toplu emirle
              (DrawLines) render motoruna göndererek 2D viewport'ta bir tel-kafes (wireframe)
              izdüşümü göstermek için — Z bileşeni korunur (ör. izometrik/3D moda geçildiğinde
              render motoru bunu kullanabilir), 2D üstten görünümde sadece X/Y etkilidir.
    */
    public override void Draw(IRenderContext context)
    {
        var segments = new List<(Vector3D start, Vector3D end)>();
        foreach (var edge in Solid.GetEdges())
            segments.Add((edge.StartVertex.Position, edge.EndVertex.Position));

        context.DrawLines(segments, Color, GetRenderWeight(), Linetype, IsDashed);
    }

    /*
       NE: Sınır Kutusu Hesapla (CalculateBoundingBox)
       NEDEN: Solid kernel'inin kendi (Vertex tabanlı) bounding box hesaplamasını
              CadEntity'nin beklediği CadBoundingBox'a aktarmak için.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        var (min, max) = Solid.GetBoundingBox();
        return new CadBoundingBox(min, max);
    }

    /*
       NE: Nesneyi Ötele (Move)
       NEDEN: Solid'in TÜM vertex'lerini aynı delta kadar kaydırmak — bu, kenar/yüz
              bağlantılarını (topolojiyi) DEĞİŞTİRMEDİĞİ için her zaman güvenlidir.
    */
    public override void Move(Vector3D delta)
    {
        foreach (var v in Solid.GetVertices())
            v.Position = v.Position + delta;
    }

    /*
       NE: Dönüşüm Uygula (Transform)
       NEDEN: Solid'in tüm vertex'lerine dünya matrisini uygulamak için (taşıma/döndürme/
              ölçekleme) — Move ile aynı gerekçeyle topolojik olarak güvenlidir.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        foreach (var v in Solid.GetVertices())
            v.Position = matrix.Transform(v.Position);
    }

    /*
       NE: Kopya Oluştur (Clone)
       NEDEN: Solid.Clone() (derin, kimlik-korumalı graf kopyası) ile bağımsız bir Solid
              üretip yeni bir SolidEntity'ye sarmalamak için (COPY komutu ve CSG komutlarının
              orijinal seçimi bozmadan çalışabilmesi için).
    */
    public override CadEntity Clone()
    {
        var clone = new SolidEntity(Solid.Clone());
        CopyBaseProperties(clone);
        return clone;
    }

    /*
       NE: Kenetlenme Noktaları (SnapPoints)
       NEDEN: Solid'in tüm köşe (vertex) noktalarını uç noktası (Endpoint) olarak
              yakalanabilir kılmak için.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        foreach (var v in Solid.GetVertices())
            yield return new SnapPoint(v.Position, SnapPointType.Endpoint);
    }

    /*
       NE: Eksene Hizalı Kutu Tespiti (TryGetAxisAlignedBox)
       NEDEN: Grip düzenlemesini SADECE gerçekten eksene-hizalı (rotasyonsuz) 8 köşeli bir
              kutuysa etkinleştirmek için — 8 vertex'in HER BİRİ hesaplanan AABB'nin bir
              köşesiyle (X/Y/Z'nin ayrı ayrı min veya max'ıyla) tam örtüşmeli VE 8 farklı
              köşenin TAMAMI temsil edilmeli (yoksa döndürülmüş bir kutu ya da başka bir
              şekil yanlışlıkla "kutu" sanılabilir).
    */
    private bool TryGetAxisAlignedBox(out Vector3D min, out Vector3D max)
    {
        var verts = Solid.GetVertices().Select(v => v.Position).ToList();
        min = Vector3D.Zero;
        max = Vector3D.Zero;
        if (verts.Count != 8) return false;

        double minX = verts.Min(p => p.X), maxX = verts.Max(p => p.X);
        double minY = verts.Min(p => p.Y), maxY = verts.Max(p => p.Y);
        double minZ = verts.Min(p => p.Z), maxZ = verts.Max(p => p.Z);
        const double eps = 1e-3; // mm

        var seenCorners = new HashSet<(int, int, int)>();
        foreach (var p in verts)
        {
            bool onX = Math.Abs(p.X - minX) < eps || Math.Abs(p.X - maxX) < eps;
            bool onY = Math.Abs(p.Y - minY) < eps || Math.Abs(p.Y - maxY) < eps;
            bool onZ = Math.Abs(p.Z - minZ) < eps || Math.Abs(p.Z - maxZ) < eps;
            if (!onX || !onY || !onZ) return false;

            seenCorners.Add((Math.Abs(p.X - minX) < eps ? 0 : 1,
                              Math.Abs(p.Y - minY) < eps ? 0 : 1,
                              Math.Abs(p.Z - minZ) < eps ? 0 : 1));
        }
        if (seenCorners.Count != 8) return false;

        min = new Vector3D(minX, minY, minZ);
        max = new Vector3D(maxX, maxY, maxZ);
        return true;
    }

    /*
       NE: Grip Noktalarını Getir (GetGripPoints)
       NEDEN: Eksene-hizalı bir kutu ise 6 yüz-merkezi grip'i sunar (bkz. sınıf başı NE/NEDEN).
    */
    public override IEnumerable<Vector3D> GetGripPoints()
    {
        if (!TryGetAxisAlignedBox(out var min, out var max)) yield break;

        double cx = (min.X + max.X) / 2, cy = (min.Y + max.Y) / 2, cz = (min.Z + max.Z) / 2;
        yield return new Vector3D(min.X, cy, cz); // 0: -X yüzü
        yield return new Vector3D(max.X, cy, cz); // 1: +X yüzü
        yield return new Vector3D(cx, min.Y, cz); // 2: -Y yüzü
        yield return new Vector3D(cx, max.Y, cz); // 3: +Y yüzü
        yield return new Vector3D(cx, cy, min.Z); // 4: -Z yüzü
        yield return new Vector3D(cx, cy, max.Z); // 5: +Z yüzü
    }

    /*
       NE: Grip Noktasını Taşı (MoveGripPointAt)
       NEDEN: Sürüklenen yüzü, KARŞI yüzü sabit tutarak ilgili eksende ölçekler — vertex
              sayısı/bağlantısı (topoloji) HİÇ değişmediği için Solid.IsValid() güvende
              kalır (Move/Transform ile birebir aynı, kanıtlanmış mekanizma). Minimum
              10mm boyut altına küçültme engellenir (dejenere/sıfır-hacimli kutu riski).
    */
    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (!TryGetAxisAlignedBox(out var min, out var max))
        {
            base.MoveGripPointAt(index, newPosition);
            return;
        }

        const double minSize = 10.0; // mm
        Matrix4x4 mat;

        switch (index)
        {
            case 0: // -X yüzü sürüklendi → +X sabit
            {
                double newMinX = Math.Min(newPosition.X, max.X - minSize);
                double scale = (max.X - newMinX) / (max.X - min.X);
                mat = ScaleAboutPlane(max.X, Axis.X, scale);
                break;
            }
            case 1: // +X yüzü sürüklendi → -X sabit
            {
                double newMaxX = Math.Max(newPosition.X, min.X + minSize);
                double scale = (newMaxX - min.X) / (max.X - min.X);
                mat = ScaleAboutPlane(min.X, Axis.X, scale);
                break;
            }
            case 2: // -Y yüzü sürüklendi → +Y sabit
            {
                double newMinY = Math.Min(newPosition.Y, max.Y - minSize);
                double scale = (max.Y - newMinY) / (max.Y - min.Y);
                mat = ScaleAboutPlane(max.Y, Axis.Y, scale);
                break;
            }
            case 3: // +Y yüzü sürüklendi → -Y sabit
            {
                double newMaxY = Math.Max(newPosition.Y, min.Y + minSize);
                double scale = (newMaxY - min.Y) / (max.Y - min.Y);
                mat = ScaleAboutPlane(min.Y, Axis.Y, scale);
                break;
            }
            case 4: // -Z yüzü sürüklendi → +Z sabit
            {
                double newMinZ = Math.Min(newPosition.Z, max.Z - minSize);
                double scale = (max.Z - newMinZ) / (max.Z - min.Z);
                mat = ScaleAboutPlane(max.Z, Axis.Z, scale);
                break;
            }
            case 5: // +Z yüzü sürüklendi → -Z sabit
            {
                double newMaxZ = Math.Max(newPosition.Z, min.Z + minSize);
                double scale = (newMaxZ - min.Z) / (max.Z - min.Z);
                mat = ScaleAboutPlane(min.Z, Axis.Z, scale);
                break;
            }
            default:
                base.MoveGripPointAt(index, newPosition);
                return;
        }

        foreach (var v in Solid.GetVertices())
            v.Position = mat.Transform(v.Position);

        InvalidateCache();
    }

    private enum Axis { X, Y, Z }

    /*
       NE: Bir Eksen Düzlemine Göre Ölçekleme Matrisi (ScaleAboutPlane)
       NEDEN: "planePosition" değerindeki (sabit tutulan yüzün konumu) sadece TEK bir
              eksende ölçekleme uygular — diğer iki eksen scale=1 olduğu için anchor
              noktasının o eksenlerdeki değeri önemsizdir (çarpı 1 sonrası fark sıfırlanır).
    */
    private static Matrix4x4 ScaleAboutPlane(double planePosition, Axis axis, double scale)
    {
        Vector3D anchor = axis switch
        {
            Axis.X => new Vector3D(planePosition, 0, 0),
            Axis.Y => new Vector3D(0, planePosition, 0),
            _      => new Vector3D(0, 0, planePosition)
        };
        Matrix4x4 scaleMat = axis switch
        {
            Axis.X => Matrix4x4.CreateScale(scale, 1, 1),
            Axis.Y => Matrix4x4.CreateScale(1, scale, 1),
            _      => Matrix4x4.CreateScale(1, 1, scale)
        };
        return Matrix4x4.CreateTranslation(anchor) * scaleMat * Matrix4x4.CreateTranslation(-anchor.X, -anchor.Y, -anchor.Z);
    }
}
