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
    // NE: Metadata Değişiklik Olayı
    // NEDEN: Reaktif hesaplama (Reactive calculation) motorunu tetiklemek için.
    public event Action<MechanicalEntity>? MetadataChanged;

    // NE: Hesaplama sırasında Metadata event'lerini baskıla
    // NEDEN: AutoSizePipes içinde InnerDiameter atandığında MetadataChanged
    //        tetiklenmemeli — aksi halde InvalidOperationException (collection-modified) oluşur.
    public bool SuppressMetadataEvents { get; set; } = false;

    protected void OnMetadataChanged()
    {
        if (SuppressMetadataEvents) return; // Hesaplama sırasında es geç
        IsCalculationUpToDate = false;
        MetadataChanged?.Invoke(this);
    }

    private MechanicalSystemType _systemType = MechanicalSystemType.Undefined;
    private double _innerDiameter = 50.0;
    private PipeMaterial _pipeMaterialType = PipeMaterial.Generic;
    private double _insulationThickness = 0.0;

    // NE: Sistem Tipi
    // NEDEN: Borunun hangi akışkan sınıfına (Sıhhi, Yangın, Gaz vb.) ait olduğunu belirlemek için.
    public MechanicalSystemType SystemType
    {
        get => _systemType;
        set { if (_systemType != value) { _systemType = value; OnMetadataChanged(); } }
    }

    // NE: İç Çap (mm)
    // NEDEN: Hidrolik hesaplamalarda (Hız, Basınç Kaybı) nominal çap yerine gerçek akış kesitini kullanmak için.
    public double InnerDiameter
    {
        get => _innerDiameter;
        set { if (Math.Abs(_innerDiameter - value) > 0.001) { _innerDiameter = value; OnMetadataChanged(); } }
    }

    // NE: Malzeme
    // NEDEN: Malzemenin pürüzlülük katsayısı (Roughness) üzerinden sürtünme kaybı hesaplamak için.
    // NE: Malzeme
    // NEDEN: Malzemenin pürüzlülük katsayısı (Roughness) üzerinden sürtünme kaybı hesaplamak için.
    public PipeMaterial PipeMaterialType
    {
        get => _pipeMaterialType;
        set { if (_pipeMaterialType != value) { _pipeMaterialType = value; OnMetadataChanged(); } }
    }

    // NE: AutoCAD Blok İsmi
    public string BlockName { get; set; } = string.Empty;

    // NE: Mekanik Nesne Tipi
    public MechanicalEntityType EntityType { get; set; } = MechanicalEntityType.Undefined;

    // NE: Hesap Geçerlilik Durumu (Dirty Flag)
    // NEDEN: Bu parça veya bağlı olduğu ağ değiştiğinde, üzerinde yazan hesap değerlerinin (Hız, Çap vb.) geçersiz/eski (Dirty) olduğunu UI'a bildirmek için.
    public bool IsCalculationUpToDate { get; set; } = true;

    // NE: Çap Kilidi (Size Override Lock)
    // NEDEN: Kullanıcı otomatik hesaplanan çapı (örn. Ø50) manuel olarak (örn. Ø75) değiştirdiğinde, motorun sonraki hesaplamalarda bu çapa dokunmamasını sağlamak için.
    public bool IsSizeLocked { get; set; } = false;

    // NE: Dış İzolasyon Kalınlığı (mm)
    // NEDEN: Boru dış çapına eklenen bu kalınlık, hem 3D modellemede (IFC) hem de mimari elemanlarla çakışma (Clash) analizinde gerçek hacmi belirler.
    public double InsulationThickness
    {
        get => _insulationThickness;
        set { if (Math.Abs(_insulationThickness - value) > 0.001) { _insulationThickness = value; OnMetadataChanged(); } }
    }

    // NE: Bağlantı Portları
    public abstract List<MechanicalPort> GetPorts();

    // NE: Mühendislik Uyarısı (Engineering Warning)
    // NEDEN: Otomatik seçim/hesaplama sırasında standart dışı bir durum (örn: standart olmayan dirsek açısı)
    //        tespit edildiğinde, bu bilgiyi Debug çıktısına gömmek yerine nesnenin üzerinde taşıyarak
    //        UI/BOM raporlarında kullanıcıya gösterilebilir hale getirmek için.
    public string? EngineeringWarning { get; set; }
}
