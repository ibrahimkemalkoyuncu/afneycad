namespace Afney.Cad.Mechanical.Rules;

/*
NE:
Mühendislik kural tanımları ve sonuçları.

NE İÇİN:
FineSANI seviyesinde validasyon raporlama.

NEREDE:
Mechanical Engine - Rule System.

NE ZAMAN:
Proje validasyonunda, gerçek zamanlı çizimde.
*/

public enum RuleSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum RuleCategory
{
    Hydraulic,  // Basınç, Debi
    Geometric,  // Çakışma, eğim
    Standard,   // Yönetmelik
    Cost        // Bütçe
}

public class ValidationResult
{
    public bool IsValid { get; }
    public RuleSeverity Severity { get; }
    public string Message { get; }
    public string StandardReference { get; } // "TS 3242 Madde 5.1"
    public Guid EntityId { get; }

    private ValidationResult(bool isValid, string message, RuleSeverity severity, string standardRef, Guid entityId)
    {
        IsValid = isValid;
        Message = message;
        Severity = severity;
        StandardReference = standardRef;
        EntityId = entityId;
    }

    public static ValidationResult Success(Guid entityId = default) 
        => new(true, "Validation Passed", RuleSeverity.Info, "", entityId);

    public static ValidationResult Failure(string message, RuleSeverity severity, string standardRef, Guid entityId)
        => new(false, message, severity, standardRef, entityId);
}

public interface IEngineeringRule
{
    string RuleId { get; }
    string Name { get; }
    RuleCategory Category { get; }
    
    // Tek bir entity kontrolü
    ValidationResult Check(object entity);

    // NE: Otomatik Düzeltme (Auto-Fix)
    // NEDEN: Yönetmelik ihlali durumunda (Örn: WC borusu < DN100) sistemi standartlara otomatik çekmek için.
    bool AutoFix(object entity);
}
