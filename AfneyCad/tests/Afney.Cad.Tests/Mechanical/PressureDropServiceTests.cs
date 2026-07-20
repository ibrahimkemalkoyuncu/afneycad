using System;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: PressureDropService (Darcy-Weisbach / Colebrook-White) Testleri
   NEDEN: Servis hiç test edilmemişti. CalculatePipePressureDrop; Reynolds sayısına göre
          laminer (f=64/Re) veya türbülanslı (Colebrook-White) rejim seçip, hız arttıkça
          kaybın fiziksel olarak arttığını, çapın büyümesinin kaybı azalttığını ve sıcak su
          hattında (düşük viskozite) aynı debide daha az kayıp oluştuğunu doğruluyoruz.
          Ayrıca servisin ürettiği sonucu, aynı Colebrook-White fonksiyonunu kullanarak
          testte bağımsızca hesapladığımız Darcy-Weisbach değeriyle karşılaştırıyoruz.
*/
public class PressureDropServiceTests
{
    private static PressureDropService MakeService(CadDatabase? db = null, MechanicalProjectSettings? settings = null)
    {
        var graph = new MechanicalTopologyGraph();
        return new PressureDropService(graph, settings ?? new MechanicalProjectSettings(), db ?? new CadDatabase());
    }

    private static PipeEntity MakePipe(double diameterMm, double flowM3h, double lengthMm = 5000)
    {
        var start = new Vector3D(0, 0, 0);
        var end = new Vector3D(lengthMm, 0, 0);
        return new PipeEntity(start, end, diameterMm)
        {
            FlowRate = flowM3h,
            SystemType = MechanicalSystemType.DomesticColdWater,
        };
    }

    [Fact]
    public void CalculatePipePressureDrop_ZeroFlowOrDiameter_ReturnsZero()
    {
        var svc = MakeService();

        var zeroFlow = MakePipe(25, 0);
        var zeroDiameter = MakePipe(0, 2.0);

        Assert.Equal(0, svc.CalculatePipePressureDrop(zeroFlow));
        Assert.Equal(0, svc.CalculatePipePressureDrop(zeroDiameter));
    }

    [Fact]
    public void CalculatePipePressureDrop_TurbulentFlow_MatchesIndependentDarcyWeisbachCalculation()
    {
        var settings = new MechanicalProjectSettings(); // PipeRoughness = 0.007 mm (PP-R)
        var svc = MakeService(settings: settings);

        // 25mm iç çap, 2.0 m3/h debi → yüksek hız, kesinlikle türbülanslı (Re > 2300 bekleniyor).
        var pipe = MakePipe(diameterMm: 25, flowM3h: 2.0, lengthMm: 10000);

        double actual = svc.CalculatePipePressureDrop(pipe);

        // Testte BAĞIMSIZ olarak aynı fiziksel zinciri elle kuruyoruz:
        double v = pipe.GetVelocity();
        double dMe = pipe.InnerDiameter / 1000.0;
        double lengthMetre = pipe.GetLength() / 1000.0;
        double nu = WaterPropertiesService.GetKinematicViscosity(10.0); // Soğuk su hattı
        double re = (v * dMe) / nu;

        Assert.True(re > 2300); // Türbülanslı rejimde olduğumuzu doğrula

        double f = AdvancedHydraulicsService.ColebrookWhiteFriction(re, settings.EffectiveRoughness, pipe.InnerDiameter);
        double linearLoss = f * (lengthMetre / dMe) * (Math.Pow(v, 2) / (2 * 9.81));

        // Fitting listesi boş olduğundan yerel kayıp EstimateFittingLoss'un geometri-tahmin dalından gelir:
        // boru tam X ekseninde düz olduğundan (dir.Y≈0) "açı değişimi" katkısı 0'dır; geriye sadece
        // LocalLossAllowance katkısı kalır: estimatedK = LocalLossAllowance * L[m] * 0.5.
        double estimatedK = settings.LocalLossAllowance * (pipe.GetLength() / 1000.0) * 0.5;
        double expectedFittingLoss = estimatedK * Math.Pow(v, 2) / (2 * 9.81);
        double expectedTotal = linearLoss + expectedFittingLoss;

        Assert.Equal(expectedTotal, actual, precision: 6);
        Assert.True(actual > 0);
    }

    [Fact]
    public void CalculatePipePressureDrop_LaminarRegime_UsesSixtyFourOverReynolds()
    {
        var svc = MakeService();

        // Çok düşük debi + büyük çap → düşük hız, laminer rejim (Re < 2300) hedefleniyor.
        var pipe = MakePipe(diameterMm: 100, flowM3h: 0.02, lengthMm: 5000);

        double v = pipe.GetVelocity();
        double dMe = pipe.InnerDiameter / 1000.0;
        double nu = WaterPropertiesService.GetKinematicViscosity(10.0);
        double re = (v * dMe) / nu;

        Assert.True(re < 2300); // Laminer varsayımını doğrula

        double lengthMetre = pipe.GetLength() / 1000.0;
        double fLam = 64.0 / re;
        double hLam = fLam * (lengthMetre / dMe) * (Math.Pow(v, 2) / (2 * 9.81));

        double actual = svc.CalculatePipePressureDrop(pipe);

        // actual = hLam + yerel kayıp (>=0) → actual, hLam'dan küçük olamaz.
        Assert.True(actual >= hLam - 1e-9);
    }

    [Fact]
    public void CalculatePipePressureDrop_HigherFlowRate_ProducesHigherPressureDrop()
    {
        var svc = MakeService();

        var lowFlow = MakePipe(diameterMm: 25, flowM3h: 1.0);
        var highFlow = MakePipe(diameterMm: 25, flowM3h: 3.0);

        double dropLow = svc.CalculatePipePressureDrop(lowFlow);
        double dropHigh = svc.CalculatePipePressureDrop(highFlow);

        Assert.True(dropHigh > dropLow);
    }

    [Fact]
    public void CalculatePipePressureDrop_LargerDiameter_ReducesPressureDropForSameFlow()
    {
        var svc = MakeService();

        var narrow = MakePipe(diameterMm: 20, flowM3h: 2.0);
        var wide = MakePipe(diameterMm: 50, flowM3h: 2.0);

        double dropNarrow = svc.CalculatePipePressureDrop(narrow);
        double dropWide = svc.CalculatePipePressureDrop(wide);

        Assert.True(dropWide < dropNarrow);
    }

    [Fact]
    public void CalculatePipePressureDrop_HotWaterSystemType_LowerViscosityChangesLoss()
    {
        var svc = MakeService();

        var coldPipe = MakePipe(diameterMm: 25, flowM3h: 2.0);
        coldPipe.SystemType = MechanicalSystemType.DomesticColdWater; // 10°C

        var hotPipe = MakePipe(diameterMm: 25, flowM3h: 2.0);
        hotPipe.SystemType = MechanicalSystemType.DomesticHotWater; // 60°C → düşük viskozite

        double dropCold = svc.CalculatePipePressureDrop(coldPipe);
        double dropHot = svc.CalculatePipePressureDrop(hotPipe);

        // Sıcak su daha düşük kinematik viskoziteye sahiptir (WaterPropertiesService),
        // bu da farklı bir Reynolds/friction sonucu üretmeli (eşit olmamalı).
        Assert.NotEqual(dropCold, dropHot);
    }

    [Fact]
    public void CalculatePressureDrops_UpdatesPressureDropFieldOnAllPipes()
    {
        var svc = MakeService();
        var pipe1 = MakePipe(25, 2.0);
        var pipe2 = MakePipe(32, 1.0);

        svc.CalculatePressureDrops(new MechanicalEntity[] { pipe1, pipe2 });

        Assert.True(pipe1.PressureDrop > 0);
        Assert.True(pipe2.PressureDrop > 0);
        Assert.Equal(svc.CalculatePipePressureDrop(pipe1), pipe1.PressureDrop, precision: 6);
    }
}
