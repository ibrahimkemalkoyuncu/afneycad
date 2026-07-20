using Afney.Cad.Geometry.Algorithms;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology;

/*
   NE: B-Rep → Üçgen Mesh Dönüştürücü (BRepTessellator)
   NEDEN: Solid/Face/Loop topolojisi render motorları (WPF Viewport3D, IFC tessellated body vb.)
          için doğrudan tüketilebilir değil — üçgen listesine indirgenmesi gerekir.
   ÇIKTI ŞEKLİ: Bilinçli olarak yeni bir mesh struct icat EDİLMEDİ — mevcut
          Afney.Cad.Mechanical.Services.Solid3DModel (Vertices + Faces üçlü-indeks listesi)
          ile birebir aynı şekilde (Vertices, Faces) tuple döner ki Pipe3DViewWindow'ın
          zaten tükettiği MeshGeometry3D/GeometryModel3D render koduna hiç dokunulmasın.
*/
public static class BRepTessellator
{
    public static (List<Vector3D> Vertices, List<(int A, int B, int C)> Faces) Tessellate(Solid solid)
    {
        var vertices = new List<Vector3D>();
        var vertexIndex = new Dictionary<Guid, int>();
        var triangles = new List<(int, int, int)>();

        int IndexOf(Vertex v)
        {
            if (vertexIndex.TryGetValue(v.Id, out int idx)) return idx;
            idx = vertices.Count;
            vertices.Add(v.Position);
            vertexIndex[v.Id] = idx;
            return idx;
        }

        foreach (var face in solid.Faces)
        {
            var outer = face.GetOuterLoop();
            if (outer == null) continue;

            var orderedVertices = outer.GetOrderedVertices();
            if (orderedVertices.Count < 3) continue;

            var points = orderedVertices.Select(v => v.Position).ToList();
            var localTriangles = PolygonTriangulator.Triangulate(points, face.Normal);

            var globalIndices = orderedVertices.Select(IndexOf).ToArray();

            foreach (var (a, b, c) in localTriangles)
                triangles.Add((globalIndices[a], globalIndices[b], globalIndices[c]));
        }

        return (vertices, triangles);
    }
}
