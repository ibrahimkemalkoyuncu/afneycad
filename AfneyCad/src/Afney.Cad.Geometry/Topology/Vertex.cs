using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology;

/*
NE:
Vertex - 3D uzayda nokta (topology için).

NE İÇİN:
BREP (Boundary Representation) solid modeling.

NEREDE:
Topology Layer - CAD Core.

NE ZAMAN:
Solid, Shell, Face gibi topolojik yapılar oluşturulurken.

AMAÇ:
Euler operators ve Winged-Edge data structure için temel.
*/
public class Vertex
{
    public Guid Id { get; }
    public Vector3D Position { get; set; }
    
    // Adjacency: Bu vertex'e bağlı edge'ler
    public List<TopologyEdge> Edges { get; }

    public Vertex(Vector3D position)
    {
        Id = Guid.NewGuid();
        Position = position;
        Edges = new List<TopologyEdge>();
    }

    public int Valence => Edges.Count; // Vertex derecesi
}
