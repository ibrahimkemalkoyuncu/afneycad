using System;
using System.IO;
using System.Linq;
using Afney.Cad.Application.Services;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Infrastructure.IO;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Mechanical.Standards;
using Xunit;

namespace Afney.Cad.Tests.Regression;

/*
   NE: Kod Denetimi (Analiz Raporu) Düzeltmeleri — Regresyon Testleri
   NEDEN: P0/P1/P2 denetiminde bulunan hataların düzeltmeleri geri gelmesin diye; her test,
          düzeltmeden ÖNCEKİ hatalı davranışı yakalayacak şekilde yazıldı.
*/
public class AuditFixesRegressionTests
{
    // ── CompositeOperation atomikliği ────────────────────────────────────────────

    private sealed class ThrowingOperation : IOperation
    {
        public string Name => "throws";
        public void Do() => throw new InvalidOperationException("simüle hata");
        public void Undo() { }
    }

    [Fact]
    public void CompositeOperation_WhenSubOperationThrows_RollsBackAppliedOnes()
    {
        var db = new CadDatabase();
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));

        var composite = new CompositeOperation("test");
        composite.Add(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(db, line));
        composite.Add(new ThrowingOperation());

        Assert.Throws<InvalidOperationException>(() => db.TransactionManager.Submit(composite));

        Assert.Empty(db.GetAllEntities());   // kısmi mutasyon kalmamalı
        Assert.False(db.TransactionManager.CanUndo); // ve sahte bir undo kaydı da olmamalı
    }

    // ── Kilitli/dondurulmuş katman seçilemez ─────────────────────────────────────

    [Fact]
    public void SelectionManager_LockedLayer_EntityNotSelectable()
    {
        var db = new CadDatabase();
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(100, 0, 0));
        db.AddEntity(line);
        db.GetLayer(line.Layer).IsLocked = true;

        var sel = new SelectionManager(db);
        sel.AddToSelection(line);
        sel.ToggleEntity(line.Id);
        sel.SelectByCrossing(new CadBoundingBox(new Vector3D(-10, -10, -10), new Vector3D(200, 10, 10)));

        Assert.Equal(0, sel.SelectedCount);
    }

    [Fact]
    public void SelectionManager_UnlockedLayer_StillSelectable()
    {
        var db = new CadDatabase();
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(100, 0, 0));
        db.AddEntity(line);

        var sel = new SelectionManager(db);
        sel.AddToSelection(line);

        Assert.Equal(1, sel.SelectedCount);
    }

    // ── Blok Transform: Rotation/Scale ───────────────────────────────────────────

    [Fact]
    public void BlockReference_Transform_AppliesRotationAndUniformScale()
    {
        var block = new BlockReferenceEntity("B", new Vector3D(10, 0, 0));

        var m = Matrix4x4.Scaling(2, 2, 2) * Matrix4x4.RotationZ(Math.PI / 2);
        block.Transform(m);

        Assert.Equal(2.0, block.Scale, 6);
        Assert.Equal(90.0, block.Rotation, 6);
        Assert.Equal(0.0, block.Position.X, 6);
        Assert.Equal(20.0, block.Position.Y, 6);
    }

    [Fact]
    public void BlockReference_Transform_ThenInverse_RestoresOriginalState()
    {
        var block = new BlockReferenceEntity("B", new Vector3D(10, 5, 0)) { Scale = 1.5, Rotation = 30 };
        var t1 = Matrix4x4.TranslationMatrix(-3, -4, 0);
        var r = Matrix4x4.RotationZ(0.7);
        var t2 = Matrix4x4.TranslationMatrix(3, 4, 0);

        block.Transform(t2 * r * t1);
        block.Transform(t2 * Matrix4x4.RotationZ(-0.7) * t1);

        Assert.Equal(1.5, block.Scale, 6);
        Assert.Equal(30.0, block.Rotation, 6);
        Assert.Equal(10.0, block.Position.X, 6);
        Assert.Equal(5.0, block.Position.Y, 6);
    }

    // ── ThermalExpansionService payda koruması ───────────────────────────────────

    [Fact]
    public void ThermalExpansion_ValveBelowPrecharge_Throws()
    {
        var svc = new ThermalExpansionService { StaticHeadM = 40, MaxPressureBar = 3.0, SystemVolumeL = 200 };
        Assert.Throws<InvalidOperationException>(() => svc.Calculate());
    }

    // ── StandardsLibrary: malzeme bazlı arama ────────────────────────────────────

    [Theory]
    [InlineData("PVC")]
    [InlineData("PPRC")]
    [InlineData("Steel")]
    public void StandardsLibrary_GetStandardForMaterial_CoversLiveMaterials(string material)
    {
        var std = new StandardsLibrary().GetStandardForMaterial(material);
        Assert.NotNull(std);
        Assert.NotEmpty(std!.AvailableSizes);
    }

    [Fact]
    public void StandardsLibrary_Steel_DN50_HasDin2440Dimensions()
    {
        var def = new StandardsLibrary().GetStandardForMaterial("Steel")!.GetBySize(50);
        Assert.NotNull(def);
        Assert.Equal(60.3, def!.OuterDiameter, 2);
        Assert.Equal(3.65, def.WallThickness, 2);
    }

    // ── DXF: Türkçe karakter kaybı ───────────────────────────────────────────────

    [Fact]
    public void DxfWriter_TurkishText_IsEscapedNotReplacedWithQuestionMark()
    {
        var db = new CadDatabase();
        db.AddEntity(new TextEntity("ŞİĞÜÖÇış", new Vector3D(0, 0, 0), 10, 0));

        string path = Path.Combine(Path.GetTempPath(), $"turkce_{Guid.NewGuid():N}.dxf");
        try
        {
            new DxfWriterService(db).WriteToFile(path);
            string content = File.ReadAllText(path);

            Assert.Contains("\\U+015E", content); // Ş
            Assert.Contains("\\U+0130", content); // İ
            Assert.DoesNotContain("?", content);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // ── AtomicFile ───────────────────────────────────────────────────────────────

    [Fact]
    public void AtomicFile_WhenWriteFails_KeepsOriginalFileAndCleansTemp()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"atomic_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "out.txt");
        try
        {
            AtomicFile.WriteAllText(target, "orijinal", System.Text.Encoding.UTF8);

            Assert.Throws<InvalidOperationException>(() =>
                AtomicFile.WriteVia(target, tmp =>
                {
                    File.WriteAllText(tmp, "yarım");
                    throw new InvalidOperationException();
                }));

            Assert.Equal("orijinal", File.ReadAllText(target));
            Assert.Single(Directory.GetFiles(dir)); // artakalan .tmp yok
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── SnapEngine: polyline diklik snap'i tüm segmentlere bakar ─────────────────

    [Fact]
    public void SnapEngine_Perpendicular_OnLaterPolylineSegment_IsFound()
    {
        var db = new CadDatabase();
        var poly = new LwPolylineEntity(new[]
        {
            new Vector3D(0, 0, 0), new Vector3D(100, 0, 0),
            new Vector3D(100, 100, 0), new Vector3D(200, 100, 0)
        });
        db.AddEntity(poly);

        var engine = new SnapEngine(db);
        var snap = engine.FindSnapPoint(new Vector3D(120, 101, 0), 1.0, lastPoint: new Vector3D(120, 50, 0));

        Assert.NotNull(snap);
        Assert.Equal(SnapPointType.Perpendicular, snap!.Value.Type);
        Assert.Equal(120.0, snap.Value.Position.X, 3);
        Assert.Equal(100.0, snap.Value.Position.Y, 3);
    }

    // ── Trim: LwPolyline SINIR olarak ────────────────────────────────────────────

    [Fact]
    public void Trim_Line_CanBeTrimmedAgainstPolylineBoundary()
    {
        var db = new CadDatabase();
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(100, 0, 0));
        var boundary = new LwPolylineEntity(new[] { new Vector3D(50, -10, 0), new Vector3D(50, 10, 0) });
        db.AddEntity(line);
        db.AddEntity(boundary);

        var trim = new TrimCommand(db, db.TransactionManager, currentZoom: 1.0);
        trim.OnPointerPressed(new Vector3D(25, 0, 0));

        var remaining = db.GetAllEntities().OfType<LineEntity>().ToList();
        Assert.Single(remaining);
        Assert.Equal(50.0, Math.Min(remaining[0].StartPoint.X, remaining[0].EndPoint.X), 3);
        Assert.Equal(100.0, Math.Max(remaining[0].StartPoint.X, remaining[0].EndPoint.X), 3);
    }

    // ── Kaydetmeden önce yedekleme (gerçek üretimde bulunan veri kaybı olayı) ───

    [Fact]
    public void FileBackup_ExistingFile_BackedUpBeforeOverwrite()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"filebackup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "proje.dwg");
        string backup = Path.Combine(dir, "proje.bak");
        try
        {
            File.WriteAllText(target, "gerçek-içerik-1.3MB-yerine");

            bool result = FileBackup.BackupBeforeOverwrite(target, out var err);

            Assert.True(result);
            Assert.Null(err);
            Assert.True(File.Exists(backup));
            Assert.Equal("gerçek-içerik-1.3MB-yerine", File.ReadAllText(backup));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void FileBackup_NoExistingFile_ReturnsFalseWithoutError()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"filebackup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "yeni_proje.dwg");
        try
        {
            bool result = FileBackup.BackupBeforeOverwrite(target, out var err);

            Assert.False(result);
            Assert.Null(err);
            Assert.False(File.Exists(Path.ChangeExtension(target, ".bak")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void FileBackup_SecondSave_OverwritesPreviousBakWithPriorVersion()
    {
        // AutoCAD'in kendi kuralıyla tutarlı: .bak her zaman "bir önceki kaydetme" içeriğini taşır.
        string dir = Path.Combine(Path.GetTempPath(), $"filebackup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "proje.dwg");
        string backup = Path.Combine(dir, "proje.bak");
        try
        {
            File.WriteAllText(target, "v1");
            FileBackup.BackupBeforeOverwrite(target, out _);
            File.WriteAllText(target, "v2"); // "kaydetme" simülasyonu

            FileBackup.BackupBeforeOverwrite(target, out _);

            Assert.Equal("v2", File.ReadAllText(backup)); // v2 kaydedilmeden ÖNCEki hâli yedeklendi
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Grip düzenleme geri alma ─────────────────────────────────────────────────

    [Fact]
    public void GripEdit_RecordApplied_DoesNotReapply_AndUndoRedoRoundTrips()
    {
        var db = new CadDatabase();
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
        db.AddEntity(line);

        var before = line.GetGripPoints().ToList();
        line.MoveGripPointAt(1, new Vector3D(20, 5, 0));          // canlı sürükleme
        var after = line.GetGripPoints().ToList();

        db.TransactionManager.RecordApplied(
            new Afney.Cad.Database.Transactions.Operations.GripEditOperation(line, before, after, db));

        Assert.Equal(20.0, line.EndPoint.X, 6);                    // Do() tekrar uygulanmadı
        Assert.True(db.TransactionManager.CanUndo);

        db.TransactionManager.Undo();
        Assert.Equal(10.0, line.EndPoint.X, 6);
        Assert.Equal(0.0, line.EndPoint.Y, 6);

        db.TransactionManager.Redo();
        Assert.Equal(20.0, line.EndPoint.X, 6);
        Assert.Equal(5.0, line.EndPoint.Y, 6);
    }

    [Fact]
    public void GripEdit_UpdateEntity_MakesEntityFindableAtNewLocation()
    {
        var db = new CadDatabase();
        var line = new LineEntity(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
        db.AddEntity(line);

        line.MoveGripPointAt(1, new Vector3D(500, 500, 0));
        db.UpdateEntity(line);

        var hits = db.QueryEntities(new CadBoundingBox(new Vector3D(450, 450, -1), new Vector3D(550, 550, 1)));
        Assert.Contains(line, hits);
    }

    // ── AutoAnnotationService.FindAnnotations (geri alınabilir silme için) ───────

    [Fact]
    public void AutoAnnotationService_FindAnnotations_DoesNotMutateDatabase()
    {
        var db = new CadDatabase();
        var svc = new AutoAnnotationService(db);
        var label = new TextEntity("DN100", new Vector3D(0, 0, 0), 10, 0) { Layer = svc.AnnotationLayer };
        db.AddEntity(label);

        var found = svc.FindAnnotations();

        Assert.Single(found);
        Assert.Contains(label, found);
        Assert.Single(db.GetAllEntities()); // hâlâ veritabanında — silinmedi
    }

    [Fact]
    public void AutoAnnotationService_ClearAnnotations_StillRemoves_BackwardCompatible()
    {
        var db = new CadDatabase();
        var svc = new AutoAnnotationService(db);
        db.AddEntity(new TextEntity("DN100", new Vector3D(0, 0, 0), 10, 0) { Layer = svc.AnnotationLayer });

        int removed = svc.ClearAnnotations();

        Assert.Equal(1, removed);
        Assert.Empty(db.GetAllEntities());
    }

    // ── Sıfır uzunluklu çizgi engeli ─────────────────────────────────────────────

    [Fact]
    public void LineCommand_SecondClickAtSamePoint_DoesNotCreateZeroLengthLine()
    {
        var db = new CadDatabase();
        var cmd = new LineCommand(db, db.TransactionManager);
        cmd.OnPointerPressed(new Vector3D(5, 5, 0));
        cmd.OnPointerPressed(new Vector3D(5, 5, 0));

        Assert.Empty(db.GetAllEntities().OfType<LineEntity>());
    }

    // ── TB-10: Arc/Circle Transform yarıçap ölçeklenmemesi ───────────────────────

    [Fact]
    public void ArcEntity_Transform_WithScaleMatrix_ScalesRadius()
    {
        var arc = new ArcEntity(Vector3D.Zero, 10.0, 0, Math.PI);
        arc.Transform(Matrix4x4.CreateScale(2.0));

        Assert.Equal(20.0, arc.Radius, 6);
    }

    [Fact]
    public void ArcEntity_Transform_WithRotationOnly_LeavesRadiusUnchanged()
    {
        var arc = new ArcEntity(new Vector3D(1, 0, 0), 10.0, 0, Math.PI);
        arc.Transform(Matrix4x4.CreateRotationZ(Math.PI / 2));

        Assert.Equal(10.0, arc.Radius, 6);
    }

    [Fact]
    public void CircleEntity_Transform_WithScaleMatrix_ScalesRadius()
    {
        var circle = new CircleEntity(Vector3D.Zero, 5.0);
        circle.Transform(Matrix4x4.CreateScale(3.0));

        Assert.Equal(15.0, circle.Radius, 6);
    }

    // ── TB-10: TableEntity hücre metni çizilmiyordu ──────────────────────────────

    [Fact]
    public void TableEntity_Draw_RendersCellText()
    {
        var table = new TableEntity(Vector3D.Zero, 2, 2);
        table.SetCell(0, 0, "DN50");
        table.SetCell(1, 1, "12 adet");

        var ctx = new Afney.Cad.Tests.Domain.FakeRenderContext();
        table.Draw(ctx);

        Assert.Contains(ctx.Texts, t => t.text == "DN50");
        Assert.Contains(ctx.Texts, t => t.text == "12 adet");
    }
}
