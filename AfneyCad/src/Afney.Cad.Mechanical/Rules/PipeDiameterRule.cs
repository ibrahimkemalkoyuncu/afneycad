using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Rules;

public class PipeDiameterRule : IEngineeringRule
{
    public string RuleId => "STD-MECH-001";
    public string Name => "Boru Nominal Çap Kontrolü";
    public RuleCategory Category => RuleCategory.Standard;

    public ValidationResult Check(object entity)
    {
        if (entity is PipeEntity pipe)
        {
            // Örnek kural: Yangın tesisatı boruları minimum 50mm olmalıdır.
            if (pipe.SystemType == MechanicalSystemType.FireProtection && pipe.InnerDiameter < 50)
            {
                return ValidationResult.Failure(
                    "Yangın tesisatı boruları minimum 50mm çapında olmalıdır.",
                    RuleSeverity.Error,
                    "Yangın Yönetmeliği Bölüm 7",
                    pipe.Id);
            }
        }
        return ValidationResult.Success();
    }

    public bool AutoFix(object entity)
    {
        if (entity is PipeEntity pipe && pipe.SystemType == MechanicalSystemType.FireProtection && pipe.InnerDiameter < 50)
        {
            pipe.InnerDiameter = 50;
            return true;
        }
        return false;
    }
}
