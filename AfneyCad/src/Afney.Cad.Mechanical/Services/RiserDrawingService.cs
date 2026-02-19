using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Kolon Şeması Çizim Servisi (RiserDrawingService)
    NEDEN: Analiz edilen kolon verilerini (RiserSchema) 2D teknik resim standartlarında CAD ortamına aktarmak için.
    
    PARAMETRELER:
    - Origin: Şemanın çizilmeye başlanacağı sol alt köşe noktası.
    - FloorSpacing: Şemadaki katlar arası dikey mesafe (Görsel temsil için, örn: 5000 birim).
    - FixtureSpacing: Şemadaki uç birimler arası yatay mesafe (Örn: 1500 birim).
*/
public class RiserDrawingService
{
    private const double FloorLineLength = 10000.0;
    private const double TextHeight = 150.0;
    private const uint RiserColor = 0xFFFFFFFF; // Beyaz
    private const uint FloorLineColor = 0xAA444444; // Koyu Gri (Kat Çizgisi)
    private const uint BranchColor = 0xFFFFA500; // Turuncu (Branşman)
    private const uint LabelColor = 0xFFFFFF00; // Sarı (Etiketler)
    private const uint HeaderColor = 0xFF00FFFF; // Cyan (Başlık)
    private const uint SymbolColor = 0xFF00FF00; // Yeşil (Semboller)

    public List<CadEntity> CreateDrawing(RiserSchema schema, Vector3D origin)
    {
        var entities = new List<CadEntity>();
        double currentY = origin.Y;
        double schematicFloorHeight = 4000.0; // Şematik kat yüksekliği (Görsel alan için)
        double fixtureSpacing = 1500.0;
        
        // Branşman açısı (İzometrik görünüm için 30 derece)
        double isoAngleRad = 30.0 * System.Math.PI / 180.0;
        double isoCos = System.Math.Cos(isoAngleRad);
        double isoSin = System.Math.Sin(isoAngleRad);

        // 1. Ana Kolon Hattı (Dikey Çizgi)
        double totalHeight = schema.Floors.Count * schematicFloorHeight;
        var riserLine = new LineEntity(origin, origin + new Vector3D(0, totalHeight, 0))
        {
            Color = RiserColor,
            Layer = "KOLON_SEMA",
            LineWeight = 50 // Kalın kolon hattı
        };
        entities.Add(riserLine);

        // 2. Başlık ve Toplam Debi Bilgisi
        entities.Add(new TextEntity($"KOLON ŞEMASI: {schema.RiserName}", origin + new Vector3D(0, -600, 0), 300) { Color = HeaderColor });
        entities.Add(new TextEntity($"TOPLAM YÜK (ΣLU): {schema.TotalLoadUnits:F1} LU", origin + new Vector3D(0, -950, 0), 180) { Color = LabelColor });
        entities.Add(new TextEntity($"TOPLAM KAYIP (ΔP): {schema.TotalPressureDrop:F3} mSS", origin + new Vector3D(0, -1200, 0), 180) { Color = LabelColor });

        // 3. Katları ve Branşmanları Çiz
        foreach (var floor in schema.Floors.OrderBy(f => f.FloorLevel))
        {
            // Kat Çizgisi (İnce ve Kesikli temsil)
            var floorLine = new LineEntity(
                new Vector3D(origin.X - 3000, currentY, 0),
                new Vector3D(origin.X + FloorLineLength, currentY, 0))
            {
                Color = FloorLineColor,
                Layer = "KOLON_SEMA_KAT",
                Linetype = "Dashed"
            };
            entities.Add(floorLine);

            // Kat İsmi ve Kot Bilgisi
            entities.Add(new TextEntity(floor.FloorName.ToUpper(), new Vector3D(origin.X - 3200, currentY + 150, 0), 200) { Color = 0xFFCCCCCC });
            entities.Add(new TextEntity($"KOT: {floor.Elevation:F2} m", new Vector3D(origin.X - 3200, currentY - 150, 0), 150) { Color = 0xFF999999 });

            // Kattaki Branşman (İzometrik 30°)
            if (floor.Fixtures.Any())
            {
                var branchStart = new Vector3D(origin.X, currentY + 600, 0); // Kolondan çıkış
                double branchLen = (floor.Fixtures.Count + 1) * fixtureSpacing;
                
                var branchEnd = branchStart + new Vector3D(branchLen * isoCos, branchLen * isoSin, 0);
                
                // Branşman Hattı
                var branchLine = new LineEntity(branchStart, branchEnd)
                {
                    Color = BranchColor,
                    Layer = "KOLON_SEMA_BRANSMAN",
                    LineWeight = 30
                };
                entities.Add(branchLine);
                
                // Kolon Bağı (Düşey)
                entities.Add(new LineEntity(new Vector3D(origin.X, currentY, 0), branchStart) { Color = BranchColor });

                // Branşman Başlangıç Vanası (Gate Valve)
                DrawValveSymbol(entities, branchStart + new Vector3D(300 * isoCos, 300 * isoSin, 0), isoAngleRad);
                
                // Branşman Etiketi (Çap ve FU)
                entities.Add(new TextEntity($"Ø{floor.BranchDiameter:F0} - {floor.Fixtures.Sum(f=>f.FixtureUnit):F1} LU", 
                    branchStart + new Vector3D(500 * isoCos, 500 * isoSin + 200, 0), 150) { Color = LabelColor, Rotation = 30.0 });

                // Uç Birimleri (Vitrifiyeleri) Yerleştir
                for (int i = 0; i < floor.Fixtures.Count; i++)
                {
                    var fixture = floor.Fixtures[i];
                    double dist = (i + 1) * fixtureSpacing + 1000;
                    var fixConnectPoint = branchStart + new Vector3D(dist * isoCos, dist * isoSin, 0);
                    var fixSymbolPoint = fixConnectPoint + new Vector3D(0, 600, 0);
                    
                    // Bağlantı Çiziği (Dikey)
                    entities.Add(new LineEntity(fixConnectPoint, fixSymbolPoint) { Color = BranchColor });
                    
                    // Cihaz Sembolü
                    DrawFixtureSymbol(entities, fixture.Type, fixSymbolPoint + new Vector3D(0, 200, 0));
                    
                    // Cihaz Bilgisi
                    entities.Add(new TextEntity(fixture.Type, fixSymbolPoint + new Vector3D(300, 200, 0), 120) { Color = 0xFFAAAAAA });
                    entities.Add(new TextEntity($"Ø{fixture.ConnectionDiameter:F0}", fixSymbolPoint + new Vector3D(300, 0, 0), 100) { Color = LabelColor });
                }
            }

            currentY += schematicFloorHeight;
        }

        return entities;
    }

    private void DrawValveSymbol(List<CadEntity> entities, Vector3D pos, double angleRad)
    {
        double size = 150.0;
        double s = System.Math.Sin(angleRad);
        double c = System.Math.Cos(angleRad);
        
        // Vana Kanatları (İki Üçgen)
        var v1 = new Vector3D(-size, size/2, 0);
        var v2 = new Vector3D(-size, -size/2, 0);
        var v3 = new Vector3D(size, size/2, 0);
        var v4 = new Vector3D(size, -size/2, 0);
        
        // Rotasyon uygula (Manual)
        var rv1 = pos + new Vector3D(v1.X * c - v1.Y * s, v1.X * s + v1.Y * c, 0);
        var rv2 = pos + new Vector3D(v2.X * c - v2.Y * s, v2.X * s + v2.Y * c, 0);
        var rv3 = pos + new Vector3D(v3.X * c - v3.Y * s, v3.X * s + v3.Y * c, 0);
        var rv4 = pos + new Vector3D(v4.X * c - v4.Y * s, v4.X * s + v4.Y * c, 0);

        entities.Add(new LineEntity(rv1, rv2) { Color = BranchColor });
        entities.Add(new LineEntity(rv2, pos) { Color = BranchColor });
        entities.Add(new LineEntity(pos, rv1) { Color = BranchColor });
        
        entities.Add(new LineEntity(rv3, rv4) { Color = BranchColor });
        entities.Add(new LineEntity(rv4, pos) { Color = BranchColor });
        entities.Add(new LineEntity(pos, rv3) { Color = BranchColor });
    }

    private void DrawFixtureSymbol(List<CadEntity> entities, string type, Vector3D pos)
    {
        uint symColor = SymbolColor;
        string t = type.ToUpperInvariant();
        
        if (t.Contains("WASHBASIN") || t.Contains("LAVABO"))
        {
            // Lavabo: Daire ve musluk detayı
            entities.Add(new CircleEntity(pos, 150) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(-150, 0, 0), pos + new Vector3D(150, 0, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(0, 0, 0), pos + new Vector3D(0, 150, 0)) { Color = symColor });
        }
        else if (t.Contains("TOILET") || t.Contains("WC") || t.Contains("KLOZET"))
        {
            // WC: Rezervuar + Oturak (Daha profesyonel görünüm)
            // Rezervuar
            entities.Add(new LineEntity(pos + new Vector3D(-180, 150, 0), pos + new Vector3D(180, 150, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(180, 150, 0), pos + new Vector3D(180, 350, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(180, 350, 0), pos + new Vector3D(-180, 350, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(-180, 350, 0), pos + new Vector3D(-180, 150, 0)) { Color = symColor });
            // Oturak
            entities.Add(new CircleEntity(pos, 150) { Color = symColor });
        }
        else if (t.Contains("SHOWER") || t.Contains("DUS") || t.Contains("DUŞ"))
        {
            // Duş: Kare ve fıskiye noktası
            entities.Add(new LineEntity(pos + new Vector3D(-200, -200, 0), pos + new Vector3D(200, -200, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(200, -200, 0), pos + new Vector3D(200, 200, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(200, 200, 0), pos + new Vector3D(-200, 200, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(-200, 200, 0), pos + new Vector3D(-200, -200, 0)) { Color = symColor });
            entities.Add(new CircleEntity(pos, 30) { Color = symColor });
        }
        else if (t.Contains("SINK") || t.Contains("EVIYE") || t.Contains("EVİYE"))
        {
            // Eviye: Kare içinde daire
            entities.Add(new LineEntity(pos + new Vector3D(-180, -180, 0), pos + new Vector3D(180, -180, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(180, -180, 0), pos + new Vector3D(180, 180, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(180, 180, 0), pos + new Vector3D(-180, 180, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(-180, 180, 0), pos + new Vector3D(-180, -180, 0)) { Color = symColor });
            entities.Add(new CircleEntity(pos, 100) { Color = symColor });
        }
        else
        {
            // Varsayılan: Baklava dilimi
            entities.Add(new LineEntity(pos + new Vector3D(0, 150, 0), pos + new Vector3D(150, 0, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(150, 0, 0), pos + new Vector3D(0, -150, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(0, -150, 0), pos + new Vector3D(-150, 0, 0)) { Color = symColor });
            entities.Add(new LineEntity(pos + new Vector3D(-150, 0, 0), pos + new Vector3D(0, 150, 0)) { Color = symColor });
        }
    }

    private void DrawRegularPolygon(List<CadEntity> entities, Vector3D center, double radius, int sides, uint color)
    {
        for (int i = 0; i < sides; i++)
        {
            double angle1 = 2 * System.Math.PI * i / sides;
            double angle2 = 2 * System.Math.PI * (i + 1) / sides;
            var p1 = center + new Vector3D(radius * System.Math.Cos(angle1), radius * System.Math.Sin(angle1), 0);
            var p2 = center + new Vector3D(radius * System.Math.Cos(angle2), radius * System.Math.Sin(angle2), 0);
            entities.Add(new LineEntity(p1, p2) { Color = color });
        }
    }
}
