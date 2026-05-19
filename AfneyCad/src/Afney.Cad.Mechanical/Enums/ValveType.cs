namespace Afney.Cad.Mechanical.Enums;

/*
    NE: Vana Tipleri (ValveType)
    NEDEN: Tesisat ağındaki kontrol elemanlarını (Kesme, Düzenleme, Emniyet) sınıflandırmak için.
    
    STANDARTLAR:
    - TS 3148 (Küresel Vanalar)
    - TS EN 1074 (Su Temini Vanaları)
*/
public enum ValveType
{
    Unknown,
    
    // Kesme Vanaları
    GateValve,          // Sürgülü Vana
    BallValve,          // Küresel Vana
    ButterflyValve,     // Kelebek Vana
    
    // Emniyet ve Kontrol
    CheckValve,         // Çek Valf (Geri Dönüşsüz)
    PRV,                // Pressure Reducing Valve (Basınç Düşürücü)
    SafetyValve,        // Emniyet Ventili
    
    // Servis Elemanları
    Filter,             // Pislik Tutucu
    Strainer,           // Filtre
    AngleValve,         // Taharet / Ara Musluk
    RadiatorValve,      // Radyatör Vanası
    ThermostaticValve   // Termostatik Radyatör Vanası
}
