using System;
using System.Collections.Generic;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Akış Hesaplama Test Birimi (FlowCalculationTests)
   NEDEN: FlowCalculationService'in CalculateSystemFlow işlevi sırasında boruların
          yük birimlerini (TotalFixtureUnits) doğru sıfırlayıp yönettiğini doğrulamak için.

   MÜHENDİSLİK NOTU:
   - PipeEntity.LoadUnits başlangıç değeri 1.0'dır (Minimum 1 LU — DN50 kuralı).
   - TotalFixtureUnits, LoadUnits alanının bir alias'ıdır.
   - CalculateSystemFlow, her çalışmada önce tüm boruların TotalFixtureUnits'ini sıfırlar.
*/
public class FlowCalculationTests
{
    [Fact]
    public void PipeEntity_DefaultLoadUnits_IsOne()
    {
        // PipeEntity mühendislik standardı: Minimum 1 LU başlangıç değeri.
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), 50.0);
        Assert.Equal(1.0, pipe.LoadUnits);
        Assert.Equal(0.0, pipe.FlowRate);
    }

    [Fact]
    public void CalculateSystemFlow_AfterCall_ResetsAllPipeTFU()
    {
        // Arrange: Boruya elle yük ata
        var graph = new MechanicalTopologyGraph();
        var calcService = new FlowCalculationService(graph);

        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), 50.0);
        pipe.TotalFixtureUnits = 99.0; // "kirli" değer

        graph.AddEntity(pipe);
        var entities = new List<MechanicalEntity> { pipe };

        // Act — hesap başında tüm borular sıfırlanır
        calcService.CalculateSystemFlow(entities);

        // Assert: Sıfırlama gerçekleşmeli (fixture yoksa sonuç 0 kalır)
        Assert.Equal(0.0, pipe.TotalFixtureUnits);
    }

    [Fact]
    public void CalculateSystemFlow_NoFixtures_FlowRateRemainsZero()
    {
        // Arrange: Sadece borular, hiç cihaz yok
        var graph = new MechanicalTopologyGraph();
        var calcService = new FlowCalculationService(graph);

        var pipe1 = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(100, 0, 0), 50.0);
        graph.AddEntity(pipe1);

        var entities = new List<MechanicalEntity> { pipe1 };
        calcService.CalculateSystemFlow(entities);

        // Hiçbir cihaz (fixture) olmadığında boruların debi ve TFU değerleri 0 kalmalıdır.
        Assert.Equal(0.0, pipe1.TotalFixtureUnits);
        Assert.Equal(0.0, pipe1.FlowRate);
    }
}
