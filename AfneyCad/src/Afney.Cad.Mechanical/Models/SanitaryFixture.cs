using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Vitrifiye Tipleri (SanitaryFixtureType)
    NEDEN: Projedeki cihazları sınıflandırmak ve yük hesabı yapmak için.
*/
public enum SanitaryFixtureType
{
    Unknown,        // Bilinmeyen
    Lavatory,       // Lavabo
    WC,             // Klozet (Alafranga)
    Shower,         // Duş Teknesi
    Bathtub,        // Küvet
    Urinal,         // Pisuvar
    Sink,           // Eviye (Mutfak)
    FloorDrain,     // Yer Süzgeci
    WashingMachine, // Çamaşır Makinesi
    DishWasher,     // Bulaşık Makinesi
    Bidet           // Bide
}

/*
    NE: Vitrifiye Nesnesi (SanitaryFixture)
    NEDEN: Bir mahal içindeki tespit edilen cihazı temsil eder.
    DETAY: Blok adı, konumu ve mekanik özellikleri (Yük Birimi, Debi) içerir.
*/
public class SanitaryFixture
{
    public SanitaryFixtureType Type { get; set; }
    public string BlockName { get; set; } = string.Empty;
    public Vector3D Location { get; set; }
    
    // Adet (Genelde 1, ama grup bloksa artabilir)
    public int Count { get; set; } = 1;
    
    // Yük Birimi (LU - Load Unit) - Tesisat Hesabı İçin
    public double LoadUnit { get; set; } 
    
    // Anlık Debi (l/s)
    public double FlowRate { get; set; } 
}
