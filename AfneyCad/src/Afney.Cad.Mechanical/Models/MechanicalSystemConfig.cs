using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Standards;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Sistem Konfigürasyonu (MechanicalSystemConfig)
    NEDEN: FineSANI Step 3 (Sistem Türü Seçimi) aşamasını gerçekleştirmek için.
    
    ÖZELLİKLER:
    - Her sistem tipi (Temiz Su, Pis Su vb.) için özel malzeme ve standart eşleşmesi sağlar.
    - Tasarım parametrelerini (Basınç, Sıcaklık) saklar.
*/
public class MechanicalSystemConfig
{
    public MechanicalSystemType SystemType { get; set; }
    public string MaterialName { get; set; } = "PPRC";
    public string PipeStandard { get; set; } = "DIN 1988";
    
    // Tasarım Parametreleri
    public double DesignPressure { get; set; } = 4.0; // bar
    public double DesignTemperature { get; set; } = 20.0; // Celsius
    
    // Görsel Ayarlar
    public uint SystemColor { get; set; }
    
    public MechanicalSystemConfig(MechanicalSystemType type)
    {
        SystemType = type;
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        switch (SystemType)
        {
            case MechanicalSystemType.DomesticColdWater:
                SystemColor = 0xFF0000FF; // MAVİ
                MaterialName = "PPRC";
                PipeStandard = "DIN 1988";
                break;
            case MechanicalSystemType.DomesticHotWater:
                SystemColor = 0xFFFF0000; // KIRMIZI
                MaterialName = "PPRC";
                PipeStandard = "DIN 1988";
                break;
            case MechanicalSystemType.WasteWater:
                SystemColor = 0xFFFF8C00; // TURUNCU
                MaterialName = "PVC";
                PipeStandard = "TS EN 12056";
                break;
            default:
                SystemColor = 0xFFFFFFFF; // BEYAZ
                break;
        }
    }
}
