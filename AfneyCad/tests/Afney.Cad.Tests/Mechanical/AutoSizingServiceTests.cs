using System;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: AutoSizingService Testleri (AutoSizingServiceTests)
   NEDEN: "Otomatik Boru Boyutlandır" komutunun (TS EN 806-3 / TS 1258 tabanlı) tüm boruları
          tek seferde çaplandıran ana motoruydu — hiç testi yoktu. Bu testler; FU->Debi
          (Walther yaklaşımı) dönüşümünün, debi->çap (süreklilik denklemi) hesabının,
          standart DN'e yukarı yuvarlamanın ve WC minimum DN100 kuralının (TS 1258)
          doğru uygulandığını doğruluyor.

   KAPSAM:
   1. FuToDesignFlow — Basınçlı sistemler (lineer bölge FU<5, Walther bölge FU>=5)
   2. FuToDesignFlow — Atık su / yağmur suyu (Manning bazlı, min 0.3 l/s tabanı)
   3. SizeAll — Uçtan uca: FU=0 borular atlanır, WC min DN100 uygulanır, DN standarda yuvarlanır
*/
public class AutoSizingServiceTests
{
    // ─────────────────────────────────────────────────────────────────
    // 1. FuToDesignFlow — Basınçlı sistemler (DomesticColdWater/HotWater vb.)
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0.25)]  // 0.15 + 0.10*1 = 0.25
    [InlineData(4, 0.55)]  // 0.15 + 0.10*4 = 0.55
    public void FuToDesignFlow_LowFuRegion_UsesLinearFormula(double fu, double expected)
    {
        double result = AutoSizingService.FuToDesignFlow(fu, MechanicalSystemType.DomesticColdWater);
        Assert.Equal(expected, result, precision: 9);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    [InlineData(100)]
    public void FuToDesignFlow_HighFuRegion_UsesWaltherFormula(double fu)
    {
        double expected = 0.682 * Math.Sqrt(fu);
        double result = AutoSizingService.FuToDesignFlow(fu, MechanicalSystemType.DomesticHotWater);
        Assert.Equal(expected, result, precision: 9);
    }

    [Fact]
    public void FuToDesignFlow_ZeroOrNegativeFu_ReturnsZero()
    {
        Assert.Equal(0, AutoSizingService.FuToDesignFlow(0, MechanicalSystemType.DomesticColdWater));
        Assert.Equal(0, AutoSizingService.FuToDesignFlow(-3, MechanicalSystemType.DomesticColdWater));
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. FuToDesignFlow — Atık su / Yağmur suyu (Manning bazlı)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FuToDesignFlow_WasteWater_UsesManningBasedFormula()
    {
        double fu = 16;
        double expected = Math.Max(0.3, 0.5 * Math.Sqrt(fu)); // 0.5*4=2.0
        double result = AutoSizingService.FuToDesignFlow(fu, MechanicalSystemType.WasteWater);
        Assert.Equal(expected, result, precision: 9);
        Assert.Equal(2.0, result, precision: 9);
    }

    [Fact]
    public void FuToDesignFlow_WasteWater_SmallFu_ClampsToMinimum0_3()
    {
        // 0.5*sqrt(0.1) = 0.158, minimum tabanın (0.3) altında kalır -> 0.3'e kenetlenmeli.
        double result = AutoSizingService.FuToDesignFlow(0.1, MechanicalSystemType.WasteWater);
        Assert.Equal(0.3, result, precision: 9);
    }

    [Fact]
    public void FuToDesignFlow_RainWater_SameFormulaAsWasteWater()
    {
        double fu = 9;
        double waste = AutoSizingService.FuToDesignFlow(fu, MechanicalSystemType.WasteWater);
        double rain = AutoSizingService.FuToDesignFlow(fu, MechanicalSystemType.RainWater);
        Assert.Equal(waste, rain, precision: 9);
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. SizeAll — Uçtan uca boyutlandırma davranışı
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SizeAll_PipeWithZeroLoadUnits_IsSkippedAndWarned()
    {
        var db = new CadDatabase();
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            LoadUnits = 0
        };
        db.AddEntity(pipe);

        var svc = new AutoSizingService();
        var result = svc.SizeAll(db);

        Assert.Equal(1, result.TotalPipes);
        Assert.Equal(1, result.UnchangedPipes);
        Assert.Empty(result.Changes);
        Assert.Contains(result.Warnings, w => w.Contains("FU"));
    }

    [Fact]
    public void SizeAll_WcLoadPipe_EnforcesMinimumDN100()
    {
        var db = new CadDatabase();
        // Düşük FU (1) normalde küçük çap gerektirir ama WC yükü taşıyorsa min DN100 uygulanmalı (TS 1258).
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.WasteWater,
            LoadUnits = 1,
            IsCarryingWCLoad = true
        };
        db.AddEntity(pipe);

        var svc = new AutoSizingService();
        var result = svc.SizeAll(db);

        Assert.Equal(1, result.WCMinimumApplied);
        Assert.Equal(100.0, pipe.InnerDiameter);
        Assert.Single(result.Changes);
        Assert.Contains("WC min DN100", result.Changes[0].Reason);
    }

    [Fact]
    public void SizeAll_LargeColdWaterLoad_ResizesToStandardDNAndComputesFlow()
    {
        var db = new CadDatabase();
        // FU=100 (Walther bölgesi) -> Q = 0.682*sqrt(100) = 6.82 l/s
        // vMax(DomesticColdWater)=2.0 m/s -> d = sqrt(4*(6.82/1000)/(pi*2.0))*1000 mm
        double fu = 100;
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            LoadUnits = fu
        };
        db.AddEntity(pipe);

        var svc = new AutoSizingService();
        var result = svc.SizeAll(db);

        double expectedFlowLs = 0.682 * Math.Sqrt(fu);
        double expectedFlowM3h = expectedFlowLs * 3.6;
        double expectedReqDiaMm = Math.Sqrt(4.0 * (expectedFlowLs / 1000.0) / (Math.PI * 2.0)) * 1000.0;

        double[] standardDN = { 12, 16, 20, 25, 32, 40, 50, 63, 75, 90, 100, 125, 150, 200, 250, 300 };
        double expectedDN = standardDN.First(dn => dn >= expectedReqDiaMm);

        Assert.Equal(expectedDN, pipe.InnerDiameter);
        Assert.Equal(expectedFlowM3h, pipe.FlowRate, precision: 6);
        Assert.Equal(1, result.ResizedPipes);
        Assert.Equal(0, result.WCMinimumApplied);
    }

    [Fact]
    public void SizeAll_UnknownSystemType_FallsBackToDefaultMaxVelocity2_0()
    {
        // Ventilation TS EN 806-3 hız tablosunda 5.0 m/s ile tanımlı; ama sözlükte olmayan
        // bir sistem tipi (Undefined) DefaultMaxVelocity = 2.0 m/s'e düşmelidir.
        var db = new CadDatabase();
        double fu = 25; // Q = 0.682*sqrt(25) = 3.41 l/s
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.Undefined,
            LoadUnits = fu
        };
        db.AddEntity(pipe);

        var svc = new AutoSizingService();
        svc.SizeAll(db);

        double expectedFlowLs = 0.682 * Math.Sqrt(fu);
        double expectedReqDiaMm = Math.Sqrt(4.0 * (expectedFlowLs / 1000.0) / (Math.PI * 2.0)) * 1000.0;

        double[] standardDN = { 12, 16, 20, 25, 32, 40, 50, 63, 75, 90, 100, 125, 150, 200, 250, 300 };
        double expectedDN = standardDN.First(dn => dn >= expectedReqDiaMm);

        Assert.Equal(expectedDN, pipe.InnerDiameter);
    }

    [Fact]
    public void SizeAll_SummaryText_ReflectsCounts()
    {
        var db = new CadDatabase();
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            LoadUnits = 0 // atlanacak
        });
        db.AddEntity(new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.WasteWater,
            LoadUnits = 1,
            IsCarryingWCLoad = true // WC min uygulanacak
        });

        var svc = new AutoSizingService();
        var result = svc.SizeAll(db);

        Assert.Equal(2, result.TotalPipes);
        Assert.Contains("2 boru kontrol edildi", result.Summary);
        Assert.Contains("1 boruda DN100 min. uygulandı", result.Summary);
    }
}
