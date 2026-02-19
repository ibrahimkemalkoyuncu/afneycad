using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology;

/*
NE:
Face - BREP'te yüzey (surface ile sınırlandırılmış bölge).

NE İÇİN:
Solid modeling - her solid birden fazla face'den oluşur.

NEREDE:
Topology Model.

NE ZAMAN:
3D katı cisim operations.

AMAÇ:
Face = Surface + Boundary loops
*/
public class Face
{
    public Guid Id { get; }
    
    // Geometry: Yüzeyin matematiksel tanımı
    // public Surface? GeometrySurface { get; set; }
    
    // Topology: Sınır edge loop'ları
    public List<Loop> Loops { get; }
    
    // Normal vektörü (hesaplanmış)
    public Vector3D Normal { get; set; }

    public Face()
    {
        Id = Guid.NewGuid();
        Loops = new List<Loop>();
    }

    /*
    METOD ADI:
    GetOuterLoop

    AMACI:
    Dış sınır loop'unu bulmak.

    KURAL:
    Face'in 1 outer loop, N inner loop (delik) olabilir.
    */
    public Loop? GetOuterLoop()
    {
        return Loops.FirstOrDefault(l => l.IsOuter);
    }

    /*
    METOD ADI:
    GetArea

    AMACI:
    Yüzey alanını hesaplamak.

    ALGORİTMA:
    Shoelace formula (2D projection) veya surface integration.
    */
    public double GetArea()
    {
        // Basitleştirilmiş: Outer loop üzerinden hesapla
        var loop = GetOuterLoop();
        if (loop == null) return 0;

        // Polygon area (2D projection)
        double area = 0;
        var edges = loop.GetEdges();
        
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            var p1 = e.StartVertex.Position;
            var p2 = e.EndVertex.Position;
            
            area += (p1.X * p2.Y - p2.X * p1.Y);
        }
        
        return Math.Abs(area / 2.0);
    }
}

/*
Loop - Face boundary (closed edge chain).
*/
public class Loop
{
    public Guid Id { get; }
    public List<TopologyEdge> Edges { get; }
    public bool IsOuter { get; set; } // true = outer boundary, false = hole

    public Loop(bool isOuter = true)
    {
        Id = Guid.NewGuid();
        Edges = new List<TopologyEdge>();
        IsOuter = isOuter;
    }

    public List<TopologyEdge> GetEdges() => Edges;

    /*
    VALIDASYON:
    Loop kapalı olmalı (son vertex = ilk vertex).
    */
    public bool IsClosed()
    {
        if (Edges.Count == 0) return false;
        
        var firstVertex = Edges[0].StartVertex;
        var lastVertex = Edges[^1].EndVertex;
        
        return firstVertex.Id == lastVertex.Id;
    }
}
