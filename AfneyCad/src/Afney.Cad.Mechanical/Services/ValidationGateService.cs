using System;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Services
{
    /*
       NE: ValidationGateService (Doğrulama Kapısı - Sistem Koruyucu)
       NEDEN: Yazılımın "Giriş-İşlem-Çıkış" döngüsünde, kirli veriyle hesaplama yapılmasını engelleyen en üst düzey kontrol noktasıdır.
              DomainGuardService'i kullanarak mühendislik hatalarını raporlar ve akışı yönetir.
    */
    public class ValidationGateService
    {
        private readonly DomainGuardService _guard;
        
        public ValidationGateService(CadDatabase database, MechanicalTopologyGraph topology)
        {
            _guard = new DomainGuardService(database, topology);
        }

        /*
           NE: Hesaplama Öncesi Kapı Denetimi (CheckGateBeforeCalculation)
           NEDEN: Proje bazlı bir recalculation başlatılmadan önce sistemin "Çözülebilir" olduğunu teyit eder.
        */
        public bool CheckGateBeforeCalculation(out ValidationResult result)
        {
            result = _guard.ValidateSystem();
            
            if (!result.IsValid)
            {
                Serilog.Log.Warning(">>> VALIDATION GATE: Hesaplama engellendi. {ErrorCount} hata bulundu.", result.Errors.Count);
                return false;
            }

            if (result.Warnings.Any())
            {
                Serilog.Log.Information(">>> VALIDATION GATE: Geçildi ancak {WarningCount} uyarı mevcut.", result.Warnings.Count);
            }

            return true;
        }
    }
}
