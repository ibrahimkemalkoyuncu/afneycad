using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Manuel Yük Noktası (MechanicalLoadNode)
   NEDEN: Vitrifiye (lavabo vb.) tanımlamadan, sistemin belirli bir noktasına 
          doğrudan mühendislik yükü (Load Unit) enjekte etmek için.
   
   MÜHENDİSLİK GEREKSİNİMİ:
   - "Zayıf Mimari" eleştirisini gidermek için: Vitrifiye sembolü şart olmamalı.
   - Tasarımcı "Bu uca 10 LU yük gelecek" diyebilmeli.
   - Topoloji grafında bir 'Sink' (Tüketim Noktası) olarak görev yapar.
*/
public class MechanicalLoadNode : MechanicalEntity
{
    // NE: Yerleşim Noktası
    public Vector3D Position { get; set; }

    // NE: Enjekte Edilen Yük (Fixture Unit / Load Unit)
    // Örn: TS 1258 standartlarına göre 2.5 LU gibi.
    public double LoadUnits { get; set; } = 1.0;

    // NE: Bağlantı Çapı ve Malzemesi
    public double NominalDiameter { get; set; } = 15.0; // Varsayılan DN15
    public PipeMaterial Material { get; set; } = PipeMaterial.Generic;

    public MechanicalLoadNode(Vector3D position, double loadUnits = 1.0)
    {
        Position = position;
        LoadUnits = loadUnits;
        EntityType = MechanicalEntityType.SanitaryFixture; // Hesaplayıcı 'Sink' olarak görsün diye
    }

    /*
       NE: Bağlantı Portunu Getir (GetPorts)
       NEDEN: Yükün sisteme bağlanacağı tek bir port sunmak için.
    */
    public override List<MechanicalPort> GetPorts()
    {
        return new List<MechanicalPort>
        {
            new MechanicalPort(Id, "LoadInlet", Position, Vector3D.ZAxis, NominalDiameter, Material)
            {
                FlowType = FlowDirection.In // Akış bu noktaya gelir (Sink)
            }
        };
    }

    /*
       NE: Görsel Temsil (Draw)
       NEDEN: Ekranda küçük bir ok veya "L" (Load) sembolü göstermek için.
    */
    public override void Draw(IRenderContext context)
    {
        uint color = IsSelected ? 0xFFFFFFFF : 0xFF00FFBC; // Neon yeşil/mavi (Mühendislik rengi)
        double size = 150.0;
        
        // Küçük bir "Yük" sembolü (Daire içinde ok)
        context.DrawCircle(Position, size / 2, color, 1.0);
        context.DrawLine(Position + new Vector3D(0, size, 0), Position, color, 1.5); // Giriş oku
        
        // Etiket
        context.DrawText($"LOAD: {LoadUnits} LU", Position + new Vector3D(size, size, 0), 0, 10, color);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double s = 100.0;
        return new CadBoundingBox(Position - new Vector3D(s, s, s), Position + new Vector3D(s, s, s));
    }

    public override void Move(Vector3D delta) => Position += delta;
    public override void Transform(Matrix4x4 matrix) => Position = matrix.Transform(Position);

    public override CadEntity Clone()
    {
        return new MechanicalLoadNode(Position, LoadUnits)
        {
            Id = Guid.NewGuid(),
            NominalDiameter = this.NominalDiameter,
            Material = this.Material,
            Color = this.Color,
            Layer = this.Layer,
            SystemType = this.SystemType
        };
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Connection);
    }
}
