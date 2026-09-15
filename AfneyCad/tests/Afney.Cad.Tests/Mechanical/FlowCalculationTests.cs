using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
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

    /*
       NE/NEDEN — GERÇEK HATA REGRESYONU (bu turda bulundu, kullanıcı onaylı "standart
       uygunluk derinliği" denetiminde): FlowCalculationService.GetCoefficients()'in bina
       tipine göre a/b/c katsayıları DIN 1988-300'ün gerçek Tablo 1'inden (Konstanten für
       den Spitzendurchfluss — IKZ-Fachplaner Ağustos 2012, Geberit ürün yönetimi, formül
       örnekleriyle çapraz doğrulandı) farklıydı. Sadece Hotel doğruydu (0.70/0.48/0.13 —
       bire bir eşleşiyor); Residential/Hospital/Office/School YANLIŞTI. En kritik etki
       Residential'da: yanlış b=0.45 (gerçek: 0.19) kökten farklı bir eğri şekli üretiyordu.
       GetCoefficients() private olduğu için reflection ile doğrudan çağrılıp katsayı
       tam sayısal değerleri kilitleniyor — bu, graf/topoloji akışının (fu dağıtımı, m³/h
       dönüşümü vb.) ayrı karmaşıklığından bağımsız, doğrudan standart uygunluğunu test eder.
    */
    [Theory]
    [InlineData(BuildingType.Residential, 1.48, 0.19, 0.94)]
    [InlineData(BuildingType.Hotel, 0.70, 0.48, 0.13)]
    [InlineData(BuildingType.Hospital, 0.75, 0.44, 0.18)]
    [InlineData(BuildingType.Office, 0.91, 0.31, 0.38)]
    [InlineData(BuildingType.School, 0.91, 0.31, 0.38)]
    public void GetCoefficients_MatchesDin1988300Table1(BuildingType buildingType, double expectedA, double expectedB, double expectedC)
    {
        var graph = new MechanicalTopologyGraph();
        var calcService = new FlowCalculationService(graph) { CurrentBuildingType = buildingType };

        var method = typeof(FlowCalculationService).GetMethod("GetCoefficients",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = ((double a, double b, double c))method.Invoke(calcService, null)!;

        Assert.Equal(expectedA, result.a, precision: 3);
        Assert.Equal(expectedB, result.b, precision: 3);
        Assert.Equal(expectedC, result.c, precision: 3);
    }
}
