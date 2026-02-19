using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Rules.Hydraulic;

/*
NE:
Maksimum Akış Hızı Kuralı.

NE İÇİN:
Boru çaplarının debiye uygunluğunu kontrol etmek.

STANDART:
TS 3242 / DIN 1988
- Su hızı 2.0 m/s'yi geçmemeli (ses ve erozyon riski).
- Emme hattında 1.0 m/s.
*/
public class MaxVelocityRule : IEngineeringRule
{
    public string RuleId => "HYD-001";
    public string Name => "Maksimum Akış Hızı Kontrolü";
    public RuleCategory Category => RuleCategory.Hydraulic;

    private const double MaxVelocityLimit = 2.0; // m/s
    private const double WarningLimit = 1.5;     // m/s

    public ValidationResult Check(object entity)
    {
        if (entity is not PipeEntity pipe)
            return ValidationResult.Success();

        // Akış hızı hesabı
        double velocity = pipe.GetVelocity();

        if (velocity > MaxVelocityLimit)
        {
            return ValidationResult.Failure(
                $"Akış hızı ({velocity:F2} m/s), izin verilen sınırı ({MaxVelocityLimit} m/s) aşıyor.",
                RuleSeverity.Error,
                "TS 3242",
                pipe.Id
            );
        }

        if (velocity > WarningLimit)
        {
            return ValidationResult.Failure(
                $"Akış hızı ({velocity:F2} m/s) sınıra yaklaşıyor. Ses problemi oluşabilir.",
                RuleSeverity.Warning,
                "DIN 1988",
                pipe.Id
            );
        }

        return ValidationResult.Success(pipe.Id);
    }

    public bool AutoFix(object entity)
    {
        if (entity is PipeEntity pipe && pipe.GetVelocity() > MaxVelocityLimit)
        {
            // Boru çapını bir boyut büyütüyoruz (Basitleştirilmiş fix)
            pipe.InnerDiameter += 5; 
            return true;
        }
        return false;
    }
}
