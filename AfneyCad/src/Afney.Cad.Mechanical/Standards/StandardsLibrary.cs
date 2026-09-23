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

        /*
           MÜHENDİSLİK: RoutePipeCommand.SetSettings, Yangın ve Gaz sistemleri için "Steel"
           malzemesini gönderiyordu ama burada hiç kayıt yoktu — GetStandard() her zaman null
           dönüyor, boru çapı gerçek et kalınlığına göre hiç düzeltilmiyordu. DIN 2440 / TS 301
           galvanizli çelik boru (orta seri) standart dış çap/et kalınlığı değerleri eklendi.
        */
        var steel = new PipeStandard { Material = "Steel", StandardName = "DIN 2440" };
        steel.AvailableSizes.AddRange(new[] {
            new PipeDefinition(15, 21.3, 2.65),
            new PipeDefinition(20, 26.9, 2.65),
            new PipeDefinition(25, 33.7, 3.25),
            new PipeDefinition(32, 42.4, 3.25),
            new PipeDefinition(40, 48.3, 3.25),
            new PipeDefinition(50, 60.3, 3.65),
            new PipeDefinition(65, 76.1, 3.65),
            new PipeDefinition(80, 88.9, 4.05),
            new PipeDefinition(100, 114.3, 4.50)
        });
        _standards.Add(steel);
    }

    public PipeStandard? GetStandard(string material, string standardName)
    {
        return _standards.FirstOrDefault(s => s.Material == material && s.StandardName == standardName);
    }

    /*
       NE: Malzemeye Göre Standart Bul (GetStandardForMaterial)
       NEDEN: RoutePipeCommand.SetSettings gibi çağıranlar standart adını (TS EN 12056/DIN 1988/
              DIN 2440) bilmek zorunda kalmadan, sadece malzeme adıyla (PPRC/PVC/Steel) doğru
              standardı bulabilsin diye — önceden çağıran taraf standart adını YANLIŞ sabit
              kodluyordu (her malzeme için "TS EN 12056"), bu da PVC dışındaki malzemelerde
              arama hep null dönmesine yol açıyordu.
    */
    public PipeStandard? GetStandardForMaterial(string material)
    {
        return _standards.FirstOrDefault(s => s.Material == material);
    }

    public IEnumerable<PipeStandard> GetAllStandards() => _standards;
}
