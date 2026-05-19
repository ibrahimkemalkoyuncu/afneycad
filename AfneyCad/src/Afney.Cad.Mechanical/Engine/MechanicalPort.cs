using System;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Engine;

/*
NE: Mekanik nesnelerin bağlantı noktası tanımı.

NEDEN VAR?
Bir borunun neresinden başka bir boruya bağlanabileceğini tanımlamak için.
Sadece koordinat (Point) yetmez; yön (Direction), tip (Flow),
çap (Diameter) ve malzeme (PipeMaterialType) gerekir.

MÜHENDİSLİK GEREKSİNİMİ:
- Çap bilgisi olmadan otomatik boru boyutlandırma ve
  uyumsuz çap uyarısı (Reducer gereksinimi) yapılamaz.
- Malzeme bilgisi olmadan pürüzlülük katsayısı (Colebrook-White)
  hesaplaması hatalı olur.
*/
public class MechanicalPort
{
    public Guid OwnerId { get; }
    public string Name { get; } // Örn: "ColdWater", "HotWater", "Drainage"
    public Vector3D Position { get; private set; }
    public Vector3D Direction { get; private set; } // Bağlantının dışa bakan normal vektörü
    public FlowDirection FlowType { get; set; } = FlowDirection.Bidirectional;

    // ── HİDROLİK METADATA ─────────────────────────────────────────
    /// <summary>
    /// Bağlantı noktasının nominal iç çapı (mm).
    /// 0 = Tanımsız / henüz belirlenmemiş.
    /// Örn: DN15=15.0, DN40=40.0, DN100=100.0
    /// </summary>
    public double Diameter { get; set; } = 0.0;

    /// <summary>
    /// Bu portta kullanılan / beklenen boru malzemesi.
    /// Pis su hatlarında PVC_SN4, sıcak su hatlarında PPRC_PN20 gibi.
    /// </summary>
    public PipeMaterial PipeMaterialType { get; set; } = PipeMaterial.Generic;

    // ── TOPOLOJİK DURUM ──────────────────────────────────────────
    public bool IsConnected { get; set; }
    public Guid? ConnectedEntityId { get; set; }
    public string? ConnectedPortName { get; set; }

    // ── CONSTRUCTOR'LAR ──────────────────────────────────────────

    /// <summary>Temel constructor — çap ve malzeme sonradan set edilir.</summary>
    public MechanicalPort(Guid ownerId, string name, Vector3D position, Vector3D direction)
    {
        OwnerId   = ownerId;
        Name      = name;
        Position  = position;
        Direction = direction.Normalize();
    }

    /// <summary>
    /// Tam constructor — çap ve malzeme dahil.
    /// FixtureLibraryService.CreateEntity() ve PipeEntity.GetPorts() tarafından kullanılır.
    /// </summary>
    public MechanicalPort(Guid ownerId, string name, Vector3D position, Vector3D direction,
                          double diameter, PipeMaterial material = PipeMaterial.Generic)
        : this(ownerId, name, position, direction)
    {
        Diameter        = diameter;
        PipeMaterialType = material;
    }

    public void UpdateGeometry(Vector3D newPosition, Vector3D newDirection)
    {
        Position  = newPosition;
        Direction = newDirection.Normalize();
    }
}

public enum FlowDirection
{
    In,
    Out,
    Bidirectional
}

