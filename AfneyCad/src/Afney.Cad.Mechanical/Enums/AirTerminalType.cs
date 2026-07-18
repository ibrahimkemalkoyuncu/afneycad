namespace Afney.Cad.Mechanical.Enums;

/*
   NE: Hava Terminal Ünitesi Tipleri (AirTerminalType)
   NEDEN: Kanal sisteminin son elemanlarını (menfez ailesini) sınıflandırmak için.

   STANDARTLAR:
   - ASHRAE Handbook — HVAC Applications, Ch. 20 (Space Air Diffusion)
   - VDI 2081 (Gürültü)
*/
public enum AirTerminalType
{
    Unknown,
    SupplyDiffuser,     // Tavan Difüzörü (besleme)
    ReturnGrille,       // Dönüş Menfezi
    ExhaustGrille,      // Egzoz Menfezi
    Louver,              // Panjur (dış hava alma/atma)
    LinearSlot,          // Lineer Yarık Difüzör
    JetNozzle,           // Jet Nozul (yüksek hacimli mekanlar)
    FloorDiffuser         // Zemin Difüzörü
}
