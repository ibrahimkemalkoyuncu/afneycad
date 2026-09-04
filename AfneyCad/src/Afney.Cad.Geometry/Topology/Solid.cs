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
    Euler-Poincaré formülü ile topological validity kontrolü — ÇOK-KABUKLU (bağlantısız
    parçalardan oluşan) Solid'leri destekler.

    NEDEN ÇOK-KABUKLU (2026-08-04 güncellemesi): `docs/Roadmap_CSG_Boolean.md` —
    `GeneralSolidSubtractor`'ın "through-slot" senaryosunda (B, A'yı ortadan bir dilim gibi
    kesip GERÇEKTEN İKİ AYRI bağlantısız parça bırakıyor) eski TEK global `V-E+F==2` testi
    kategorik olarak reddediyordu (iki bağımsız kutu birleşince eulerChar=4 çıkıyor) — ama
    bu GEÇERLİ bir B-Rep'tir (iki ayrı, kendi içinde topolojik olarak sağlam kabuk).
    GERÇEK CSG kernel'leri (OpenCASCADE/CGAL) Solid'i "kabuk (shell) listesi" olarak
    modelleyip HER kabuğun kendi Euler karakteristiğini genus-0 (`V-E+F==2`) olarak
    doğrular, TOPLAMDA değil.

    FORMÜL (HER bağlantılı bileşen için ayrı ayrı):
    V - E + F = 2 - 2*G  (G = genus, bu implementasyon SADECE genus-0/basit kabukları
    destekler — bir kabuğun kendi içinde delik/tünel içermesi hâlâ kapsam dışı, TEK
    değişiklik "kaç kabuk var" sorusunun artık 1'e sabitlenmemiş olması).

    KURAL:
    - Her edge tam 2 face'e ait olmalı (manifold) — TÜM Solid için, kabuk sınırı fark etmez.
    - Her loop kapalı olmalı.
    - Face'ler, paylaştıkları TopologyEdge'ler üzerinden bağlantılı bileşenlere (kabuklara)
      ayrılır (BFS); HER bileşen kendi başına V-E+F==2 sağlamalı.
    - Self-intersection kontrolü YOK (roadmap'in bilinen sınırlaması, değişmedi).

    NEDEN KOMŞULUK `edge.LeftFace`/`RightFace` ÜZERİNDEN DEĞİL, `Faces` LİSTESİ ÜZERİNDEN
    KURULUYOR (ilk yazımda GERÇEK bir regresyon yakalandı — `PlaneCutterTests`/`SolidSubtractorTests`
    başarısız oldu): `FaceSplitter`/`PlaneCutter` bir Face'i ikiye böldüğünde, KOMŞU Face'in
    paylaşılan kenarındaki `LeftFace`/`RightFace` alanı HER ZAMAN yeni (bölünmüş) Face'e
    yönlendirilmiyor olabilir — eski kod bu "stale" referanslara hiç bakmıyordu (V/E/F HEP
    `Faces` listesinden sayılıyordu). `LeftFace`/`RightFace` alanlarını komşuluk GRAFI için
    kullanmak, `Faces` listesinde ARTIK OLMAYAN "hayalet" Face'leri bileşene dahil edebiliyordu
    (yanlış V/E/F sayımı). Çözüm: komşuluk, HER zaman `Faces`'teki (authoritative) Face'lerin
    kendi `Loop.Edges`'inde HANGİ kenarları PAYLAŞTIĞINA bakılarak kurulur — `LeftFace`/
    `RightFace` alanları sadece manifold (2-face) kontrolünde kullanılır (değişmedi).
    */
    public bool IsValid()
    {
        var edges = GetEdges().ToList();

        // Manifold check: Her edge'in 2 face'i olmalı (kabuk sınırından BAĞIMSIZ, global).
        foreach (var edge in edges)
        {
            if (edge.LeftFace == null || edge.RightFace == null)
                return false;
        }

        // Loop closure check.
        foreach (var face in Faces)
        {
            foreach (var loop in face.Loops)
            {
                if (!loop.IsClosed())
                    return false;
            }
        }

        // Komşuluk grafiği: `Faces` listesindeki (authoritative) Face'ler, PAYLAŞTIKLARI
        // TopologyEdge nesneleri üzerinden bağlanır (stale LeftFace/RightFace alanlarına DEĞİL).
        var edgeOwners = new Dictionary<TopologyEdge, List<Face>>();
        foreach (var face in Faces)
        {
            foreach (var loop in face.Loops)
            {
                foreach (var edge in loop.Edges)
                {
                    if (!edgeOwners.TryGetValue(edge, out var owners))
                    {
                        owners = new List<Face>();
                        edgeOwners[edge] = owners;
                    }
                    owners.Add(face);
                }
            }
        }

        // Bağlantılı bileşenlere (kabuklara) ayır, HER birini kendi Euler karakteristiğiyle doğrula.
        var visited = new HashSet<Face>();
        foreach (var startFace in Faces)
        {
            if (!visited.Add(startFace))
                continue;

            var componentFaces = new List<Face>();
            var queue = new Queue<Face>();
            queue.Enqueue(startFace);

            while (queue.Count > 0)
            {
                var face = queue.Dequeue();
                componentFaces.Add(face);

                foreach (var loop in face.Loops)
                {
                    foreach (var edge in loop.Edges)
                    {
                        foreach (var neighbor in edgeOwners[edge])
                        {
                            if (!ReferenceEquals(neighbor, face) && visited.Add(neighbor))
                                queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            var componentVertices = new HashSet<Vertex>();
            var componentEdges = new HashSet<TopologyEdge>();
            foreach (var face in componentFaces)
            {
                foreach (var loop in face.Loops)
                {
                    foreach (var edge in loop.Edges)
                    {
                        componentEdges.Add(edge);
                        componentVertices.Add(edge.StartVertex);
                        componentVertices.Add(edge.EndVertex);
                    }
                }
            }

            int V = componentVertices.Count;
            int E = componentEdges.Count;
            int F = componentFaces.Count;

            if (V - E + F != 2)
                return false;
        }

        return true;
    }

    /*
    METOD ADI:
    GetVolume

    AMACI:
    Katı cismin hacmini hesaplamak.

    ALGORİTMA:
    Divergence theorem (Gauss), düzlemsel face'ler için:
    V = (1/3) * Σ (p · n̂) * A

    p = face üzerindeki HERHANGİ bir nokta (düzlemsel bir face'de p·n̂ tüm noktalar için
        sabittir — bu yüzden ilk vertex yeterli, gerçek centroid gerekmez)
    n̂ = BİRİM face normali
    A = face alanı

    NEDEN 1/3 (ESKİ KOD 1/6 KULLANIYORDU — HATALIYDI):
    Her face, orijinden face düzlemine kurulan bir piramidin tabanıdır; bu piramidin hacmi
    (1/3)*taban_alanı*yükseklik'tir (yükseklik = orijinin düzleme birim-normal boyunca
    izdüşümü = p·n̂). 1/6 çarpanı, üçgen mesh'lerde orijin+üçgenin 3 köşesinden oluşan
    tetrahedron hacmi formülünden (V=(1/6)|v1·(v2×v3)|) miras kalmış görünüyor; ama burada
    Normal ayrı bir alan (GetArea()) ile çarpılıyor, üçgen değil GERÇEK face alanı kullanılıyor
    — bu yüzden 1/6 kullanmak hacmi tam 2 kat küçük hesaplıyordu (bir kutu için doğrulanabilir:
    analitik hacim ile karşılaştırıldığında eski kod yarısını dönüyordu).
    */
    public double GetVolume()
    {
        double volume = 0;

        foreach (var face in Faces)
        {
            var area = face.GetArea();
            if (area < 1e-12) continue;

            var normal = face.Normal.Normalize();
            if (normal.LengthSquared() < 1e-12) continue;

            var loop = face.GetOuterLoop();
            var vertices = loop?.GetOrderedVertices();
            if (vertices == null || vertices.Count == 0) continue;

            var p = vertices[0].Position;

            double contribution = (p.X * normal.X + p.Y * normal.Y + p.Z * normal.Z) * area;
            volume += contribution;
        }

        return Math.Abs(volume / 3.0);
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

    /*
    METOD ADI:
    Clone

    AMACI:
    Bu Solid'in TAM (derin) bir kopyasını üretmek — CadEntity.Clone() (ör. COPY komutu)
    gibi domain-katmanı işlemlerin, kaynak Solid'in Vertex/TopologyEdge/Face grafiğini
    PAYLAŞMADAN bağımsız bir kopya üzerinde çalışabilmesi için (bkz. SolidEntity.Clone —
    Afney.Cad.Domain).

    NEDEN (kimlik-korumalı graf kopyası gerekir, basit "yeniden tessellate" YETERSİZ):
    Winged-Edge yapısında bir TopologyEdge/Vertex BİRDEN FAZLA Face/Loop tarafından
    PAYLAŞILIR (ör. bir kutunun bir kenarı tam 2 face'e ait). Kopyalama sırasında bu
    paylaşımın korunması gerekir — aksi halde klon, artık "aynı kenarı iki farklı Face
    kendi kopyasıyla" tutan, manifold OLMAYAN (Solid.IsValid()==false) bozuk bir graf
    olurdu. Bu yüzden Vertex/TopologyEdge/Face eşlemesi bir sözlük (identity map) ile
    TEK SEFER kopyalanır, sonraki her referans aynı klonu geri döner.
    */
    public Solid Clone(string? newName = null)
    {
        var vertexMap = new Dictionary<Vertex, Vertex>();
        var edgeMap = new Dictionary<TopologyEdge, TopologyEdge>();
        var faceMap = new Dictionary<Face, Face>();

        Vertex CloneVertex(Vertex v)
        {
            if (!vertexMap.TryGetValue(v, out var nv))
            {
                nv = new Vertex(v.Position);
                vertexMap[v] = nv;
            }
            return nv;
        }

        TopologyEdge CloneEdge(TopologyEdge e)
        {
            if (!edgeMap.TryGetValue(e, out var ne))
            {
                ne = new TopologyEdge(CloneVertex(e.StartVertex), CloneVertex(e.EndVertex));
                edgeMap[e] = ne;
            }
            return ne;
        }

        // 1. Geçiş: Face kabukları (Loop/Edge içeriğiyle) kur.
        foreach (var face in Faces)
        {
            var nf = new Face { Normal = face.Normal };
            faceMap[face] = nf;

            foreach (var loop in face.Loops)
            {
                var nl = new Loop(loop.IsOuter);
                foreach (var edge in loop.Edges)
                    nl.Edges.Add(CloneEdge(edge));
                nf.Loops.Add(nl);
            }
        }

        // 2. Geçiş: Winged-Edge komşuluk (Left/Right Face, Next/Prev) bağlantılarını kur.
        foreach (var (oldEdge, newEdge) in edgeMap)
        {
            newEdge.LeftFace = oldEdge.LeftFace != null && faceMap.TryGetValue(oldEdge.LeftFace, out var lf) ? lf : null;
            newEdge.RightFace = oldEdge.RightFace != null && faceMap.TryGetValue(oldEdge.RightFace, out var rf) ? rf : null;
            newEdge.NextLeftEdge = oldEdge.NextLeftEdge != null ? CloneEdge(oldEdge.NextLeftEdge) : null;
            newEdge.PrevLeftEdge = oldEdge.PrevLeftEdge != null ? CloneEdge(oldEdge.PrevLeftEdge) : null;
            newEdge.NextRightEdge = oldEdge.NextRightEdge != null ? CloneEdge(oldEdge.NextRightEdge) : null;
            newEdge.PrevRightEdge = oldEdge.PrevRightEdge != null ? CloneEdge(oldEdge.PrevRightEdge) : null;
        }

        var result = new Solid(newName ?? Name);
        foreach (var face in Faces)
            result.Faces.Add(faceMap[face]);

        return result;
    }
}
