using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Genel Keşif — Birleşik Metraj Raporu (UnifiedBomService)
   NEDEN — GERÇEK BOŞLUK (Session #75 iş akışı denetiminde bulundu, madde 04): Projede
          4 ayrı BOM servisi vardı (BomService/HvacBomService/SelectionBomService/
          ArchitecturalBomService), her biri kendi ekranından ayrı ayrı çağrılıyordu —
          tek bir "bu projenin tüm keşfi budur" raporu yoktu.
   KAPSAM: BomService (tesisat: boru/fitting/vitrifiye + HVAC: kanal/terminal/damper —
          gerçek yerleştirilmiş entity sayımı) ve ArchitecturalBomService (duvar/kolon/
          kiriş/kapı/pencere/mahal) burada TEK bir rapora birleştiriliyor. Maliyet
          tarafında UYDURMA fiyat eklemek yerine sadece gerçek/doğrulanmış fiyatlandırma
          kaynakları kullanılıyor: PipeCostService (boru, malzeme+işçilik+ek parça) ve
          PozKatalogService'in GRUP 30 (Havalandırma) kalemleri (kanal/terminal/damper —
          madde 68'de eklendi, önceden HVAC hiç poz kataloğu kapsamındaydı).
          SelectionBomService buraya dahil EDİLMEDİ — o seçili nesne alt kümesi için ayrı
          bir "hızlı tahmin" aracı, bütün-belge keşfiyle aynı amaca hizmet etmiyor.
*/
public class UnifiedBomResult
{
    public List<BomItem> MechanicalItems { get; set; } = [];
    public List<ArchBomItem> ArchitecturalItems { get; set; } = [];
    public ArchBomResult ArchSummary { get; set; } = new();
    public double PipeCostTl { get; set; }
    public double DuctCostTl { get; set; }
    public double AirTerminalCostTl { get; set; }
    public double DamperCostTl { get; set; }
    public double TotalEstimatedCostTl => PipeCostTl + DuctCostTl + AirTerminalCostTl + DamperCostTl;
}

public class UnifiedBomService
{
    private readonly CadDatabase _database;
    private readonly PozKatalogService _poz;

    public UnifiedBomService(CadDatabase database, PozKatalogService? pozKatalog = null)
    {
        _database = database;
        _poz = pozKatalog ?? new PozKatalogService();
    }

    public UnifiedBomResult Generate()
    {
        var mech = new BomService(_database).GenerateBom();
        var arch = new ArchitecturalBomService(_database).Generate();
        var entities = _database.GetAllEntities().ToList();

        double pipeCost = new PipeCostService().CalculateFromDatabase(_database).TotalCostTl;

        double ductCost = entities.OfType<DuctEntity>()
            .GroupBy(d => d.Shape)
            .Sum(g =>
            {
                var poz = _poz.FindForDuct(g.Key);
                double areaM2 = g.Sum(d => d.GetInsulationArea()); // gerçek sac yüzey alanı (perimeter × uzunluk)
                return poz != null ? areaM2 * (double)poz.BirimFiyat : 0;
            });

        var terminalPoz = _poz.FindForAirTerminal();
        double terminalCost = terminalPoz != null ? entities.OfType<AirTerminalEntity>().Count() * (double)terminalPoz.BirimFiyat : 0;

        var damperPoz = _poz.FindForDamper();
        double damperCost = damperPoz != null ? entities.OfType<DamperEntity>().Count() * (double)damperPoz.BirimFiyat : 0;

        return new UnifiedBomResult
        {
            MechanicalItems = mech,
            ArchitecturalItems = arch.Items,
            ArchSummary = arch,
            PipeCostTl = pipeCost,
            DuctCostTl = ductCost,
            AirTerminalCostTl = terminalCost,
            DamperCostTl = damperCost
        };
    }

    public string ExportToHtml(UnifiedBomResult r, string? projectName = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Genel Keşif</title>");
        sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;margin:30px;color:#222}");
        sb.AppendLine("h1{color:#0D47A1;border-bottom:2px solid #0D47A1;padding-bottom:6px}");
        sb.AppendLine("h2{color:#37474F;margin-top:26px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:8px 0 18px}");
        sb.AppendLine("th,td{border:1px solid #CCC;padding:6px 10px;text-align:left;font-size:12px}");
        sb.AppendLine("th{background:#E3F2FD;font-weight:bold}");
        sb.AppendLine("tr:nth-child(even){background:#FAFAFA}");
        sb.AppendLine(".summary{display:flex;gap:16px;flex-wrap:wrap;margin:15px 0}");
        sb.AppendLine(".card{background:#F5F5F5;border-radius:6px;padding:12px 18px;min-width:120px}");
        sb.AppendLine(".card .val{font-size:20px;font-weight:bold;color:#0D47A1}");
        sb.AppendLine(".note{font-size:11px;color:#777;margin:4px 0 18px}</style></head><body>");

        sb.AppendLine("<h1>GENEL KEŞİF — Birleşik Metraj Raporu</h1>");
        if (!string.IsNullOrEmpty(projectName))
            sb.AppendLine($"<p><b>Proje:</b> {projectName} | <b>Tarih:</b> {DateTime.Now:dd.MM.yyyy}</p>");

        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<div class='card'><div>Boru Maliyeti</div><div class='val'>{r.PipeCostTl:N0} TL</div></div>");
        sb.AppendLine($"<div class='card'><div>Kanal Maliyeti</div><div class='val'>{r.DuctCostTl:N0} TL</div></div>");
        sb.AppendLine($"<div class='card'><div>Terminal/Damper Maliyeti</div><div class='val'>{(r.AirTerminalCostTl + r.DamperCostTl):N0} TL</div></div>");
        sb.AppendLine($"<div class='card'><div>Tahmini Toplam</div><div class='val'>{r.TotalEstimatedCostTl:N0} TL</div></div>");
        sb.AppendLine($"<div class='card'><div>Duvar</div><div class='val'>{r.ArchSummary.WallCount}</div></div>");
        sb.AppendLine($"<div class='card'><div>Mahal</div><div class='val'>{r.ArchSummary.RoomCount}</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<p class='note'>Tahmini toplam boru (PipeCostService: malzeme+işçilik+ek parça) ve HVAC " +
                       "(kanal/terminal/damper — PozKatalogService GRUP 30, 2024 ÇŞB referanslı) kalemlerini kapsar. " +
                       "Vitrifiye ve mimari kalemlerin poz-fiyat kataloğu henüz bu kategorileri kapsamadığı için " +
                       "(bkz. Kullanici_kitabi.md) buraya uydurma bir fiyat eklenmedi, sadece adet/miktar gösteriliyor.</p>");

        sb.AppendLine("<h2>Tesisat / HVAC (gerçek yerleştirilmiş nesneler)</h2>");
        sb.AppendLine("<table><tr><th>Kategori</th><th>Açıklama</th><th>Malzeme/Tip</th><th>Miktar</th><th>Birim</th></tr>");
        foreach (var item in r.MechanicalItems)
            sb.AppendLine($"<tr><td>{item.Category}</td><td>{item.Description}</td><td>{item.Material}</td><td>{item.Quantity:F1}</td><td>{item.Unit}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Mimari</h2>");
        sb.AppendLine("<table><tr><th>Kategori</th><th>Açıklama</th><th>Boyut</th><th>Adet</th><th>Alan (m²)</th><th>Hacim (m³)</th></tr>");
        foreach (var item in r.ArchitecturalItems)
            sb.AppendLine($"<tr><td>{item.Category}</td><td>{item.Description}</td><td>{item.Size}</td><td>{item.Quantity}</td><td>{(item.Area > 0 ? item.Area.ToString("F2") : "-")}</td><td>{(item.Volume > 0 ? item.Volume.ToString("F3") : "-")}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine($"<p style='font-size:10px;color:#999'>AfneyCAD v4.0.0 — Genel Keşif | {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
