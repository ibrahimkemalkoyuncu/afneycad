using System.Linq;
using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: PlacePipeWizardTemplateCommand Testleri
   NEDEN — GERÇEK HATA (Session #75 iş akışı denetiminde bulundu): PipeWizardDialog önceden
          şablonu HER ZAMAN sabit dünya orijinine ((0,0,0)) yerleştiriyordu ve TransactionManager'ı
          hiç kullanmıyordu (Ctrl+Z çalışmıyordu). Bu testler, yeni komutun (a) şablonu GERÇEKTEN
          tıklanan noktaya göre yerleştirdiğini ve (b) TransactionManager üzerinden Submit edip
          Undo ile tamamen geri alınabildiğini kilitler.
*/
public class PlacePipeWizardTemplateCommandTests
{
    [Fact]
    public void OnPointerPressed_PlacesTemplateAtClickedPoint_NotAtWorldOrigin()
    {
        var db = new CadDatabase();
        var cmd = new PlacePipeWizardTemplateCommand(
            db, db.TransactionManager, PipeWizardService.TemplateType.GuestToilet, MechanicalSystemType.DomesticColdWater);

        var clickPoint = new Vector3D(5000, 3000, 0);
        cmd.Start();
        cmd.OnPointerPressed(clickPoint);

        var entities = db.GetAllEntities().ToList();
        Assert.NotEmpty(entities);

        // Şablonun en az bir nesnesi tıklanan noktaya (world origin'e değil) yakın olmalı —
        // eskiden her zaman (0,0,0) civarına yerleşirdi.
        bool anyNearClickPoint = entities.Any(e => e.GetBoundingBox().Center.DistanceTo(clickPoint) < 2000);
        bool anyNearWorldOrigin = entities.Any(e => e.GetBoundingBox().Center.DistanceTo(Vector3D.Zero) < 100);

        Assert.True(anyNearClickPoint, "Şablon tıklanan noktaya yakın hiçbir nesne üretmedi.");
        Assert.False(anyNearWorldOrigin, "Şablon hâlâ (eski hatadaki gibi) dünya orijinine yerleşiyor.");
    }

    [Fact]
    public void OnPointerPressed_SubmitsThroughTransactionManager_UndoRemovesEverything()
    {
        var db = new CadDatabase();
        var cmd = new PlacePipeWizardTemplateCommand(
            db, db.TransactionManager, PipeWizardService.TemplateType.StandardBathroom, MechanicalSystemType.DomesticColdWater);

        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(1000, 1000, 0));

        Assert.NotEmpty(db.GetAllEntities());
        Assert.True(db.TransactionManager.CanUndo);

        db.TransactionManager.Undo();

        Assert.Empty(db.GetAllEntities());
    }

    [Fact]
    public void OnPointerPressed_RaisesCompletedEvent()
    {
        var db = new CadDatabase();
        var cmd = new PlacePipeWizardTemplateCommand(
            db, db.TransactionManager, PipeWizardService.TemplateType.Kitchen, MechanicalSystemType.WasteWater);

        bool completed = false;
        cmd.OnCompleted += () => completed = true;

        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));

        Assert.True(completed);
    }
}
