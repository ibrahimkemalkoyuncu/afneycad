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

    ALGORİTMA (Newell's method genellemesi):
    Eski kod sadece (X,Y) düzlemine izdüşüm alıyordu (shoelace 2D) — bu yüzden düşey/eğik
    bir yüzeyde (ör. bir duvar yan yüzü, normal ≈ (1,0,0)) izdüşüm bir doğruya çöker ve
    alan ~0 çıkardı. Herhangi bir düzlemsel poligonun 3D alanı, ardışık kenar vektörlerinin
    orijine göre çapraz çarpımlarının toplamının yarı büyüklüğü ile bulunur — yüzey
    yöneliminden bağımsızdır ve ek bir 2D projeksiyon bazı gerektirmez:
        Area = 0.5 * |Σ (Vi × Vi+1)|
    Delik (inner loop) varsa alanından düşülür (dış sınır - iç sınırlar).
    */
    public double GetArea()
    {
        double total = 0;
        foreach (var loop in Loops)
        {
            double loopArea = GetLoopArea(loop);
            total += loop.IsOuter ? loopArea : -loopArea;
        }
        return Math.Abs(total);
    }

    private static double GetLoopArea(Loop loop)
    {
        var vertices = loop.GetOrderedVertices();
        if (vertices.Count < 3) return 0;

        var sum = new Vector3D(0, 0, 0);
        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = vertices[i].Position;
            var p2 = vertices[(i + 1) % n].Position;
            sum += p1.Cross(p2);
        }

        return sum.Length() / 2.0;
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
    METOD ADI:
    GetOrderedVertices

    AMACI:
    Loop'un sınır vertex dizisini, kenarların kendi sabit StartVertex/EndVertex yönünden
    BAĞIMSIZ olarak, gerçek gezinme (walk) sırasına göre üretmek.

    NEDEN GEREKLİ:
    Winged-Edge yapısında paylaşılan bir TopologyEdge, iki komşu face'den biri için
    StartVertex→EndVertex yönünde, diğeri (RightFace) için ise EndVertex→StartVertex
    yönünde gezilir — edge nesnesinin kendisi tek bir sabit yön taşır. Bu yüzden alan/hacim
    hesaplarının doğru olması için, her loop'un vertex sırası ardışık kenarların PAYLAŞTIĞI
    vertex'e göre zincirlenerek (chaining) çıkarılmalı; edge.StartVertex'i körlemesine
    "bu loop'taki giriş noktası" varsaymak, paylaşılan (RightFace tarafından ters gezilen)
    kenarlarda yanlış/dejenere poligonlar üretir.
    */
    public List<Vertex> GetOrderedVertices()
    {
        var result = new List<Vertex>();
        if (Edges.Count == 0) return result;

        Vertex current;
        if (Edges.Count > 1)
        {
            var first = Edges[0];
            var second = Edges[1];
            bool startSharedWithNext = second.StartVertex.Id == first.StartVertex.Id || second.EndVertex.Id == first.StartVertex.Id;
            current = startSharedWithNext ? first.EndVertex : first.StartVertex;
        }
        else
        {
            current = Edges[0].StartVertex;
        }

        foreach (var e in Edges)
        {
            var next = e.StartVertex.Id == current.Id ? e.EndVertex : e.StartVertex;
            result.Add(current);
            current = next;
        }

        return result;
    }

    /*
    VALIDASYON:
    Loop kapalı olmalı: ardışık her kenar bir öncekiyle bir vertex paylaşmalı (zincir kopmamalı)
    ve son kenardan çıkış, ilk kenara giriş vertex'i ile eşleşmeli.
    */
    public bool IsClosed()
    {
        if (Edges.Count < 3) return false;

        Vertex current;
        var first = Edges[0];
        var second = Edges[1];
        bool startSharedWithNext = second.StartVertex.Id == first.StartVertex.Id || second.EndVertex.Id == first.StartVertex.Id;
        var loopStart = startSharedWithNext ? first.EndVertex : first.StartVertex;
        current = loopStart;

        foreach (var e in Edges)
        {
            if (e.StartVertex.Id != current.Id && e.EndVertex.Id != current.Id)
                return false; // zincir koptu — bu kenar önceki vertex'e bağlı değil

            current = e.StartVertex.Id == current.Id ? e.EndVertex : e.StartVertex;
        }

        return current.Id == loopStart.Id;
    }
}
