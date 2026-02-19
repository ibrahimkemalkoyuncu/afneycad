using Afney.Cad.Mechanical.Rules;
using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Engine.Constraints;

/*
    NE: Dinamik Kısıt Çözücü (ConstraintSolver)
    NEDEN: AutoCAD/FINE SANI benzeri "Akıllı Nesne" davranışı sergilemek için. 
    Kullanıcı bir nesneyi değiştirdiğinde (Örn: Çapı küçültme), mühendislik kurallarına aykırı bir durum oluşursa sistemi otomatik düzeltir veya uyarır.
    
    NASIL (Mühendislik Modu):
    1. Kayıtlı tüm IEngineeringRule'ları sırayla çalıştırır.
    2. Hata (Error) durumunda kuralın AutoFix metodunu tetikleyerek parametreleri yönetmelik sınırına çeker.
    3. Kullanıcıya geri bildirim (Feedback) sağlar.
*/
public class ConstraintSolver
{
    private readonly List<IEngineeringRule> _rules;

    public ConstraintSolver(IEnumerable<IEngineeringRule> rules)
    {
        _rules = rules.ToList();
    }

    /*
        NE: Nesneyi Çöz (Solve)
        AMACI: Verilen nesne üzerinde tüm kısıtlamaları kontrol eder ve gerekirse düzeltir.
    */
    public List<ValidationResult> Solve(object entity, bool applyFixes = true)
    {
        var results = new List<ValidationResult>();

        foreach (var rule in _rules)
        {
            var result = rule.Check(entity);
            if (!result.IsValid)
            {
                results.Add(result);

                // KRİTİK ADIM: Otomatik Düzeltme
                if (applyFixes && result.Severity == RuleSeverity.Error)
                {
                    bool isFixed = rule.AutoFix(entity);
                    if (isFixed)
                    {
                        // Düzeltme sonrası tekrar doğrula (Mühendislik sağlaması)
                        var fixedResult = rule.Check(entity);
                        if (fixedResult.IsValid)
                        {
                            results.Add(ValidationResult.Success(result.EntityId)); 
                        }
                    }
                }
            }
        }

        return results;
    }

    /*
        NE: Tüm Ağı Çöz (SolveNetwork)
        AMACI: Topoloji grafındaki tüm boru ve cihazları yönetmeliklere göre denetler.
    */
    public void SolveNetwork(IEnumerable<MechanicalEntity> entities)
    {
        foreach (var entity in entities)
        {
            Solve(entity, applyFixes: true);
        }
    }
}
