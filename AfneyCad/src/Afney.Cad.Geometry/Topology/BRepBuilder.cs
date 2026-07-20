using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology;

/*
   NE: B-Rep Katı Cisim İnşa Aracı (BRepBuilder)
   NEDEN: Topology.Solid/Face/Loop/TopologyEdge/Vertex sınıfları bu oturuma kadar hiçbir yerde
          örneklenmiyordu (grep doğrulaması: `new Solid(`/`new Face(` sadece kendi dosyalarında
          geçiyor) — gerçek bir kullanım yoktu. Bu sınıf, düzlemsel bir profili bir vektör
          boyunca ekstrude ederek TOPOLOJİK OLARAK GEÇERLİ (Euler V-E+F=2, manifold: her kenar
          tam 2 face'e ait, tüm loop'lar kapalı) bir winged-edge Solid üretir.

   WINGED-EDGE PAYLAŞIM KURALI:
   Komşu iki face arasındaki her kenar TEK bir TopologyEdge nesnesidir (referans paylaşımı) —
   biri (LeftFace) StartVertex→EndVertex yönünde, diğeri (RightFace) EndVertex→StartVertex
   yönünde gezer. Bu builder her kenarı doğru Left/Right ataması ve Next/Prev zinciriyle kurar.

   KAPSAM DIŞI (bilinçli): Boolean (union/subtract/intersect), delikli (kapı/pencere) face'ler,
   NURBS yüzeyler, fillet/chamfer — bunlar ayrı, çok daha büyük bir CSG kernel gerektirir.
*/
public static class BRepBuilder
{
    /*
       NE: Düzlemsel poligonu bir vektör boyunca ekstrude ederek kapalı bir Solid üretir.
       GİRDİ: profile — düzlemsel, basit (self-intersect olmayan), kapalı kabul edilen nokta
              dizisi (son nokta ilk noktaya otomatik kapatılır, tekrar verilmemeli).
              extrudeVector — ekstrüzyon yönü VE uzunluğu (ör. yükseklik * yukarı birim vektör).
       YÖNELİM: Çağıranın profile sarım yönü serbesttir — bu metod profilin Newell normalini
              hesaplayıp extrudeVector ile hizalar (gerekirse ters çevirir), böylece üretilen
              tüm face normalleri her zaman DIŞA doğru olur.
    */
    public static Solid ExtrudePolygon(IReadOnlyList<Vector3D> profile, Vector3D extrudeVector, string name = "ExtrudedSolid")
    {
        if (profile.Count < 3)
            throw new ArgumentException("Ekstrüzyon için en az 3 noktalı bir profil gerekir.", nameof(profile));
        if (extrudeVector.LengthSquared() < 1e-12)
            throw new ArgumentException("Ekstrüzyon vektörü sıfır olamaz.", nameof(extrudeVector));

        var pts = OrientCcwTowardExtrude(profile, extrudeVector);
        int n = pts.Count;

        var bottom = new Vertex[n];
        var top = new Vertex[n];
        for (int i = 0; i < n; i++)
        {
            bottom[i] = new Vertex(pts[i]);
            top[i] = new Vertex(pts[i] + extrudeVector);
        }

        var edgeBottom = new TopologyEdge[n]; // bottom[i] -> bottom[i+1]
        var edgeTop = new TopologyEdge[n];    // top[i]    -> top[i+1]
        var edgeVert = new TopologyEdge[n];   // bottom[i] -> top[i]
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            edgeBottom[i] = new TopologyEdge(bottom[i], bottom[j]);
            edgeTop[i] = new TopologyEdge(top[i], top[j]);
            edgeVert[i] = new TopologyEdge(bottom[i], top[i]);
        }

        var solid = new Solid(name);

        // Alt kapak (bottom cap): edgeBottom[i]'ler AZALAN indeksle, HER BİRİ TERS yönde
        // gezilir → walk: bottom[0]→bottom[n-1]→bottom[n-2]→...→bottom[1]→bottom[0],
        // yani orijinal profile sırasının tersi (dışa normal = -extrudeVector).
        var bottomFace = new Face { Normal = ComputeNewellNormal(Enumerable.Range(0, n).Select(i => bottom[n - 1 - i].Position).ToList()).Normalize() };
        var bottomDirected = new List<(TopologyEdge Edge, bool Forward)>(n);
        for (int i = n - 1; i >= 0; i--)
            bottomDirected.Add((edgeBottom[i], false)); // Right kullanıcı
        AttachFace(bottomFace, bottomDirected);
        solid.Faces.Add(bottomFace);

        // Üst kapak (top cap): profile ORİJİNAL sırayla (dışa normal = +extrudeVector yönü).
        var topFace = new Face { Normal = ComputeNewellNormal(Enumerable.Range(0, n).Select(i => top[i].Position).ToList()).Normalize() };
        var topDirected = new List<(TopologyEdge Edge, bool Forward)>(n);
        for (int i = 0; i < n; i++)
            topDirected.Add((edgeTop[i], true)); // Left kullanıcı
        AttachFace(topFace, topDirected);
        solid.Faces.Add(topFace);

        // Yan yüzeyler: quad(bottom[i], bottom[i+1], top[i+1], top[i])
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var sideNormal = (bottom[j].Position - bottom[i].Position).Cross(top[i].Position - bottom[i].Position).Normalize();
            var sideFace = new Face { Normal = sideNormal };
            var directed = new List<(TopologyEdge Edge, bool Forward)>
            {
                (edgeBottom[i], true),   // bottom[i] -> bottom[i+1]
                (edgeVert[j], true),     // bottom[i+1] -> top[i+1]
                (edgeTop[i], false),     // top[i+1] -> top[i]  (edgeTop[i] ters)
                (edgeVert[i], false),    // top[i] -> bottom[i] (edgeVert[i] ters)
            };
            AttachFace(sideFace, directed);
            solid.Faces.Add(sideFace);
        }

        return solid;
    }

    /*
       NE: Eksene hizalı olmayan (oriented) dikdörtgen prizma — duvar gibi yerel bir çerçevede
           (uAxis=uzunluk yönü, vAxis=kalınlık yönü, wAxis=yükseklik yönü) tanımlanan kutu.
       NOT: axisU/axisV/axisW'nin ortonormal olması beklenir; sağ-el kuralına uymasa bile
            ExtrudePolygon'daki OrientCcwTowardExtrude otomatik düzeltir.
    */
    public static Solid ExtrudeBox(Vector3D origin, Vector3D axisU, Vector3D axisV, Vector3D axisW, double lenU, double lenV, double lenW, string name = "Box")
    {
        var u = axisU.Normalize() * lenU;
        var v = axisV.Normalize() * lenV;
        var w = axisW.Normalize() * lenW;

        var profile = new List<Vector3D>
        {
            origin,
            origin + u,
            origin + u + v,
            origin + v,
        };

        return ExtrudePolygon(profile, w, name);
    }

    /*
       NE: Bir face'in Loop'unu, yönlü (edge, forward) listesinden kurar: LeftFace/RightFace
           atar, Loop.Edges dizisini doldurur, ve NextLeftEdge/PrevLeftEdge veya
           NextRightEdge/PrevRightEdge zincirini bu face'in gezinme sırasına göre bağlar.
    */
    private static void AttachFace(Face face, List<(TopologyEdge Edge, bool Forward)> directedEdges)
    {
        var loop = new Loop(isOuter: true);
        int m = directedEdges.Count;

        for (int i = 0; i < m; i++)
        {
            var (edge, forward) = directedEdges[i];
            loop.Edges.Add(edge);

            if (forward) edge.LeftFace = face; else edge.RightFace = face;

            var (nextEdge, _) = directedEdges[(i + 1) % m];
            var (prevEdge, _) = directedEdges[(i - 1 + m) % m];

            if (forward)
            {
                edge.NextLeftEdge = nextEdge;
                edge.PrevLeftEdge = prevEdge;
            }
            else
            {
                edge.NextRightEdge = nextEdge;
                edge.PrevRightEdge = prevEdge;
            }
        }

        face.Loops.Add(loop);
    }

    private static Vector3D ComputeNewellNormal(IReadOnlyList<Vector3D> pts)
    {
        var sum = new Vector3D(0, 0, 0);
        int n = pts.Count;
        for (int i = 0; i < n; i++)
            sum += pts[i].Cross(pts[(i + 1) % n]);
        return sum;
    }

    private static List<Vector3D> OrientCcwTowardExtrude(IReadOnlyList<Vector3D> profile, Vector3D extrudeVector)
    {
        var list = profile.ToList();
        var normal = ComputeNewellNormal(list);
        if (normal.Dot(extrudeVector) < 0)
            list.Reverse();
        return list;
    }
}
