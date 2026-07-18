namespace Afney.Cad.Mechanical.Enums;

/*
   NE: Damper (Kanal Klapesi) Tipleri (DamperType)
   NEDEN: Kanal hattı üzerine seri bağlanan debi/güvenlik elemanlarını sınıflandırmak için.

   STANDARTLAR:
   - EN 1751 (Volume Control Dampers)
   - EN 15650 / TS EN 1366-2 (Fire Dampers)
*/
public enum DamperType
{
    Volume,     // Debi Kontrol Klapesi (VCD)
    Fire,       // Yangın Damperi (72°C/95°C eriyen sigorta)
    Smoke,      // Duman Damperi (motorlu, BMS'ten kumandalı)
    FireSmoke,  // Kombine Yangın/Duman Damperi
    BackDraft   // Geri Tepme Klapesi (Çek Valf muadili)
}
