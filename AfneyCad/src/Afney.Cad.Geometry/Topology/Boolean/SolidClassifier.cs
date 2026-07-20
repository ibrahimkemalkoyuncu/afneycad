using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: İç/Dış Sınıflandırma (SolidClassifier) — CSG Boolean Faz 3
   NEDEN: FaceSplitter'ın ürettiği alt-Face'lerden hangilerinin diğer Solid'in İÇİNDE,
          hangilerinin DIŞINDA kaldığını belirlemek — SUBTRACT/UNION/INTERSECT montajının
          (Faz 4) girdisi. Yöntem: bir noktadan ışın gönder, Solid'in (BRepTessellator ile
          üçgenlenmiş) yüzeyini kaç kez kestiğini say — tek sayı = içeride (standart nokta-
          içi-katı testi, Jordan eğri teoreminin 3D genellemesi).
*/
public static class SolidClassifier
{
    // Eksen-hizalı geometrilerde (ör. kutular) dejenere (kenar/köşeden geçen) ışın kesişimini
    // önlemek için rastgele-ama-sabit bir yön kullanılıyor.
    private static readonly Vector3D DefaultRayDirection = new Vector3D(0.6123, 0.5217, 0.5941).Normalize();

    public static bool IsPointInside(Solid solid, Vector3D point, Vector3D? rayDirection = null)
    {
        var dir = (rayDirection ?? DefaultRayDirection).Normalize();
        var (vertices, faces) = BRepTessellator.Tessellate(solid);

        int crossings = 0;
        foreach (var (a, b, c) in faces)
        {
            if (RayIntersectsTriangle(point, dir, vertices[a], vertices[b], vertices[c]))
                crossings++;
        }
        return crossings % 2 == 1;
    }

    /*
       NE: Möller–Trumbore ışın-üçgen kesişim testi.
       NEDEN: Endüstri standardı, sayısal olarak kararlı ışın-üçgen kesişim algoritması —
              barycentric (u,v) koordinatlarını ve ışın parametresini (t) aynı anda çözer.
    */
    private static bool RayIntersectsTriangle(Vector3D origin, Vector3D dir, Vector3D v0, Vector3D v1, Vector3D v2)
    {
        const double eps = 1e-9;

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var h = dir.Cross(edge2);
        double a = edge1.Dot(h);
        if (Math.Abs(a) < eps) return false; // ışın üçgenin düzlemine paralel

        double f = 1.0 / a;
        var s = origin - v0;
        double u = f * s.Dot(h);
        if (u < -eps || u > 1 + eps) return false;

        var q = s.Cross(edge1);
        double v = f * dir.Dot(q);
        if (v < -eps || u + v > 1 + eps) return false;

        double t = f * edge2.Dot(q);
        return t > eps; // sadece ışının POZİTİF yönündeki kesişimler sayılır
    }
}
