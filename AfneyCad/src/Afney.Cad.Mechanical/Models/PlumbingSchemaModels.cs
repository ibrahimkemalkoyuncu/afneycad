using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Models;

/*
   NE: Kolon Şeması Topolojisi (RiserSchema)
   NEDEN: Gerçek 3D MEP modelinden soyutlanmış, mühendislik çizim standartlarına uygun 2D kolon şemasını temsil etmek için.

   NASIL (Mühendislik Detayı):
   - 1. Seviye: 'RiserSchema' (Tüm kolonun genel yapısı)
   - 2. Seviye: 'FloorSchema' (Kat bazlı ayrıştırma)
   - 3. Seviye: 'FixtureSchema' (Kattaki uç birimler ve bağlantı çapları)
*/
public class RiserSchema
{
    public string RiserName { get; set; } = "K-1"; // Kolon Numarası
    public List<FloorSchema> Floors { get; set; } = new();
    public double TotalFlowRate { get; set; } // Toplam debi (l/s)
    public double TotalLoadUnits { get; set; } // Toplam Yük Birimi (LU/FU)
    public double TotalPressureDrop { get; set; } // Toplam Basınç Kaybı (mSS)
}

public class FloorSchema
{
    public int FloorLevel { get; set; } // Zemin = 0, 1. Kat = 1
    public string FloorName { get; set; } = "Floor";
    public double Elevation { get; set; } // Kat Kotu (m)
    
    // Kattaki uç birimler (Lavabo, WC vb.)
    public List<FixtureSchema> Fixtures { get; set; } = new();
    
    // Kattaki ana branşman bilgileri
    public double BranchDiameter { get; set; }
    public double FloorTotalLU { get; set; } // Kattaki toplam yük
}

public class FixtureSchema
{
    public string Type { get; set; } = "WC";
    public double ConnectionDiameter { get; set; } = 110.0;
    public double FixtureUnit { get; set; }
    
    // Şema üzerindeki yerleşim sırası (Yatayda Collision engellemek için)
    public int OrderIndex { get; set; }
}
