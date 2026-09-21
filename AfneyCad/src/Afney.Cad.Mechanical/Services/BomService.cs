using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

public class BomItem
{
    public string Category { get; set; } = ""; // "Pipe", "Fitting", "Fixture" vs.
    public string Description { get; set; } = "";
    public string Material { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = ""; // "m", "Adet"
}

public class BomService
{
    private readonly CadDatabase _database;

    public BomService(CadDatabase database)
    {
        _database = database;
    }

    public List<BomItem> GenerateBom()
    {
        var bomList = new List<BomItem>();
        var entities = _database.GetAllEntities().ToList();

        // 1. Borular (Pipes) - Çap ve Malzemeye göre grupla, uzunlukları topla
        var pipes = entities.OfType<PipeEntity>().ToList();
        var pipeGroups = pipes.GroupBy(p => new { p.InnerDiameter, p.PipeMaterialType });

        foreach (var group in pipeGroups)
        {
            /*
               NE/NEDEN — GERÇEK HATA (bu turda bulundu): Bu satır önceden `p.Length`'i (mm
               cinsinden, dünya-koordinat mesafesi) HİÇ dönüştürmeden "m" etiketiyle
               gösteriyordu — aynı dosyadaki kanal (Kanal) grubu ise doğru şekilde /1000.0
               uyguluyordu (satır ~117), ve uygulamadaki HER DİĞER servis (PipeCostService,
               PressureDropService, HydraulicReportService, CalculationTableService,
               RealTimeCostService, SelectionBomService, vb. — 15+ çağrı yeri) pipe.Length'i
               "mm → m" için /1000.0 ile böler. Bu satır tek istisnaydı: metraj raporunda
               her boru kalemi GERÇEK uzunluğunun 1000 KATI olarak görünüyordu (örn. 25m'lik
               bir hat "25000 m" yazıyordu).
            */
            double totalLength = group.Sum(p => p.Length) / 1000.0;

            bomList.Add(new BomItem
            {
                Category = "Boru",
                Description = $"Boru DN{group.Key.InnerDiameter}",
                Material = group.Key.PipeMaterialType.ToString(),
                Quantity = Math.Round(totalLength, 2),
                Unit = "m"
            });
        }

        // 2. Dirsekler (Elbows)
        var elbows = entities.OfType<ElbowEntity>().ToList();
        var elbowGroups = elbows.GroupBy(e => new { e.InnerDiameter });

        foreach (var group in elbowGroups)
        {
            bomList.Add(new BomItem
            {
                Category = "Bağlantı Parçası",
                Description = $"Dirsek DN{group.Key.InnerDiameter}",
                Material = "Standart",
                Quantity = group.Count(),
                Unit = "Adet"
            });
        }

        // 3. T-Parçaları (Tees)
        var tees = entities.OfType<TeeEntity>().ToList();
        var teeGroups = tees.GroupBy(t => new { t.InnerDiameter });

        foreach (var group in teeGroups)
        {
            bomList.Add(new BomItem
            {
                Category = "Bağlantı Parçası",
                Description = $"T-Parçası DN{group.Key.InnerDiameter}",
                Material = "Standart",
                Quantity = group.Count(),
                Unit = "Adet"
            });
        }

        // 4. Uç Noktalar (Fixtures)
        var fixtures = entities.OfType<SanitaryFixtureEntity>().ToList();
        var fixtureGroups = fixtures.GroupBy(f => new { f.FixtureType });

        foreach (var group in fixtureGroups)
        {
            bomList.Add(new BomItem
            {
                Category = "Sağlık Gereci",
                Description = group.Key.FixtureType.ToString(),
                Material = "-",
                Quantity = group.Count(),
                Unit = "Adet"
            });
        }

        /*
           NE/NEDEN — GERÇEK BOŞLUK (Session #75 iş akışı denetiminde bulundu, HVAC modülü):
           Bu metod önceden sadece Pipe/Elbow/Tee/SanitaryFixture sayıyordu — PlaceAirTerminalCommand/
           PlaceDamperCommand ile çizime yerleştirilebilen kanal/difüzör/damper hiç sayılmıyordu.
           Bir kullanıcı HVAC ekipmanı yerleştirse bile bu metraj raporunda hiç görünmüyordu.
        */
        // 5. Kanallar (Ducts) - Şekil+Tip+Boyuta göre grupla, uzunlukları topla
        var ducts = entities.OfType<DuctEntity>().ToList();
        var ductGroups = ducts.GroupBy(d => new { d.Shape, d.Type, Size = d.GetSizeText() });

        foreach (var group in ductGroups)
        {
            double totalLength = group.Sum(d => d.GetLength()) / 1000.0; // mm -> m
            bomList.Add(new BomItem
            {
                Category = "Kanal",
                Description = $"{group.First().GetTypeText()} Kanal {group.Key.Size}",
                Material = group.Key.Shape.ToString(),
                Quantity = Math.Round(totalLength, 2),
                Unit = "m"
            });
        }

        // 6. Hava Terminalleri (Difüzör/Menfez)
        var terminals = entities.OfType<AirTerminalEntity>().ToList();
        var terminalGroups = terminals.GroupBy(t => new { t.TerminalType });

        foreach (var group in terminalGroups)
        {
            bomList.Add(new BomItem
            {
                Category = "Hava Terminali",
                Description = group.Key.TerminalType.ToString(),
                Material = "-",
                Quantity = group.Count(),
                Unit = "Adet"
            });
        }

        // 7. Damperler
        var dampers = entities.OfType<DamperEntity>().ToList();
        var damperGroups = dampers.GroupBy(d => new { d.DamperType });

        foreach (var group in damperGroups)
        {
            bomList.Add(new BomItem
            {
                Category = "Damper",
                Description = group.Key.DamperType.ToString(),
                Material = "-",
                Quantity = group.Count(),
                Unit = "Adet"
            });
        }

        // 8. Kanal ekipmanları (esnek bağlantı/filtre/bobin/CAV/VAV)
        var equipmentGroups = entities.OfType<DuctEquipmentEntity>()
            .GroupBy(e => new { e.EquipmentType, Diameter = Math.Round(e.InnerDiameter) });

        foreach (var group in equipmentGroups)
        {
            var first = group.First();
            string detail = group.Key.EquipmentType switch
            {
                Enums.DuctEquipmentType.FlexConnector => $"L={first.Size:F0} mm",
                Enums.DuctEquipmentType.Filter => first.FilterClass,
                Enums.DuctEquipmentType.HeatingCoil or Enums.DuctEquipmentType.CoolingCoil => $"{first.CapacityKw:F1} kW",
                _ => $"{first.MinFlowM3h:F0}–{first.AirFlowM3h:F0} m³/h"
            };

            bomList.Add(new BomItem
            {
                Category = "Kanal Ekipmanı",
                Description = $"{group.Key.EquipmentType} DN{group.Key.Diameter:F0} ({detail})".Replace(" ()", ""),
                Material = "-",
                Quantity = group.Count(),
                Unit = "Adet"
            });
        }

        return bomList.OrderBy(b => b.Category).ThenBy(b => b.Description).ToList();
    }
}
