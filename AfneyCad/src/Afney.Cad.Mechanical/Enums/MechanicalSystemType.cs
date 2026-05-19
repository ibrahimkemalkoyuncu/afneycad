using System;

namespace Afney.Cad.Mechanical.Enums;

/*
   NE: Mekanik Sistem Tipleri (MechanicalSystemType)
   NEDEN: Boruların kullanım amacına göre (Soğuk su, Sıcak su, Pis su vb.) ayrılması ve otomatik renklendirme için.
   
   AMACI:
   - Tesisatın fonksiyonunu tanımlar.
   - Hidrolik hesaplama kurallarını (hız limitleri, malzeme seçimi) belirler.
   - Görsel standartları (Renk, Katman) otomatik yönetir.
*/
public enum MechanicalSystemType
{
    Undefined,
    DomesticColdWater, // Temiz Soğuk Su
    DomesticHotWater,  // Temiz Sıcak Su
    WasteWater,        // Pis Su / Gider
    RainWater,         // Yağmur Suyu (Çatı Drenajı — TS EN 12056-3)
    Ventilation,       // Havalandırma
    FireProtection,    // Yangın Tesisatı
    Gas                // Doğalgaz
}
