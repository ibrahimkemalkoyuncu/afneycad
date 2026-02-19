using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı Teknik Şartname ve Keşif Özeti Jeneratörü (AutoSpecService)
   NEDEN: Projedeki tüm imalatları analiz ederek, Bayındırlık Poz Nolarına uygun keşif özeti ve mühendislik şartnamesi oluşturmak için. (Suggestion 21)
*/
public class AutoSpecService
{
    private readonly IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> _entities;

    public AutoSpecService(IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> entities)
    {
        _entities = entities;
    }

    /*
       NE: Keşif Özeti Üret (GenerateBoMReport)
       AMACI: Boru metrajları, armatür adetleri ve fittings listesini profesyonel bir tablo olarak sunmak.
    */
    public List<SpecItem> GenerateBoMReport()
    {
        var items = new List<SpecItem>();
        var mechanicalEntities = _entities.OfType<MechanicalEntity>().ToList();

        // 1. Boru Metrajları (Çap ve Malzeme bazlı gruplama)
        var pipes = mechanicalEntities.OfType<PipeEntity>()
            .GroupBy(p => new { Material = p.PipeMaterialType, p.InnerDiameter, p.SystemType })
            .Select(g => new SpecItem
            {
                Code = GetPozNo(g.Key.Material, g.Key.InnerDiameter),
                Description = $"{g.Key.Material} {g.Key.InnerDiameter}mm {GetSystemName(g.Key.SystemType)} Borusu",
                Unit = "mt",
                Quantity = g.Sum(p => (p.EndPoint - p.StartPoint).Length() / 1000.0) // mm -> metre
            });
        
        items.AddRange(pipes);

        // 2. Vitrifiye ve Cihaz Adetleri
        var fixtures = mechanicalEntities.OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType)
            .Select(g => new SpecItem
            {
                Code = GetFixturePozNo(g.Key),
                Description = $"{g.Key} Montajı ve Malzemesi",
                Unit = "Adet",
                Quantity = g.Count()
            });

        items.AddRange(fixtures);

        return items;
    }

    /*
       NE: Teknik Şartname Taslağı Üret (GenerateSpecificationText)
       AMACI: Seçilen malzemelere göre otomatik bir mühendislik şartnamesi metni oluşturmak.
    */
    public string GenerateSpecificationText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("AFNEYCAD MEKANİK TESİSAT TEKNİK ŞARTNAMESİ");
        sb.AppendLine("===========================================");
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy}");
        sb.AppendLine();
        
        sb.AppendLine("1. GENEL STANDARTLAR");
        sb.AppendLine("- Tesisat tasarımı TS 1258 ve TS EN 12056 standartlarına uygundur.");
        sb.AppendLine("- Borulama sistemleri sızdırmazlık testine tabi tutulacaktır.");
        sb.AppendLine();

        sb.AppendLine("2. BORU SİSTEMLERİ");
        var materials = _entities.OfType<PipeEntity>().Select(p => p.PipeMaterialType).Distinct();
        foreach (var mat in materials)
        {
            sb.AppendLine($"- {mat} borular, üretici kataloglarındaki basınç sınıflarına ve montaj kurallarına göre döşenecektir.");
        }
        
        sb.AppendLine();
        sb.AppendLine("3. HİDROLİK KRİTERLER");
        sb.AppendLine("- Temiz su hatlarında kritik hız 2.0 m/s asılmayacaktır.");
        sb.AppendLine("- Pis su hatları min %1 - %2 eğimle döşenecektir.");

        return sb.ToString();
    }

    private string GetPozNo(PipeMaterial material, double diameter)
    {
        // Örnek Bayındırlık Poz Noları
        return $"MB.{material.ToString().Substring(0, 2)}.{diameter}";
    }

    private string GetFixturePozNo(string type)
    {
        return $"MF.{type.Substring(0, 2)}.001";
    }

    private string GetSystemName(MechanicalSystemType type)
    {
        return type switch
        {
            MechanicalSystemType.DomesticColdWater => "Sıcak Su",
            MechanicalSystemType.DomesticHotWater => "Soğuk Su",
            MechanicalSystemType.WasteWater => "Pis Su",
            _ => "Genel"
        };
    }
}

public class SpecItem
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double Quantity { get; set; }
}
