using System;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Rules.Plumbing;

/*
   NE: Klozet Çap Kontrolü (WCDiameterRule)
   NEDEN: Sıhhi tesisat yönetmelikleri (TS EN 12056) uyarınca, klozet (WC) bağlanan ve klozet yükü taşıyan hatların minimum DN 100 (110mm dış çap) olması zorunluluğunu denetlemek için.

   NASIL (Mühendislik Detayı):
   - Mete Bey: "Klozetin katı atık çıkışını sağlıklı tahliye edebilmesi için ana hat DN100'den küçük olamaz."
   - Kural: PipeEntity üzerindeki IsCarryingWCLoad bayrağı true ise ve çap < 100 ise hata döner.
*/
public class WCDiameterRule : IEngineeringRule
{
    public string RuleId => "STD-PLB-003";
    public string Name => "Klozet Bağlantı Çap Kontrolü";
    public RuleCategory Category => RuleCategory.Standard;

    public ValidationResult Check(object entity)
    {
        if (entity is PipeEntity pipe)
        {
            // Sadece Pis Su sistemi için geçerli
            if (pipe.SystemType != MechanicalSystemType.WasteWater)
                return ValidationResult.Success(pipe.Id);

            // Eğer boru klozet yükü taşıyorsa ve çapı DN 100'den küçükse
            if (pipe.IsCarryingWCLoad && pipe.InnerDiameter < 100)
            {
                return ValidationResult.Failure(
                    $"Klozet yükü taşıyan hat DN 100'den küçük olamaz! (Mevcut çap: DN{pipe.InnerDiameter})",
                    RuleSeverity.Error,
                    "TS EN 12056",
                    pipe.Id);
            }
        }

        return ValidationResult.Success();
    }

    public bool AutoFix(object entity)
    {
        if (entity is PipeEntity pipe && pipe.IsCarryingWCLoad && pipe.InnerDiameter < 100)
        {
            pipe.InnerDiameter = 100; // Standart minimum çap
            return true;
        }
        return false;
    }
}
