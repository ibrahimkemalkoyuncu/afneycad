using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Kritik Hat Segmenti (CriticalPathSegment)
    NEDEN: Basınç kaybı raporunda her bir boru parçasının teknik detaylarını göstermek için.
*/
public class CriticalPathSegment
{
    public string PipeId { get; set; } = string.Empty;
    public double Diameter { get; set; }
    public double Length { get; set; }
    public double FlowRate { get; set; }
    public double Velocity { get; set; }
    public double PressureDrop { get; set; } // mSS
    public double StaticHead { get; set; } // mSS
    public double CumulativeLoss { get; set; } // mSS
}

/*
    NE: Kritik Hat Rapor Modeli (CriticalPathReport)
    NEDEN: Mühendislik hesap raporu çıktısı için veri yapısı sunmak.
*/
public class CriticalPathReport
{
    public string SystemType { get; set; } = string.Empty;
    public List<CriticalPathSegment> Segments { get; set; } = new();
    public double TotalLinearLoss { get; set; } // mSS
    public double StaticHead { get; set; } // mSS
    public double RequiredResidualPressure { get; set; } // mSS
    public double TotalPressureRequired { get; set; } // mSS
    public double MaxVelocity { get; set; }
    public string DisadvantagedFixture { get; set; } = "Unknown";
}
