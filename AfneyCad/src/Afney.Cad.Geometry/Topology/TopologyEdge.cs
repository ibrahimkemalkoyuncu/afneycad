using Afney.Cad.Geometry.Advanced;

namespace Afney.Cad.Geometry.Topology;

/*
NE:
TopologyEdge - Winged-Edge data structure.

NE İÇİN:
Solid modeling için edge-based navigation.

NEREDE:
BREP Topology Model.

NE ZAMAN:
3D solid operations (Boolean, sweep, fillet).

AMAÇ:
AutoCAD/Revit seviyesinde topology yönetimi.
Her edge komşu face'leri ve vertex'leri bilir.

REFERANS:
Baumgart, B. (1974) - Winged-Edge Polyhedron Representation
*/
public class TopologyEdge
{
    public Guid Id { get; }
    
    // Geometry
    public Vertex StartVertex { get; set; }
    public Vertex EndVertex { get; set; }
    public NURBSCurve? GeometryCurve { get; set; } // Opsiyonel: Eğri geometrisi
    
    // Winged-Edge: Her edge 2 face'e komşu
    public Face? LeftFace { get; set; }  // Sol taraftaki yüzey
    public Face? RightFace { get; set; } // Sağ taraftaki yüzey
    
    // Winged-Edge: Her face için next/prev edge'ler
    public TopologyEdge? NextLeftEdge { get; set; }
    public TopologyEdge? PrevLeftEdge { get; set; }
    public TopologyEdge? NextRightEdge { get; set; }
    public TopologyEdge? PrevRightEdge { get; set; }

    public TopologyEdge(Vertex start, Vertex end)
    {
        Id = Guid.NewGuid();
        StartVertex = start;
        EndVertex = end;
        
        // Adjacency güncelle
        start.Edges.Add(this);
        end.Edges.Add(this);
    }

    /*
    METOD ADI:
    GetNextEdge

    AMACI:
    Belirli bir face için sıradaki edge'i bulmak.

    KULLANIM:
    Loop traversal (face boundary dolaşma).
    */
    public TopologyEdge? GetNextEdge(Face face)
    {
        if (face == LeftFace)
            return NextLeftEdge;
        else if (face == RightFace)
            return NextRightEdge;
        
        return null;
    }

    public bool IsBoundary => LeftFace == null || RightFace == null;
}
