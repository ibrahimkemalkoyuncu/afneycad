using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: CSG Boolean Komut Entegrasyon Testleri (SolidUnionCommand/SolidSubtractCommand/
       SolidIntersectCommand)
   NEDEN: Denetim raporu bulgusu — `GeneralSolidUnion`/`GeneralSolidSubtractor`/
          `GeneralSolidIntersector` (Geometry katmanında 506 testle doğrulanmış) Presentation
          katmanında SIFIR referansla duruyordu. Bu testler, kernel'in artık gerçek bir CAD
          komutu (seç → seç → uygula) üzerinden CadDatabase'i DOĞRU güncellediğini — eski iki
          SolidEntity'nin silinip TEK bir sonuç SolidEntity'nin eklendiğini, ve TÜM işlemin TEK
          bir Undo/Redo adımında geri alınabilir/yinelenebilir olduğunu — kilitler.

   GİRDİ: GeneralSolidUnionTests.Union_TrueCornerNotch_ThreePlanes... ile AYNI iki kutu
          (A=[0,2000]^3, B=[1500,3000]^3 köşe-çakışması, coplanar OLMAYAN — kernel'in
          desteklediği bilinen-geçerli senaryo) — kernel'in kendi doğruluğu zaten Geometry
          testlerinde kanıtlı, burada sadece KOMUT/VERİTABANI kablolamasının doğruluğu test edilir.
*/
public class SolidBooleanCommandTests
{
    private static (CadDatabase db, SolidEntity a, SolidEntity b) MakeOverlappingBoxes()
    {
        var db = new CadDatabase();
        var solidA = BRepBuilder.ExtrudeBox(new Vector3D(0, 0, 0), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
        var solidB = BRepBuilder.ExtrudeBox(new Vector3D(1500, 1500, 1500), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 1500, 1500, 1500);
        var a = new SolidEntity(solidA);
        var b = new SolidEntity(solidB);
        db.AddEntity(a);
        db.AddEntity(b);
        return (db, a, b);
    }

    [Fact]
    public void SolidUnionCommand_TwoOverlappingBoxes_ReplacesOriginalsWithSingleValidResult()
    {
        var (db, a, b) = MakeOverlappingBoxes();
        var aCenter = a.GetBoundingBox().Center;
        var bCenter = b.GetBoundingBox().Center;

        var cmd = new SolidUnionCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(aCenter);
        cmd.OnPointerPressed(bCenter);

        var solids = db.GetAllEntities().OfType<SolidEntity>().ToList();
        var result = Assert.Single(solids);
        Assert.True(result.Solid.IsValid());

        double intersectionVolume = 500.0 * 500.0 * 500.0;
        double expectedVolume = 2000.0 * 2000.0 * 2000.0 + 1500.0 * 1500.0 * 1500.0 - intersectionVolume;
        Assert.Equal(expectedVolume, result.Solid.GetVolume(), precision: 3);
    }

    [Fact]
    public void SolidUnionCommand_Undo_RestoresOriginalTwoSolidsAsOneStep()
    {
        var (db, _, _) = MakeOverlappingBoxes();
        var a = db.GetAllEntities().OfType<SolidEntity>().First();
        var b = db.GetAllEntities().OfType<SolidEntity>().Skip(1).First();

        var cmd = new SolidUnionCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(a.GetBoundingBox().Center);
        cmd.OnPointerPressed(b.GetBoundingBox().Center);

        Assert.Single(db.GetAllEntities().OfType<SolidEntity>());

        db.TransactionManager.Undo();

        var restored = db.GetAllEntities().OfType<SolidEntity>().ToList();
        Assert.Equal(2, restored.Count);
        Assert.Contains(restored, e => e.Id == a.Id);
        Assert.Contains(restored, e => e.Id == b.Id);

        db.TransactionManager.Redo();
        Assert.Single(db.GetAllEntities().OfType<SolidEntity>());
    }

    [Fact]
    public void SolidSubtractCommand_TwoOverlappingBoxes_ProducesValidResultWithSubtractedVolume()
    {
        var (db, a, b) = MakeOverlappingBoxes();
        var cmd = new SolidSubtractCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(a.GetBoundingBox().Center);
        cmd.OnPointerPressed(b.GetBoundingBox().Center);

        var result = Assert.Single(db.GetAllEntities().OfType<SolidEntity>());
        Assert.True(result.Solid.IsValid());

        double intersectionVolume = 500.0 * 500.0 * 500.0;
        double expectedVolume = 2000.0 * 2000.0 * 2000.0 - intersectionVolume;
        Assert.Equal(expectedVolume, result.Solid.GetVolume(), precision: 3);
    }

    [Fact]
    public void SolidIntersectCommand_TwoOverlappingBoxes_ProducesValidResultWithIntersectionVolume()
    {
        var (db, a, b) = MakeOverlappingBoxes();
        var cmd = new SolidIntersectCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(a.GetBoundingBox().Center);
        cmd.OnPointerPressed(b.GetBoundingBox().Center);

        var result = Assert.Single(db.GetAllEntities().OfType<SolidEntity>());
        Assert.True(result.Solid.IsValid());

        double expectedVolume = 500.0 * 500.0 * 500.0;
        Assert.Equal(expectedVolume, result.Solid.GetVolume(), precision: 3);
    }

    [Fact]
    public void SolidBoxCommand_TwoClicks_AddsValidSolidEntityWithExpectedVolume()
    {
        var db = new CadDatabase();
        var cmd = new SolidBoxCommand(db, db.TransactionManager, heightMm: 800);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 500, 0));

        var entity = Assert.Single(db.GetAllEntities().OfType<SolidEntity>());
        Assert.True(entity.Solid.IsValid());
        Assert.Equal(1000.0 * 500.0 * 800.0, entity.Solid.GetVolume(), precision: 3);
    }
}
