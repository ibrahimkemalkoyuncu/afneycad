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
            double totalLength = group.Sum(p => p.Length);
            
            // Eğer totalLength miktarını metre cinsinden istiyorsak (çizim birimi mm ise / 1000)
            // Varsayılan olarak AfneyCAD çizim birimini m kabul ediyorsak direkt alabiliriz.
            // Fakat GetLength() genelde çizim birimi (örn: mm) dönebilir. Şimdilik m olarak varsayalım
            // veya mm ise 1000'e bölelim. Standardımız genelde metredir.
            // (PipeEntity hesaplarında length m olarak kullanılır, FlowCalculationService'de vb.)
            
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

        return bomList.OrderBy(b => b.Category).ThenBy(b => b.Description).ToList();
    }
}
