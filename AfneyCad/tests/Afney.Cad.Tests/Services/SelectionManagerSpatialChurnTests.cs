using Afney.Cad.Application.Services;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Services;

/*
   NE: SelectionManager - Gereksiz QuadTree Churn Testleri
   NEDEN: Performans denetiminde ToggleEntity/AddToSelection gibi salt seçim metodlarının,
          geometri hiç değişmediği hâlde CadDatabase.UpdateEntity(entity) çağırarak QuadTree'de
          gereksiz Remove+Insert (ve MechanicalKernel.EntityUpdated aboneleri için gereksiz
          yeniden hesaplama) tetiklediği tespit edildi. Bu testler:
          1. Seçim işlemlerinin EntityUpdated event'ini TETİKLEMEDİĞİNİ,
          2. entity.IsSelected bayrağının yine de doğru güncellendiğini (davranış aynı),
          doğrular.
*/
public class SelectionManagerSpatialChurnTests
{
    private static CircleEntity CreateCircle(double x = 0, double y = 0, double radius = 5)
        => new CircleEntity(new Vector3D(x, y, 0), radius);

    [Fact]
    public void AddToSelection_DoesNotRaiseEntityUpdated()
    {
        var db = new CadDatabase();
        var circle = CreateCircle();
        db.AddEntity(circle);

        int updatedCount = 0;
        db.EntityUpdated += _ => updatedCount++;

        var selection = new SelectionManager(db);
        selection.AddToSelection(circle);

        Assert.True(circle.IsSelected);
        Assert.Equal(0, updatedCount);
    }

    [Fact]
    public void ToggleEntity_SelectAndDeselect_DoesNotRaiseEntityUpdated_AndFlagIsCorrect()
    {
        var db = new CadDatabase();
        var circle = CreateCircle();
        db.AddEntity(circle);

        int updatedCount = 0;
        db.EntityUpdated += _ => updatedCount++;

        var selection = new SelectionManager(db);

        selection.ToggleEntity(circle.Id);
        Assert.True(circle.IsSelected);
        Assert.True(selection.IsSelected(circle.Id));

        selection.ToggleEntity(circle.Id);
        Assert.False(circle.IsSelected);
        Assert.False(selection.IsSelected(circle.Id));

        Assert.Equal(0, updatedCount);
    }

    [Fact]
    public void SelectByWindow_ManyEntities_DoesNotRaiseEntityUpdated()
    {
        var db = new CadDatabase();
        for (int i = 0; i < 50; i++)
        {
            db.AddEntity(CreateCircle(i * 2, i * 2, 0.5));
        }

        int updatedCount = 0;
        db.EntityUpdated += _ => updatedCount++;

        var selection = new SelectionManager(db);
        var box = new CadBoundingBox(new Vector3D(-1000, -1000, 0), new Vector3D(1000, 1000, 0));
        selection.SelectByWindow(box);

        Assert.True(selection.SelectedCount > 0);
        Assert.Equal(0, updatedCount);
    }

    [Fact]
    public void ClearSelection_DoesNotRaiseEntityUpdated_AndClearsFlags()
    {
        var db = new CadDatabase();
        var circle = CreateCircle();
        db.AddEntity(circle);

        var selection = new SelectionManager(db);
        selection.AddToSelection(circle);

        int updatedCount = 0;
        db.EntityUpdated += _ => updatedCount++;

        selection.ClearSelection();

        Assert.False(circle.IsSelected);
        Assert.Equal(0, selection.SelectedCount);
        Assert.Equal(0, updatedCount);
    }
}
