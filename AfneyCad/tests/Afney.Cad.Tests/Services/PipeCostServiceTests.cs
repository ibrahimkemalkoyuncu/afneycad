using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Services;

/*
   NE: PipeCostService.CalculateFromDatabase — Birim Dönüşüm Regresyon Testi
   NEDEN: Denetim taraması sırasında GERÇEK, UI'a bağlı (BOMDialog.xaml.cs) bir hata bulundu:
          `pipe.Length` (== StartPoint.DistanceTo(EndPoint)) uygulama genelinde mm cinsinden —
          ama `CalculateFromDatabase` bunu doğrudan `PricePerMeterTl` (TL/METRE) ile çarpıyordu,
          hiç /1000 yapmadan. Sonuç: her maliyet 1000 KAT şişikti (10 metrelik bir boru,
          10 metrelik değil 10.000 metrelik gibi fiyatlandırılıyordu). Bu test o dönüşümün
          kalıcı olduğunu kilitliyor.
*/
public class PipeCostServiceTests
{
    [Fact]
    public void CalculateFromDatabase_TenMeterSteelPipe_ProducesCorrectlyScaledCost()
    {
        var db = new CadDatabase();
        // 10 metre (10.000mm) uzunluğunda DN25 çelik boru — katalogda tam eşleşen fiyat: 140 TL/m malzeme, 55 TL/m işçilik.
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(10000, 0, 0), 25);
        db.AddEntity(pipe);

        var svc = new PipeCostService();
        var result = svc.CalculateFromDatabase(db, contingencyPct: 0);

        var item = Assert.Single(result.Items);
        Assert.Equal(10.0, item.LengthM, precision: 3);
        Assert.Equal(1400.0, item.MaterialCostTl, precision: 1); // 140 TL/m × 10m
        Assert.Equal(550.0, item.LaborCostTl, precision: 1);     // 55 TL/m × 10m

        // Eski (hatalı) davranışta bu sayılar 1000 kat büyük (1.400.000 / 550.000) çıkardı.
        Assert.True(result.TotalMaterialTl < 10000,
            $"Malzeme maliyeti {result.TotalMaterialTl} TL — mm/m dönüşümü unutulmuş olabilir (1000x şişik).");
    }
}
