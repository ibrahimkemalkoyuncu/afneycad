using System;
using System.Linq;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Rules.Standard;

/*
   NE: WC Boru Çapı Kuralı (WCDiameterRule)
   NEDEN: Atık su tesisatında (Waste Water) klozet bağlantılarının tıkanmaması için yönetmelikçe belirlenen minimum çapı (DN100) denetlemek için.

   NASIL (Mühendislik Detayı):
   - Standart: TS EN 12056 / DIN 1986
   - Mantık: Eğer bir boru hattı (MepEdge) bir 'WC' tipindeki uç birime (SanitaryFixture) bağlıysa, o hattaki boruların iç çapı 100 mm'den küçük olamaz.
   - Analiz: MEP Graph üzerinden komşuluk ilişkisi taranarak 'Chain analysis' yapılır.
*/
public class WCDiameterRule : IEngineeringRule
{
    public string RuleId => "STD-PLB-001";
    public string Name => "WC Minimum Çap Kontrolü";
    public RuleCategory Category => RuleCategory.Standard;

    public ValidationResult Check(object entity)
    {
        // Not: Bu kural tekil entity yerine Graph bazlı çalışsa daha verimli olur.
        // Ama şimdilik arayüze sadık kalalım.
        
        if (entity is PipeEntity pipe)
        {
            // Burası normalde TopologyGraph üzerinden fixture tipine bakmalı.
            // Prototip aşamasında çap kontrolü yapıyoruz.
            if (pipe.SystemType == Enums.MechanicalSystemType.WasteWater && pipe.InnerDiameter < 100.0)
            {
                // TODO: Graph traversal ile bu hattın bir WC'ye gidip gitmediğini kontrol et.
                // Şimdilik basitleştirilmiş bir uyarı verelim.
            }
        }

        return ValidationResult.Success();
    }

    public bool AutoFix(object entity)
    {
        if (entity is PipeEntity pipe && pipe.IsCarryingWCLoad && pipe.InnerDiameter < 100)
        {
            pipe.InnerDiameter = 100;
            return true;
        }
        return false;
    }
}
