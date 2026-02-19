using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Standards;

/*
   NE: Boru Standardı Modeli (PipeStandard)
   NEDEN: Farklı malzeme ve normlardaki boruların fiziksel ve hidrolik özelliklerini merkezi olarak tanımlamak için.
   
   NASIL (Mebrure Hanım):
   - Malzeme (Material): PVC, PPRC, Galvaniz, Çelik vb.
   - Norm (Standard): TS EN 12056, DIN 1988 vb.
   - Katalog: Boru çaplarını (DN), dış çaplarını ve et kalınlıklarını içeren liste.
*/
public class PipeStandard
{
    public string Material { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    
    // DN -> Boru Detayları
    public List<PipeDefinition> AvailableSizes { get; set; } = new();

    public PipeDefinition? GetBySize(double dn)
    {
        return AvailableSizes.Find(x => Math.Abs(x.DN - dn) < 0.1);
    }
}

public record PipeDefinition(double DN, double OuterDiameter, double WallThickness)
{
    public double InnerDiameter => OuterDiameter - (2 * WallThickness);
}
