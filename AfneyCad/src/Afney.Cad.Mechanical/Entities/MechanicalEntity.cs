using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine; 
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Mekanik Nesne Soyutlaması (MechanicalEntity)
   NEDEN: Boru, vana, dirsek gibi mekanik sistem bileşenlerinin ortak mühendislik özelliklerini (Çap, Malzeme, Sistem Tipi) yönetmek için.

   NASIL (Mühendislik Detayı):
   - Tüm mekanik bileşenlerin bir hidrolik sisteme (Soğuk Su, Pis Su vb.) ait olduğunu varsayar.
   - Akış hesaplamaları için kritik olan iç çap (InnerDiameter) ve malzeme pürüzlülüğü bilgilerini taşır.
   - 'GetPorts' metodu ile topolojik bağlantı noktalarını tanımlayarak bir ağ (Network) oluşturulmasına imkan sağlar.
*/
public abstract class MechanicalEntity : CadEntity
{
    // NE: Sistem Tipi
    // NEDEN: Borunun hangi akışkan sınıfına (Sıhhi, Yangın, Gaz vb.) ait olduğunu belirlemek için.
    public MechanicalSystemType SystemType { get; set; } = MechanicalSystemType.Undefined;

    // NE: İç Çap (mm)
    // NEDEN: Hidrolik hesaplamalarda (Hız, Basınç Kaybı) nominal çap yerine gerçek akış kesitini kullanmak için.
    public double InnerDiameter { get; set; } = 50.0;

    // NE: Malzeme
    // NEDEN: Malzemenin pürüzlülük katsayısı (Roughness) üzerinden sürtünme kaybı hesaplamak için.
    // NE: Malzeme
    // NEDEN: Malzemenin pürüzlülük katsayısı (Roughness) üzerinden sürtünme kaybı hesaplamak için.
    public PipeMaterial PipeMaterialType { get; set; } = PipeMaterial.Generic;

    // NE: AutoCAD Blok İsmi
    public string BlockName { get; set; } = string.Empty;

    // NE: Mekanik Nesne Tipi
    public MechanicalEntityType EntityType { get; set; } = MechanicalEntityType.Undefined;

    // NE: Bağlantı Portları
    public abstract List<MechanicalPort> GetPorts();
}


