using System;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Rules.Plumbing;

/*
   NE: Boru Yük Kapasite Kuralı (FixtureLoadCapacityRule)
   NEDEN: Atık su (Pis su) borularının taşıdığı yükleme birimi (FU) sayısının, boru çapına göre yönetmeliklerdeki (TS EN 12056) limitleri aşmadığını doğrulamak için.

   NASIL (Mühendislik Detayı):
   - Standart: DIN 1986 / TS EN 12056 Tablo değerleri.
   - Örn (Eğim %2 için): 
     - DN 50: Max 0.8 FU
     - DN 70: Max 1.5 FU
     - DN 100: Max 12.0 FU (Ana kolon/yatay)
   - Kural: Boru üzerindeki 'TotalFixtureUnits' değeri, belirtilen çapın kapasitesinden büyükse 'Failure' döner.
*/
public class FixtureLoadCapacityRule : IEngineeringRule
{
    public string RuleId => "STD-PLB-002";
    public string Name => "Boru Yük Kapasite Kontrolü";
    public RuleCategory Category => RuleCategory.Hydraulic;

    public ValidationResult Check(object entity)
    {
        if (entity is PipeEntity pipe)
        {
            // Pis su sistemi değilse kontrol etme (Şimdilik)
            if (pipe.SystemType != MechanicalSystemType.WasteWater)
                return ValidationResult.Success(pipe.Id);

            double capacity = GetCapacityForDiameter(pipe.InnerDiameter);

            if (pipe.TotalFixtureUnits > capacity)
            {
                return ValidationResult.Failure(
                    $"Boru yük kapasitesi aşıldı! Mevcut: {pipe.TotalFixtureUnits} FU, İzin Verilen: {capacity} FU (Çap: DN{pipe.InnerDiameter})",
                    RuleSeverity.Error,
                    "TS EN 12056",
                    pipe.Id);
            }
        }

        return ValidationResult.Success();
    }

    public bool AutoFix(object entity)
    {
        if (entity is PipeEntity pipe && pipe.SystemType == MechanicalSystemType.WasteWater)
        {
            bool fixedDiameter = false;

            // 1. Kural: WC Yükü Kontrolü
            if (pipe.IsCarryingWCLoad && pipe.InnerDiameter < 100)
            {
                pipe.InnerDiameter = 100;
                fixedDiameter = true;
            }

            // 2. Kural: Kapasite Kontrolü
            double capacity = GetCapacityForDiameter(pipe.InnerDiameter);
            if (pipe.LoadUnits > capacity)
            {
                // Çapı bir üst seviyeye çek
                if (pipe.InnerDiameter < 50) pipe.InnerDiameter = 50;
                else if (pipe.InnerDiameter < 75) pipe.InnerDiameter = 75;
                else if (pipe.InnerDiameter < 110) pipe.InnerDiameter = 110;
                else if (pipe.InnerDiameter < 125) pipe.InnerDiameter = 125;
                else pipe.InnerDiameter += 25;
                fixedDiameter = true;
            }
            return fixedDiameter;
        }
        return false;
    }

    private double GetCapacityForDiameter(double diameter)
    {
        // Basitleştirilmiş tablo değerleri (Gerçekte Eğim/Slope parametresine de bağlıdır)
        if (diameter < 40) return 0.0;
        if (diameter < 50) return 0.5;
        if (diameter < 75) return 0.8;
        if (diameter < 110) return 1.5;
        if (diameter < 125) return 12.0;
        return 50.0;
    }
}
