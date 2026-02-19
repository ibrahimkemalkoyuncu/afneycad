namespace Afney.Cad.Mechanical.Enums;

/*
    NE: Mahal Tipi (RoomType)
    NEDEN: Mimari mahal özelliklerini sınıflandırmak ve otomatik vitrifiye önerisi yapmak için.
*/
public enum RoomType
{
    Unknown = 0,    // Bilinmeyen / Tanımsız
    StandardRoom,   // Standart Yaşam Alanı (Salon, Yatak Odası)
    Kitchen,        // Mutfak
    Bathroom,       // Banyo (Islak Hacim)
    Toilet,         // WC
    UtilityRoom,    // Teknik Hacim / Çamaşır Odası
    Corridor        // Koridor / Hol
}
