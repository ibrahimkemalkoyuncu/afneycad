using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Proje Üst Verisi (ProjectMetadata)
    NEDEN: FineSANI'deki "Proje Tanımlama" (Step 1) aşamasını gerçekleştirmek için.
    
    PARAMETRELER:
    - Proje Adı, Firma, Türü (Konut, Hastane vb.)
    - Hesaplama Standartları (TS, DIN, EN)
*/
public class ProjectMetadata
{
    public string ProjectName { get; set; } = "Yeni MEP Projesi";
    public string CompanyName { get; set; } = "Afney Mühendislik";
    public string DesignerName { get; set; } = "Mete Bey";
    
    // Bina Tipi (Kemal Bey'in isteği üzerine Otel, Hastane vb.)
    public BuildingType BuildingType { get; set; } = BuildingType.Residential;
    
    // Hesaplama Standardı
    public string CalculationStandard { get; set; } = "DIN 1988 / TS 1258";

    public DateTime CreationDate { get; set; } = DateTime.Now;
}
