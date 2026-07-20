using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using GeoVector3D = Afney.Cad.Geometry.Primitives.Vector3D;

namespace Afney.Cad.Render3D;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;

    public Vertex(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }
}

/*
   NE: Mesh Yükleyici (MeshBuffer)
   NEDEN: `BRepTessellator.Tessellate(Solid)`'in çıktısını (List<Vector3D> Vertices,
          List<(int,int,int)> Faces — bkz. Afney.Cad.Geometry.Topology) DOĞRUDAN GPU vertex/
          index buffer'a yükleyen köprü. Mevcut B-Rep/tessellasyon koduna SIFIR değişiklik
          gerektirmez — WallBRepService/DuctBRepService/Pipe3DModelService'in ürettiği aynı
          veri, sadece yeni bir tüketici (Pipe3DViewWindow'un WPF MeshGeometry3D'si yerine
          burada ID3D11Buffer).

   GÖLGELENDİRME: Düz (flat) gölgelendirme — her üçgen kendi köşelerini KOPYALAR ve üçgenin
   yüz normalini taşır (CAD/mimari görselleştirmede kenarların net görünmesi için tercih
   edilir; yumuşak/smooth gölgelendirme — komşu üçgenlerin normal ortalaması — ileride
   eklenebilir).
*/
public sealed class MeshBuffer : IDisposable
{
    public ID3D11Buffer VertexBuffer { get; }
    public int VertexCount { get; }

    public MeshBuffer(ID3D11Device device, IReadOnlyList<GeoVector3D> vertices, IReadOnlyList<(int A, int B, int C)> faces)
    {
        var flatVertices = new Vertex[faces.Count * 3];
        int i = 0;
        foreach (var (a, b, c) in faces)
        {
            var pa = ToVector3(vertices[a]);
            var pb = ToVector3(vertices[b]);
            var pc = ToVector3(vertices[c]);
            var normal = Vector3.Normalize(Vector3.Cross(pb - pa, pc - pa));

            flatVertices[i++] = new Vertex(pa, normal);
            flatVertices[i++] = new Vertex(pb, normal);
            flatVertices[i++] = new Vertex(pc, normal);
        }

        VertexCount = flatVertices.Length;
        VertexBuffer = device.CreateBuffer(flatVertices, BindFlags.VertexBuffer);
    }

    private static Vector3 ToVector3(GeoVector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);

    public void Dispose() => VertexBuffer.Dispose();
}
