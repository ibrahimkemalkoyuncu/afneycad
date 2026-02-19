using System;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Engine;

/*
NE:
Mekanik nesnelerin bağlantı noktası tanımı.

NEDEN VAR?
Bir borunun neresinden başka bir boruya bağlanabileceğini tanımlamak için.
Sadece koordinat (Point) yetmez, yön (Direction) ve tip (Flow) gerekir.

NEREDE KULLANILIR?
MechanicalEntity türevlerinde ve TopologyEngine içinde.

NE ZAMAN ÇALIŞIR?
Bağlantı kontrolü (ConnectionRule) ve Routing sırasında.
*/
public class MechanicalPort
{
    public Guid OwnerId { get; }
    public string Name { get; } // Örn: "Inlet", "Outlet", "Port1"
    public Vector3D Position { get; private set; }
    public Vector3D Direction { get; private set; } // Bağlantının dışa bakan normal vektörü
    public FlowDirection FlowType { get; set; } = FlowDirection.Bidirectional;
    
    // Topolojik durum
    public bool IsConnected { get; set; }
    public Guid? ConnectedEntityId { get; set; }
    public string? ConnectedPortName { get; set; }

    public MechanicalPort(Guid ownerId, string name, Vector3D position, Vector3D direction)
    {
        OwnerId = ownerId;
        Name = name;
        Position = position;
        Direction = direction.Normalize(); // Her zaman birim vektör
    }

    public void UpdateGeometry(Vector3D newPosition, Vector3D newDirection)
    {
        Position = newPosition;
        Direction = newDirection.Normalize();
    }
}

public enum FlowDirection
{
    In,
    Out,
    Bidirectional
}
