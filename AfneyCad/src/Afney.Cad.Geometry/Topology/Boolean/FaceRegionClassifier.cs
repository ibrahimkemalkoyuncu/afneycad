using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Yüz-Bölge Sınıflandırıcısı (FaceRegionClassifier) — CSG Boolean, `GeneralSolidSubtractor`
       için yardımcı yapı taşı.
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — çok-yüzlü genel SUBTRACT montajında, B'nin A'yı
          BİRDEN FAZLA yüzden ardışık kestiği (`PlaneCutter.CutWithPlaneKeepDiscarded`) her
          adımda üretilen "mirror cap" yüzeyi, iki farklı anlama gelebilir: (1) A\B'nin GERÇEK
          dış sınırı (o adımdaki kesim düzlemi B'nin sınırıyla çakışıyor — bitişik bölge
          gerçekten A∩B), (2) İKİ AYRI atılan parçanın (Dᵢ, Dⱼ) arasındaki İÇ ara-yüzey (o
          adımdaki kesim düzlemi henüz uygulanmamış SONRAKİ bir düzlemin dışında kalıyor —
          bitişik bölge başka bir Dⱼ, A∩B DEĞİL). Bu ikisi ayırt edilmeden mirror cap'ler
          körlemesine birleştirilirse, A\B'nin içine sahte (yanlış) bir iç yüzey karışır.
   YÖNTEM: Face'in centroid'i, KENDİ OUTWARD normali boyunca (kendi sahibi Solid'in DIŞINA,
          komşu bölgeye doğru, +Normal) küçük bir epsilon kadar kaydırılıp
          `SolidClassifier.IsPointInside` ile hedef bölgenin (`region`, örn. A∩B) içinde mi
          diye test edilir — içindeyse Face gerçekten o bölgeye bitişiktir. (`GeneralSolidSubtractor`
          bağlamında: mirror cap'in normali D_i'nin dışına, A∩B'ye doğru bakar — bkz.
          `PlaneCutter.CutWithPlaneKeepDiscarded`'ın "mirror cap outward'i +n" sözleşmesi.)
   KAPSAM (bilinçli, dar): Face TÜMÜYLE tek bir bölgeye bitişik varsayılır (ikili karar) —
          bir Face'in KISMEN A∩B'ye, KISMEN başka bir Dⱼ'ye bitişik olduğu (parçalı örtüşme)
          durum kapsam dışı; bu, `ConvexPolygonClipper2D` ile Face'in kendisinin bölünmesini
          gerektirir, ayrı bir oturum konusu.
*/
public static class FaceRegionClassifier
{
    /// <summary>
    /// `face`'in verilen `region` Solid'ine GERÇEKTEN bitişik olup olmadığını (centroid'in
    /// içe-doğru epsilon-kaydırılmış probe noktasının `region` içinde olup olmadığına bakarak)
    /// belirler.
    /// </summary>
    public static bool IsFaceAdjacentToRegion(Face face, Solid region, double epsilon = 1e-3)
    {
        var loop = face.GetOuterLoop();
        if (loop == null) return false;

        var verts = loop.GetOrderedVertices();
        if (verts.Count == 0) return false;

        var centroid = Vector3D.Zero;
        foreach (var v in verts) centroid += v.Position;
        centroid /= verts.Count;

        var normal = face.Normal.Normalize();
        var probe = centroid + normal * epsilon;

        return SolidClassifier.IsPointInside(region, probe);
    }
}
