using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Boşaltma Noktası (DrainageOutletEntity) — Rögar / Tahliye Çıkışı
   NEDEN: OtoNET'teki "Boşaltma Noktası" komutunun AfneyCAD karşılığı.
          Pis su ve yağmur suyu hatlarının bina dışına (rögara) bağlandığı son noktayı temsil eder.

   MÜHENDİSLİK DETAYI:
   - Pis Su: Zemin kattaki tüm kolonların birleştiği ve binayı terk ettiği nokta.
   - Yağmur Suyu: Yağmur kolonlarının doğrudan yere boşaldığı uç nokta.
   - HydraulicNetwork hesaplamasında "Sink" (alıcı) düğüm olarak işaretlenir.
   - Tesisatı Kabul Et doğrulamasında her WasteWater ve RainWater ağında
     en az bir DrainageOutletEntity olması zorunludur.
*/
public class DrainageOutletEntity : MechanicalEntity
{
    public enum OutletType
    {
        SewerManhole,   // Rögar — pis su bağlantısı
        RainDrain,      // Yağmur suyu zemine boşaltma
        Septic          // Fosseptik / arıtma
    }

    private OutletType _outletType;
    private double _invertLevel;  // Kanal taban kotu (metre)
    private string _label = "";

    public OutletType Type
    {
        get => _outletType;
        set { _outletType = value; OnMetadataChanged(); }
    }

    // Kanal taban kotu — zemin ise 0.0, bodrum çıkışı ise negatif
    public double InvertLevel
    {
        get => _invertLevel;
        set { _invertLevel = value; OnMetadataChanged(); }
    }

    public string Label
    {
        get => _label;
        set { _label = value; OnMetadataChanged(); }
    }

    public DrainageOutletEntity(Vector3D position, OutletType type = OutletType.SewerManhole)
    {
        Position = position;
        _outletType = type;
        SystemType = type == OutletType.RainDrain
            ? MechanicalSystemType.RainWater
            : MechanicalSystemType.WasteWater;
    }

    public override IEnumerable<MechanicalPort> GetPorts()
    {
        // Tek giriş portu — boşaltma noktaları alıcıdır, çıkış vermez
        yield return new MechanicalPort
        {
            Position = Position,
            Direction = new Vector3D(0, -1, 0),
            PortType = MechanicalPort.PortKind.Inlet,
            NominalDiameter = InnerDiameter
        };
    }

    public override void Draw(Afney.Cad.Domain.Abstractions.IRenderContext ctx)
    {
        // Rögar sembolü: çarpı içinde daire (standart sıhhi tesisat paftası sembolü)
        float r = 0.15f;
        var col = SystemType == MechanicalSystemType.RainWater
            ? new SkiaSharp.SKColor(0, 150, 255)   // Mavi — yağmur suyu
            : new SkiaSharp.SKColor(139, 90, 43);  // Kahverengi — pis su

        ctx.DrawCircle((float)Position.X, (float)Position.Y, r, col, filled: false);
        ctx.DrawLine(
            (float)(Position.X - r), (float)Position.Y,
            (float)(Position.X + r), (float)Position.Y, col, 1.5f);
        ctx.DrawLine(
            (float)Position.X, (float)(Position.Y - r),
            (float)Position.X, (float)(Position.Y + r), col, 1.5f);

        if (!string.IsNullOrEmpty(_label))
            ctx.DrawText(_label, (float)(Position.X + r + 0.05), (float)Position.Y, 10f, col);
    }

    public override CadEntity Clone()
    {
        return new DrainageOutletEntity(Position, _outletType)
        {
            InnerDiameter = InnerDiameter,
            InvertLevel = _invertLevel,
            Label = _label,
            LayerId = LayerId
        };
    }
}
