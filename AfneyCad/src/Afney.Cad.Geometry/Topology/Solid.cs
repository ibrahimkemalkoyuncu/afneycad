using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology;

/*
NE:
Solid - BREP katı cisim modeli.

NE İÇİN:
3D solid modeling (AutoCAD/Revit level).

NEREDE:
CAD Kernel - Topology.

NE ZAMAN:
Boolean operations, extrude, sweep, revolve.

AMAÇ:
Topologically valid solid representation.

EULER-POINCARÉ FORMÜLÜ:
V - E + F = 2  (genus 0 için)
V = Vertex sayısı
E = Edge sayısı
F = Face sayısı
*/
public class Solid
{
    public Guid Id { get; }
    public List<Face> Faces { get; }
    public string Name { get; set; }

    public Solid(string name = "Solid")
    {
        Id = Guid.NewGuid();
        Faces = new List<Face>();
        Name = name;
    }

    /*
    PROPERTY:
    Vertices, Edges - Computed from faces
    */
    public IEnumerable<Vertex> GetVertices()
    {
        var vertices = new HashSet<Vertex>();
        
        foreach (var face in Faces)
        {
            foreach (var loop in face.Loops)
            {
                foreach (var edge in loop.Edges)
                {
                    vertices.Add(edge.StartVertex);
                    vertices.Add(edge.EndVertex);
                }
            }
        }
        
        return vertices;
    }

    public IEnumerable<TopologyEdge> GetEdges()
    {
        var edges = new HashSet<TopologyEdge>();
        
        foreach (var face in Faces)
        {
            foreach (var loop in face.Loops)
            {
                foreach (var edge in loop.Edges)
                {
                    edges.Add(edge);
                }
            }
        }
        
        return edges;
    }

    /*
    METOD ADI:
    IsValid

    AMACI:
    Euler-Poincaré formülü ile topological validity kontrolü.

    FORMÜL:
    V - E + F = 2 - 2*G
    G = genus (torus için 1, sphere için 0)

    KURAL:
    - Her edge tam 2 face'e ait olmalı (manifold)
    - Her loop kapalı olmalı
    - Self-intersection olmamalı
    */
    public bool IsValid()
    {
        int V = GetVertices().Count();
        int E = GetEdges().Count();
        int F = Faces.Count;
        
        // Euler characteristic (genus 0)
        int eulerChar = V - E + F;
        
        if (eulerChar != 2)
            return false;
        
        // Manifold check: Her edge'in 2 face'i olmalı
        foreach (var edge in GetEdges())
        {
            if (edge.LeftFace == null || edge.RightFace == null)
                return false;
        }
        
        // Loop closure check
        foreach (var face in Faces)
        {
            foreach (var loop in face.Loops)
            {
                if (!loop.IsClosed())
                    return false;
            }
        }
        
        return true;
    }

    /*
    METOD ADI:
    GetVolume

    AMACI:
    Katı cismin hacmini hesaplamak.

    ALGORİTMA:
    Divergence theorem (Gauss):
    V = (1/6) * Σ (p · n) * A
    
    p = face center
    n = face normal
    A = face area
    */
    public double GetVolume()
    {
        double volume = 0;
        
        foreach (var face in Faces)
        {
            var area = face.GetArea();
            var normal = face.Normal;
            
            // Face center (basitleştirilmiş: ilk vertex)
            var firstEdge = face.GetOuterLoop()?.Edges.FirstOrDefault();
            if (firstEdge == null) continue;
            
            var center = firstEdge.StartVertex.Position;
            
            // Dot product
            double contribution = (center.X * normal.X + center.Y * normal.Y + center.Z * normal.Z) * area;
            volume += contribution;
        }
        
        return Math.Abs(volume / 6.0);
    }

    /*
    METOD ADI:
    GetBoundingBox

    AMACI:
    Solid'in axis-aligned bounding box'ını hesaplamak.
    */
    public (Vector3D Min, Vector3D Max) GetBoundingBox()
    {
        var vertices = GetVertices().ToList();
        if (vertices.Count == 0)
            return (Vector3D.Zero, Vector3D.Zero);
        
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        
        foreach (var v in vertices)
        {
            if (v.Position.X < minX) minX = v.Position.X;
            if (v.Position.Y < minY) minY = v.Position.Y;
            if (v.Position.Z < minZ) minZ = v.Position.Z;
            
            if (v.Position.X > maxX) maxX = v.Position.X;
            if (v.Position.Y > maxY) maxY = v.Position.Y;
            if (v.Position.Z > maxZ) maxZ = v.Position.Z;
        }
        
        return (new Vector3D(minX, minY, minZ), new Vector3D(maxX, maxY, maxZ));
    }
}
