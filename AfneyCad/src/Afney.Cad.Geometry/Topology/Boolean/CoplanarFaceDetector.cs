using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Coplanar Yüz Tespiti (CoplanarFaceDetector) — CSG Boolean için 2. yapı taşı
   NEDEN: `docs/Roadmap_CSG_Boolean.md` — genel iki-katı SUBTRACT'in ikinci ön koşulu (1.'si
          `VertexWelder`, tamamlandı). `PlaneIntersection.Intersect`, paralel düzlemleri
          (dirLenSq≈0) "dejenere, kapsam dışı" sayıp null döner — ama bu, "paralel FARKLI
          düzlem" ile "coplanar (AYNI düzlem)" durumlarını AYIRT ETMİYOR. Araştırma ajanının
          bulgusu: gerçek CSG kernel'leri (OpenCASCADE/CGAL) bu ikisini İKİ AYRI test ile
          ayırt eder — (1) normal paralelliği (açısal tolerans), (2) düzlem denklemi ofset
          eşitliği (mesafe toleransı). Bu sınıf, `PlaneIntersection`'a HİÇ dokunmadan (mevcut
          testleri bozma riski yok) izole bu iki testi uygular.

   TOLERANS NOTU: `VertexWelder`'la aynı prensip — bu, kullanıcı-çizim toleransı
   (`MergeTolerance`, 5mm) DEĞİL, geometrik "aynı düzlem mi" kararı; varsayılan olarak
   `PlaneCutter.Tolerance` (1e-6) mertebesinde sıkı bir değer kullanılır, çağıran isterse
   override edebilir.
*/
public static class CoplanarFaceDetector
{
    private const double DefaultTolerance = 1e-6;

    /*
       NE: İki Face Aynı Düzlemde mi? (AreCoplanar)
       NASIL:
       1. Normaller paralel mi? (`|n1·n2| ≈ 1` — B-Rep'te komşu iki Solid'in ortak yüzü
          genelde ZIT normal taşır, biri dışa biri içe bakar; bu yüzden hem aynı yönlü hem
          ters yönlü paralellik "coplanar" adayı sayılır — yön kontrolü çağıranın işi.)
       2. Paralellerse, iki yüzün düzlem ofseti (n1'e göre) eşit mi? (`n1·p1 ≈ n1·p2`)
    */
    public static bool AreCoplanar(Face a, Face b, double angleTolerance = DefaultTolerance, double offsetTolerance = DefaultTolerance)
    {
        var na = a.Normal.Normalize();
        var nb = b.Normal.Normalize();
        if (na.LengthSquared() < 1e-12 || nb.LengthSquared() < 1e-12) return false;

        double dot = na.Dot(nb);
        bool parallel = Math.Abs(Math.Abs(dot) - 1.0) <= angleTolerance;
        if (!parallel) return false;

        var pointOnA = GetAnyPoint(a);
        var pointOnB = GetAnyPoint(b);
        if (pointOnA == null || pointOnB == null) return false;

        // Her iki noktayı da AYNI normal (na) ile ölçüyoruz — nb ters yönlü olabileceğinden
        // ofset karşılaştırmasının normal işaretinden bağımsız olması için.
        double offsetA = na.Dot(pointOnA.Value);
        double offsetB = na.Dot(pointOnB.Value);
        return Math.Abs(offsetA - offsetB) <= offsetTolerance;
    }

    private static Vector3D? GetAnyPoint(Face face)
    {
        var vertices = face.GetOuterLoop()?.GetOrderedVertices();
        return vertices is { Count: > 0 } ? vertices[0].Position : null;
    }
}
