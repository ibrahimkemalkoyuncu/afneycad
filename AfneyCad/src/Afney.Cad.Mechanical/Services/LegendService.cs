using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Lejant Oluşturma Servisi (LegendService)
    NEDEN: Projede kullanılan tüm vitrifiyeleri ve sembollerini içeren bir 'Açıklama Tablosu' (Legend) oluşturmak için.
*/
public class LegendService
{
    private const double RowHeight = 600.0;
    private const double TableWidth = 5000.0;
    
    private readonly IEnumerable<CadEntity> _projectEntities;

    public LegendService(IEnumerable<CadEntity> projectEntities)
    {
        _projectEntities = projectEntities;
    }

    public List<CadEntity> GenerateLegend(Vector3D origin)
    {
        var entities = new List<CadEntity>();
        
        // 1. Kullanılan benzersiz vitrifiyeleri bul
        var uniqueFixtures = _projectEntities.OfType<SanitaryFixtureEntity>()
            .Select(f => f.FixtureType)
            .Distinct()
            .ToList();

        if (!uniqueFixtures.Any()) return entities;

        // 2. Tablo Başlığı
        entities.Add(new TextEntity("SEMBOLLER VE AÇIKLAMALAR", origin + new Vector3D(0, 1000, 0), 300) { Color = 0xFF00FFFF });

        // 3. Tablo Çerçevesi
        double totalHeight = (uniqueFixtures.Count + 1) * RowHeight;
        entities.Add(new LineEntity(origin, origin + new Vector3D(TableWidth, 0, 0))); // Alt
        entities.Add(new LineEntity(origin, origin + new Vector3D(0, totalHeight, 0))); // Sol
        entities.Add(new LineEntity(origin + new Vector3D(TableWidth, 0, 0), origin + new Vector3D(TableWidth, totalHeight, 0))); // Sağ
        entities.Add(new LineEntity(origin + new Vector3D(0, totalHeight, 0), origin + new Vector3D(TableWidth, totalHeight, 0))); // Üst

        // 4. İç Çizgiler ve Veriler
        double currentY = totalHeight - RowHeight;
        
        // Başlık Satırı
        entities.Add(new LineEntity(origin + new Vector3D(0, currentY, 0), origin + new Vector3D(TableWidth, currentY, 0)));
        entities.Add(new TextEntity("SEMBOL", origin + new Vector3D(500, currentY + 200, 0), 150));
        entities.Add(new TextEntity("AÇIKLAMA", origin + new Vector3D(2000, currentY + 200, 0), 150));

        currentY -= RowHeight;

        var symbolDrawer = new RiserDrawingService(); // Sembol çizim mantığını bozmamak için tekrar kullanalım

        foreach (var type in uniqueFixtures)
        {
            // Satır Çizgisi
            entities.Add(new LineEntity(origin + new Vector3D(0, currentY, 0), origin + new Vector3D(TableWidth, currentY, 0)));
            
            // Sembolü Çiz (Orta noktaya)
            var symPos = origin + new Vector3D(800, currentY + (RowHeight / 2), 0);
            // Manuel DrawFixtureSymbol çağrısı (Service private olduğu için burada basitleştirelim)
            DrawSimpleSymbol(entities, type, symPos);

            // Açıklama Yazısı
            entities.Add(new TextEntity(TranslateType(type), origin + new Vector3D(2000, currentY + 200, 0), 150));

            currentY -= RowHeight;
        }

        return entities;
    }

    private void DrawSimpleSymbol(List<CadEntity> entities, string type, Vector3D pos)
    {
        uint col = 0xFF00FF00;
        if (type.Contains("Washbasin") || type.Contains("Lavabo"))
            DrawPoly(entities, pos, 100, 8, col);
        else if (type.Contains("WC"))
            entities.Add(new LineEntity(pos + new Vector3D(-100, -100, 0), pos + new Vector3D(100, 100, 0)) { Color = col });
        else
            DrawPoly(entities, pos, 80, 4, col);
    }

    private void DrawPoly(List<CadEntity> entities, Vector3D center, double r, int sides, uint col)
    {
        for (int i = 0; i < sides; i++)
        {
            double a1 = 2 * Math.PI * i / sides;
            double a2 = 2 * Math.PI * (i + 1) / sides;
            entities.Add(new LineEntity(center + new Vector3D(r * Math.Cos(a1), r * Math.Sin(a1), 0), 
                                      center + new Vector3D(r * Math.Cos(a2), r * Math.Sin(a2), 0)) { Color = col });
        }
    }

    private string TranslateType(string type)
    {
        if (type.Contains("Washbasin") || type.Contains("Lavabo")) return "Lavabo (Yarım Ayak)";
        if (type.Contains("Toilet") || type.Contains("WC")) return "Klozet (Rezervuarlı)";
        if (type.Contains("Shower") || type.Contains("Duş")) return "Duş Teknesi / Kabin";
        return type;
    }
}
