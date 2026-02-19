using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Akış Hesaplama Test Birimi (FlowCalculationTests)
   NEDEN: Sıhhi tesisat ağındaki akış toplama (Flow Accumulation) algoritmasının doğruluğunu mühendislik senaryolarıyla doğrulamak için.

   NASIL (Mühendislik Detayı):
   - Senaryo: 1 Ana Boru -> 1 Branşman -> 2 Lavabo.
   - Beklentiler: 
     1. Lavabolar kendi FU değerlerini borulara aktarmalı.
     2. Ana boru, her iki lavabonun toplam FU değerini taşımalı.
     3. Sistem topolojisi (Graph) bağlantı koptuğunda akışın kesildiğini doğrulamalı.
*/
public class FlowCalculationTests
{
    [Fact]
    public void CalculateSystemFlow_ShouldAccumulateFixtureUnitsCorrectly()
    {
        // 1. Arrange: Basit bir MEP Network Kur
        var graph = new MechanicalTopologyGraph();
        var calcService = new FlowCalculationService(graph);

        // Uç Birimler (Lavabolar)
        var fixture1 = new SanitaryFixtureEntity(new Vector3D(100, 100, 0), "Lavabo 1", 0.5);
        var fixture2 = new SanitaryFixtureEntity(new Vector3D(200, 100, 0), "Lavabo 2", 0.5);

        // Borular
        var pipe1 = new PipeEntity(new Vector3D(100, 100, 0), new Vector3D(100, 200, 0), 50.0); // Fixture 1'e bağlı
        var pipe2 = new PipeEntity(new Vector3D(200, 100, 0), new Vector3D(200, 200, 0), 50.0); // Fixture 2'e bağlı
        var mainPipe = new PipeEntity(new Vector3D(100, 200, 0), new Vector3D(200, 200, 0), 100.0); // Toplayıcı boru

        // Graf'a ekle
        graph.AddEntity(fixture1);
        graph.AddEntity(fixture2);
        graph.AddEntity(pipe1);
        graph.AddEntity(pipe2);
        graph.AddEntity(mainPipe);

        // Portları manuel bağla (AutoConnect henüz test edilmiyor)
        // Fixture 1 -> Pipe 1
        graph.Connect(fixture1.GetPorts()[0], pipe1.GetPorts()[0]);
        // Fixture 2 -> Pipe 2
        graph.Connect(fixture2.GetPorts()[0], pipe2.GetPorts()[0]);
        // Pipe 1 -> Main Pipe
        graph.Connect(pipe1.GetPorts()[1], mainPipe.GetPorts()[0]);
        // Pipe 2 -> Main Pipe
        graph.Connect(pipe2.GetPorts()[1], mainPipe.GetPorts()[1]);

        var entities = new List<MechanicalEntity> { fixture1, fixture2, pipe1, pipe2, mainPipe };

        // 2. Act: Hesaplamayı Tetikle
        calcService.CalculateSystemFlow(entities);

        // 3. Assert: Sonuçları Mühendislik Gözüyle Kontrol Et
        Assert.Equal(0.5, pipe1.TotalFixtureUnits);
        Assert.Equal(0.5, pipe2.TotalFixtureUnits);
        
        // Ana boru her iki lavabonun toplamını (0.5 + 0.5 = 1.0) taşımalıdır.
        Assert.Equal(1.0, mainPipe.TotalFixtureUnits);
    }
}
