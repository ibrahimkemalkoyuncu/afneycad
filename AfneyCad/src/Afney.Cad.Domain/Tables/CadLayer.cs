using System;

namespace Afney.Cad.Domain.Tables;

/*
NE:
Katman Tanımı (Layer Definition).

NE İÇİN:
Nesneleri mantıksal gruplara ayırmak, renk ve görünürlüklerini topluca yönetmek için.

NEREDE:
Domain / Tables katmanında.

AMAÇ:
AutoCAD Layer Tablosu standardını karşılamak (Name, Color, Visible, Locked).
*/
public class CadLayer
{
    public string Name { get; set; } = "0";
    public uint Color { get; set; } = 0xFFFFFFFF; // Varsayılan Beyaz
    public bool IsVisible { get; set; } = true;
    public bool IsFrozen { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public double LineWeight { get; set; } = 1.0;

    public CadLayer(string name)
    {
        Name = name;
    }
}
