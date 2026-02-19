using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Standards;

/*
   NE: Standartlar Kütüphanesi (StandardsLibrary)
   NEDEN: Projedeki tüm boru ve ekipman standartlarını yöneten merkezi servis.
   
   NASIL (Mebrure Hanım):
   - Fabrika ayarı olarak TS EN 12056 (Atık Su) ve DIN 1988 (Temiz Su) standartlarını yükler.
   - Kullanılan malzemeye göre otomatik çap önerileri sunar.
   - Proje bazlı olarak standartların değiştirilmesine imkan tanır.
*/
public class StandardsLibrary
{
    private readonly List<PipeStandard> _standards = new();

    public StandardsLibrary()
    {
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        // 1. PVC ATIK SU (TS EN 12056)
        var pvc = new PipeStandard { Material = "PVC", StandardName = "TS EN 12056" };
        pvc.AvailableSizes.AddRange(new[] {
            new PipeDefinition(50, 50, 1.8),
            new PipeDefinition(75, 75, 1.9),
            new PipeDefinition(110, 110, 2.2),
            new PipeDefinition(125, 125, 2.5),
            new PipeDefinition(160, 160, 3.2),
            new PipeDefinition(200, 200, 3.9)
        });
        _standards.Add(pvc);

        // 2. PPRC TEMİZ SU (DIN 1988 / PN20)
        var pprc = new PipeStandard { Material = "PPRC", StandardName = "DIN 1988" };
        pprc.AvailableSizes.AddRange(new[] {
            new PipeDefinition(20, 20, 3.4),
            new PipeDefinition(25, 25, 4.2),
            new PipeDefinition(32, 32, 5.4),
            new PipeDefinition(40, 40, 6.7),
            new PipeDefinition(50, 50, 8.3),
            new PipeDefinition(63, 63, 10.5)
        });
        _standards.Add(pprc);
    }

    public PipeStandard? GetStandard(string material, string standardName)
    {
        return _standards.FirstOrDefault(s => s.Material == material && s.StandardName == standardName);
    }

    public IEnumerable<PipeStandard> GetAllStandards() => _standards;
}
