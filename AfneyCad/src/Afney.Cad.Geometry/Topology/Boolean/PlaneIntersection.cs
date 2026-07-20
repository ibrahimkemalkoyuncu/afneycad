using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Düzlem-Düzlem Kesişimi (PlaneIntersection)
   NEDEN: Gerçek topolojik B-Rep boolean'ın (bkz. docs/Roadmap_CSG_Boolean.md) ilk adımı —
          iki katı cismin yüzeylerinin (Face) hangi doğru boyunca kesiştiğini bulmak. Bu doğru
          FaceIntersection tarafından her iki Face'in poligon sınırına kırpılarak gerçek
          kesişim segmentine indirgenir.
*/
public static class PlaneIntersection
{
    /*
       NE: İki düzlemin kesişim doğrusunu hesaplar.
       GİRDİ: Her düzlem bir nokta + BİRİM normal ile tanımlanır.
       ÇIKTI: (doğru üzerinde bir nokta, doğru yönü) — paralel/çakışık düzlemlerde null.
       FORMÜL: dir = n1×n2; point = ((d1·n2 - d2·n1) × dir) / |dir|²
               (d1 = n1·p1, d2 = n2·p2 — düzlem denklemi n·X = d)
    */
    public static (Vector3D Point, Vector3D Direction)? Intersect(
        Vector3D pointOnA, Vector3D normalA, Vector3D pointOnB, Vector3D normalB)
    {
        var n1 = normalA.Normalize();
        var n2 = normalB.Normalize();

        var dir = n1.Cross(n2);
        double dirLenSq = dir.LengthSquared();
        if (dirLenSq < 1e-16)
            return null; // Paralel (veya çakışık) düzlemler — dejenere, kapsam dışı

        double d1 = n1.Dot(pointOnA);
        double d2 = n2.Dot(pointOnB);

        var numerator = (n2 * d1 - n1 * d2).Cross(dir);
        var point = numerator / dirLenSq;

        return (point, dir.Normalize());
    }
}
