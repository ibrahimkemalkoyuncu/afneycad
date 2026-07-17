using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: DimensionStyleService Yönetim Testleri
   NEDEN: DimensionStyleService yazılmıştı ama hiçbir UI'dan oluşturulan/düzenlenen/kaydedilen
          stilleri kalıcı hale getiren bir yol yoktu — bu oturumda eklenen
          DimensionStyleManagerDialog, AddStyle/RemoveStyle/ExportToJson/ImportFromJson
          metodlarına dayanıyor. Bu testler o alt katmanın (WPF olmayan kısmın) gerçekten
          doğru çalıştığını doğruluyor: özel stil ekleme, ArrowStyle dahil round-trip.
*/
public class DimensionStyleServiceManagementTests
{
    [Fact]
    public void AddStyle_CustomStyle_IsRetrievableAndSelectable()
    {
        var svc = new DimensionStyleService();
        svc.AddStyle(new DimensionStyle { Name = "Mimari-1_50", TextHeight = 300, ArrowStyle = "Architectural" });

        svc.SetActiveStyle("Mimari-1_50");

        Assert.Equal("Mimari-1_50", svc.ActiveStyleName);
        Assert.Equal("Architectural", svc.ActiveStyle.ArrowStyle);
    }

    [Fact]
    public void ExportImportJson_RoundTrip_PreservesCustomStyleAndArrowStyle()
    {
        var svc = new DimensionStyleService();
        svc.AddStyle(new DimensionStyle { Name = "Ozel", TextHeight = 400, ArrowStyle = "Dot", Precision = 2 });
        svc.SetActiveStyle("Ozel");

        string json = svc.ExportToJson();

        var svc2 = new DimensionStyleService();
        svc2.ImportFromJson(json);

        Assert.Contains("Ozel", svc2.StyleNames);
        var restored = svc2.GetStyle("Ozel");
        Assert.NotNull(restored);
        Assert.Equal(400, restored!.TextHeight, precision: 6);
        Assert.Equal("Dot", restored.ArrowStyle);
        Assert.Equal(2, restored.Precision);
        Assert.Equal("Ozel", svc2.ActiveStyleName);
    }

    [Fact]
    public void RemoveStyle_StandardCannotBeRemoved()
    {
        var svc = new DimensionStyleService();
        svc.RemoveStyle("Standard");

        Assert.Contains("Standard", svc.StyleNames);
    }

    [Fact]
    public void RemoveStyle_CustomStyle_IsActuallyRemoved()
    {
        var svc = new DimensionStyleService();
        svc.AddStyle(new DimensionStyle { Name = "Gecici" });
        svc.RemoveStyle("Gecici");

        Assert.DoesNotContain("Gecici", svc.StyleNames);
    }
}
