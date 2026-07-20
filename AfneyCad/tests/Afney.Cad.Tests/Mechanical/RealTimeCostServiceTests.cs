using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: RealTimeCostService Testleri
   NEDEN — GERÇEK, ÖNCEDEN VAR OLAN BİR HATA: `CalculateProjectCost` önceden boru
          malzeme fiyatını `pipe.SystemType` (DomesticColdWater/WasteWater/...) ile
          `_priceTable`'da (PPRC_PN20/PVC_SN4/...) arıyordu — bu iki küme HİÇ kesişmiyordu,
          yani her boru sessizce varsayılan (50 TRY/m) fiyata düşüyordu, gerçek malzemesi
          ne olursa olsun. Bu testler artık `pipe.PipeMaterialType` kullanıldığını ve farklı
          malzemelerin GERÇEKTEN farklı (ve fiyat tablosuyla eşleşen) maliyet ürettiğini
          kanıtlıyor.
*/
public class RealTimeCostServiceTests
{
    [Fact]
    public void CalculateProjectCost_UsesPipeMaterialType_NotSystemType()
    {
        // Aynı sistem tipi (DomesticColdWater), farklı malzeme — eski hatalı kodda ikisi de
        // aynı varsayılan fiyata düşerdi çünkü SystemType hiçbir zaman fiyat tablosunda
        // bulunamazdı. Artık malzemeye göre GERÇEKTEN farklı maliyet çıkmalı.
        var dbCheap = new CadDatabase();
        var pprc = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            PipeMaterialType = PipeMaterial.PPRC_PN20
        };
        dbCheap.AddEntity(pprc);

        var dbExpensive = new CadDatabase();
        var steel = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            PipeMaterialType = PipeMaterial.Steel_Galvanized
        };
        dbExpensive.AddEntity(steel);

        var service = new RealTimeCostService();
        var cheapCost = service.CalculateProjectCost(dbCheap);
        var expensiveCost = service.CalculateProjectCost(dbExpensive);

        Assert.True(expensiveCost.PipeCost > cheapCost.PipeCost,
            $"Galvanizli çelik, PPRC'den daha pahalı olmalı. PPRC: {cheapCost.PipeCost}, Çelik: {expensiveCost.PipeCost}");
    }

    [Fact]
    public void CalculateProjectCost_PipeCost_MatchesPriceTableWithinDnFactor()
    {
        // 10m, DN20, PVC_SN4: taban fiyat 35 TRY/m, DN faktörü = 1 + (20-15)/100 = 1.05
        var db = new CadDatabase();
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 20)
        {
            PipeMaterialType = PipeMaterial.PVC_SN4
        };
        db.AddEntity(pipe);

        var service = new RealTimeCostService();
        var result = service.CalculateProjectCost(db);

        double expected = 10.0 * (35.0 * 1.05);
        Assert.Equal(expected, result.PipeCost, precision: 2);
    }

    [Fact]
    public void CalculateSinglePipeCost_DifferentMaterials_ProduceDifferentPrices()
    {
        var service = new RealTimeCostService();

        double pprcCost = service.CalculateSinglePipeCost(5000, PipeMaterial.PPRC_PN20, 20);
        double steelCost = service.CalculateSinglePipeCost(5000, PipeMaterial.Steel_Galvanized, 20);

        Assert.NotEqual(pprcCost, steelCost);
        Assert.True(steelCost > pprcCost);
    }
}
