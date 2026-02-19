namespace Afney.Cad.Mechanical.Enums;

/*
    NE: Bina Tipi (BuildingType)
    NEDEN: Hidrolik hesaplamalarda kullanılan debi katsayılarını (a, b, c) ve eş zamanlılık faktörünü (K) binanın kullanım amacına göre belirlemek için.
*/
public enum BuildingType
{
    Residential,        // Konutlar (Standart)
    Hotel,              // Oteller (Yüksek eş zamanlılık)
    Hospital,           // Hastaneler (Yüksek güvenlik ve sürekli kullanım)
    Office,             // Ofisler
    School,             // Okullar
    Industrial,         // Endüstriyel Yapılar
    PublicArea          // Kamusal Alanlar (Max eş zamanlılık)
}
