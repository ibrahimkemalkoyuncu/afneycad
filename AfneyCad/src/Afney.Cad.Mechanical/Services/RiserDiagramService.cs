using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models; // EKLENDİ (RiserSchema)

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Kolon Şeması Motoru (RiserDiagramService)
   NEDEN: FINE SANI / 4M standardında, kat planındaki mahal verilerinden tek tıkla izometrik tesisat kolon şeması üretmek için.
   
   MÜHENDİSLİK DETAYI (Mete):
   - Kümülatif Yük Birimi: En üstten en alta doğru tüm katların LU değerlerini toplar (Diversity factor uygulanabilir).
   - İzometrik Projeksiyon: 2D çizim alanında 3D derinlik hissi veren (30-60 derece) koordinat dönüşümü.
   - Otomatik Etiketleme: Her katta çap ve debi bilgilerini şema üzerine yerleştirir.
*/
public class RiserDiagramService
{
    private readonly CadDatabase _database;
    private readonly HydraulicCalculationService _hydCalc;

    public RiserDiagramService(CadDatabase database)
    {
        _database = database;
        _hydCalc = new HydraulicCalculationService();
    }

    /*
       NE: Kolon Şeması Üret (GenerateRiserDiagram)
       AMACI: Belirlenen mahallerden yola çıkarak dikey bir hat ve branşmanlar oluşturmak.
    */
    /*
       NE: Kolon Şeması Üret (GenerateRiserDiagram)
       NEDEN: Mahal verilerinden dikey bir hat ve branşmanlar oluşturarak tesisatın katlar arası hiyerarşisini izometrik projeksiyonla (2D) görselleştirmek için.
    */
    /*
       NE: Kolon Şeması Üret (GenerateRiserDiagram) - Mühendislik Modu
       NEDEN: RiserEngine tarafından hesaplanmış RiserSchema verilerini kullanarak, 2D izometrik şema (Single Line Diagram) çizmek için.
    */
    public List<CadEntity> GenerateRiserDiagram(List<RiserSchema> schemas, Vector3D insertionPoint)
    {
        var diagramEntities = new List<CadEntity>();
        if (schemas == null || !schemas.Any()) return diagramEntities;

        double currentXOffset = 0;
        const double SchematicFloorHeight = 1500.0;
        const double RiserSpacing = 2000.0;

        foreach (var schema in schemas)
        {
            var riserBasePoint = insertionPoint + new Vector3D(currentXOffset, 0, 0);
            
            // 1. Riser Başlığı
            var title = new Afney.Cad.Domain.Entities.Basic.TextEntity(schema.RiserName, riserBasePoint + new Vector3D(0, -200, 0), 25, 0);
            title.Color = 0xFFFFFFFF;
            diagramEntities.Add(title);

            // 2. Katlar Arası Çizim
            double lastHeight = 0;
            
            // Katları Yükseklik Sırasına Göre Sırala
            var sortedFloors = schema.Floors.OrderBy(f => f.Elevation).ToList();

            foreach (var floor in sortedFloors)
            {
                double schematicY = floor.FloorLevel * SchematicFloorHeight;
                
                // Dikey Hat (Önceki kattan buraya)
                var p1 = riserBasePoint + new Vector3D(0, lastHeight, 0);
                var p2 = riserBasePoint + new Vector3D(0, schematicY, 0);
                
                if (Math.Abs(p2.Y - p1.Y) > 1.0)
                {
                    var riserLine = new PipeEntity(p1, p2, 50.0); // Şematik çap (görsel)
                    riserLine.Color = 0xFF0000FF;
                    diagramEntities.Add(riserLine);
                }

                // Yatay Branşman (Katmanda)
                var branchEnd = p2 + new Vector3D(600, 200, 0); // İzometrik 30 derece
                var branchLine = new PipeEntity(p2, branchEnd, floor.BranchDiameter > 0 ? floor.BranchDiameter : 32.0);
                branchLine.Color = 0xFF0000FF;
                diagramEntities.Add(branchLine);

                // Kat Etiketi
                var lbl = new Afney.Cad.Domain.Entities.Basic.TextEntity(
                    $"{floor.FloorName}\nØ{floor.BranchDiameter:F0}", 
                    branchEnd + new Vector3D(20, 0, 0), 12, 0);
                diagramEntities.Add(lbl);

                // Cihazları Listele
                double fixtureY = 0;
                foreach(var fix in floor.Fixtures)
                {
                    var fixLbl = new Afney.Cad.Domain.Entities.Basic.TextEntity(
                        $"- {fix.Type} (LU:{fix.FixtureUnit:F1})", 
                        branchEnd + new Vector3D(20, -20 + fixtureY, 0), 10, 0);
                    fixLbl.Color = 0xFFAAAAAA;
                    diagramEntities.Add(fixLbl);
                    fixtureY -= 15;
                }

                lastHeight = schematicY;
            }

            currentXOffset += RiserSpacing;
        }

        return diagramEntities;
    }
}
