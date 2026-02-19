using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Application.Core;

/*
   NE: CAD Veri Serileştirici (CadSerializer)
   NEDEN: Proje verilerini (dosyaya kaydetmek veya ağ üzerinden göndermek için) JSON formatına dönüştürmek ve geri okumak için.

   MÜHENDİSLİK DETAYI:
   - Polimorfik serileştirme (JSON Polymorphism) kullanarak farklı tipteki nesneleri (Boru, Vana, Çizgi) ortak bir tabanda yönetir.
   - "$type" belirteci (discriminator) sayesinde nesne tiplerini korur.
   - Proje dosyalarının (.afneycad) kaydedilip yüklenmesi için çekirdek bileşendir.
*/
public class CadSerializer
{
    private readonly JsonSerializerOptions _options;

    public CadSerializer()
    {
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        // Polimorfik yapılandırma
        _options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { RegisterCadPolymorphism }
        };
    }

    /*
       NE: Polimorfizm Kaydı (RegisterCadPolymorphism)
       NEDEN: JSON serileştirme sırasında farklı CAD nesnelerinin (Line, Pipe, Valve vb.) tip bilgilerini koruyarak, dosya yüklenirken her nesnenin kendi sınıfında doğru şekilde canlandırılmasını sağlamak için.
    */
    private void RegisterCadPolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(CadEntity))
        {
            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType,
                DerivedTypes = 
                {
                    new JsonDerivedType(typeof(LineEntity), "Line"),
                    new JsonDerivedType(typeof(CircleEntity), "Circle"),
                    new JsonDerivedType(typeof(LwPolylineEntity), "Polyline"),
                    // Mekanik Bileşenler
                    new JsonDerivedType(typeof(PipeEntity), "Pipe"),
                    new JsonDerivedType(typeof(Valve), "Valve"),
                    new JsonDerivedType(typeof(ElbowEntity), "Elbow"),
                    new JsonDerivedType(typeof(TeeEntity), "Tee")
                }
            };
        }
    }

    /*
       NE: Serileştir (Serialize)
       NEDEN: Bellekteki CAD nesnelerini JSON formatında bir metin dizisine dönüştürerek dosyaya kaydedilebilir hale getirmek için.
    */
    public string Serialize(IEnumerable<CadEntity> entities)
    {
        return JsonSerializer.Serialize(entities, _options);
    }

    /*
       NE: Geri Yükle (Deserialize)
       NEDEN: JSON formatındaki proje dosyasını okuyarak bellekte tekrar canlı CAD nesnelerine dönüştürmek için.
    */
    public List<CadEntity> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<List<CadEntity>>(json, _options) ?? new List<CadEntity>();
    }
}

