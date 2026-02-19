using System;
using System.Collections.Generic;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Models;

/*
   NE: Kat/Seviye Tanımı (MepLevel)
   NEDEN: Binadaki kat yüksekliklerini (Elevation) ve kat planlarını MEP ağında ayrıştırmak için. (Suggestion 17)
   
   BIM-Lite: FineSANI ve AutoCAD MEP mantığında, katlar sadece çizim düzlemi değil, nesnelerin ait olduğu uzamsal konteynerlardır.
*/
public class MepLevel
{
    public string Name { get; set; } = "Floor 0";
    public double Elevation { get; set; }
    public double Height { get; set; } = 3000.0; 

    // --- BIM-Lite Hiyerarşisi ---
    public List<MahalEntity> Rooms { get; } = new();
    public List<MechanicalEntity> Entities { get; } = new();

    public MepLevel(string name, double elevation, double height = 3000.0)
    {
        Name = name;
        Elevation = elevation;
        Height = height;
    }
}
