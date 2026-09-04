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
     çizer — gerçek gölgelendirilmiş/dolu 3D render için ayrı bir Direct3D entegrasyonu
     gerekir (kapsam dışı, mevcut Direct3DViewportControl zaten CadDatabase'den ayrı bir
     yoldan Solid tessellate ediyor).
   - GetGripPoints() BİLİNÇLİ OLARAK override edilmedi (taban sınıfın boş varsayılanı
     kullanılıyor): tekil vertex'leri sürükleyerek serbestçe deforme etmek Euler/manifold
     geçerliliğini (Solid.IsValid()) bozabilir — CSG kernel'i sadece topolojik olarak
     geçerli Solid'lerle çalışır. Taşıma (Move) tüm vertex'leri birlikte kaydırdığı için
     topolojiyi bozmaz, bu yüzden güvenlidir.
   - DXF/DWG/IFC export'a henüz bağlanmadı (Infrastructure katmanındaki writer'lar bu
     tipi tanımıyor) — kaydedilen bir çizim SolidEntity'leri İÇERMEZ (sessizce atlanır,
     mevcut export'lar bu yeni tip yüzünden BOZULMAZ, ama solid'ler dosyaya yazılmaz).
     Sonraki adım olarak dokümante edildi.
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
}
