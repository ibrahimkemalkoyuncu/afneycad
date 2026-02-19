namespace Afney.Cad.Mechanical.Enums;

/*
    NE: Boru Malzeme Tipleri (PipeMaterial)
    NEDEN: Mühendislik hesaplarında (basınç kaybı, hız, iç çap) malzemenin pürüzlülük katsayısını ve standart çap serisini (SDR) belirlemek için.
*/
public enum PipeMaterial
{
    Generic = 0,        // Varsayılan (Tanımsız)
    PPRC_PN20,          // Polipropilen (Soğuk Su) - DIN 8077/8078 (SDR 11)
    PPRC_PN25,          // Polipropilen (Sıcak/Kompozit) - (SDR 6 / 7.4)
    PVC_SN4,            // Sert PVC (Pis Su) - TS EN 1329 / DIN 19531
    PEX_b,              // Cross-linked Polietilen (Mobil Sistem)
    Steel_Galvanized,   // Galvanizli Çelik (Yangın/Eski Tesisat)
    Silent_PP           // Sessiz Boru (3 Katmanlı - Mineral Takviyeli)
}
