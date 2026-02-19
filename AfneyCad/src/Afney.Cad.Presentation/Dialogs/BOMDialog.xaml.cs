using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BOMDialog : Window
{
    public List<PipeBOMRow> PipeGroups { get; set; } = new();
    public List<FixtureBOMRow> FixtureGroups { get; set; } = new();

    public BOMDialog(IEnumerable<CadEntity> entities)
    {
        InitializeComponent();
        CalculateBOM(entities);
        DataContext = this;
    }

    /*
       NE: Metraj Hesapla (CalculateBOM)
       NEDEN: Çizimdeki tüm boru ve vitrifiyeleri tip ve çaplarına göre gruplayarak; toplam boru metrajını (metre) ve cihaz adetlerini (BOM) raporlamak için.
    */
    private void CalculateBOM(IEnumerable<CadEntity> entities)
    {
        var allEntities = entities.ToList();

        // Boru Metrajı
        PipeGroups = allEntities.OfType<PipeEntity>()
            .GroupBy(p => new { p.InnerDiameter, p.SystemType })
            .Select(g => new PipeBOMRow
            {
                Diameter = g.Key.InnerDiameter.ToString("F0"),
                SystemType = g.Key.SystemType.ToString(),
                TotalLength = g.Sum(p => (p.EndPoint - p.StartPoint).Length()) / 1000.0 // mm -> m
            })
            .OrderBy(r => double.Parse(r.Diameter))
            .ToList();

        // Vitrifiye Metrajı
        FixtureGroups = allEntities.OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType)
            .Select(g => new FixtureBOMRow
            {
                Type = g.Key,
                Count = g.Count()
            })
            .OrderBy(r => r.Type)
            .ToList();
    }
}

public class PipeBOMRow
{
    public string Diameter { get; set; } = "";
    public string SystemType { get; set; } = "";
    public double TotalLength { get; set; }
}

public class FixtureBOMRow
{
    public string Type { get; set; } = "";
    public int Count { get; set; }
}
